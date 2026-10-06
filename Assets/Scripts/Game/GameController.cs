using UnityEngine;
using UnityEngine.Rendering;

namespace SledSurfers
{
    /// Oyunun tamamını kuran ve yöneten bileşen: sahnede tek bir boş objeye eklenir.
    /// Akış: Ana ekran (yükseltmeler, dokun-oyna) → Nişan (sapanı ger) → Koşu → Sonuç → Ana ekran …
    public class GameController : MonoBehaviour
    {
        enum State { Hub, Aim, Run, Result }

        const float PhysicsStep = 1f / 240f;
        const float EmptyLimit = 0.12f;      // sıvı bunun altına inerse atış biter
        const int FinishBonus = 1000;

        static readonly Color Sky = new Color(0.62f, 0.82f, 0.97f);

        TrackProfile track;
        SledConfig cfg;
        SledPhysics sled;
        readonly LiquidSim liquid = new LiquidSim();
        readonly InputReader input = new InputReader();
        RiderView rider;
        WorldView world;
        Camera cam;
        Light sun;
        Collectibles loot;
        int pickupStreak;
        float lastPickupTime = -10f;

        // Ücretsiz sandık her ChestMinutes dakikada bir açılır.
        const int ChestMinutes = 20;
        int pendingChestCoins, pendingChestGems;

        bool wasInPuddle;
        Vector3 preCrashVelocity;   // çarpmadan hemen önceki hız (karakter bununla fırlar)

        // Karakter ekranında göz atılan (önizlenen) karakter
        int shownAvatar = -1;
        SaveData save;

        SoundFx sfx;
        GameUI ui;

        State state;
        float accumulator;
        float pull, aim;
        bool aimArmed, aimDragging;
        float runTime;

        // His (juice): kamera tepkisi, gövde yayı, ses olayları
        float launchTime = -10f;
        float shakeAmount;
        Vector3 feltAccel;
        Vector2 torsoAngle, torsoVel;   // x = öne eğilme, y = yana yatma (derece)
        float lastPull;
        int heardLandings;
        float lastFill = 1f, splashCooldown;

        // Ana ekranda kamera karakterin önünde yavaşça salınır.
        float orbit;
        bool lidOpen;
        bool rocketRequested;

        // Atış bitince önce bayrak dikilir, sonuç kartı kısa bir süre sonra açılır.
        const float ResultDelay = 1.3f;
        float resultDelay;
        RunSummary pendingResult;

        // Otomatik ekran görüntüsü testi: kızağı ve bardağı otomatik oyuncu sürer.
        public bool debugAutopilot;
        readonly AutoPilot pilot = new AutoPilot();

        void Awake()
        {
            Application.targetFrameRate = 60;
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.Portrait;

            save = SaveData.Load();
            SetupEnvironment();
            LoadTrack(save.track);
            rider = new RiderView();
            ShowAvatarModel(save.avatar);
            sfx = new SoundFx(cam.transform);
            SoundFx.SetEnabled(save.sound);
            new PartPreview();   // kart önizlemeleri (arayüzden önce)
            cam.cullingMask &= ~(1 << 31);
            ui = new GameUI(gameObject, sfx.Click);
            PushGemMarks();
            WireUi();
            EnterHub();
            SnapCamera();
            AutoShot.AttachIfRequested(this);
        }

        // ---------------------------------------------------------------- otomatik test kancaları

