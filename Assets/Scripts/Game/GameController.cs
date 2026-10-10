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
        InputReader input;   // Awake'te kurulur: alan başlatıcısında SystemInfo (ivmeölçer) sorgulanamaz (telefonda açılış hatası)
        RiderView rider;
        WorldView world;
        Camera cam;
        Light sun;
        Collectibles loot;
        int pickupStreak;
        float lastPickupTime = -10f;
        bool rocketPickupShown;   // havada alınan roket hakkının efekti bu atışta oynatıldı mı

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
        /// Otomatik pilotun direksiyonunu ezer (NaN: kapalı) — köprüden düşme çekimi için.
        public float debugSteer = float.NaN;
        readonly AutoPilot pilot = new AutoPilot();
        /// Kartlardaki "+X m" önizlemesi: ana ekrandayken arka planda hesaplanır.
        readonly GainPredictor gains = new GainPredictor();

        void Awake()
        {
            Application.targetFrameRate = 60;
            input = new InputReader();
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.Portrait;

            if (AutoShot.Requested) SaveData.Prefix = "autotest.";   // test oyunu gerçek kayda dokunmaz
            save = SaveData.Load();
            input.sensitivity = save.steerSens;
            SetupEnvironment();
            LoadTrack(save.track);
            rider = new RiderView();
            ShowAvatarModel(save.avatar);
            sfx = new SoundFx(cam.transform);
            SoundFx.SetEnabled(save.sound);
            new PartPreview();   // kart önizlemeleri (arayüzden önce)
            cam.cullingMask &= ~(1 << 31);
            ui = new GameUI(gameObject, sfx.Click);
            input.blocksPointer = ui.BlocksPointer;
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
        /// Hak bitti penceresi (7 dk önce bir hak harcanmış gibi: sayaç 18 dk gösterir).
        public void DebugNoLaunches()
        {
            save.launches = 0;
            save.launchTicks = System.DateTime.UtcNow.AddMinutes(-7).Ticks;
            ui.ShowNoLaunches();
        }

        public void DebugRestoreLaunches()
        {
            save.launches = SaveData.MaxLaunches;
            ui.HideNoLaunches();
        }

        public void DebugChest() { save.chestReadyTicks = 0; OpenChest(); }
        public void DebugTripleChest() => ui.DebugTripleChest();
        public void DebugGems(int g) { save.gems = g; ui.RefreshHub(save, -1); }
        public bool DebugRocketFiring => sled.RocketFiring;
        public void DebugAim() => EnterAim();
        public void DebugSettings(bool show) => ui.DebugSettings(show);
        public void DebugBuy(int i) => Buy(i);
        public void DebugMap() => ui.ShowMap(save);
        public void DebugClosePart() => ui.DebugClosePart();
        /// Parça duyurusu: part = PartNames dizini (0 kanat ... 5 jet), araç bir sonraki parkurun aracı.
        public void DebugPartUnlock(int part)
        {
            int next = Mathf.Min(part + 1, TrackLibrary.Count - 1);
            ui.ShowPartUnlock(Upgrades.PartNames[part], Upgrades.PartTexts[part],
                              PartPreview.Instance.RenderUnlock(TrackLibrary.Vehicles[next], part + 1));
        }
        public void DebugForceFinish()
        {
            if (state != State.Run) return;
            sled.maxDistance = track.finishZ;
            sled.ForceEnd(RunEnd.Finished, "Parkur tamamlandı!");
        }
        /// İlk tümsek dizisinin (big = false) ya da zorlu kamburun (big = true) başladığı z; yoksa -1.
        public float DebugBumpZ(bool big)
        {
            if (track == null) return -1f;
            foreach (var b in track.bumps)
                if (b.z0 > 300f && (big ? b.height >= 2f && b.halfWidth > 30f : b.height < 1.5f && b.length < 18f)) return b.z0;
            return -1f;
        }

        /// after'dan sonraki ilk tepe (hemen ardından dik iniş: eğim −0.45'ten dik); yoksa -1.
        public float DebugCrestZ(float after)
        {
            if (track == null) return -1f;
            for (float z = after; z < track.finishZ - 50f; z += 1f)
                if (track.Slope(z) >= 0f && track.Slope(z + 1f) < 0f)
                    for (float d = 2f; d <= 18f; d += 2f)
                        if (track.Slope(z + d) < -0.45f) return z;
            return -1f;
        }

        /// i. vadinin (su) başladığı z; yoksa -1.
        public float DebugRocketHillZ => track != null && track.rocketHills.Count > 0 ? track.rocketHills[0] : -1f;
        public float DebugForkZ(int i) => track != null && i < track.forks.Count ? track.forks[i].z0 : -1f;
        public float DebugGapZ(int i) => track != null && i < track.water.Count ? track.water[i].x : -1f;

        /// 300 m'den sonraki ilk verilen türde yüzey lekesinin başladığı z; yoksa -1.
        public float DebugSurfaceZ(Surface type, float after = 300f)
        {
            if (track == null) return -1f;
            float best = -1f;
            foreach (var s in track.surfaces)
                if (s.type == type && s.z0 > after && (best < 0f || s.z0 < best)) best = s.z0;
            return best;
        }

        public float DebugRocketPickupZ => track != null && track.hasRocketPickup ? track.rocketPickup.z : -1f;

        public void DebugTrack(int i, int unlocked)
        {
            save.unlocked = Mathf.Max(save.unlocked, unlocked);
            save.track = i;
            LoadTrack(i);
            EnterHub();
        }
        public float DebugDistance => sled.maxDistance;
        public bool DebugRunning => state == State.Run && sled.end == RunEnd.None;

        public void DebugReset(int[] levels, int money)
        {
            foreach (var tl in save.trackLevels) levels.CopyTo(tl, 0);   // test: her pistte aynı seviyeler
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
            Upgrades.Track = index;   // yükseltmeler pist başına: aşama sayısı, fiyat ve güç bu pistin
            Upgrades.PartTier = save.unlocked;   // kalıcı parçalar: bitirilen parkur sayısı
            track = TrackLibrary.Get(index);
            var theme = TrackTheme.Get(index);
            world = new WorldView(track, theme);
            trail = new SledTrail(world.Root, track, theme);
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
            trail?.Clear();
            liquid.Reset(cfg);
            reflex.Reset();
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
            rocketPickupShown = false;
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
            RefreshQuests(false);
            // Günlük giriş ödülü: oturumda bir kez kendiliğinden açılır (otomatik testte açılmaz).
            if (!loginShown && !AutoShot.Requested && save.PendingLoginDay() > 0)
            {
                loginShown = true;
                ShowLogin();
            }
        }

        // ---------------------------------------------------------------- günlük görevler ve giriş ödülü

        DailyQuests.Quest[] quests;
        int questsDay = -1, questsTrack = -1;
        bool loginShown;

        /// Gün değiştiyse listeyi yeniler; görev listesi (gün, parkur) için bir kez üretilir.
        void RefreshQuests(bool open)
        {
            save.RollQuests();
            if (quests == null || questsDay != save.questDay || questsTrack != save.questTrack)
            {
                int keepTrack = Upgrades.Track;   // TrackLibrary.Get bağlamı değiştirir
                quests = DailyQuests.Generate(save.questDay, save.questTrack, TrackLibrary.Get(save.questTrack));
                Upgrades.Track = keepTrack;
                questsDay = save.questDay;
                questsTrack = save.questTrack;
                save.Save();
            }
            ui.ShowQuests(quests, save.questProgress, save.questClaimed, open);
        }

        void RecordQuests(bool finished, float fill)
        {
            RefreshQuests(false);
            var st = sled.stats;
            var report = new RunReport
            {
                distance = sled.maxDistance, fill = fill, airTime = st.airTime, longestAir = st.longestAir,
                iceMeters = st.meters[(int)LossSurface.Ice], wetMeters = st.meters[(int)LossSurface.Mud] + st.meters[(int)LossSurface.Puddle],
                coins = loot.coins, gems = loot.gems, bridges = st.bridgesCrossed, forksGood = st.forksGood, rockets = st.rocketsFired,
                perfect = st.perfectLaunch, finished = finished,
            };
            for (int i = 0; i < quests.Length; i++)
                save.questProgress[i] = Mathf.Min(DailyQuests.Apply(quests[i], save.questProgress[i], report), 1 << 24);
        }

        void ClaimQuestReward(int i)
        {
            if (quests == null || i < 0 || i >= quests.Length) return;
            if ((save.questClaimed & (1 << i)) != 0 || save.questProgress[i] < quests[i].target) return;
            save.questClaimed |= 1 << i;
            save.money += quests[i].coins;
            save.gems += quests[i].gems;
            save.Save();
            sfx.Coin();
            ui.RefreshHub(save, -1);
            RefreshQuests(false);
        }

        int[] LoginCoins(out int[] gems)
        {
            var coins = new int[7];
            gems = new int[7];
            for (int d = 0; d < 7; d++) DailyQuests.LoginReward(d + 1, save.unlocked, out coins[d], out gems[d]);
            return coins;
        }

        void ShowLogin()
        {
            int day = save.PendingLoginDay();
            if (day <= 0) return;
            var coins = LoginCoins(out var gems);
            ui.ShowLogin(day, coins, gems);
        }

        void ClaimLoginReward()
        {
            int day = save.PendingLoginDay();
            if (day <= 0) return;
            DailyQuests.LoginReward(day, save.unlocked, out int coins, out int gems);
            save.money += coins;
            save.gems += gems;
            save.ClaimLogin(day);
            save.Save();
            sfx.Coin();
            ui.RefreshHub(save, -1);
        }

        public void DebugQuests(int[] progress)
        {
            RefreshQuests(false);
            System.Array.Copy(progress, save.questProgress, Mathf.Min(progress.Length, save.questProgress.Length));
            RefreshQuests(true);
        }
        public void DebugLogin(int streak)
        {
            save.loginDay = DailyQuests.Yesterday;
            save.loginStreak = streak;
            ShowLogin();
        }

        /// Ekran görüntüsü testi onlarca kez fırlatır: kota uygulanmaz.
        public bool debugUnlimitedLaunches;
        static bool NoTiltDevice => Application.isMobilePlatform && !SystemInfo.supportsAccelerometer;
        /// Mükemmel bırakış: sapan tam gerildikten (≥%97) sonraki bu süre içinde bırakılırsa lastik %6 güçlü iter.
        const float PerfectWindow = 0.35f;
        float fullPullTime = -1f;
        bool PerfectWindowOpen => fullPullTime >= 0f && Time.time - fullPullTime <= PerfectWindow;
        Light fillLight;

        void EnterAim()
        {
            if (!debugUnlimitedLaunches && !save.LaunchesFree && save.launches <= 0)
            {
                // Hak yok: ana ekranda bekleme/reklam penceresi.
                if (state != State.Hub) EnterHub();
                sfx.Deny();
                ui.ShowNoLaunches();
                return;
            }
            PrepareSled();
            state = State.Aim;
            ui.ShowAim();
        }

        void Launch()
        {
            if (!debugUnlimitedLaunches && !save.LaunchesFree)
            {
                save.RegenLaunches(System.DateTime.UtcNow.Ticks);
                save.launches = Mathf.Max(0, save.launches - 1);
                save.Save();
            }
            bool perfect = PerfectWindowOpen;
            fullPullTime = -1f;
            sled.launchBoost = perfect ? SledPhysics.PerfectLaunchBoost : 1f;
            sled.stats.perfectLaunch = perfect;
            sled.Launch();
            state = State.Run;
            runTime = 0f;
            accumulator = 0f;
            launchTime = Time.time;
            shakeAmount = 0.05f + 0.1f * pull;
            sfx.Twang(pull);
            world.ShowGuide(aim, 0f);
            ui.ShowRun();
            if (perfect) ui.Toast("MÜKEMMEL ATIŞ!");
            hintedRocket = hintedNose = hintedBridge = hintedFork = false;
            ShowHint(Hint.Steer, IconKind.Swap, input.TiltMode ? "Sağa-sola sürükle: yön" : "A / D: yön", 3);
        }

        SledTrail trail;
        int pendingPart = -1;   // bu atışla açılan kalıcı parça (Upgrades.PartNames dizini)
        int lastEarned;   // son atışın kazancı (sonuçta reklamla 3 katına çıkarılabilir)

        void EndRun()
        {
            state = State.Result;
            int distance = Mathf.FloorToInt(sled.maxDistance);
            float fill = liquid.FillRatio;
            bool finished = sled.end == RunEnd.Finished;
            float income = Upgrades.IncomeMultiplier(save.levels[(int)UpgradeType.Income]) * (save.Perk == Perk.Income ? 1.1f : 1f);
            int bonus = FinishBonus * (save.track + 1);
            int earned = Mathf.RoundToInt((distance * (0.4f + 0.6f * Mathf.Clamp01(fill)) + loot.coins * Collectibles.CoinValue
                                           + (finished ? bonus : 0)) * income * TrackLibrary.PayFactors[save.track]);
            save.gems += loot.gems;
            string unlockedName = null;
            if (finished && save.track == save.unlocked && save.unlocked < TrackLibrary.Count - 1)
            {
                save.unlocked++;
                unlockedName = TrackLibrary.Names[save.unlocked];
                Upgrades.PartTier = save.unlocked;
                pendingPart = save.unlocked - 1;   // sonuç kartından sonra duyurulur
            }
            bool record = distance > save.best;
            float previousBest = save.best;
            if (record)
            {
                save.best = distance;
                world.SetBest(save.best);
            }
            save.money += earned;
            lastEarned = earned;
            RecordQuests(finished, fill);
            save.Save();

            float flagZ = Mathf.Max(sled.maxDistance, 1f);
            world.PlantFlag(new Vector3(sled.position.x, track.Height(sled.position.x, flagZ), flagZ), distance);
            sfx.Thud(5f);
            resultDelay = ResultDelay;
            if (sled.end == RunEnd.Crashed)
            {
                // Karakter kızaktan fırlar, yuvarlanır, ayağa kalkıp "Tekrar deneyelim!" der: sonuç kartı bekler.
                rider.Crash(preCrashVelocity);
                resultDelay = 4.8f;
                if (sled.stats.fellInRavine && track.ravineIsWater)
                {
                    rider.Splash(1f, true);   // dereye düşüş: büyük sıçrama
                    sfx.Splash(1.4f);
                }
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
                tip = RunTips.Tip(sled.stats, sled.end, sled.endReason, cfg, sled.maxDistance),
            };
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            input.Update(dt, state == State.Run);
            TickLaunches(Time.unscaledDeltaTime);

            switch (state)
            {
                case State.Hub:
                    gains.Request(track, save.levels, save.Perk);
                    gains.Tick();
                    ui.SetGains(gains);
                    // Ana ekranda boş bir yere dokunmak oyunu başlatır (düğmeler ve kartlar hariç).
                    if (ui.Current == GameUI.Page.Hub && input.pointerDown && !ui.BlocksPointer(input.pointerPos)) EnterAim();
                    break;
                case State.Aim: UpdateAim(dt); break;
                case State.Run: UpdateRun(dt); break;
                case State.Result:
                    if (resultDelay > 0f)
                    {
                        resultDelay -= dt;
                        if (resultDelay <= 0f)
                        {
                            ui.ShowResult(pendingResult);
                            if (pendingPart >= 0 && pendingPart < Upgrades.PartNames.Length)
                            {
                                // Yeni parça bir sonraki parkurun aracında gösterilir (son üç haritada araç da değişir).
                                var tex = PartPreview.Instance?.RenderUnlock(TrackLibrary.Vehicles[save.unlocked], save.unlocked);
                                if (tex != null) ui.ShowPartUnlock(Upgrades.PartNames[pendingPart], Upgrades.PartTexts[pendingPart], tex);
                            }
                            pendingPart = -1;
                        }
                    }
                    break;
            }

            orbit += dt;
            UpdateBody(dt, out Vector3 shake);
            rider.Sync(sled, liquid, GlassPitch, GlassRoll, torsoAngle.x, torsoAngle.y, shake);
            trail?.Tick(sled, state == State.Run);
            rider.TickCrash(dt, (x, z) => track.Height(x, z), cam.transform.position);
            // Ayağa kalkan karakterin başının üstünde konuşma baloncuğu.
            if (rider.CrashStanding)
            {
                var sp = cam.WorldToScreenPoint(rider.CrashHeadTop + Vector3.up * 0.12f);
                if (sp.z > 0f) ui.ShowBubble("Tekrar deneyelim!", sp);
                else ui.HideBubble();
            }
            else ui.HideBubble();
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
            // Düşme sahnesinde kameranın yanında yumuşak dolgu ışığı: güneş arkada kalsa da yüz aydınlık.
            if (fillLight == null)
            {
                fillLight = new GameObject("Crash Fill Light").AddComponent<Light>();
                fillLight.type = LightType.Point;
                fillLight.range = 7f;
                fillLight.intensity = 1.1f;
                fillLight.color = new Color(1f, 0.95f, 0.88f);
                fillLight.shadows = LightShadows.None;
            }
            fillLight.enabled = rider.Crashing;
            if (rider.Crashing) fillLight.transform.position = cam.transform.position + Vector3.up * 0.6f;
            world.UpdateFlag(dt, cam.transform.position);
            loot.Animate(dt, sled.position.z, Time.time);
            rider.glassCam.enabled = debugGlassFull || (!rider.Crashing && (state == State.Aim || state == State.Run || (state == State.Result && resultDelay > 0f)));
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
            if (pull >= 0.97f) { if (fullPullTime < 0f) fullPullTime = Time.time; }
            else if (pull < 0.95f) fullPullTime = -1f;
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
            RunHints();
            accumulator += dt;
            if (rocketRequested || Input.GetKeyDown(KeyCode.R)) TryFireRocket();
            rocketRequested = false;
            while (accumulator >= PhysicsStep)
            {
                accumulator -= PhysicsStep;
                // İvmeölçeri olmayan telefonda oyuncu bardağı eğemez: karakter dengelemeyi büyük ölçüde üstlenir.
                reflex.Step(sled, NoTiltDevice ? Mathf.Max(cfg.glassReflex, 0.9f) : cfg.glassReflex, PhysicsStep);
                if (debugAutopilot)
                {
                    pilot.StepGlass(sled, PhysicsStep);
                    if (AutoPilot.ShouldFire(track, sled, cfg)) TryFireRocket();
                    float st = float.IsNaN(debugSteer) ? AutoPilot.Steer(track, sled) : debugSteer;
                    sled.Step(st, AutoPilot.Pitch(track, sled, cfg), PhysicsStep);
                }
                else sled.Step(input.steer, input.pitch, PhysicsStep);
                liquid.lidClosed = sled.position.z < cfg.lidDistance;
                if (!liquid.lidClosed && !lidOpen)
                {
                    lidOpen = true;
                    rider.PopLid();
                    sfx.Pop();
                    ui.LidOpened();
                    ShowHint(Hint.Milk, IconKind.Glass, input.TiltMode ? "Telefonu eğ: süt dökülmesin" : "I J K L: bardağı eğ, süt dökülmesin", 3);
                }
                float fillBefore = liquid.FillRatio;
                liquid.Step(sled.acceleration, GlassRotation(), PhysicsStep);
                float spilled = fillBefore - liquid.FillRatio;
                if (spilled > 0f) sled.stats.milkLost[(int)RunStats.Classify(sled, track)] += spilled;
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
            if (sled.rocketPickupTaken && !rocketPickupShown)
            {
                rocketPickupShown = true;
                loot.TakeRocket();
                rider.RocketPickup();
                sfx.Gem();
                shakeAmount = Mathf.Max(shakeAmount, 0.08f);
            }
        }

        bool hintedRocket, hintedNose, hintedBridge, hintedFork;

        /// Aşamalı öğretim: ipucu, oyuncu o anı ilk birkaç kez yaşarken gösterilir (yol haritası aşama 5).
        void ShowHint(Hint h, IconKind icon, string text, int times)
        {
            if (save.TutorialCount(h) >= times) return;
            save.BumpTutorial(h);
            ui.ShowHint(icon, text);
        }

        /// Koşu sırasında bağlama göre ipuçları: roket hazır, ilk gerçek uçuş, köprü yaklaşıyor.
        void RunHints()
        {
            if (!hintedRocket && cfg.rocketCharges > 0 && runTime > 1.5f && sled.CanFireRocket)
            {
                hintedRocket = true;
                ShowHint(Hint.Rocket, IconKind.Rocket, input.TiltMode ? "Roket: kızak hızlıyken bas" : "R: roketi hızlıyken ateşle", 3);
            }
            if (!hintedNose && !sled.grounded && !sled.launching
                && sled.position.y - track.Height(sled.position.x, sled.position.z) > 1.5f)
            {
                hintedNose = true;
                ShowHint(Hint.Nose, IconKind.Wing, input.TiltMode ? "Havada yukarı-aşağı sürükle: burun" : "W / S: havada burun", 2);
            }
            if (!hintedBridge && track.BridgeAt(sled.position.z + 30f, out _))
            {
                hintedBridge = true;
                ShowHint(Hint.Bridge, IconKind.Runners, "İnce köprü! Ortadan düz git", 3);
            }
            if (!hintedFork && track.ForkAt(sled.position.z + 25f, out _))
            {
                hintedFork = true;
                ShowHint(Hint.Fork, IconKind.Runners, "Yol ayrılıyor! Şeridini seç", 3);
            }
        }

        void TryFireRocket()
        {
            if (!sled.FireRocket())
            {
                // Havada alınan son roket hakkı inişe kadar kilitli: oyuncuya neden basamadığını söyle.
                if (state == State.Run && sled.pickupAwaitingLanding && sled.rocketCharges > 0)
                {
                    ui.Toast("İNİNCE HAZIR!");
                    sfx.Deny();
                }
                return;
            }
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

        // Otomatik oyuncu tam dengeler; gerçek oyuncunun eğmesi karakterin refleksine eklenir.
        float GlassPitch => debugAutopilot ? pilot.glassPitch : Mathf.Clamp(reflex.pitch + input.glassPitch, -50f, 50f);
        float GlassRoll => debugAutopilot ? pilot.glassRoll : Mathf.Clamp(reflex.roll + input.glassRoll, -50f, 50f);
        readonly GlassReflex reflex = new GlassReflex();

        Quaternion GlassRotation() => sled.Orientation * Quaternion.Euler(GlassPitch, 0f, -GlassRoll);

        // ---------------------------------------------------------------- kamera

        /// Test: kamerayı bir noktaya sabitler (yüzey yakın çekimleri). null = normal kamera.
        Vector3? debugCamPos, debugCamLook;

        /// Verilen türdeki 300 m'den sonraki ilk lekeye 9 m geriden, 4,5 m yukarıdan bakar (false: kamerayı serbest bırakır).
        public bool DebugViewSurface(Surface type, bool on = true)
        {
            debugCamPos = debugCamLook = null;
            ui.DebugSetVisible(!on);
            if (!on || track == null) return false;
            foreach (var s in track.surfaces)
            {
                if (s.type != type || s.z0 < 300f) continue;
                float x = (s.onPath ? track.PathX(s.cz) : track.CenterX(s.cz)) + s.cx;
                var c = new Vector3(x, track.Height(x, s.cz), s.cz);
                debugCamLook = c;
                debugCamPos = c + new Vector3(2f, 7.5f, -7f);   // yukarıdan: tepe arkasında kalmasın
                return true;
            }
            return false;
        }

        Vector3 CameraTarget(out Vector3 look)
        {
            if (debugCamPos.HasValue)
            {
                look = debugCamLook.Value;
                return debugCamPos.Value;
            }
            Vector3 p = sled.position;
            if (state == State.Hub)
            {
                // Ana ekran: kamera karakterin önünde, hafifçe sağa-sola salınır (referanstaki gibi yüz yüze).
                // Dikey ekranda yatay görüş dar (~31°): kanatlar büyüdükçe kamera geri çekilir.
                bool closeUp = ui != null && ui.Current == GameUI.Page.Avatars;
                float a = 0.35f * Mathf.Sin(orbit * 0.3f);
                float radius = closeUp ? 3.6f : 5.4f + 1.2f * cfg.wingSpan;
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
                // Yakın plan: karakter (ayağa kalkınca) ekranın üçte birini doldurur, başının üstünde baloncuğa yer kalır.
                float side = f.x > track.CenterX(f.z) ? -1f : 1f;
                look = f + new Vector3(0f, 0.45f, 0f);
                return f + new Vector3(side * 1.75f, 0.85f, 1.55f);
            }
            // Koşu: kamera vadinin yönüne hizalı durur (vadi kıvrıldıkça döner), kızağı yana doğru kısmen izler.
            float c = track.CenterX(p.z);
            float bend = (track.CenterX(p.z + 6f) - track.CenterX(p.z - 6f)) / 12f;
            float lateral = (p.x - c) * 0.85f;   // yamaca çıkan kızak ekranın ortasına yakın kalsın
            float lookX = c + bend * 8f + lateral;
            // İnişte bakış biraz aşağı: yamaç yukarıdan okunsun (ileri zeminle kızak arası).
            // Vadinin üstünde (zemin çok aşağıda) kamera suya dönmesin: bakış kızağın en çok 1.5 m altına iner.
            float lookY = Mathf.Max(Mathf.Lerp(p.y + 1f, track.Height(lookX, p.z + 8f) + 1.2f, 0.5f), p.y - 1.5f);
            // Yol haritası aşama 6: kamera 8 m yerine 6.5 m geride, biraz alçak (karakter büyük görünür); bakış 8 m ileri.
            look = new Vector3(lookX, lookY, p.z + 8f);
            var camPos = new Vector3(c - bend * 6.5f + lateral, p.y + 2.6f, p.z - 6.5f);
            camPos.y = AboveGround(camPos, p + new Vector3(0f, 0.8f, 0f));
            return camPos;
        }

        /// Kameranın yüksekliği: kendi altındaki zeminden en az 2 m yukarıda ve kızağa bakan görüş çizgisi araziye
        /// girmeyecek kadar yüksek (tepe aşılıp inişe geçilince kamera arkadaki yamacın içinde kalıyordu: yol alttan
        /// şeffaf görünüyordu).
        float AboveGround(Vector3 camPos, Vector3 target)
        {
            float y = Mathf.Max(camPos.y, track.Height(camPos.x, camPos.z) + 2f);
            for (int i = 1; i <= 6; i++)
            {
                float u = i / 7f;
                float qx = Mathf.Lerp(camPos.x, target.x, u), qz = Mathf.Lerp(camPos.z, target.z, u);
                float need = track.Height(qx, qz) + 0.9f;
                // Doğru üzerindeki nokta: y + u·(hedef − y) ≥ need  →  y ≥ (need − u·hedef) / (1 − u)
                y = Mathf.Max(y, (need - u * target.y) / (1f - u));
            }
            return y;
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
            // Yumuşak takip geride kalsa da kamera zeminin altına inmez.
            if (state == State.Run && !rider.Crashing && track != null)
                pos.y = Mathf.Max(pos.y, track.Height(pos.x, pos.z) + 1.5f);
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
            ui.OpenQuests += () => RefreshQuests(true);
            ui.ClaimQuest += ClaimQuestReward;
            ui.ClaimLogin += ClaimLoginReward;
            ui.TripleEarnings += () =>
            {
                save.money += 2 * lastEarned;   // kazanç zaten eklendi: 3 kat için 2 kat daha
                lastEarned = 0;
                save.Save();
            };
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
            ui.WatchLaunchAd += () =>
            {
                save.RegenLaunches(System.DateTime.UtcNow.Ticks);
                save.launches = Mathf.Min(SaveData.MaxLaunches, save.launches + 1);
                save.Save();
                sfx.Coin();
            };
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
            ui.SetSensitivity += v =>
            {
                save.steerSens = v;
                input.sensitivity = v;
                save.Save();
            };
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
            if (lvl >= Upgrades.MaxLevel(i))
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
                string detail = i == (int)UpgradeType.Runners ? Vehicles.StageName(TrackLibrary.Vehicles[save.track], stage).ToUpper(tr)
                              : i == (int)UpgradeType.Glass ? Upgrades.GlassStageName(stage).ToUpper(tr)
                              : "YENİ GÖRÜNÜM";
                ui.StageUp(Upgrades.Name(i).ToUpper(tr) + " " + stage + ". SEVİYE!\n" + detail);
                rider.Punch();
            }
        }

        float launchCheck;

        /// Saniyede bir: süre dolduysa fırlatma hakkı ekler ve kaydeder.
        void TickLaunches(float dt)
        {
            launchCheck -= dt;
            if (launchCheck > 0f) return;
            launchCheck = 1f;
            if (save.RegenLaunches(System.DateTime.UtcNow.Ticks)) save.Save();
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
                sensitivity = save.steerSens,
                distance = Mathf.FloorToInt(sled.maxDistance),
                progress = Mathf.Clamp01(sled.maxDistance / track.finishZ),
                bestProgress = Mathf.Clamp01(save.best / track.finishZ),
                speedKmh = sled.Speed * 3.6f,
                fill = liquid.FillRatio,
                milkDanger = state == State.Run ? liquid.Danger : 0f,
                pull = pull,
                perfectWindow = state == State.Aim && PerfectWindowOpen,
                runTime = runTime,
                // Yalnızca gerçek uçuşta (yerden 1.2 m'den yüksek) yazı çıkar; "SÜZÜL!" kanat hâlâ taşıyorsa.
                airborne = state == State.Run && !sled.grounded && !sled.launching
                           && sled.position.y - track.Height(sled.position.x, sled.position.z) > 1.2f,
                canGlide = sled.WingsOpen,
                dragging = aimDragging,
                tiltMode = input.TiltMode,
                glassRect = rider.glassCam.rect,
                lidLeft = lidOpen ? 0f : cfg.lidDistance - (state == State.Run ? sled.position.z : 0f),
                trackIndex = save.track,
                trackName = track.name,
                hasRocket = cfg.rocketCharges > 0 || (state == State.Run && (sled.rocketCharges > 0 || sled.RocketFiring)),
                rocketReady = state == State.Run && sled.CanFireRocket,
                rocketCharges = sled.rocketCharges,
                gems = save.gems,
                runCoins = loot.coins,
                runGems = loot.gems,
                chestSeconds = ChestSeconds(),
                launches = save.launches,
                freeLaunches = save.LaunchesFree,
                crashScene = rider.Crashing,
                launchSeconds = save.SecondsToNextLaunch(System.DateTime.UtcNow.Ticks),
                rocketFuel = sled.RocketFiring ? Mathf.Clamp01(sled.rocketTime / Mathf.Max(sled.rocketBurn, 0.01f)) : 0f,
            };
        }
    }
}
