using UnityEngine;

namespace SlingRiders
{
    public enum RunEnd { None, Stopped, Crashed, Spilled, Finished }
    /// Stable gameplay reason; display text can change or be translated independently.
    public enum CrashCause { None, HardLanding, Flip, OffTrack, Ravine, Valley, Wall, Obstacle, Birds }

    /// Kızağın fizik modeli.
    /// Zemindeyken kızak yüzeyi takip eder: yerçekimi bileşeni, kinetik sürtünme (μ·N)
    /// ve hava direnci etki eder. Zemin, yerçekiminin çekebileceğinden hızlı "kaçarsa"
    /// (rampa ucu, tepe üstü) normal kuvvet sıfırlanır ve kızak havalanır.
    /// Havada yerçekimi + hava direnci + (varsa) kanat kaldırma kuvveti çalışır.
    public class SledPhysics
    {
        /// Yerçekimi (m/s²): pist başına (Mars 0.38 g). Kızak kurulurken pistten alınır; sıvı, refleks ve otomatik
        /// pilot da bunu okur (aynı anda tek pist koşar).
        public static float Gravity = 9.81f;
        public const float LowSpeedBrake = 0.45f;   // m/s²
        public const float EarthGravity = 9.81f;
        const float AirDensity = 1.2f;
        public const float MaxAim = 0.44f;   // rad (~25°), sapanın en fazla yan açısı
        const float MaxHeading = 0.8f;   // rad (~46°): vadi kıvrılır, patika S çizer
        const float MaxSideGrip = 16f;   // m/s², yana dönüşte en büyük ivme (kıvrımlı patikayı izleyebilsin)
        const float MaxTurnRate = 1.6f;  // rad/s
        const float DeckAlignRate = 1.0f;
        const float ApproachAssist = 35f, ApproachAlignRate = 0.7f;   // m, rad/s: köprüye yaklaşma yardımı   // rad/s: köprü tablasında kızağın kendiliğinden hizalanma hızı
        const float PitchRate = 1.8f;    // rad/s
        /// Kanadın profil sürükleme katsayısı. Küçük kanatlarda 0.12; büyük (üst seviye) kanatlar daha ince ve temizdir:
        /// toplam profil sürüklemesi (Cd0·alan) sabit kalır, böylece büyük kanat kızağı yavaşlatmaz, daha iyi süzülür.
        static float WingCd0(float area) => Mathf.Min(0.12f, 0.19f / Mathf.Max(area, 0.01f));

        static float AspectRatio(float wingArea) => 2f + wingArea;

        /// En iyi süzülme oranı (kaldırma/sürükleme): küçük kanatta ~3, en büyükte ~4,8.
        public static float MaxGlideRatio(float wingArea) =>
            0.5f * Mathf.Sqrt(Mathf.PI * AspectRatio(wingArea) * 0.8f / WingCd0(wingArea));  // rad, kanatlı kızağın kendiliğinden tuttuğu hücum açısı (en iyi süzülmeye yakın)
        const float MaxPitch = 1.2f;     // rad
        /// Kanatsız kızakta burnun uçuş yolundan bu kadar (rad, ~26°) fazla kalkması dengeyi bozar.
        public const float UnstableExcess = 0.45f;
        /// Kanatsız kızağın dengesi bozuk (burun fazla kalktı): oyuncu öne itmeli.
        public bool Unstable;
        const float WallSlope = 1.4f;    // ~55°, bundan dik yüzey duvardır
        const float HalfWidth = 0.38f;
        /// Köprüden dereye düşme sonu (oyun su sıçratır).
        public const string FellInCreek = "Fell in the creek!";
        public const string WentOffTrack = "Off the track!";
        const float HalfLength = 0.8f;

        public Vector3 position;
        public Vector3 velocity;
        public Vector3 acceleration;     // son adımın ivmesi (bardak bunu hisseder)
        public float pitch;              // rad, burun yukarı +
        public float heading;            // rad, sağa +
        public float lean;               // rad, viraja yatma
        public bool grounded = true;
        public bool launching;
        public RunEnd end;
        public string endReason = "";
        public CrashCause crashCause;
        public float maxDistance;
        public float topSpeed;
        public int landings;             // her inişte artar (ses/efekt için)
        public float clock;              // atışın kendi saati (kuş sürüleri buna göre salınır)
        public float lastImpact;         // son inişin dik hızı (m/s)
        public float rocketTime;         // kalan yanma süresi (s)
        public int rocketCharges;        // kalan ateşleme hakkı
        public float rocketThrust, rocketBurn;   // takılı roket (havada alınan hak, roketsizse 1. seviye roket verir)
        public bool rocketPickupTaken;   // pistteki havada alınan roket hakkı alındı mı
        public bool RocketFiring => rocketTime > 0f && end == RunEnd.None;
        public bool CanFireRocket => rocketCharges > 0 && rocketTime <= 0f && !launching && end == RunEnd.None
                                     && !(pickupAwaitingLanding && rocketCharges <= 1);
        /// Havada alınan roket hakkı ancak yere inince ateşlenebilir: vadinin üstünde ateşlenip her genişlikte vadiyi
        /// geçirmesin (kanat seviyesi kapısı anlamlı kalsın).
        public bool pickupAwaitingLanding;
        /// Koşunun ölçümleri (yüzey kayıpları, süt, roket); sonuç ekranı ve simler okur.
        public readonly RunStats stats = new RunStats();
        /// Son inişten (ya da koşu başından) beri geçen süre (s).
        public float sinceLanding = 9f;
        /// Sapan lastiğinin bu atıştaki gücü: mükemmel bırakışta 1.06 (yol haritası aşama 3, fırlatma becerisi).
        public float launchBoost = 1f;
        public const float PerfectLaunchBoost = 1.06f;