        public bool DebugInResult => state == State.Result;
        public void DebugHub() => EnterHub();
        public void DebugAvatars(int browse)
        {
            ui.ShowAvatars(save, save.avatar);
            ShowAvatarModel(browse);
            rider.Apply(cfg, save.hand);
            ui.RefreshAvatars(save, browse);
        }
        public void DebugSelectAvatar(int i) { save.avatarsOwned |= 1 << i; save.avatar = i; ShowAvatarModel(i); PrepareSled(); }
        /// Bardak modelini değiştirir ve bardak kamerasını tam ekran yapar (model < 0: normale döner).
        bool debugGlassFull;
        public void DebugGlass(int model)
        {
            if (model >= 0) save.levels[(int)UpgradeType.Glass] = model * Upgrades.StageSize;
            PrepareSled();
            rider.DebugGlassFull(debugGlassFull = model >= 0);
        }
        public void DebugCrash() { if (state == State.Run) sled.ForceEnd(RunEnd.Crashed, "Kasaya çarptın!"); }
        public void DebugHideTutorial() => ui.debugHideTutorial = true;
        public void DebugChest() { save.chestReadyTicks = 0; OpenChest(); }
        public void DebugTripleChest() => ui.DebugTripleChest();
        public void DebugGems(int g) { save.gems = g; ui.RefreshHub(save, -1); }
        public bool DebugRocketFiring => sled.RocketFiring;
        public void DebugAim() => EnterAim();
        public void DebugSettings(bool show) => ui.DebugSettings(show);
        public void DebugBuy(int i) => Buy(i);
        public void DebugMap() => ui.ShowMap(save);
        public void DebugForceFinish()
        {
            if (state != State.Run) return;
            sled.maxDistance = track.finishZ;
            sled.ForceEnd(RunEnd.Finished, "Parkur tamamlandı!");
        }
        public void DebugTrack(int i, int unlocked)
        {
            save.unlocked = Mathf.Max(save.unlocked, unlocked);
            save.track = i;
            LoadTrack(i);
            EnterHub();
        }
        public float DebugDistance => sled.maxDistance;

        public void DebugReset(int[] levels, int money)
        {
            levels.CopyTo(save.levels, 0);
            save.money = money;
            save.unlocked = save.track = 0;
            System.Array.Clear(save.bests, 0, save.bests.Length);
            LoadTrack(0);
            EnterAim();
            SnapCamera();
        }

        public void DebugSetPull(float value, float aimRad = 0f)
        {
            pull = value;
            aim = aimRad;
            sled.PlaceForAim(pull, aim);
        }

        public void DebugLaunch(float value, float aimRad = 0f)
        {
            DebugSetPull(value, aimRad);
            Launch();
        }

        /// Parkuru (zemin, ağaçlar, engeller, sapan) ve renk temasını yükler.
        void LoadTrack(int index)
        {
            world?.Destroy();
            track = TrackLibrary.Get(index);
            var theme = TrackTheme.Get(index);
            world = new WorldView(track, theme);
            world.SetBest(save.best);
            loot = new Collectibles(track, world.Root);
            PushGemMarks();
            theme.Apply(cam, sun);
        }

        void SetupEnvironment()
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Sky;
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 600f;
            // Görüntü kalitesi: her platformda aynı (Android varsayılanı 'Medium' kenar yumuşatmasız ve sert gölgeliydi).
            QualitySettings.SetQualityLevel(3, true);   // High
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadowDistance = 120f;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.pixelLightCount = 1;
            Application.targetFrameRate = 60;
            cam.gameObject.AddComponent<GradeEffect>();

            sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.95f;
            sun.shadows = LightShadows.Soft;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.52f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.32f, 0.26f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Sky;
            RenderSettings.fogStartDistance = 80f;
            RenderSettings.fogEndDistance = 330f;
        }

        // ---------------------------------------------------------------- akış

        /// Kızağı güncel yükseltmelerle sapanın önüne yerleştirir.
        void PrepareSled()
        {
            cfg = Upgrades.BuildConfig(save.levels, save.Perk);
            sled = new SledPhysics(track, cfg);
            liquid.Reset(cfg);
            rider.Apply(cfg, save.hand);
            pull = aim = lastPull = 0f;
            aimArmed = false;
            aimDragging = false;
            accumulator = 0f;
            heardLandings = 0;
            lastFill = 1f;
            input.Calibrate();
            lidOpen = false;
            pilot.Reset();
            rider.CloseLid();
            sled.PlaceForAim(0f);
            world.ShowGuide(0f, 0f);
            world.ClearFlag();
            resultDelay = 0f;
            loot.ResetRun();
            rider.ResetCrash();
            rider.ResetRockets();
            rider.Apply(cfg, save.hand);
            pickupStreak = 0;
            world.SetSlingLevel(save.levels[(int)UpgradeType.Slingshot]);
        }

