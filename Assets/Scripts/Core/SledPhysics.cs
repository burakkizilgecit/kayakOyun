using UnityEngine;

namespace SledSurfers
{
    public enum RunEnd { None, Stopped, Crashed, Spilled, Finished }

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
        const float DeckAlignRate = 1.0f;   // rad/s: köprü tablasında kızağın kendiliğinden hizalanma hızı
        const float PitchRate = 1.8f;    // rad/s
        /// Kanadın profil sürükleme katsayısı. Küçük kanatlarda 0.12; büyük (üst seviye) kanatlar daha ince ve temizdir:
        /// toplam profil sürüklemesi (Cd0·alan) sabit kalır, böylece büyük kanat kızağı yavaşlatmaz, daha iyi süzülür.
        static float WingCd0(float area) => Mathf.Min(0.12f, 0.19f / Mathf.Max(area, 0.01f));

        static float AspectRatio(float wingArea) => 2f + wingArea;

        /// En iyi süzülme oranı (kaldırma/sürükleme): küçük kanatta ~3, en büyükte ~4,8.
        public static float MaxGlideRatio(float wingArea) =>
            0.5f * Mathf.Sqrt(Mathf.PI * AspectRatio(wingArea) * 0.8f / WingCd0(wingArea));  // rad, kanatlı kızağın kendiliğinden tuttuğu hücum açısı (en iyi süzülmeye yakın)
        const float MaxPitch = 1.2f;     // rad
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
            stillTime = 0f;
            landHold = 0f;
            rocketTime = 0f;
            rocketCharges = cfg.rocketCharges;
            rocketThrust = cfg.rocketThrust;
            rocketBurn = cfg.rocketBurn;
            rocketPickupTaken = false;
            maxDistance = 0f;
            topSpeed = 0f;
            clock = 0f;
            sinceLanding = 9f;
            launchBoost = 1f;
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

        public void ForceEnd(RunEnd reason, string text)
        {
            if (end == RunEnd.None) { end = reason; endReason = text; }
        }