        /// Hızlandırıcı: kızak ileri doğru BoostKick m/s kazanır (BoostDuration boyunca yayılır: 0.45 s'de
        /// verilen hız simde ortalama oyuncunun sütünü boşaltıyordu). Fırlatma hissi kamerada (GameController).
        /// 2026-10-23 (kullanıcı: "bir roket kadar hız kazandırmalı"): ~10 m/s, 1.2 s'ye yayılır (~0.85 g, roketin ivmesi kadar).
        public static float BoostKick = 10f;
        public const float BoostDuration = 1.2f;
        int lastBoost = -1;
        int lastBouncer = -1, iceIdx = -1;
        float iceTime;
        /// Bu atışta kırılan ince buzlar (görünüş okur).
        public bool[] brokenIce = new bool[0];
        public int bounces;
        /// Bu atıştaki sürpriz engeller (TrackProfile.hazards sırasıyla).
        public HazardState[] hazards = new HazardState[0];
        /// Çöken köprü: kızağın bastığı köprünün başlangıcı (r0) ve basış anı (clock); yoksa NaN.
        public float ledgeR0 = float.NaN, ledgeR1, ledgeEnter;
        /// Çöken köprünün track.bridges içindeki sırası (aynı deredeki yan köprüler ayrı çöker).
        public int ledgeBridge = -1;
        /// Çöküş cephesi (z): bunun gerisindeki taş düşmüştür (görünüş de bunu izler). Köprüye basılmadıysa -inf.
        public float CollapseFront => float.IsNaN(ledgeR0) ? float.NegativeInfinity
                                    : ledgeR0 + Mathf.Max(0f, clock - ledgeEnter - track.collapseDelay) * track.collapseSpeed;
        float boostTime;
        public bool Boosting => boostTime > 0f;

        float bankRoll;                  // rad, yamaçta gövdenin yana yatışı (sağ taraf yukarı +)
        float stillTime;
        float landHold;                  // inişten hemen sonra kızak zemine bastırılır (aynı tümsekte tekrar seğmez)
        Vector2 aimDir = Vector2.up;     // fırlatma yönü (x, z)

        readonly TrackProfile track;
        readonly SledConfig cfg;

        public SledPhysics(TrackProfile track, SledConfig cfg)
        {
            Gravity = track.gravity;
            this.track = track;
            this.cfg = cfg;
        }

        /// Gövde: gidiş yönüne bakar, zeminde yüzeye oturur (ileri eğim + yamaçta yana yatış), dönüşte içe yatar.
        public Quaternion Orientation =>
            Quaternion.Euler(-pitch * Mathf.Rad2Deg, heading * Mathf.Rad2Deg, (bankRoll - lean) * Mathf.Rad2Deg);

        public float Speed => velocity.magnitude;

        /// Sapan geriye çekilirken kızağı konumlandırır. pull 0..1, aim: fırlatma yönü (rad, sağa +).
        /// Kızak, fırlatma yönünün tam tersine, sapanın orta noktasından pull·maxStretch uzağa çekilir.
        public void PlaceForAim(float pull, float aim = 0f)
        {
            aim = Mathf.Clamp(aim, -MaxAim, MaxAim);
            aimDir = new Vector2(Mathf.Sin(aim), Mathf.Cos(aim));
            float stretch = pull * cfg.maxStretch;
            float x = -aimDir.x * stretch;
            float z = -aimDir.y * stretch;
            position = new Vector3(x, track.Height(x, z), z);
            velocity = acceleration = Vector3.zero;
            pitch = Mathf.Atan(track.SlopeZ(x, z));
            bankRoll = 0f;
            heading = aim;
            lean = 0f;
            landings = 0;
            lastImpact = 0f;
            grounded = true;
            launching = false;
            end = RunEnd.None;
            endReason = "";
            crashCause = CrashCause.None;
            stillTime = 0f;
            landHold = 0f;
            rocketTime = 0f;
            rocketCharges = cfg.rocketCharges;
            rocketThrust = cfg.rocketThrust;
            rocketBurn = cfg.rocketBurn;
            rocketPickupTaken = false;
            pickupAwaitingLanding = false;
            maxDistance = 0f;
            topSpeed = 0f;
            clock = 0f;
            sinceLanding = 9f;
            launchBoost = 1f;
            lastBoost = -1;
            boostTime = 0f;
            lastBouncer = iceIdx = -1;
            iceTime = 0f;
            bounces = 0;
            brokenIce = new bool[track.thinIce.Count];
            hazards = new HazardState[track.hazards.Count];
            for (int i = 0; i < hazards.Length; i++)
                hazards[i] = new HazardState { kind = track.hazards[i].kind,
                                               radius = track.hazards[i].kind == HazardKind.Snowball ? 1.6f : track.hazards[i].kind == HazardKind.Boulder ? 2.1f
                                                      : track.hazards[i].kind == HazardKind.Meteor ? 1.4f : 1.2f };
            ledgeR0 = float.NaN;
            ledgeBridge = -1;
            collapsing = false;
            stats.Reset();
        }

        public void Launch() => launching = true;

        public bool FireRocket()
        {
            if (!CanFireRocket) return false;
            if (stats.rocketsFired == 0) { stats.rocketSpeed = Speed; stats.rocketZ = position.z; }
            stats.rocketsFired++;
            rocketCharges--;
            rocketTime = rocketBurn;
            stillTime = 0f;
            return true;
        }

        public void ForceEnd(RunEnd reason, string text, CrashCause cause = CrashCause.None)
        {
            if (end != RunEnd.None) return;
            end = reason;
            endReason = text;
            crashCause = reason == RunEnd.Crashed ? cause : CrashCause.None;
        }