        /// Elmasların yerlerini koşu ekranındaki ilerleme çubuğuna işler (arayüz henüz yoksa bekler).
        void PushGemMarks()
        {
            if (ui == null || loot == null) return;
            var marks = loot.GemPositions();
            for (int i = 0; i < marks.Count; i++) marks[i] /= track.finishZ;
            ui.SetRunGems(marks);
        }

        void ShowAvatarModel(int index)
        {
            if (index == shownAvatar) return;
            shownAvatar = index;
            rider.SetAvatar(AvatarLibrary.All[index].model);
        }

        void EnterHub()
        {
            ShowAvatarModel(save.avatar);
            PrepareSled();
            state = State.Hub;
            ui.ShowHub(save);
        }

        void EnterAim()
        {
            PrepareSled();
            state = State.Aim;
            ui.ShowAim();
        }

        void Launch()
        {
            sled.Launch();
            state = State.Run;
            runTime = 0f;
            accumulator = 0f;
            launchTime = Time.time;
            shakeAmount = 0.05f + 0.1f * pull;
            sfx.Twang(pull);
            world.ShowGuide(aim, 0f);
            ui.ShowRun();
        }

        void EndRun()
        {
            state = State.Result;
            int distance = Mathf.FloorToInt(sled.maxDistance);
            float fill = liquid.FillRatio;
            bool finished = sled.end == RunEnd.Finished;
            float income = Upgrades.IncomeMultiplier(save.levels[(int)UpgradeType.Income]) * (save.Perk == Perk.Income ? 1.1f : 1f);
            int bonus = FinishBonus * (save.track + 1);
            int earned = Mathf.RoundToInt((distance * (0.4f + 0.6f * Mathf.Clamp01(fill)) + loot.coins * Collectibles.CoinValue
                                           + (finished ? bonus : 0)) * income);
            save.gems += loot.gems;
            string unlockedName = null;
            if (finished && save.track == save.unlocked && save.unlocked < TrackLibrary.Count - 1)
            {
                save.unlocked++;
                unlockedName = TrackLibrary.Names[save.unlocked];
            }
            bool record = distance > save.best;
            float previousBest = save.best;
            if (record)
            {
                save.best = distance;
                world.SetBest(save.best);
            }
            save.money += earned;
            save.Save();

            float flagZ = Mathf.Max(sled.maxDistance, 1f);
            world.PlantFlag(new Vector3(sled.position.x, track.Height(sled.position.x, flagZ), flagZ), distance);
            sfx.Thud(5f);
            resultDelay = ResultDelay;
            if (sled.end == RunEnd.Crashed)
            {
                // Karakter kızaktan fırlar, yuvarlanır, oturup gülümser: sonuç kartı biraz bekler.
                rider.Crash(preCrashVelocity);
                resultDelay = 3.4f;
            }
            pendingResult = new RunSummary
            {
                title = sled.endReason,
                finished = finished,
                record = record,
                distance = distance,
                earned = earned,
                finishBonus = bonus,
                coins = loot.coins,
                gems = loot.gems,
                trackName = track.name,
                unlockedName = unlockedName,
                unlockedLength = unlockedName != null ? Mathf.RoundToInt(TrackLibrary.Lengths[save.unlocked]) : 0,
                lastTrack = finished && save.track == TrackLibrary.Count - 1,
                fill = fill,
                progress = Mathf.Clamp01(distance / track.finishZ),
                bestProgress = Mathf.Clamp01(previousBest / track.finishZ),
            };
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            input.Update(dt, state == State.Run);

            switch (state)
            {
                case State.Hub:
                    // Ana ekranda boş bir yere dokunmak oyunu başlatır (düğmeler ve kartlar hariç).
                    if (ui.Current == GameUI.Page.Hub && input.pointerDown && !ui.BlocksPointer(input.pointerPos)) EnterAim();
                    break;
                case State.Aim: UpdateAim(dt); break;
                case State.Run: UpdateRun(dt); break;
                case State.Result:
                    if (resultDelay > 0f)
                    {
                        resultDelay -= dt;
                        if (resultDelay <= 0f) ui.ShowResult(pendingResult);
                    }
                    break;
            }

            orbit += dt;
            UpdateBody(dt, out Vector3 shake);
            rider.Sync(sled, liquid, GlassPitch, GlassRoll, torsoAngle.x, torsoAngle.y, shake);
            rider.TickCrash(dt, (x, z) => track.Height(x, z), cam.transform.position);
            if (rider.Crashing) loot.HideAround(rider.CrashFocus, 7f);
            if (sled.end == RunEnd.None) preCrashVelocity = sled.velocity;
            world.AnimateBirds(sled.clock, Time.time);
            // Su birikintisi: raylardan su sıçrar, ilk girişte büyük sıçrama ve ses.
            var underSled = state == State.Run && sled.grounded && sled.Speed > 0.8f
                            ? track.SurfaceAt(sled.position.z, sled.position.x) : Surface.Ground;
            bool inPuddle = underSled == Surface.Puddle || underSled == Surface.Mud;
            float wet = inPuddle ? Mathf.Clamp01(sled.Speed / 14f) : 0f;
            rider.Splash(wet, inPuddle && !wasInPuddle, underSled == Surface.Mud);
            if (inPuddle && !wasInPuddle) sfx.Splash(0.4f + wet);
            wasInPuddle = inPuddle;
            bool attached = state == State.Aim || (state == State.Run && sled.launching);
            Vector3 pouch = sled.position + sled.Orientation * new Vector3(0f, 0.45f, -0.8f);
            world.UpdateSlingshot(pouch, sled.velocity, attached, dt);
            UpdateSound(dt);
            UpdateCamera(dt);
            world.UpdateFlag(dt, cam.transform.position);
            loot.Animate(dt, sled.position.z, Time.time);
            rider.glassCam.enabled = debugGlassFull || state == State.Aim || state == State.Run || (state == State.Result && resultDelay > 0f);
            ui.Tick(dt, BuildHud());
        }

