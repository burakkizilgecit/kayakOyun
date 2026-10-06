using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    public enum Surface { Ground, Puddle, Ice, Mud }

    /// Pistin bir bölümünün yüzeyi: su ve çamur frenler, buz kaydırır. Her biri düzensiz kenarlı bir lekedir
    /// (merkez cz/cx, yarıçaplar rz/rx, kenar dalgası seed'e göre): görünen kenar ile fizikteki kenar aynıdır.
    /// cx ve yanal konumlar vadinin (ya da onPath ise patikanın) ortasına göredir.
    public struct SurfaceZone
    {
        public float z0, z1, x0, x1;   // sınır kutusu (hızlı eleme)
        public float cz, cx, rz, rx, seed;
        public bool onPath;
        public Surface type;

        public static SurfaceZone Blob(Surface type, float cz, float cx, float rz, float rx, float seed, bool onPath)
        {
            const float m = 1.32f;
            return new SurfaceZone { type = type, cz = cz, cx = cx, rz = rz, rx = rx, seed = seed, onPath = onPath,
                                     z0 = cz - rz * m, z1 = cz + rz * m, x0 = cx - rx * m, x1 = cx + rx * m };
        }

        /// Kenarın merkeze uzaklık çarpanı (açıya göre): her leke kendine özgü, dalgalı bir şekle sahip.
        public float Edge(float angle) =>
            1f + 0.16f * Mathf.Sin(3f * angle + seed) + 0.09f * Mathf.Sin(5f * angle + seed * 1.7f) + 0.05f * Mathf.Sin(9f * angle + seed * 0.3f);

        /// dx: vadi ya da patika ortasına göre yanal konum.
        public bool Contains(float z, float dx)
        {
            if (z < z0 || z > z1 || dx < x0 || dx > x1) return false;
            float u = (z - cz) / rz, v = (dx - cx) / rx;
            float r2 = u * u + v * v;
            if (r2 < 0.55f) return true;
            return Mathf.Sqrt(r2) <= Edge(Mathf.Atan2(v, u));
        }
    }

    /// Havadaki engel: vadi atlayışında süzülme yüksekliğinde sağa sola salınan kuş sürüsü.
    public struct Flock
    {
        public float z, x, y;          // x: vadi ortasına göre; y: mutlak yükseklik
        public float sway, period, radius;
    }

    public struct Obstacle
    {
        public float x, z;
        public float halfWidth, halfDepth, height;
        public bool isRock;
        public int variant;   // görünüm: kasa/sandık/varil ya da kaya/taş/kütük yığını/kütük
    }

    /// Parkur: +z yönünde uzanan bir vadi. Taban yüksekliği z'ye bağlı profil H(z); vadinin ekseni CenterX(z) ile
    /// sağa sola kıvrılır, tabanın dışında yamaçlar yükselir (çit yok: yerçekimi kızağı ortaya geri iter).
    /// Tabanda S çizen toprak patika (PathX) normal hızdır, çimen biraz yavaşlatır.
    /// Kontrol noktaları arası doğrusal çizilir. Parça birleşim köşeleri (corners) ±RoundLength boyunca
    /// eğimi sürekli bir eğriyle (kübik Hermite) değiştirilir: eğim geçişleri köşeli değil ovaldir. Yakın köşelerin
    /// aralıkları birleşir. Rampa dudakları ve dereler (sharpZones) yuvarlanmaz, sıçratmaya devam eder.
    /// En sonda kısa bir kayan ortalama kalan küçük kırıkları siler.
    public class TrackProfile
    {
        public readonly string name;
        public readonly float startZ;
        public readonly float finishZ;
        public readonly float halfWidth;   // vadi tabanının yarı genişliği (düz kısım)
        public readonly List<Obstacle> obstacles = new List<Obstacle>();
        /// Dere ve kanyon dipleri: x = başlangıç z, y = bitiş z, z = su yüzeyi yüksekliği.
        public readonly List<Vector3> water = new List<Vector3>();
        public readonly List<SurfaceZone> surfaces = new List<SurfaceZone>();
        public readonly List<Flock> flocks = new List<Flock>();
        /// Roket tepelerinin zirve z'leri.
        public readonly List<float> rocketHills = new List<float>();
        /// Havada alınan roket hakkı: pistin %70'inden sonraki ilk rampanın uçuş yolunda, yerden yetişilemeyecek
        /// yükseklikte (her pistte en fazla bir tane). Kızağın merkezi (konum + 0.6 m) bu yarıçapa girerse alınır.
        public bool hasRocketPickup;
        public Vector3 rocketPickup;
        public const float RocketPickupRadius = 1.5f;

        /// Su birikintisinde sürtünmeye eklenen katsayı; buzda ve çimende sürtünme çarpanı.
        public const float PuddleDrag = 0.3f, MudDrag = 0.45f, IceFactor = 0.25f, GrassFactor = 1.5f;
        public const float PathHalfWidth = 1.7f;
        /// Yamaç: tabanın kenarından itibaren BankCurve·d² yükselir (d ≤ BankFlex), sonra aynı eğimle sürer.
        /// Kızak tabandan en fazla BankLimit uzaklaşabilir.
        public const float BankCurve = 0.09f, BankFlex = 6f, BankLimit = 9f;

        // Vadi kıvrımı ve patika (SetValley)
        float meanderAmp, meanderLength = 160f, pathAmp, pathLength = 55f, phaseA, phaseB, phaseC;

        readonly float step;
        readonly float[] heights;

        const float RoundLength = 14f;

        public TrackProfile(string name, Vector2[] points, float finishZ, float halfWidth,
                            IList<float> corners = null, IList<Vector2> sharpZones = null,
                            float step = 0.25f, float smoothRadius = 3f)
        {
            this.name = name;
            this.finishZ = finishZ;
            this.halfWidth = halfWidth;
            this.step = step;
            startZ = points[0].x;

            float endZ = points[points.Length - 1].x;
            int n = Mathf.CeilToInt((endZ - startZ) / step) + 1;
            var raw = new float[n];
            int seg = 0;
            for (int i = 0; i < n; i++)
            {
                float z = startZ + i * step;
                while (seg < points.Length - 2 && z > points[seg + 1].x) seg++;
                Vector2 a = points[seg], b = points[seg + 1];
                float t = b.x > a.x ? Mathf.Clamp01((z - a.x) / (b.x - a.x)) : 1f;
                raw[i] = Mathf.Lerp(a.y, b.y, t);
            }

            int r = Mathf.Max(1, Mathf.RoundToInt(smoothRadius / step));
            if (corners != null) RoundCorners(raw, corners, sharpZones);
            heights = BoxBlur(raw, r);

            if (EndZ < finishZ + 20f)
                Debug.LogWarning("[TRACK] " + name + ": pist " + EndZ + " m'de bitiyor, bitiş çizgisi " + finishZ + " m");
        }

        /// Her köşenin çevresindeki [z-R, z+R] aralığını (korumalı bölgelere girmeden) iki ucundaki eğimlere
        /// teğet kübik bir eğriyle değiştirir.
        void RoundCorners(float[] raw, IList<float> corners, IList<Vector2> sharpZones)
        {
            var spans = new List<Vector2>();
            foreach (float c in corners)
            {
                float a = c - RoundLength, b = c + RoundLength;
                bool inside = false;
                if (sharpZones != null)
                    foreach (var zone in sharpZones)
                    {
                        if (c >= zone.x && c <= zone.y) inside = true;
                        else if (zone.y < c) a = Mathf.Max(a, zone.y);
                        else b = Mathf.Min(b, zone.x);
                    }
                if (!inside && b - a > 1f) spans.Add(new Vector2(a, b));
            }
            spans.Sort((u, v) => u.x.CompareTo(v.x));
            // Çakışan aralıklar birleşir (ör. yokuş tepesindeki kısa düzlüğün iki köşesi tek yay olur).
            var merged = new List<Vector2>();
            foreach (var sp in spans)
            {
                if (merged.Count > 0 && sp.x <= merged[merged.Count - 1].y)
                    merged[merged.Count - 1] = new Vector2(merged[merged.Count - 1].x, Mathf.Max(merged[merged.Count - 1].y, sp.y));
                else merged.Add(sp);
            }
            int n = raw.Length;
            foreach (var sp in merged)
            {
                int i0 = Mathf.Clamp(Mathf.RoundToInt((sp.x - startZ) / step), 1, n - 2);
                int i1 = Mathf.Clamp(Mathf.RoundToInt((sp.y - startZ) / step), 1, n - 2);
                if (i1 - i0 < 2) continue;
                float y0 = raw[i0], y1 = raw[i1];
                float s0 = (raw[i0] - raw[i0 - 1]) / step, s1 = (raw[i1 + 1] - raw[i1]) / step;
                float len = (i1 - i0) * step;
                for (int i = i0 + 1; i < i1; i++)
                {
                    float t = (i - i0) / (float)(i1 - i0);
                    float t2 = t * t, t3 = t2 * t;
                    raw[i] = (2 * t3 - 3 * t2 + 1) * y0 + (t3 - 2 * t2 + t) * len * s0
                           + (-2 * t3 + 3 * t2) * y1 + (t3 - t2) * len * s1;
                }
            }
        }

        static float[] BoxBlur(float[] src, int r)
        {
            int n = src.Length;
            var dst = new float[n];
            float sum = 0f;
            for (int k = -r; k <= r; k++) sum += src[Mathf.Clamp(k, 0, n - 1)];
            for (int i = 0; i < n; i++)
            {
                dst[i] = sum / (2 * r + 1);
                sum += src[Mathf.Min(i + r + 1, n - 1)] - src[Mathf.Max(i - r, 0)];
            }
            return dst;
        }

        /// Vadinin kıvrımını ve patikanın S çizmesini ayarlar (başlangıç düzlüğünde ikisi de düz başlar).
        /// straightZones (z0, z1): rampa ve atlayış bölgeleri — vadi ve patika burada dümdüz gider, böylece havada
        /// düz uçan kızak patikaya iner. Kıvrım bölgeden önce StraightBlend boyunca yumuşakça açılır.
        public void SetValley(float amp, float length, float pathAmplitude, float pathWave, int seed,
                              IList<Vector2> straightZones = null)
        {
            meanderAmp = amp;
            meanderLength = length;
            pathAmp = pathAmplitude;
            pathLength = pathWave;
            var rnd = new System.Random(seed);
            phaseA = (float)rnd.NextDouble() * 6.28f;
            phaseB = (float)rnd.NextDouble() * 6.28f;
            phaseC = (float)rnd.NextDouble() * 6.28f;
            BuildBendTable(straightZones);
        }

        const float StraightBlend = 30f;
        float[] bendTable;   // z (1 m adım, startZ'den) → kıvrım parametresi u; düz bölgede u sabit kalır

        void BuildBendTable(IList<Vector2> zones)
        {
            bendTable = null;
            if (zones == null || zones.Count == 0) return;
            int n = Mathf.CeilToInt(EndZ - startZ) + 2;
            bendTable = new float[n];
            float u = startZ;
            for (int i = 0; i < n; i++)
            {
                bendTable[i] = u;
                float z = startZ + i + 0.5f, w = 0f;
                foreach (var zone in zones)
                {
                    float d = z < zone.x ? zone.x - z : z > zone.y ? z - zone.y : 0f;
                    if (d < StraightBlend) w = Mathf.Max(w, 1f - Mathf.SmoothStep(0f, 1f, d / StraightBlend));
                }
                u += 1f - w;
            }
        }

        float Bend(float z)
        {
            if (bendTable == null) return z;
            float f = z - startZ;
            if (f <= 0f) return z;
            int i = (int)f;
            if (i >= bendTable.Length - 1) return bendTable[bendTable.Length - 1] + (f - (bendTable.Length - 1));
            return Mathf.Lerp(bendTable[i], bendTable[i + 1], f - i);
        }

        /// Vadi ekseninin yanal konumu.
        public float CenterX(float z)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 100f, z));
            float u = Bend(z);
            return k * meanderAmp * (0.75f * Mathf.Sin(u * 6.2832f / meanderLength + phaseA)
                                     + 0.25f * Mathf.Sin(u * 6.2832f / (meanderLength * 0.41f) + phaseB));
        }

        /// Patikanın orta çizgisi: vadi içinde S çizer.
        public float PathX(float z)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(25f, 70f, z));
            return CenterX(z) + k * pathAmp * Mathf.Sin(Bend(z) * 6.2832f / pathLength + phaseC);
        }

        public bool OnPath(float z, float x) => Mathf.Abs(x - PathX(z)) <= PathHalfWidth;

        /// Yamaç yüksekliği; dx: vadi eksenine uzaklık.
        public float Bank(float dx)
        {
            float d = Mathf.Abs(dx) - halfWidth;
            if (d <= 0f) return 0f;
            if (d <= BankFlex) return BankCurve * d * d;
            return BankCurve * BankFlex * BankFlex + 2f * BankCurve * BankFlex * (d - BankFlex);
        }

        /// Zemin yüksekliği (vadi tabanı + yamaç).
        public float Height(float x, float z) => Height(z) + Bank(x - CenterX(z));

        /// Yanal eğim dH/dx (yamaçta kızağı ortaya iten).
        public float SlopeX(float x, float z) => (Height(x + 0.1f, z) - Height(x - 0.1f, z)) / 0.2f;

        /// Kızağın x konumunda ileri eğim dH/dz (kıvrımın yamaç etkisi dahil).
        public float SlopeZ(float x, float z) => (Height(x, z + step) - Height(x, z - step)) / (2f * step);

        /// Kuş sürüsünün t anındaki merkezi (atışın kendi saatine göre: fizik ve görüntü aynı yeri kullanır).
        public Vector3 FlockCenter(Flock f, float t) =>
            new Vector3(CenterX(f.z) + f.x + f.sway * Mathf.Sin(t * 6.2832f / f.period + f.z), f.y, f.z);

        /// Yüzey normali (birim).
        public Vector3 Normal(float x, float z) => new Vector3(-SlopeX(x, z), 1f, -SlopeZ(x, z)).normalized;

        public float CurvatureAt(float x, float z)
        {
            const float d = 0.5f;
            float second = (Height(x, z + d) - 2f * Height(x, z) + Height(x, z - d)) / (d * d);
            float first = SlopeZ(x, z);
            return second / Mathf.Pow(1f + first * first, 1.5f);
        }

        public Surface SurfaceAt(float z, float x)
        {
            float dc = x - CenterX(z), dp = x - PathX(z);
            foreach (var s in surfaces)
                if (s.Contains(z, s.onPath ? dp : dc)) return s.type;
            return Surface.Ground;
        }

        /// Konuma göre kinetik sürtünme katsayısı: patika normal, çimen biraz yavaş, su çok yavaş, buz kaygan.
        public float Friction(float z, float x, float baseFriction)
        {
            float mu = OnPath(z, x) ? baseFriction : baseFriction * GrassFactor;
            switch (SurfaceAt(z, x))
            {
                case Surface.Puddle: return mu + PuddleDrag;
                case Surface.Mud: return mu + MudDrag;
                case Surface.Ice: return baseFriction * IceFactor;
                default: return mu;
            }
        }

        /// Su ve çamurun hıza bağlı direnci (1/m): ivme = -k·v². Hızlı giren kızak çok daha fazla yavaşlar
        /// (10 m su birikintisi hızı ~%18, 8 m çamur ~%22 düşürür; üstüne sürtünme eklenir).
        public const float PuddleResist = 0.02f, MudResist = 0.03f;

        public float SurfaceResist(float z, float x)
        {
            switch (SurfaceAt(z, x))
            {
                case Surface.Puddle: return PuddleResist;
                case Surface.Mud: return MudResist;
                default: return 0f;
            }
        }

        public float EndZ => startZ + (heights.Length - 1) * step;

        public float Height(float z)
        {
            float f = (z - startZ) / step;
            if (f <= 0f) return heights[0];
            int i = (int)f;
            if (i >= heights.Length - 1) return heights[heights.Length - 1];
            return Mathf.Lerp(heights[i], heights[i + 1], f - i);
        }

        /// dH/dz
        public float Slope(float z) => (Height(z + step) - Height(z - step)) / (2f * step);

        /// Profil eğriliği κ = H'' / (1 + H'²)^1.5  (negatif = dışbükey, tepe/rampa ucu)
        public float Curvature(float z)
        {
            const float d = 0.5f;
            float second = (Height(z + d) - 2f * Height(z) + Height(z - d)) / (d * d);
            float first = Slope(z);
            return second / Mathf.Pow(1f + first * first, 1.5f);
        }

        /// Engeller patikaya göre konur (x = patika ortasından yanal uzaklık): patikayı izleyen kaçmak zorunda kalır.
        /// Kasa türü engel (kasa, sağlam kasa, varil): boyutu ve görünümü konumundan türetilir, her biri farklıdır.
        public void AddCrate(float z, float x)
        {
            int v = Mathf.Abs(Mathf.RoundToInt(z * 7.3f + x * 3.1f)) % 3;
            float s = 0.85f + 0.3f * Mathf.Repeat(z * 0.137f, 1f);
            float hw = (v == 2 ? 0.45f : 0.6f) * s;
            obstacles.Add(new Obstacle { x = PathX(z) + x, z = z, halfWidth = hw, halfDepth = hw, height = (v == 2 ? 1.2f : 1.1f) * s, variant = v });
        }

        /// Kaya türü engel (iri kaya, taş, kütük yığını, kütük).
        public void AddRock(float z, float x)
        {
            int v = Mathf.Abs(Mathf.RoundToInt(z * 5.7f + x * 2.3f)) % 4;
            float s = 0.8f + 0.4f * Mathf.Repeat(z * 0.173f, 1f);
            var o = new Obstacle { x = PathX(z) + x, z = z, isRock = true, variant = v };
            switch (v)
            {
                case 2: o.halfWidth = 1.1f * s; o.halfDepth = 0.6f * s; o.height = 0.8f * s; break;   // kütük yığını: geniş, alçak
                case 3: o.halfWidth = 0.5f * s; o.halfDepth = 0.5f * s; o.height = 0.7f * s; break;   // kütük
                default: o.halfWidth = 0.8f * s; o.halfDepth = 0.7f * s; o.height = 0.9f * s; break;  // kaya / taş
            }
            obstacles.Add(o);
        }

        public void SortObstacles() => obstacles.Sort((a, b) => a.z.CompareTo(b.z));
    }
}