        public void Step(float steer, float pitchInput, float dt)
        {
            if (end != RunEnd.None) return;
            clock += dt;
            sinceLanding += dt;
            float zBefore = position.z;
            Vector3 before = velocity;

            // Yükselen havaya kanatla yeterince hızlı giren kızak yerden kalkar (2026-10-19): alçak rampadan sonra
            // kızak bölgeyi yerde geçiyor, rüzgâr hiç işe yaramıyordu. Kalkış hafif: yükselişi bölgenin kendisi verir.
            if (grounded && !launching && cfg.wingArea > 0f && Speed > 8f && track.UpdraftAt(position.z) > 1f)
            {
                grounded = false;
                airTime = 0f;
                velocity += track.Normal(position.x, position.z) * 2.2f;
            }
            if (track.fragileBridges) FragileBridge();
            if (track.thinIce.Count > 0) ThinIce(dt);
            if (track.bouncers.Count > 0 && grounded && !launching)
            {
                int bi = track.BouncerAt(position.x, position.z);
                if (bi >= 0 && bi != lastBouncer)
                {
                    lastBouncer = bi;
                    bounces++;
                    grounded = false;
                    airTime = 0f;
                    velocity.y = Mathf.Max(velocity.y, 0f) + TrackProfile.BouncePower * Mathf.Sqrt(Gravity / EarthGravity);
                    var flat = new Vector3(velocity.x, 0f, velocity.z);
                    if (flat.sqrMagnitude > 0.01f) velocity += flat.normalized * 1.5f;
                }
            }
            int boost = grounded && !launching ? track.BoostAt(position.x, position.z) : -1;
            if (boost >= 0 && boost != lastBoost)
            {
                lastBoost = boost;
                boostTime = BoostDuration;
                stats.boosts++;
                // Sıçrama yok: kızağı havaya atınca iniş darbesi simde ortalama oyuncunun sütünü her levhada %10-30 döküyordu.
            }
            if (grounded) StepGround(steer, dt);
            else StepAir(steer, pitchInput, dt);
            // Kanatsız kızakta itiş yalnız yerde: ağırlıksız sıçramada ileri itiş sütü bardaktan geri döküyordu. Kanatlı
            // kızak (taşıma bardağa "aşağı" hissi verir) rampadan sonra da itilir: rampadan uçuşa güçlü geçiş.
            if (!grounded && cfg.wingArea <= 0f) boostTime = 0f;
            if (boostTime > 0f && end == RunEnd.None)
            {
                var flat = new Vector3(velocity.x, 0f, velocity.z);
                // Yerçekimiyle ölçekli: Mars'ta aynı hız artışı düşük sürtünmeyle çok uzun sürüyor, parkur 2 atışta bitiyordu.
                if (flat.sqrMagnitude > 0.01f) velocity += flat.normalized * (BoostKick * (Gravity / EarthGravity) / BoostDuration * Mathf.Min(dt, boostTime));
                boostTime -= dt;
            }

            if (end == RunEnd.None)
            {
                OffTrack();
                CheckObstacles();
                CheckRocketPickup();
                UpdateHazards(dt);
            }

            acceleration = (velocity - before) / dt;

            if (end == RunEnd.None)
            {
                maxDistance = Mathf.Max(maxDistance, position.z);
                topSpeed = Mathf.Max(topSpeed, velocity.magnitude);
                // Aynı derenin yan köprüleri tek geçiş sayılır.
                float counted = float.NaN;
                foreach (var b in track.bridges)
                    if (zBefore < b.r1 && position.z >= b.r1 && b.r1 != counted) { stats.bridgesCrossed++; counted = b.r1; }
                // Çatalın ortasından geçerken hangi şeritte (altınlı mı) olduğu (günlük görevler)
                foreach (var f in track.forks)
                {
                    float mid = (f.z0 + f.z1) * 0.5f;
                    if (zBefore < mid && position.z >= mid && grounded)
                    {
                        stats.forksPassed++;
                        if ((position.x - track.CenterX(mid)) * f.good > 0f) stats.forksGood++;
                    }
                }
                if (position.z >= track.finishZ) ForceEnd(RunEnd.Finished, "Track complete!");
            }
        }

        /// Zeminde: hız her an yüzeye teğettir. Yerçekiminin yüzey boyunca bileşeni (iniş, yokuş, yamaç),
        /// normal kuvvetle orantılı kinetik sürtünme ve hava direnci uygulanır; direksiyon hızı yüzey normali
        /// etrafında döndürür (yanal tutunma sınırlı). Yamaca çıkan kızak yavaşlar, durur ve geri kayar.
        void StepGround(float steer, float dt)
        {
            float m = cfg.TotalMass;
            Vector3 n = track.Normal(position.x, position.z);
            Vector3 v = Vector3.ProjectOnPlane(velocity, n);
            float speed = v.magnitude;

            var g = new Vector3(0f, -Gravity, 0f);
            Vector3 a = Vector3.ProjectOnPlane(g, n);   // yüzey boyunca yerçekimi
            float normalAcc = Gravity * n.y;            // normal kuvvet / kütle

            Vector3 headDir = Vector3.ProjectOnPlane(new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading)), n).normalized;
            if (rocketTime > 0f)
            {
                a += headDir * (rocketThrust / m);   // roket kızağın baktığı yöne iter
                rocketTime -= dt;
            }
            // Kalıcı pervane: hız 2-8 m/s arasında açılır; sürtünmenin %70'ini aşmaz (düzlükte kızak yine durur).
            if (cfg.propThrust > 0f && !launching && end == RunEnd.None)
                a += headDir * (Mathf.Min(cfg.propThrust, 0.7f * cfg.groundFriction * Gravity) * Mathf.Clamp01((speed - 2f) / 6f));
            if (launching)
            {
                // Sapan lastiği: F = k·x, sapanın ortasına doğru. Kızak sapan hattını geçince lastik boşa düşer.
                if (position.x * aimDir.x + position.z * aimDir.y < 0f)
                    a += Vector3.ProjectOnPlane(new Vector3(-position.x, 0f, -position.z) * (cfg.bandStiffness * launchBoost / m), n);
                else
                {
                    launching = false;
                    stats.launchSpeed = speed;
                }
            }