        void UpdateAim(float dt)
        {
            if (!input.pointerHeld && !input.spaceHeld) aimArmed = true;

            if (aimArmed && input.pointerDown && !ui.BlocksPointer(input.pointerPos)) aimDragging = true;
            if (aimDragging && input.pointerHeld)
            {
                // Parmak geriye ve yana çekilir; kızak çekilen yönün tersine fırlar (sapan gibi).
                Vector2 d = input.pointerPos - input.pointerStart;
                float scale = 0.28f * Screen.height;
                float back = -d.y / scale, side = d.x / scale;
                float raw = Mathf.Sqrt(back * back + side * side);
                // Sona doğru germek zorlaşır: parmak, lastikten daha fazla yol alır.
                pull = back <= 0f ? 0f : 1f - Mathf.Pow(1f - Mathf.Clamp01(raw / 1.1f), 1.6f);
                if (back > 0.03f) aim = Mathf.Clamp(Mathf.Atan2(-side, back), -SledPhysics.MaxAim, SledPhysics.MaxAim);
            }
            if (aimArmed && input.spaceHeld)
            {
                pull = Mathf.Clamp01(pull + dt * 0.7f);
                aim = Mathf.Clamp(aim + input.steer * 0.8f * dt, -SledPhysics.MaxAim, SledPhysics.MaxAim);
            }

            bool released = (aimDragging && input.pointerUp) || (aimArmed && input.spaceUp);
            sled.PlaceForAim(pull, aim);
            world.ShowGuide(aim, pull);

            // Bekleme sırasında da bardak çalkalanabilir (eli eğerek denenebilir).
            StepLiquid(Vector3.zero, dt);

            if (released)
            {
                aimDragging = false;
                if (pull > 0.08f) Launch();
                else
                {
                    pull = 0f;
                    world.ShowGuide(aim, 0f);
                }
            }
        }