        public void Step(float steer, float pitchInput, float dt)
        {
            if (end != RunEnd.None) return;
            clock += dt;
            sinceLanding += dt;
            float zBefore = position.z;
            Vector3 before = velocity;

            if (grounded) StepGround(steer, dt);
            else StepAir(steer, pitchInput, dt);

            if (end == RunEnd.None)
            {
                OffTrack();
                CheckObstacles();
                CheckRocketPickup();
            }

            acceleration = (velocity - before) / dt;

            if (end == RunEnd.None)
            {
                maxDistance = Mathf.Max(maxDistance, position.z);
                topSpeed = Mathf.Max(topSpeed, velocity.magnitude);
                foreach (var b in track.bridges)
                    if (zBefore < b.r1 && position.z >= b.r1) stats.bridgesCrossed++;
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
                float rate = Mathf.Min(MaxSideGrip / speed, MaxTurnRate);
                float turn = steer * rate * dt;
                float h = Mathf.Atan2(v.x, v.z);
                if ((turn > 0f && h > MaxHeading) || (turn < 0f && h < -MaxHeading)) turn = 0f;
                v = Quaternion.AngleAxis(turn * Mathf.Rad2Deg, n) * v;
            }
            // Köprü tablası (arcade yardımı): direksiyona sert basılmazsa kızak yavaşça tablanın yönüne hizalanır.
            // Beceri, köprüye ortadan girmek ve tablada oynamamak; hafif bir açı artık ölümcül değil (simde ortalama
            // oyuncu hızlandıkça her köprüden düşüyordu, yükseltme köprüyü zorlaştırıyordu).
            if (speed > 0.5f && Mathf.Abs(steer) < 0.3f && track.OnDeck(position.x, position.z) && track.BridgeAt(position.z, out var deck))
            {
                // Hafif oluk: hem tablanın yönüne hem ortasına doğru (en çok ~6°'lik açıyla) döner.
                float h = Mathf.Atan2(v.x, v.z);
                float want = Mathf.Clamp(0.5f * (deck.x - position.x) / Mathf.Max(speed * 0.5f, 1f), -0.1f, 0.1f);
                float turn = Mathf.Clamp(want - h, -DeckAlignRate * dt, DeckAlignRate * dt);
                v = Quaternion.AngleAxis(turn * Mathf.Rad2Deg, n) * v;
            }
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
                ForceEnd(RunEnd.Crashed, InGap(position.z) ? "Fell into the valley!" : "Hit the wall!");
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
                if (!InGap(position.z) && !InGap(position.z + v.z * 0.8f))
                {
                    float above = position.y - track.Height(position.x, position.z);
                    // Yamacın eğimini izler (0.5 s ileride), üstüne yüksekliğe orantılı dalış: yüksekteyken yere yaklaşır,
                    // yere yakınken yamaca paralel uçup yumuşak değer; önde yokuş/düzlük varsa burun ona göre kalkar.
                    if (above > 1.5f)
                    {
                        float slopeAhead = Mathf.Atan(track.SlopeZ(position.x, position.z + Mathf.Max(v.z, 0f) * 0.5f));
                        // En az ~6°: yamacın birkaç metre üstünde asılı kalmasın (süzülme süresini boşa harcamasın), yumuşakça değsin.
                        float dive = Mathf.Max(0.1f, Mathf.Atan((above - 1f) / Mathf.Max(10f, v.z * 1.5f)));
                        glidePath = Mathf.Clamp(slopeAhead - dive, -1.2f, 0.3f);
                    }
                }
                float alpha = Mathf.Clamp(0.12f + 1.5f * (glidePath - gamma), 0f, 0.24f);
                pitch = Mathf.MoveTowards(pitch, gamma + alpha, 1.5f * dt);
            }
            else pitch = Mathf.MoveTowards(pitch, gamma, 0.5f * dt);
            pitch = Mathf.Clamp(pitch, -MaxPitch, MaxPitch);
            // Kanatlı kızak (kuyruklu planör gibi) kendini akışa hizalar: burun akışın
            // altına bastırılamaz, böylece eksi g oluşmaz; en fazla ~25° kabartılabilir.
            if (wingsOpen) pitch = Mathf.Clamp(pitch, gamma, gamma + 0.45f);

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
            bool underDeck = track.OnDeck(position.x, position.z) && position.y < ground - 0.3f;
            if (underDeck) ground = track.TerrainHeight(position.x, position.z);
            if ((underDeck || track.OverRavine(position.x, position.z)) && position.y <= ground + 0.9f)
            {
                velocity = Vector3.zero;
                stats.fellInRavine = true;
                ForceEnd(RunEnd.Crashed, track.fallText);
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
                ForceEnd(RunEnd.Crashed, InGap(position.z) ? "Fell into the valley!" : slope > WallSlope ? "Hit the wall!" : "Landed too hard!");
                return;
            }

            float airPitch = pitch;
            SurfacePose(n);
            float mismatch = Mathf.Abs(airPitch - pitch);
            if (mismatch > 1.0f && impact > 4f)
            {
                velocity = Vector3.zero;
                ForceEnd(RunEnd.Crashed, "You flipped!");
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
        void OffTrack()
        {
            float dx = Mathf.Abs(position.x - track.CenterX(position.z)) - track.halfWidth;
            if (dx > TrackProfile.BankRim) ForceEnd(RunEnd.Crashed, WentOffTrack);
        }

        void CheckObstacles()
        {
            if (!grounded)
                foreach (var f in track.flocks)
                {
                    if (Mathf.Abs(position.z - f.z) > f.radius + HalfLength) continue;
                    if ((track.FlockCenter(f, clock) - position).sqrMagnitude > f.radius * f.radius) continue;
                    velocity = Vector3.zero;
                    ForceEnd(RunEnd.Crashed, "Hit a flock of birds!");
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
                ForceEnd(RunEnd.Crashed, o.variant == 4 ? "Hit a tree!" : o.isRock ? "Hit a rock!" : "Hit a crate!");
                return;
            }
        }
    }
}
