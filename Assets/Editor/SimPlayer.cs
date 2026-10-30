using System.Collections.Generic;
using UnityEngine;

namespace SlingRiders.EditorTools
{
    /// Sim oyuncu tipleri. Usta: bugünkü otomatik pilot (kusursuz rota ve bardak). Ortalama: gecikmeli ve titrek
    /// direksiyon, bardağı yarım yamalak dengeler, yavaşladığını görünce roketi ateşler. Eğmeyen: ortalama gibi
    /// sürer ama telefonu hiç eğmez (yalnızca karakterin kendi refleksi).
    public enum PlayerKind { Usta, Ortalama, Egmeyen }

    /// Roketin ateşlenme anı: Gec = durmak üzereyken (otomatik pilot), Yari = hız tepe hızın yarısına inince,
    /// Erken = sapandan çıkar çıkmaz (en hızlı an).
    public enum RocketPolicy { Varsayilan, Gec, Yari, Erken }

    /// Simde oyuncuyu taklit eder: direksiyon, bardak eğimi ve roket kararı. Aynı tohum aynı koşuyu verir.
    public class SimPlayer
    {
        public readonly PlayerKind kind;
        readonly RocketPolicy rocket;
        readonly System.Random rnd;
        readonly AutoPilot pilot = new AutoPilot();
        readonly GlassReflex reflex = new GlassReflex();

        // İnsan tepkisi: hedef direksiyon ve bardak düzeltmesi gecikmeyle uygulanır.
        const float SteerDelay = 0.25f, GlassDelay = 0.25f;
        readonly Queue<float> steerQueue = new Queue<float>();
        readonly Queue<Vector2> glassQueue = new Queue<Vector2>();
        float noise, glassNoiseP, glassNoiseR, topSpeed, smooth;

        public SimPlayer(PlayerKind kind, int seed, RocketPolicy rocket = RocketPolicy.Varsayilan)
        {
            this.kind = kind;
            this.rocket = rocket != RocketPolicy.Varsayilan ? rocket : kind == PlayerKind.Usta ? RocketPolicy.Gec : RocketPolicy.Yari;
            rnd = new System.Random(seed * 7919 + (int)kind);
        }

        float Gauss()
        {
            double u = 1.0 - rnd.NextDouble(), v = rnd.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u)) * System.Math.Cos(2.0 * System.Math.PI * v));
        }

        public float Steer(TrackProfile track, SledPhysics sled, float dt)
        {
            float ideal = AutoPilot.Steer(track, sled);
            if (kind == PlayerKind.Usta) return ideal;
            steerQueue.Enqueue(ideal);
            float delayed = steerQueue.Count > Mathf.RoundToInt(SteerDelay / dt) ? steerQueue.Dequeue() : 0f;
            // İnsan gecikmeli olduğu için daha yumuşak düzeltir (otomatik pilotun sert kazancı gecikmeyle zikzak
            // çizdiriyordu): %70 kazanç ve 0.12 s'lik yumuşatma. Üstüne parmak titremesi (Ornstein-Uhlenbeck, std ~0.1).
            smooth = Mathf.MoveTowards(smooth, 0.7f * delayed, dt / 0.12f);
            noise += -2f * noise * dt + 0.2f * Mathf.Sqrt(dt) * Gauss();
            return Mathf.Clamp(smooth + noise, -1f, 1f);
        }

        public float Pitch(TrackProfile track, SledPhysics sled, SledConfig cfg) => AutoPilot.Pitch(track, sled, cfg);

        /// Mükemmel bırakış: usta her zaman, öbürleri atışların ~%40'ında yakalar.
        public bool PerfectLaunch() => kind == PlayerKind.Usta || rnd.NextDouble() < 0.4;

        /// Bardağın kızağa göre eğimi (x = ileri, y = sağa; derece). Oyundaki gibi: karakterin refleksi + oyuncunun
        /// eğmesi (ustada otomatik pilot). Fizik adımından önce çağrılır; yönelim adımdan sonra uygulanır.
        public Vector2 Glass(SledPhysics sled, SledConfig cfg, float dt)
        {
            pilot.StepGlass(sled, dt);
            if (kind == PlayerKind.Usta) return new Vector2(pilot.glassPitch, pilot.glassRoll);
            reflex.Step(sled, cfg.glassReflex, dt);
            float p = reflex.pitch, r = reflex.roll;
            if (kind == PlayerKind.Ortalama)
            {
                // Eksik kalanın yarısını gecikmeli ve hafif titrek düzeltir.
                glassQueue.Enqueue(new Vector2(pilot.glassPitch - reflex.pitch, pilot.glassRoll - reflex.roll));
                Vector2 want = glassQueue.Count > Mathf.RoundToInt(GlassDelay / dt) ? glassQueue.Dequeue() : Vector2.zero;
                glassNoiseP += -1.5f * glassNoiseP * dt + 2f * Mathf.Sqrt(dt) * Gauss();
                glassNoiseR += -1.5f * glassNoiseR * dt + 2f * Mathf.Sqrt(dt) * Gauss();
                p += 0.5f * want.x + glassNoiseP;
                r += 0.5f * want.y + glassNoiseR;
            }
            return new Vector2(Mathf.Clamp(p, -50f, 50f), Mathf.Clamp(r, -50f, 50f));
        }

        public static Quaternion GlassRotation(SledPhysics sled, Vector2 g) => sled.Orientation * Quaternion.Euler(g.x, 0f, -g.y);

        public bool Fire(TrackProfile track, SledPhysics sled, SledConfig cfg)
        {
            topSpeed = Mathf.Max(topSpeed, sled.Speed);
            if (!sled.CanFireRocket) return false;
            switch (rocket)
            {
                case RocketPolicy.Erken: return !sled.launching && sled.position.z > 2f;
                case RocketPolicy.Yari: return !sled.launching && sled.position.z > 20f && sled.Speed < 0.5f * topSpeed;
                default: return AutoPilot.ShouldFire(track, sled, cfg);
            }
        }

        public static string Name(PlayerKind k) => k == PlayerKind.Usta ? "usta" : k == PlayerKind.Ortalama ? "ortalama" : "eğmeyen";

        public static PlayerKind Parse(string s) =>
            s.StartsWith("ort") ? PlayerKind.Ortalama : s.StartsWith("eg") || s.StartsWith("eğ") ? PlayerKind.Egmeyen : PlayerKind.Usta;
    }
}