        /// Gövde, kalçadan bağlı sönümlü bir yay gibi hissedilen ivmeye tepki verir:
        /// hızlanırken geriye, yavaşlarken öne, virajda dışa savrulur. Germe sırasında geriye yaslanıp titrer.
        void UpdateBody(float dt, out Vector3 shake)
        {
            Vector2 target;
            shake = Vector3.zero;
            if (state == State.Aim)
            {
                target = new Vector2(-16f * pull, 0f);
                float tremble = 0.015f * pull * pull;
                shake = new Vector3(Random.Range(-tremble, tremble), 0f, Random.Range(-tremble, tremble));
            }
            else
            {
                feltAccel = Vector3.Lerp(feltAccel, sled.acceleration, 1f - Mathf.Exp(-12f * dt));
                Vector3 local = Quaternion.Inverse(sled.Orientation) * Vector3.ClampMagnitude(feltAccel, 60f);
                target = new Vector2(Mathf.Clamp(-local.z * 1.6f, -30f, 25f), Mathf.Clamp(-local.x * 2f, -20f, 20f));
            }
            const float w = 14f, zeta = 0.45f;
            Vector2 acc = w * w * (target - torsoAngle) - 2f * zeta * w * torsoVel;
            torsoVel += acc * dt;
            torsoAngle += torsoVel * dt;
        }

        void UpdateSound(float dt)
        {
            float tension = state == State.Aim ? Mathf.Clamp01(Mathf.Abs(pull - lastPull) / Mathf.Max(dt, 0.001f) * 0.8f) : 0f;
            lastPull = pull;
            sfx.Drive(tension, sled.Speed, sled.grounded, state == State.Run, state == State.Run && sled.RocketFiring, dt);

            if (sled.landings > heardLandings)
            {
                heardLandings = sled.landings;
                if (sled.lastImpact > 1.5f)
                {
                    sfx.Thud(sled.lastImpact);
                    shakeAmount = Mathf.Max(shakeAmount, Mathf.Min(0.25f, sled.lastImpact * 0.02f));
                }
            }

            splashCooldown -= dt;
            float fill = liquid.FillRatio;
            if (fill > lastFill) lastFill = fill;
            if (lastFill - fill > 0.015f && splashCooldown <= 0f)
            {
                sfx.Splash((lastFill - fill) * 5f);
                splashCooldown = 0.35f;
                lastFill = fill;
            }
        }

        void UpdateRun(float dt)
        {
            runTime += dt;
            accumulator += dt;
            if (rocketRequested || Input.GetKeyDown(KeyCode.R)) TryFireRocket();
            rocketRequested = false;
            while (accumulator >= PhysicsStep)
            {
                accumulator -= PhysicsStep;
                if (debugAutopilot)
                {
                    pilot.StepGlass(sled, PhysicsStep);
                    if (AutoPilot.ShouldFire(track, sled, cfg)) TryFireRocket();
                    sled.Step(AutoPilot.Steer(track, sled), AutoPilot.Pitch(track, sled, cfg), PhysicsStep);
                }
                else sled.Step(input.steer, input.pitch, PhysicsStep);
                liquid.lidClosed = sled.position.z < cfg.lidDistance;
                if (!liquid.lidClosed && !lidOpen)
                {
                    lidOpen = true;
                    rider.PopLid();
                    sfx.Pop();
                    ui.LidOpened();
                }
                liquid.Step(sled.acceleration, GlassRotation(), PhysicsStep);
                Pickup();
                if (sled.end == RunEnd.None && liquid.FillRatio < EmptyLimit)
                    sled.ForceEnd(RunEnd.Spilled, "Bardak boşaldı!");
                if (sled.end != RunEnd.None)
                {
                    EndRun();
                    return;
                }
            }
        }