            float mu = track.Friction(position.z, position.x, cfg.groundFriction);
            float friction = mu * normalAcc;
            // Düşük hızda kızak kara/toprağa gömülür (kullanıcı 2026-10-10: 20 km/sa ile düzlükte sürünerek gitmek turu
            // uzatıyordu): 6 m/s'nin altında ek fren, 2 m/s'de tam. Hızlı kızağı etkilemez.
            if (!launching && rocketTime <= 0f)
                friction += LowSpeedBrake * (Gravity / EarthGravity) * Mathf.Clamp01((6f - speed) / 4f);
            // Elle itme: tepeyi zar zor aşan kızak, sürtünme açısından yatık (~3-12°) inişte durup kalmasın; sürücü
            // elleriyle iterek dik kısma kadar ağır ağır ilerler (net ileri ivme en az 0.3 m/s²).
            // Yalnızca tepe aşıldıktan hemen sonra: 15 m içinde sürtünmeyi yenen dik bir yer varsa (oraya kadar itilir).
            float downhill = Vector3.Dot(a, headDir);
            if (!launching && speed < 2f && downhill > 0.5f)
                for (float d = 3f; d <= 15f; d += 3f)
                    if (-track.SlopeZ(position.x, position.z + d) > mu * 1.05f)
                    {
                        friction = Mathf.Min(friction, downhill - 0.3f);
                        break;
                    }
            // Ölçüm: patikadan fazla yavaşlatan yüzeyler (çamur, su, çimen) ve buzun kazandırdığı.
            if (!launching && speed > 0.3f)
            {
                var surf = track.SurfaceAt(position.z, position.x);
                float extra = (mu - cfg.groundFriction) * normalAcc + track.SurfaceResist(position.z, position.x) * (Gravity / EarthGravity) * speed * speed;
                var ls = surf == Surface.Mud ? LossSurface.Mud : surf == Surface.Puddle ? LossSurface.Puddle
                       : surf == Surface.Ice ? LossSurface.Ice : track.OnPath(position.z, position.x) ? LossSurface.Count : LossSurface.Grass;
                if (ls != LossSurface.Count)
                {
                    float along = Vector3.Dot(a, v / speed);   // yokuş aşağı +
                    float net = cfg.groundFriction * normalAcc - along + 0.5f * AirDensity * cfg.dragArea / m * speed * speed;
                    stats.AddGround(ls, speed * dt, extra, net);
                }
            }
            if (speed < 0.05f)
            {
                // Duruyor: itiş sürtünmeyi aşarsa harekete geçer.
                float push = a.magnitude;
                v = push <= friction ? Vector3.zero : v + a * ((push - friction) / push * dt);
            }
            else
            {
                float drag = (0.5f * AirDensity * cfg.dragArea / m + track.SurfaceResist(position.z, position.x) * (Gravity / EarthGravity)) * speed;   // su/çamur direnci yerçekimiyle (Mars tozu hafif)
                Vector3 vNew = v + (a - friction * v / speed - drag * v) * dt;
                // Sürtünme hareketi tersine çeviremez.
                if (Vector3.Dot(vNew, v) < 0f && a.magnitude <= friction) vNew = Vector3.zero;
                v = vNew;
            }

            // Direksiyon: hızı yüzey normali etrafında döndürür (enerji korunur). Oyuncu vadinin yönünden
            // MaxHeading'den fazla sapamaz; yerçekimi (yamaç) ise yönü serbestçe değiştirir.
            speed = v.magnitude;
            if (speed > 0.5f && !launching)
            {
                // Yanal tutuş yerçekiminden bağımsız (arcade): Mars'ta hover aracı iticilerle döner; tutuş yerçekimiyle
                // azalınca ortalama oyuncu köprüye hizalanamıyordu.
                float rate = Mathf.Min(MaxSideGrip * track.LateralGrip(position.z, position.x) / speed, MaxTurnRate);
                float turn = steer * rate * dt;
                float h = Mathf.Atan2(v.x, v.z);
                if ((turn > 0f && h > MaxHeading) || (turn < 0f && h < -MaxHeading)) turn = 0f;
                v = Quaternion.AngleAxis(turn * Mathf.Rad2Deg, n) * v;
            }
            // Köprü tablası ve yaklaşma yardımı kaldırıldı (kullanıcı 2026-10-19: bıraktığımda kızak kendiliğinden köprüye
            // dönüyor; yönü tamamen kullanıcı versin).
            if (speed > 0.3f) heading = Mathf.Atan2(v.x, Mathf.Max(v.z, 0.5f));
            lean = Mathf.MoveTowards(lean, steer * 0.22f, 1.2f * dt);

            Vector3 next = position + v * dt;
            // Köprü tablasından (ya da dere kıyısından) boşluğa kayan kızak havalanır ve dereye düşer. Neredeyse durmuş
            // kızak kıyıda kalır (dere kenarına sürünerek varıp düşmek haksız hissettiriyordu).
            if (track.OverRavine(next.x, next.z) && speed < 1.5f)
            {
                velocity = Vector3.zero;
                ForceEnd(RunEnd.Stopped, "Sled stopped");
                return;
            }
            if (track.OverRavine(next.x, next.z))
            {
                grounded = false;
                airTime = 0f;
                position = next;
                velocity = v;
                return;
            }
            float slopeAhead = track.SlopeZ(next.x, next.z);
            if (v.z > 0f && slopeAhead > WallSlope)
            {
                velocity = Vector3.zero;
                bool inGap = InGap(position.z);
                ForceEnd(RunEnd.Crashed, inGap ? "Fell into the valley!" : "Hit the wall!", inGap ? CrashCause.Valley : CrashCause.Wall);
                return;
            }

            // Havalanma: pist boyunca dışbükey bir yüzeyde (rampa ucu, tepe) gereken merkezcil ivme
            // yerçekiminin dik bileşenini aşarsa normal kuvvet sıfırlanır: -κ·v² > g·cosθ
            float curvature = track.CurvatureAt(position.x, position.z);
            landHold -= dt;
            // İnişten hemen sonra hafif kavislerde (tümsek tepesi) kızak zemine bastırılır; rampa dudağı gibi keskin
            // kavislerde her zaman havalanır.
            float lift = -curvature * v.z * v.z;
            bool hold = landHold > 0f && lift < 2.5f * normalAcc;
            if (!hold && v.z > 2f && lift > normalAcc)
            {
                grounded = false;
                airTime = 0f;
                position = next;
                velocity = v;
                return;
            }

            // Yeni noktada yüzeye otur, hızı yeni yüzeye teğet yap (büyüklüğü korunur).
            next.y = track.Height(next.x, next.z);
            Vector3 n2 = track.Normal(next.x, next.z);
            float sp = v.magnitude;
            Vector3 vt = Vector3.ProjectOnPlane(v, n2);
            v = vt.sqrMagnitude > 1e-8f ? vt.normalized * sp : Vector3.zero;
            position = next;
            velocity = v;
            SurfacePose(n2);

