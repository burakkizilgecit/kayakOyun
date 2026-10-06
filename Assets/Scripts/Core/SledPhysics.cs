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
        public const float Gravity = 9.81f;
        const float AirDensity = 1.2f;
        public const float MaxAim = 0.44f;   // rad (~25°), sapanın en fazla yan açısı
        const float MaxHeading = 0.8f;   // rad (~46°): vadi kıvrılır, patika S çizer
        const float MaxSideGrip = 16f;   // m/s², yana dönüşte en büyük ivme (kıvrımlı patikayı izleyebilsin)
        const float MaxTurnRate = 1.6f;  // rad/s
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
        public bool CanFireRocket => rocketCharges > 0 && rocketTime <= 0f && !launching && end == RunEnd.None;

        float bankRoll;                  // rad, yamaçta gövdenin yana yatışı (sağ taraf yukarı +)
        float stillTime;
        Vector2 aimDir = Vector2.up;     // fırlatma yönü (x, z)

        readonly TrackProfile track;
        readonly SledConfig cfg;

        public SledPhysics(TrackProfile track, SledConfig cfg)
        {
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
            rocketTime = 0f;
            rocketCharges = cfg.rocketCharges;
            rocketThrust = cfg.rocketThrust;
            rocketBurn = cfg.rocketBurn;
            rocketPickupTaken = false;
            maxDistance = 0f;
            topSpeed = 0f;
            clock = 0f;
        }

        public void Launch() => launching = true;

        public bool FireRocket()
        {
            if (!CanFireRocket) return false;
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
            Vector3 before = velocity;

            if (grounded) StepGround(steer, dt);
            else StepAir(steer, pitchInput, dt);

            if (end == RunEnd.None)
            {
                SideWalls();
                CheckObstacles();
                CheckRocketPickup();
            }

            acceleration = (velocity - before) / dt;

            if (end == RunEnd.None)
            {
                maxDistance = Mathf.Max(maxDistance, position.z);
                topSpeed = Mathf.Max(topSpeed, velocity.magnitude);
                if (position.z >= track.finishZ) ForceEnd(RunEnd.Finished, "Parkur tamamlandı!");
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
            if (launching)
            {
                // Sapan lastiği: F = k·x, sapanın ortasına doğru. Kızak sapan hattını geçince lastik boşa düşer.
                if (position.x * aimDir.x + position.z * aimDir.y < 0f)
                    a += Vector3.ProjectOnPlane(new Vector3(-position.x, 0f, -position.z) * (cfg.bandStiffness / m), n);
                else launching = false;
            }

            float mu = track.Friction(position.z, position.x, cfg.groundFriction);
            float friction = mu * normalAcc;
            if (speed < 0.05f)
            {
                // Duruyor: itiş sürtünmeyi aşarsa harekete geçer.
                float push = a.magnitude;
                v = push <= friction ? Vector3.zero : v + a * ((push - friction) / push * dt);
            }
            else
            {
                float drag = (0.5f * AirDensity * cfg.dragArea / m + track.SurfaceResist(position.z, position.x)) * speed;
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
                float rate = Mathf.Min(MaxSideGrip / speed, MaxTurnRate);
                float turn = steer * rate * dt;
                float h = Mathf.Atan2(v.x, v.z);
                if ((turn > 0f && h > MaxHeading) || (turn < 0f && h < -MaxHeading)) turn = 0f;
                v = Quaternion.AngleAxis(turn * Mathf.Rad2Deg, n) * v;
            }
            if (speed > 0.3f) heading = Mathf.Atan2(v.x, Mathf.Max(v.z, 0.5f));
            lean = Mathf.MoveTowards(lean, steer * 0.22f, 1.2f * dt);

            Vector3 next = position + v * dt;
            float slopeAhead = track.SlopeZ(next.x, next.z);
            if (v.z > 0f && slopeAhead > WallSlope)
            {
                velocity = Vector3.zero;
                ForceEnd(RunEnd.Crashed, "Duvara çarptın!");
                return;
            }

            // Havalanma: pist boyunca dışbükey bir yüzeyde (rampa ucu, tepe) gereken merkezcil ivme
            // yerçekiminin dik bileşenini aşarsa normal kuvvet sıfırlanır: -κ·v² > g·cosθ
            float curvature = track.CurvatureAt(position.x, position.z);
            if (v.z > 2f && -curvature * v.z * v.z > normalAcc)
            {
                grounded = false;
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
                ForceEnd(RunEnd.Stopped, "Kızak durdu");
                return;
            }

            bool canRest = Vector3.ProjectOnPlane(g, n2).magnitude <= mu * Gravity * n2.y;
            if (!launching && sp < 0.2f && canRest)
            {
                stillTime += dt;
                if (stillTime > 0.6f) ForceEnd(RunEnd.Stopped, "Kızak durdu");
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

        void StepAir(float steer, float pitchInput, float dt)
        {
            float m = cfg.TotalMass;
            Vector3 v = velocity;
            float speed = v.magnitude;
            Vector3 a = new Vector3(0f, -Gravity, 0f);
            a -= 0.5f * AirDensity * cfg.dragArea / m * speed * v;

            float gamma = Mathf.Atan2(v.y, Mathf.Max(v.z, 0.01f));   // uçuş yolu açısı
            if (Mathf.Abs(pitchInput) > 0.05f) pitch += pitchInput * PitchRate * dt;
            // Dokunulmazsa: kanatlı kızak kendiliğinden sabit bir süzülme yolu tutar (tırmanıp tutunmayı
            // kaybetmez); kanatsız kızak burnunu yavaşça uçuş yönüne çevirir.
            else if (cfg.wingArea > 0f)
            {
                // Süzülme denetimi: ~14°'lik iniş yolunu tutar; tırmanırsa hücum açısını azaltır, fazla dalarsa artırır.
                float glidePath = -Mathf.Atan(1f / (0.9f * MaxGlideRatio(cfg.wingArea)));
                float alpha = Mathf.Clamp(0.12f + 1.5f * (glidePath - gamma), 0f, 0.24f);
                pitch = Mathf.MoveTowards(pitch, gamma + alpha, 1.5f * dt);
            }
            else pitch = Mathf.MoveTowards(pitch, gamma, 0.5f * dt);
            pitch = Mathf.Clamp(pitch, -MaxPitch, MaxPitch);
            // Kanatlı kızak (kuyruklu planör gibi) kendini akışa hizalar: burun akışın
            // altına bastırılamaz, böylece eksi g oluşmaz; en fazla ~25° kabartılabilir.
            if (cfg.wingArea > 0f) pitch = Mathf.Clamp(pitch, gamma, gamma + 0.45f);

            if (cfg.wingArea > 0f)
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
                    a += q * cfg.wingArea / m * (cl * liftDir - cd * dragDir);
                }
            }

            if (rocketTime > 0f)
            {
                a += Orientation * Vector3.forward * (rocketThrust / m);
                rocketTime -= dt;
            }

            a.x += steer * (1.5f + cfg.wingArea * 1.2f);
            lean = Mathf.MoveTowards(lean, steer * 0.35f, 1.5f * dt);

            velocity += a * dt;
            position += velocity * dt;
            heading = Mathf.Atan2(velocity.x, Mathf.Max(velocity.z, 0.5f));
            bankRoll = Mathf.MoveTowards(bankRoll, 0f, 1.5f * dt);

            float ground = track.Height(position.x, position.z);
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

        void Land(float ground)
        {
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
                ForceEnd(RunEnd.Crashed, slope > WallSlope ? "Duvara çarptın!" : "Çok sert indin!");
                return;
            }

            float airPitch = pitch;
            SurfacePose(n);
            float mismatch = Mathf.Abs(airPitch - pitch);
            if (mismatch > 1.0f && impact > 4f)
            {
                velocity = Vector3.zero;
                ForceEnd(RunEnd.Crashed, "Takla attın!");
                return;
            }

            // Yüzeye paralel inmek hızı korur, burun üstü inmek hız kaybettirir.
            float keep = (1f - 0.6f * Mathf.Clamp01((mismatch - 0.25f) / 0.75f)) * (1f - 0.015f * impact);
            Vector3 vt = Vector3.ProjectOnPlane(velocity, n) * keep;
            if (vt.z < 0f) vt.z = 0f;
            velocity = Vector3.ProjectOnPlane(vt, n);
            grounded = true;
        }

        /// Havada asılı roket hakkı: içinden geçen kızağa bir ateşleme hakkı ekler.
        void CheckRocketPickup()
        {
            if (!track.hasRocketPickup || rocketPickupTaken) return;
            float r = TrackProfile.RocketPickupRadius;
            if ((position + new Vector3(0f, 0.6f, 0f) - track.rocketPickup).sqrMagnitude > r * r) return;
            rocketPickupTaken = true;
            rocketCharges++;
            if (rocketThrust <= 0f) { rocketThrust = 92f; rocketBurn = 0.635f; }   // roketsiz kızağa 1. seviye roket
        }

        void SideWalls()
        {
            // Yamacın en dik yerinden öteye çıkılmaz (vadi eksenine göre).
            float center = track.CenterX(position.z);
            float limit = track.halfWidth + TrackProfile.BankLimit - HalfWidth;
            float dx = position.x - center;
            if (Mathf.Abs(dx) <= limit) return;
            float side = Mathf.Sign(dx);
            position.x = center + side * limit;
            position.y = Mathf.Max(position.y, track.Height(position.x, position.z));
            if (velocity.x * side > 0f) velocity.x = -velocity.x * 0.3f;
        }

        void CheckObstacles()
        {
            if (!grounded)
                foreach (var f in track.flocks)
                {
                    if (Mathf.Abs(position.z - f.z) > f.radius + HalfLength) continue;
                    if ((track.FlockCenter(f, clock) - position).sqrMagnitude > f.radius * f.radius) continue;
                    velocity = Vector3.zero;
                    ForceEnd(RunEnd.Crashed, "Kuş sürüsüne çarptın!");
                    return;
                }
            foreach (var o in track.obstacles)
            {
                if (Mathf.Abs(position.z - o.z) > o.halfDepth + HalfLength) continue;
                if (Mathf.Abs(position.x - o.x) > o.halfWidth + HalfWidth) continue;
                if (position.y - track.Height(o.x, o.z) > o.height) continue;
                velocity = Vector3.zero;
                ForceEnd(RunEnd.Crashed, o.isRock ? "Kayaya çarptın!" : "Kasaya çarptın!");
                return;
            }
        }
    }
}