        void Pickup()
        {
            loot.Collect(sled.position, save.Perk == Perk.Magnet ? 1.4f : 1f, out int coins, out int gems);
            if (coins > 0)
            {
                pickupStreak = Time.time - lastPickupTime < 0.6f ? pickupStreak + coins : 0;
                lastPickupTime = Time.time;
                sfx.Pickup(pickupStreak);
            }
            if (gems > 0) sfx.Gem();
        }

        void TryFireRocket()
        {
            if (!sled.FireRocket()) return;
            sfx.Pop();
            shakeAmount = Mathf.Max(shakeAmount, 0.12f);
        }

        void StepLiquid(Vector3 accel, float dt)
        {
            accumulator += dt;
            while (accumulator >= PhysicsStep)
            {
                accumulator -= PhysicsStep;
                liquid.lidClosed = true;
                liquid.Step(accel, GlassRotation(), PhysicsStep);
            }
        }

        float GlassPitch => debugAutopilot ? pilot.glassPitch : input.glassPitch;
        float GlassRoll => debugAutopilot ? pilot.glassRoll : input.glassRoll;

        Quaternion GlassRotation() => sled.Orientation * Quaternion.Euler(GlassPitch, 0f, -GlassRoll);

        // ---------------------------------------------------------------- kamera

        Vector3 CameraTarget(out Vector3 look)
        {
            Vector3 p = sled.position;
            if (state == State.Hub)
            {
                // Ana ekran: kamera karakterin önünde, hafifçe sağa-sola salınır (referanstaki gibi yüz yüze).
                // Dikey ekranda yatay görüş dar (~31°): kanatlar büyüdükçe kamera geri çekilir.
                bool closeUp = ui != null && ui.Current == GameUI.Page.Avatars;
                float a = 0.35f * Mathf.Sin(orbit * 0.3f);
                float radius = closeUp ? 3.6f : 5.4f + 0.8f * cfg.wingArea;
                Vector3 center = p + new Vector3(0f, 0.7f, 0f);
                look = center + new Vector3(0f, closeUp ? -0.5f : -0.35f, 0f);
                return center + new Vector3(Mathf.Sin(a) * radius, 1.2f, Mathf.Cos(a) * radius);
            }
            if (state == State.Aim)
            {
                // Nişanda kamera sapanın arkasında durur, germe arttıkça biraz geri çekilir.
                look = new Vector3(Mathf.Sin(aim) * 6f, track.Height(6f) + 1f, 7f);
                return new Vector3(p.x * 0.5f, p.y + 3.3f + pull * 0.6f, p.z - 7.5f - pull * 1.2f);
            }
            // Çarpmada kamera fırlayan karaktere yaklaşır, çaprazdan ona bakar (oturup gülümsemesi görünsün).
            if (rider != null && rider.Crashing)
            {
                Vector3 f = rider.CrashFocus;
                // Kamera vadinin ortasına doğru yandan ve biraz yukarıdan bakar (yol üstündeki coinler arada kalmaz).
                float side = f.x > track.CenterX(f.z) ? -1f : 1f;
                look = f + new Vector3(0f, 0.3f, 0f);
                return f + new Vector3(side * 2.6f, 1.05f, 1.9f);
            }
            // Koşu: kamera vadinin yönüne hizalı durur (vadi kıvrıldıkça döner), kızağı yana doğru kısmen izler.
            float c = track.CenterX(p.z);
            float bend = (track.CenterX(p.z + 6f) - track.CenterX(p.z - 6f)) / 12f;
            float lateral = (p.x - c) * 0.85f;   // yamaca çıkan kızak ekranın ortasına yakın kalsın
            look = new Vector3(c + bend * 7f + lateral, p.y + 1f, p.z + 7f);
            return new Vector3(c - bend * 8f + lateral, p.y + 3.0f, p.z - 8f);
        }