            // Atış biter: kızak pist boyunca geri gitmeye başlarsa ya da ileri hızı bitip neredeyse durmuşsa.
            // (Yamaca yan tırmanırken ileri hızı az olsa da devam eder.)
            if (!launching && maxDistance > 0.5f && (v.z < -0.3f || (v.z <= 0.05f && sp < 1.5f)))
            {
                velocity = Vector3.zero;
                ForceEnd(RunEnd.Stopped, "Sled stopped");
                return;
            }

            bool canRest = Vector3.ProjectOnPlane(g, n2).magnitude <= mu * Gravity * n2.y;
            if (!launching && sp < 0.2f && canRest)
            {
                stillTime += dt;
                if (stillTime > 0.6f) ForceEnd(RunEnd.Stopped, "Sled stopped");
            }
            else stillTime = 0f;
        }

        /// Gövdeyi zemine oturtur: gidiş yönündeki eğim (pitch) ve yana yatış (bankRoll).
        void SurfacePose(Vector3 n)
        {
            var forward = new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading));
            var right = new Vector3(forward.z, 0f, -forward.x);
            // Eğim = -n_yatay / n_y (yükseklik gradyanı)
            float alongSlope = -(n.x * forward.x + n.z * forward.z) / n.y;
            float acrossSlope = -(n.x * right.x + n.z * right.z) / n.y;
            pitch = Mathf.Atan(alongSlope);
            bankRoll = Mathf.Atan(acrossSlope);
        }

        /// Havada geçen süre (son kalkıştan beri). Kanat yalnızca GlideTime kadar taşır, sonra katlanır.
        public float airTime;

        /// Kanadın bir kalkışta taşıdığı süre: SledConfig.glideTime (Upgrades.GlideTime, kızak seviyesine göre).
        /// Kullanıcı kararı: süzülme sınırlı — yoksa pist süzülme açısından dik inince kızak hiç inmeden sona kadar uçuyordu.
        float GlideTime => cfg.glideTime;

        /// Kanat taşıması 0..1: süre dolunca 3 s içinde yumuşakça %50'ye iner (kullanıcı 2026-10-10: süzülme daha
        /// doğal olsun, biraz daha uzun sürsün; eskiden 1.5 s'de %35'e düşüyor, kanat birden katlanıyordu). Kızak burnunu
        /// indirip alçalır; havada asılı kalamaz ama sert de çakılmaz.
        public float WingLift => cfg.wingArea <= 0f ? 0f : grounded ? 1f
            : Mathf.Max(0.5f, 1f - Mathf.Max(0f, airTime - GlideTime) / 6f);

        /// Kanatlar tam açık mı (yerde her zaman; havada süre dolana kadar).
        public bool WingsOpen => cfg.wingArea > 0f && (grounded || airTime < GlideTime);

        void StepAir(float steer, float pitchInput, float dt)
        {
            airTime += dt;
            stats.airTime += dt;
            stats.longestAir = Mathf.Max(stats.longestAir, airTime);
            bool wingsOpen = cfg.wingArea > 0f;   // kanat taşıması süreyle azalır (WingLift), kanat kapanmaz
            float liftScale = WingLift;
            float area = cfg.wingArea * liftScale;
            float m = cfg.TotalMass;
            Vector3 v = velocity;
            float speed = v.magnitude;
            Vector3 a = new Vector3(0f, -Gravity, 0f);
            a -= 0.5f * AirDensity * cfg.dragArea / m * speed * v;
            // Yükselen hava: kanat alanıyla orantılı yukarı itiş (küçük kanat az, büyük kanat tam); taşıma süresi
            // tükenmez (kanat tutunur). Düşük yerçekiminde (Mars) itiş de oranlı.
            float updraft = cfg.wingArea > 0f ? track.UpdraftAt(position.z) * (Gravity / EarthGravity) : 0f;
            if (updraft > 0f)
            {
                // Hedef tırmanma hızına çeker (kanat büyüdükçe hızlı: ~2-4 m/s): yalnız ivme eklemek yavaş kızakta
                // yerçekimini ancak dengeliyordu, kızak bölgede 0.3-0.7 m yükseliyordu.
                // Hava hızına bağlı: yavaş kızak (7 m/s altı) yükselmez, 13 m/s'de tam (yavaşken 7.7 m'ye çıkıp bölge
                // çıkışında tutunamadan taş gibi düşüyor, "Landed too hard" oluyordu).
                // Kanat alanıyla orantılı (taban payı yok): küçük kanat (0.7 m²) 14 m/s'de ağırlığın ancak beşte birini
                // taşır; 12 m'ye çıkınca bölge çıkışında çakılıyordu. Küçük kanat birkaç metre, büyük kanat tam yükselir.
                float k = Mathf.Clamp01(cfg.wingArea / 2.2f) * Mathf.Clamp01((speed - 7f) / 6f);
                float climb = updraft * 0.65f * k;
                a.y += Gravity * 0.6f * k + Mathf.Max(0f, climb - v.y) * 2.5f;
                // Bölgedeyken taşıma süresi baştan: çıkışta kanadın tam süzülme süresi kalır (orta seviyede süre bölge
                // içinde bitiyor, kızak yüksekten düşüp "Landed too hard" oluyordu).
                airTime = 0f;
            }

            float gamma = Mathf.Atan2(v.y, Mathf.Max(v.z, 0.01f));   // uçuş yolu açısı
            if (Mathf.Abs(pitchInput) > 0.05f) pitch += pitchInput * PitchRate * dt;
            // Dokunulmazsa: kanatlı kızak kendiliğinden sabit bir süzülme yolu tutar (tırmanıp tutunmayı
            // kaybetmez); kanatsız kızak burnunu yavaşça uçuş yönüne çevirir.
            else if (wingsOpen)
            {
                // Süzülme denetimi: ~14°'lik iniş yolunu tutar; tırmanırsa hücum açısını azaltır, fazla dalarsa artırır.
                float glidePath = -Mathf.Atan(1f / (0.9f * MaxGlideRatio(cfg.wingArea) * liftScale));
                // Arazi takibi: altındaki yamaç süzülme yolundan dikse (vadi/dere üstü hariç) kızak yamacı izler,
                // birkaç metre yüksekte kalır; dik inişlerin üstünde yükselip sonra çakılmaz. Sığ süzülme
                // (sürtünmesiz, uzağa taşır) yalnızca vadi ve derelerin üstünde.
                if (updraft > 0f) glidePath = 0.35f;   // yükselen havada tırmanır (~20°); 0.06 iken kontrol itişi burnu indirerek boşa harcıyordu
                else if (!InGap(position.z) && !InGap(position.z + v.z * 0.8f))
                {
                    float above = position.y - track.Height(position.x, position.z);
                    float slopeAhead = Mathf.Atan(track.SlopeZ(position.x, position.z + Mathf.Max(v.z, 0f) * 0.5f));
                    // Yere yakınken (1.5 m altı) yamaçtan biraz dik iner, oturur (2026-10-23: dalgalı tepelerde hızla kalkan
                    // kızak 1 m yukarıda yamaca paralel yüzlerce metre "süzülüyor", değip kalkarak titriyordu).
                    if (above <= 1.5f) glidePath = Mathf.Min(glidePath, slopeAhead - 0.12f);
                    // Yamacın eğimini izler (0.5 s ileride), üstüne yüksekliğe orantılı dalış: yüksekteyken yere yaklaşır.
                    if (above > 1.5f)
                    {
                        // En az ~6°: yamacın birkaç metre üstünde asılı kalmasın (süzülme süresini boşa harcamasın), yumuşakça değsin.
                        // Dalış en çok ~29° (2026-10-19): yükselen havadan sonra 18 m yüksekten 49°'lik dalışla 11 m/s ile
                        // yere çakılıp "Landed too hard" oluyordu.
                        float dive = Mathf.Clamp(Mathf.Atan((above - 1f) / Mathf.Max(10f, v.z * 1.5f)), 0.1f, 0.5f);
                        glidePath = Mathf.Clamp(slopeAhead - dive, -1.2f, 0.3f);
                    }
                }
                // İniş yolu en çok ~26°, hızlı düşüşte kanat tam açıyla tutunur (2026-10-19): yükselen havadan sonra 12 m
                // yüksekten hücum açısı 0'a inip kızak serbest düşüyor, 14 m/s ile çakılıyordu.
                if (updraft <= 0f && position.y - track.Height(position.x, position.z) > 3f) glidePath = Mathf.Max(glidePath, -0.45f);
                float alpha = Mathf.Clamp(0.12f + 1.5f * (glidePath - gamma), 0f, 0.24f);
                if (v.y < -7f) alpha = 0.24f;
                pitch = Mathf.MoveTowards(pitch, gamma + alpha, 1.5f * dt);
            }
            else
            {
                // Kanatsız (kullanıcı 2026-10-24: "roketle rampada kızağın önü havaya kalkıp geri yatıyor; hafif kalkmalı"):
                // burun uçuş yoluna hızla döner (0.5 rad/s'de geride kalıp roketle daha da kalkıyordu), hafif yukarıda durur.
                // Aşırı kalkarsa denge bozulur: burun kendiliğinden kalkmaya devam eder; oyuncu parmağını öne iterek düzeltir
                // (PitchRate devrilmeden hızlı), yoksa ters iner ("You flipped!").
                float excess = pitch - gamma;
                if (excess > UnstableExcess) pitch += (excess - UnstableExcess * 0.5f) * 0.9f * dt;
                else pitch = Mathf.MoveTowards(pitch, gamma + 0.06f, 1.6f * dt);
            }
            Unstable = !wingsOpen && pitch - gamma > UnstableExcess;
            pitch = Mathf.Clamp(pitch, -MaxPitch, MaxPitch);
            // Kanatlı kızak (kuyruklu planör gibi) kendini akışa hizalar: burun akışın
            // altına bastırılamaz, böylece eksi g oluşmaz; en fazla ~25° kabartılabilir.
            if (wingsOpen) pitch = Mathf.Clamp(pitch, gamma, gamma + 0.45f);
            // Hızlı düşüşte kanat kendiliğinden tutunur (burun dalışta bile en az 0.24 rad hücum açısı): oyuncu ya da pilot
            // burnu yamaca indirince taşıma sıfırlanıp kızak yükselen havadan sonra yere çakılıyordu.
            if (wingsOpen && v.y < -7f) pitch = Mathf.Max(pitch, gamma + 0.24f);

            if (wingsOpen)
            {
                float vzy = Mathf.Sqrt(v.y * v.y + v.z * v.z);
                if (vzy > 1f)
                {
                    float cl = LiftCoefficient(pitch - gamma);
                    float q = 0.5f * AirDensity * vzy * vzy;
                    // Kısa, kalın kızak kanadı: profil sürüklemesi yüksek (planör gibi uçmaz). Büyük kanadın açıklığı
                    // daha geniştir (AR = 2 + alan): daha az indüklenmiş sürükleme, daha iyi süzülme.
                    float cd = WingCd0(cfg.wingArea) + cl * cl / (Mathf.PI * AspectRatio(cfg.wingArea) * 0.8f);
                    var liftDir = new Vector3(0f, v.z, -v.y) / vzy;
                    var dragDir = new Vector3(0f, v.y, v.z) / vzy;
                    a += q * cfg.wingArea / m * (cl * liftScale * liftDir - cd * dragDir);
                }
            }

            if (rocketTime > 0f)
            {
                a += Orientation * Vector3.forward * (rocketThrust / m);
                rocketTime -= dt;
            }

            // Havada yana itiş; gövde uçak gibi dönüşe yatar (koordineli dönüş): hissedilen ivme bardağın dibine
            // doğru kalır, süt yana dökülmez.
            float steerAcc = steer * (1.5f + area * 0.8f);
            a.x += steerAcc;
            lean = Mathf.MoveTowards(lean, Mathf.Atan2(steerAcc, Gravity), 3f * dt);

            velocity += a * dt;
            position += velocity * dt;
            heading = Mathf.Atan2(velocity.x, Mathf.Max(velocity.z, 0.5f));
            bankRoll = Mathf.MoveTowards(bankRoll, 0f, 1.5f * dt);

            float ground = track.Height(position.x, position.z);
            // Tabladan düşüp altına girmiş kızak tablaya alttan konamaz: zemini derenin kendisidir.
            bool underDeck = track.OnDeck(position.x, position.z) && (position.y < ground - 0.3f || collapsing);
            if (underDeck) ground = track.TerrainHeight(position.x, position.z);
            if ((underDeck || track.OverRavine(position.x, position.z)) && position.y <= ground + 0.9f)
            {
                velocity = Vector3.zero;
                stats.fellInRavine = true;
                ForceEnd(RunEnd.Crashed, collapsing ? "The rock bridge collapsed!" : track.fallText, CrashCause.Ravine);
                return;
            }
            if (position.y <= ground) Land(ground);
        }

        /// İnce kanat: küçük açıda CL ≈ 2π·α, ~15°'den sonra stall.
        static float LiftCoefficient(float alpha)
        {
            const float stall = 0.26f;
            const float slope = 2f * Mathf.PI * 0.9f;
            float abs = Mathf.Abs(alpha);
            if (abs <= stall) return slope * alpha;
            float peak = slope * stall;
            return Mathf.Sign(alpha) * peak * Mathf.Max(0.35f, 1f - (abs - stall) * 3f);
        }

        /// Vadi/dere tabanında mı (track.water: x..y = suyun z aralığı)?
        bool InGap(float z)
        {
            foreach (var w in track.water)
                if (z > w.x - 3f && z < w.y + 3f && !track.BridgeAt((w.x + w.y) * 0.5f, out _)) return true;
            return false;
        }

        void Land(float ground)
        {
            pickupAwaitingLanding = false;
            sinceLanding = 0f;
            Vector3 n = track.Normal(position.x, position.z);
            float vn = Vector3.Dot(velocity, n);   // yüzeye dik (eksi = çarpma)
            float impact = -vn;
            position.y = ground;
            landings++;
            lastImpact = impact;
            float slope = track.SlopeZ(position.x, position.z);

            if (impact > cfg.crashImpact)
            {
                velocity = Vector3.zero;
                bool inGap = InGap(position.z);
                ForceEnd(RunEnd.Crashed, inGap ? "Fell into the valley!" : slope > WallSlope ? "Hit the wall!" : "Landed too hard!",
                    inGap ? CrashCause.Valley : slope > WallSlope ? CrashCause.Wall : CrashCause.HardLanding);
                return;
            }

            float airPitch = pitch;
            SurfacePose(n);
            float mismatch = Mathf.Abs(airPitch - pitch);
            if (mismatch > 1.0f && impact > 4f)
            {
                velocity = Vector3.zero;
                ForceEnd(RunEnd.Crashed, "You flipped!", CrashCause.Flip);
                return;
            }

            // Yüzeye paralel inmek hızı korur, burun üstü inmek hız kaybettirir.
            float keep = (1f - 0.6f * Mathf.Clamp01((mismatch - 0.25f) / 0.75f)) * (1f - 0.015f * impact);
            Vector3 vt = Vector3.ProjectOnPlane(velocity, n) * keep;
            if (vt.z < 0f) vt.z = 0f;
            velocity = Vector3.ProjectOnPlane(vt, n);
            grounded = true;
            landHold = 0.12f;
        }

        /// Havada asılı roket hakkı: içinden geçen kızağa bir ateşleme hakkı ekler.
        void CheckRocketPickup()
        {
            if (!track.hasRocketPickup || rocketPickupTaken) return;
            float r = TrackProfile.RocketPickupRadius;
            if ((position + new Vector3(0f, 0.6f, 0f) - track.rocketPickup).sqrMagnitude > r * r) return;
            rocketPickupTaken = true;
            rocketCharges++;
            if (!grounded) pickupAwaitingLanding = true;
            if (rocketThrust <= 0f) { rocketThrust = Upgrades.RocketThrust(1); rocketBurn = Upgrades.RocketBurn(1); }   // roketsiz kızağa 1. seviye roket
        }

        /// Yan yamaçta görünmez duvar yok: yamacın tepesini aşıp pistin dışına çıkan kızak orada düşer
        /// (kullanıcı kararı 2026-10-08). Tepeye çıkmak için yamaca hızla, dik açıyla sürmek gerekir.
        void ThinIce(float dt)
        {
            int i = grounded ? track.ThinIceAt(position.x, position.z) : -1;
            if (i < 0 || brokenIce[i]) { iceIdx = -1; iceTime = 0f; return; }
            if (i != iceIdx) { iceIdx = i; iceTime = 0f; }
            iceTime += dt;
            if (iceTime > TrackProfile.IceHold && end == RunEnd.None)
            {
                brokenIce[i] = true;
                stats.fellInRavine = true;
                velocity = Vector3.zero;
                ForceEnd(RunEnd.Crashed, track.fallText, CrashCause.Ravine);
            }
        }

        void UpdateHazards(float dt)
        {
            for (int i = 0; i < hazards.Length && end == RunEnd.None; i++)
            {
                var h = hazards[i];
                if (h.phase == 3) continue;
                if (h.phase == 0)
                {
                    if (position.z < track.hazards[i].z || launching) continue;
                    h.phase = 1;
                    h.t = 0f;
                    float cx = track.CenterX(position.z);
                    if (h.kind == HazardKind.Boulder)
                    {
                        // Arkada doğar: kaçmak için hız gerekir.
                        float z = position.z - HazardState.BoulderBehind;
                        h.pos = new Vector3(position.x, track.TerrainHeight(position.x, z) + h.radius, z);
                    }
                    else if (h.kind == HazardKind.Snowball)
                    {
                        // Önde, kızağın şimdiki hizasında belirir (yana kaçmak gerekir).
                        float z = position.z + HazardState.SnowballLead;
                        float x = Mathf.Clamp(position.x, track.CenterX(z) - track.halfWidth + h.radius, track.CenterX(z) + track.halfWidth - h.radius);
                        h.pos = new Vector3(x, track.TerrainHeight(x, z) + h.radius, z);
                    }
                    else
                    {
                        // Kızağın düşüş süresi sonra varacağı yer (şimdiki hızla); yanda duvar/yamaç içine düşmez.
                        float T = h.FallDuration;
                        float z = position.z + Mathf.Max(velocity.z, 4f) * T;
                        float half = (track.wallHalf > 0f ? track.WallHalfAt(z) : track.halfWidth) - h.radius - 0.2f;
                        float x = Mathf.Clamp(position.x + velocity.x * T * 0.5f, track.CenterX(z) - half, track.CenterX(z) + half);
                        h.pos = new Vector3(x, track.TerrainHeight(x, z), z);
                    }
                    continue;
                }
                h.t += dt;
                if (h.kind == HazardKind.Boulder)
                {
                    h.pos.z += HazardState.BoulderSpeed * dt;
                    h.pos.x = Mathf.MoveTowards(h.pos.x, position.x, 1.2f * dt);   // kızağın yanına yavaşça kayar
                    h.pos.y = track.TerrainHeight(h.pos.x, h.pos.z) + h.radius;
                    if (h.t * HazardState.BoulderSpeed > HazardState.BoulderRun || h.pos.z < position.z - 160f) { h.phase = 3; continue; }
                    Vector2 d = new Vector2(position.x - h.pos.x, position.z - h.pos.z);
                    if (d.magnitude < h.radius + HalfWidth && position.y < h.pos.y + h.radius * 0.8f) Hit(h, "Crushed by the boulder!");
                    continue;
                }
                if (h.kind == HazardKind.Snowball)
                {
                    h.pos.z -= HazardState.SnowballSpeed * dt;
                    h.pos.y = track.TerrainHeight(h.pos.x, h.pos.z) + h.radius;
                    if (h.pos.z < position.z - 12f || h.t > 14f) { h.phase = 3; continue; }
                    Vector2 d = new Vector2(position.x - h.pos.x, position.z - h.pos.z);
                    if (d.magnitude < h.radius + HalfWidth && position.y < h.pos.y + h.radius * 0.8f) Hit(h, "Hit by a snowball!");
                    continue;
                }
                if (h.phase == 1 && h.t >= h.FallDuration) h.phase = 2;   // indi: yolda kaya
                if (h.phase == 2)
                {
                    if (position.z > h.pos.z + h.radius + 2f) { h.phase = 3; continue; }
                    Vector2 d = new Vector2(position.x - h.pos.x, position.z - h.pos.z);
                    float top = h.pos.y + h.radius * 1.4f;
                    if (d.magnitude < h.radius + HalfWidth && position.y < top)
                        Hit(h, h.kind == HazardKind.Meteor ? "Hit by a meteor!" : "Hit by a falling rock!");
                }
            }
        }

        void Hit(HazardState h, string text)
        {
            h.hit = true;
            velocity = Vector3.zero;
            ForceEnd(RunEnd.Crashed, text, CrashCause.Obstacle);
        }

        void FragileBridge()
        {
            // Yeni köprüye basış: çatlama başlar (önceki köprünün kaydı unutulur).
            if (grounded && track.OnDeck(position.x, position.z) && track.BridgeAt(position.z, position.x, out var b)
                && b.r0 != ledgeR0 && position.z > b.r0 - 0.5f)
            {
                ledgeR0 = b.r0;
                ledgeR1 = b.r1;
                ledgeEnter = clock;
                ledgeBridge = track.bridges.IndexOf(b);
            }
            if (float.IsNaN(ledgeR0) || end != RunEnd.None) return;
            // Kızak çöken kısmın üstünde (yarığın içinde, yerde ya da alçakta): taş altından gider.
            // Kızak çöken kısmın üstünde: taşla birlikte yarığa düşer (yere değince koşu biter, StepAir).
            if (!collapsing && position.z > ledgeR0 && position.z < ledgeR1 && position.z < CollapseFront
                && track.OnDeck(position.x, position.z))
            {
                collapsing = true;
                stats.collapsed = true;
                grounded = false;
                airTime = 0f;
                velocity.y = Mathf.Min(velocity.y, -1f);
            }
        }

        /// Altındaki kaya köprü çöktü, kızak yarığa düşüyor.
        public bool collapsing;

        void OffTrack()
        {
            float dx = Mathf.Abs(position.x - track.CenterX(position.z)) - track.halfWidth;
            if (dx > TrackProfile.BankRim) ForceEnd(RunEnd.Crashed, WentOffTrack, CrashCause.OffTrack);
            // Dar kanyon: kızağın kenarı duvara değerse düşer.
            if (track.wallHalf > 0f && position.y < track.Height(position.z) + TrackProfile.WallTop
                && Mathf.Abs(position.x - track.CenterX(position.z)) + HalfWidth > track.WallHalfAt(position.z))
            {
                velocity = Vector3.zero;
                ForceEnd(RunEnd.Crashed, "Hit the canyon wall!", CrashCause.Wall);
            }
        }

        void CheckObstacles()
        {
            if (!grounded)
                foreach (var f in track.flocks)
                {
                    if (Mathf.Abs(position.z - f.z) > f.radius + HalfLength) continue;
                    if ((track.FlockCenter(f, clock) - position).sqrMagnitude > f.radius * f.radius) continue;
                    velocity = Vector3.zero;
                    ForceEnd(RunEnd.Crashed, "Hit a flock of birds!", CrashCause.Birds);
                    return;
                }
            foreach (var o in track.obstacles)
            {
                if (Mathf.Abs(position.z - o.z) > o.halfDepth + HalfLength) continue;
                if (Mathf.Abs(position.x - o.x) > o.halfWidth + HalfWidth) continue;
                // Ağaçta yalnız gövdenin alt 1.5 m'si çarpar: dalgadan havalanan kızak dalların arasından geçer
                // (hızlı kızak havada yön değiştiremediği için her uçuş ağaçta bitiyordu).
                if (position.y - track.Height(o.x, o.z) > (o.variant == 4 ? 1.5f : o.height)) continue;
                velocity = Vector3.zero;
                ForceEnd(RunEnd.Crashed, o.variant == 4 ? "Hit a tree!" : o.isRock ? "Hit a rock!" : "Hit a crate!", CrashCause.Obstacle);
                return;
            }
        }
    }
}