        void SnapCamera()
        {
            cam.transform.position = CameraTarget(out var look);
            cam.transform.rotation = Quaternion.LookRotation(look - cam.transform.position);
        }

        void UpdateCamera(float dt)
        {
            Vector3 target = CameraTarget(out var look);
            float sinceLaunch = Time.time - launchTime;

            // Bırakınca kamera bir an geride kalır: kızak ileri fırlar, sonra kamera yetişir.
            float follow = rider.Crashing ? 4f
                         : state == State.Run ? Mathf.Lerp(1.2f, 6f, Mathf.Clamp01((sinceLaunch - 0.15f) / 0.6f)) : 3f;
            float k = 1f - Mathf.Exp(-follow * dt);
            Vector3 pos = Vector3.Lerp(cam.transform.position, target, k);
            cam.transform.position = pos;
            var rot = Quaternion.LookRotation(look - pos);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, rot, 1f - Mathf.Exp(-8f * dt));

            // Görüş açısı: fırlatma anında açılır, hızla birlikte genişler.
            float kick = sinceLaunch < 1.2f ? Mathf.Sin(Mathf.Clamp01(sinceLaunch / 0.2f) * Mathf.PI * 0.5f) * Mathf.Exp(-sinceLaunch * 2.5f) : 0f;
            float speedFov = state == State.Run ? Mathf.Clamp(sled.Speed - 8f, 0f, 20f) * 0.45f : 0f;
            float baseFov = state == State.Hub ? 55f : 70f;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFov + speedFov + kick * 14f, 1f - Mathf.Exp(-10f * dt));

            // Sarsıntı
            if (shakeAmount > 0.001f)
            {
                cam.transform.position += Random.insideUnitSphere * shakeAmount;
                shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, dt * 0.5f);
            }
        }

        // ---------------------------------------------------------------- arayüz

        void WireUi()
        {
            ui.Retry += EnterAim;
            ui.OpenHub += EnterHub;
            ui.OpenChest += OpenChest;
            ui.OpenAvatars += () => ui.ShowAvatars(save, save.avatar);
            ui.BrowseAvatar += i =>
            {
                ShowAvatarModel(i);
                rider.Apply(cfg, save.hand);
                ui.RefreshAvatars(save, i);
            };
            ui.AvatarAction += i =>
            {
                var info = AvatarLibrary.All[i];
                if (!save.Owns(i))
                {
                    if (save.gems < info.price)
                    {
                        sfx.Deny();
                        return;
                    }
                    save.gems -= info.price;
                    save.avatarsOwned |= 1 << i;
                    sfx.Coin();
                }
                else sfx.Click();
                save.avatar = i;
                save.Save();
                PrepareSled();   // avantaj fizik ayarlarına yansısın
                rider.Punch();
                ui.RefreshAvatars(save, i);
                ui.RefreshHub(save, -1);
            };
            ui.ClaimChest += mult =>
            {
                save.money += pendingChestCoins * mult;
                save.gems += pendingChestGems * mult;
                save.chestReadyTicks = System.DateTime.UtcNow.AddMinutes(ChestMinutes).Ticks;
                save.Save();
                sfx.Coin();
                ui.RefreshHub(save, -1);
            };
            ui.FireRocket += () => rocketRequested = true;
            ui.OpenMap += () =>
            {
                EnterHub();
                ui.ShowMap(save);
            };
            ui.SelectTrack += i =>
            {
                if (i > save.unlocked) return;
                save.track = i;
                save.Save();
                LoadTrack(i);
                EnterHub();
            };
            ui.NextTrack += () =>
            {
                save.track = save.unlocked;
                save.Save();
                LoadTrack(save.track);
                EnterHub();
            };
            ui.Buy += Buy;
            ui.SetHand += SetHand;
            ui.SetSound += on =>
            {
                save.sound = on;
                save.Save();
                SoundFx.SetEnabled(on);
            };
        }

        void Buy(int i)
        {
            int lvl = save.levels[i];
            if (lvl >= Upgrades.Cap(i, save.unlocked))
            {
                ui.Deny(i);
                sfx.Deny();
                return;
            }
            int cost = Upgrades.Cost(i, lvl);
            int gemCost = Upgrades.GemCost(i, lvl);
            if (save.money >= cost) save.money -= cost;
            else if (save.gems >= gemCost) save.gems -= gemCost;   // coin yetmezse elmasla
            else
            {
                ui.Deny(i);
                sfx.Deny();
                return;
            }
            save.levels[i]++;
            save.Save();
            PrepareSled();
            rider.Punch();
            sfx.Coin();
            ui.RefreshHub(save, i);
            // Aşama tamamlandı: parça kızakta yeni görünümüne geçti, kutlanır.
            if (save.levels[i] % Upgrades.StageSize == 0)
            {
                var tr = new System.Globalization.CultureInfo("tr-TR");
                int stage = Upgrades.Stage(save.levels[i]);
                string detail = i == (int)UpgradeType.Runners ? Upgrades.RunnerStageNames[stage - 1].ToUpper(tr)
                              : i == (int)UpgradeType.Glass ? Upgrades.GlassNames[Upgrades.GlassModel(save.levels[i])].ToUpper(tr)
                              : "YENİ GÖRÜNÜM";
                ui.StageUp(Upgrades.Names[i].ToUpper(tr) + " " + stage + ". SEVİYE!\n" + detail);
                rider.Punch();
            }
        }

        int ChestSeconds() =>
            Mathf.Max(0, Mathf.CeilToInt((float)new System.TimeSpan(save.chestReadyTicks - System.DateTime.UtcNow.Ticks).TotalSeconds));

        void OpenChest()
        {
            if (ChestSeconds() > 0)
            {
                sfx.Deny();
                return;
            }
            // Ödül açılmış en ileri parkura göre büyür; arada bir pembe elmas çıkar.
            int tier = save.unlocked + 1;
            pendingChestCoins = 60 * tier + Random.Range(0, 13) * 5 * tier;
            if (save.Perk == Perk.Chest) pendingChestCoins = Mathf.RoundToInt(pendingChestCoins * 1.25f / 5f) * 5;
            pendingChestGems = Random.value < 0.35f ? 1 : 0;
            sfx.Coin();
            ui.ShowChest(pendingChestCoins, pendingChestGems);
        }

        void SetHand(int hand)
        {
            if (hand == save.hand) return;
            save.hand = hand;
            save.Save();
            rider.Apply(cfg, save.hand);
            ui.RefreshHub(save, -1);
        }

        HudInfo BuildHud()
        {
            return new HudInfo
            {
                money = save.money,
                best = save.best,
                hand = save.hand,
                sound = save.sound,
                distance = Mathf.FloorToInt(sled.maxDistance),
                progress = Mathf.Clamp01(sled.maxDistance / track.finishZ),
                bestProgress = Mathf.Clamp01(save.best / track.finishZ),
                speedKmh = sled.Speed * 3.6f,
                fill = liquid.FillRatio,
                pull = pull,
                runTime = runTime,
                airborne = state == State.Run && !sled.grounded && !sled.launching,
                canGlide = cfg.wingArea > 0f,
                dragging = aimDragging,
                tiltMode = input.TiltMode,
                glassRect = rider.glassCam.rect,
                lidLeft = lidOpen ? 0f : cfg.lidDistance - (state == State.Run ? sled.position.z : 0f),
                trackIndex = save.track,
                trackName = track.name,
                hasRocket = cfg.rocketCharges > 0,
                rocketReady = state == State.Run && sled.CanFireRocket,
                rocketCharges = sled.rocketCharges,
                gems = save.gems,
                runCoins = loot.coins,
                runGems = loot.gems,
                chestSeconds = ChestSeconds(),
                rocketFuel = sled.RocketFiring ? Mathf.Clamp01(sled.rocketTime / Mathf.Max(cfg.rocketBurn, 0.01f)) : 0f,
            };
        }
    }
}
