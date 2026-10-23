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

        /// Kenarın merkeze uzaklık çarpanı (açıya göre): büyük loblar + küçük girinti çıkıntılar (kullanıcı: yuvarlak
        /// ovaller yapay duruyordu). Fizik de aynı kenarı kullanır.
        public float Edge(float angle) =>
            1f + 0.1f * Mathf.Sin(2f * angle + seed) + 0.06f * Mathf.Sin(3f * angle + seed * 1.7f)
               + 0.05f * Mathf.Sin(5f * angle + seed * 2.3f) + 0.03f * Mathf.Sin(7f * angle + seed * 3.1f)
               + 0.018f * Mathf.Sin(11f * angle + seed * 4.7f);

        /// Görsel iç katman: merkezi (yarıçap biriminde) kaydırılmış, başka biçimli leke (iç içe halka gibi durmasın).
        public SurfaceZone Inner(float du, float dv, float seedAdd)
        {
            var z = this;
            z.cz += du * rz;
            z.cx += dv * rx;
            z.seed += seedAdd;
            return z;
        }

        /// dx: vadi ya da patika ortasına göre yanal konum.
        public bool Contains(float z, float dx)
        {
            if (z < z0 || z > z1 || dx < x0 || dx > x1) return false;
            float u = (z - cz) / rz, v = (dx - cx) / rx;
            float r2 = u * u + v * v;
            if (r2 < 0.48f) return true;   // en içteki kenar ~0.7
            return Mathf.Sqrt(r2) <= Edge(Mathf.Atan2(v, u));
        }
    }

    /// Havadaki engel: vadi atlayışında süzülme yüksekliğinde sağa sola salınan kuş sürüsü.
    public struct Flock
    {
        public float z, x, y;          // x: vadi ortasına göre; y: mutlak yükseklik
        public float sway, period, radius;
    }

    /// Zemindeki tümsek: z0'dan length boyunca kosinüs biçimli kambur (yükseklik height). Yanda vadi ortasına göre
    /// xc merkezli, halfWidth genişliğinde (kenarda 3 m'de yumuşakça söner); halfWidth ≥ 30: tüm taban.
    public struct Bump
    {
        public float z0, length, height, xc, halfWidth;
        /// Rampa (hızlandırıcı rampası): simetrik kambur yerine yavaşça dikleşerek yükselir, dudaktan sonra keskin düşer;
        /// yanları 1.2 m'de söner.
        public bool ramp;
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
        /// Pistin yerçekimi (m/s²): Mars 0.38 g. SledPhysics bunu kullanır.
        public float gravity = 9.81f;
        /// Köprü açıklığına düşünce çıkan yazı (dere / kanal / lav / krater) ve düşülen şey su mu (sıçrama).
        public string fallText = "Fell in the creek!";
        public bool ravineIsWater = true;
        public readonly float startZ;
        public readonly float finishZ;
        public readonly float halfWidth;   // vadi tabanının yarı genişliği (düz kısım)
        public readonly List<Obstacle> obstacles = new List<Obstacle>();
        /// Profilin üstüne eklenen tümsekler (dalga dizileri, zorlu kamburlar, yana kaymış tümsekler).
        public readonly List<Bump> bumps = new List<Bump>();
        List<int>[] bumpIndex;
        const float BumpBucket = 4f;
        /// Dere ve kanyon dipleri: x = başlangıç z, y = bitiş z, z = su yüzeyi yüksekliği.
        public readonly List<Vector3> water = new List<Vector3>();
        /// İnce köprüler (kullanıcı kararı 2026-10-08): pisti enine kesen derenin (z0..z1) üstünde, patika hizasında
        /// (x) düz tahta tabla. z0..z1 / y0 → y1: tabla (kıyılara biraz uzanır: profil yumuşatması dudağı yuvarlar,
        /// tabla dudaktan önce başlamazsa hızlı kızak dudakta havalanıyordu). r0..r1: derenin kendisi; orada tablanın
        /// dışına kayan kızak dereye düşer, koşu biter.
        /// hw: tablanın yarı genişliği (0: BridgeHalfWidth). Enine derelerin köprüsü daha geniş (oyuncu yerini bulmalı,
        /// ip cambazlığı yapmamalı).
        /// shape/bend (kullanıcı 2026-10-18: köprüler hep dümdüzdü, bazıları dönemeçli ve zorlayıcı olmalı): tabla derenin
        /// üstünde (r0..r1) yana kıvrılır. 0 düz, 1 yay (ortada bend kadar yana), 2 S (bir yana sonra öbür yana),
        /// 3 çapraz (girişte -bend, çıkışta +bend; yumuşak geçiş), 4 zikzak (çok zor: uzun tablada iki tam kıvrım, uçlarda
        /// düz girer çıkar). Kıyıdaki uçlar hep düz.
        public struct Bridge
        {
            public float z0, z1, r0, r1, x, y0, y1, hw, bend;
            public int shape;
            /// Aynı derenin üstündeki yan köprü (kullanıcı 2026-10-23: derede 1-3 köprü; biri düz, ötekiler S ve üstünde
            /// coin/elmas): riskli ama ödüllü yol.
            public bool bonus;
            public float HalfWidth => hw > 0f ? hw : BridgeHalfWidth;

            /// Tablanın z'deki orta çizgisi.
            public float XAt(float z)
            {
                if (shape == 0 || bend == 0f) return x;
                float u = Mathf.Clamp01((z - r0) / Mathf.Max(r1 - r0, 0.1f));
                switch (shape)
                {
                    case 1: return x + bend * Mathf.Sin(Mathf.PI * u);
                    case 2: return x + bend * Mathf.Sin(2f * Mathf.PI * u);
                    // Zikzak: kıvrımlar ortada tam, uçlara doğru küçülür (çıkıştaki son keskin kıvrımda simde ortalama oyuncu
                    // 10'da 9 düşüyordu).
                    case 4: return x + bend * Mathf.Sin(4f * Mathf.PI * u) * Mathf.Sin(Mathf.PI * u);
                    default: return x + bend * (2f * Mathf.SmoothStep(0f, 1f, u) - 1f);
                }
            }

            /// Tablanın z'deki yönü (dx/dz).
            public float SlopeAt(float z) => (XAt(z + 0.25f) - XAt(z - 0.25f)) * 2f;
        }
        public readonly List<Bridge> bridges = new List<Bridge>();

        /// Dar kanyon duvarları (kullanıcı 2026-10-23): wallHalf > 0 ise vadi ortasından bu uzaklıkta dik kaya duvar; kızak
        /// (gövdesiyle) değerse düşer. pinches: duvarın daraldığı boğazlar (x = orta z, y = uzunluk, z = en dar yarı aralık).
        public float wallHalf;

        /// Çöken kaya köprü (kullanıcı 2026-10-23: "yavaş geçiyorsa köprü taşımasın, çöksün"): kızak tablaya basınca taş
        /// çatlar; CollapseDelay s sonra çöküş, girişten itibaren CollapseSpeed m/s ile kızağın arkasından ilerler (çatlağın
        /// kayada yayılma hızı). Ondan hızlı giden kızak karşıya geçer; yavaş giden ya da duranın altındaki taş çöker.
        /// 24 m'lik köprüde en düşük ortalama hız ~ 24 / (24/8 + 0.6) ≈ 6.7 m/s (24 km/s).
        public bool fragileBridges;
        public const float CollapseDelay = 0.6f, CollapseSpeed = 8f;
        public readonly List<Vector3> pinches = new List<Vector3>();
        public const float WallTop = 14f;

        public float WallHalfAt(float z)
        {
            float w = wallHalf;
            foreach (var p in pinches)
            {
                // Boğazın ortasındaki yarısı tam dar, kenarlarda yumuşakça açılır.
                float q = p.y * 0.25f;
                float d = (Mathf.Abs(z - p.x) - q) / q;
                if (d < 1f) w = Mathf.Min(w, Mathf.Lerp(p.z, wallHalf, Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, d))));
            }
            return w;
        }

        /// Hızlandırıcı yüzey (kullanıcı 2026-10-23, 3. parkurdan itibaren): yerde parlak gümüş levha, üstünde yanıp sönen
        /// kırmızı ok başları. Üstünden geçen kızak hafifçe sıçrar ve hızlanır (SledPhysics). x: levhanın ortası (z boyunca düz).
        public struct Boost { public float z0, z1, x; public bool ramp; }
        public readonly List<Boost> boosts = new List<Boost>();
        public const float BoostHalfWidth = 1.3f, BoostLength = 7f;

        /// (x, z) hangi hızlandırıcının üstünde (yoksa -1)?
        public int BoostAt(float x, float z)
        {
            for (int i = 0; i < boosts.Count; i++)
            {
                var b = boosts[i];
                if (z >= b.z0 && z <= b.z1 && Mathf.Abs(x - b.x) <= BoostHalfWidth) return i;
            }
            return -1;
        }
        /// Tablanın yarı genişliği (kızağın ortası bu aralıkta kalmalı; kızak 0.76 m genişliğinde). 0.6'da simdeki
        /// ortalama oyuncu Kanyon ve Karlı Dağ'ın ilk köprüsünden hep düşüyordu (yol haritası aşama 3).
        public const float BridgeHalfWidth = 1.0f;   // 2026-10-23: 0.8 -> 1.0 (otomatik hizalama kalkınca düz köprüler çok affetmezdi; yön yine tamamen oyuncuda)

        /// Çatal: patika z0..z1 boyunca ikiye ayrılır; ortada çimenli alçak sırt, iki yanda birer şerit (vadi ortasından
        /// ±ForkOffset). Şeritlerden biri yavaş (su/çamur/tümsek), öteki hızlı ya da temiz: oyuncu seçer, seçimi mesafeye
        /// yansır. good: altınların dizildiği (iyi) şeridin yönü (-1 sol, +1 sağ).
        public struct Fork { public float z0, z1; public int good; }
        public readonly List<Fork> forks = new List<Fork>();
        // 2026-10-19: sırt 0.75 -> 0.45 m ve daha geniş, ayrılma/birleşme 14 -> 20 m (çatal 44 m): çatal çıkışında şerit 14 m içinde 3 m
        // yana kayıyordu, sert yan ivme ortalama oyuncuya sütün %16-62sini döktürüyordu.
        public const float ForkOffset = 3.1f, ForkRidge = 0.45f, ForkRidgeHalf = 2.2f, ForkBlend = 20f;

        /// Patikanın vadi ortasına toplandığı bölgeler (köprü yaklaşımı ve tablası; z0, z1), CenterBlend ile yumuşak.
        public readonly List<Vector2> centerZones = new List<Vector2>();
        const float CenterBlend = 25f;

        float CenterWeight(float z)
        {
            for (int i = 0; i < centerZones.Count; i++)
            {
                var c = centerZones[i];
                if (z <= c.x - CenterBlend || z >= c.y + CenterBlend) continue;
                float d = z < c.x ? c.x - z : z > c.y ? z - c.y : 0f;
                return 1f - Mathf.SmoothStep(0f, 1f, d / CenterBlend);
            }
            return 0f;
        }

        /// Çatalın açıklığı: 0 tek patika, 1 tam ayrık iki şerit (giriş ve çıkışta yumuşakça).
        public float ForkWeight(float z)
        {
            for (int i = 0; i < forks.Count; i++)
            {
                var f = forks[i];
                if (z <= f.z0 || z >= f.z1) continue;
                return Mathf.SmoothStep(0f, 1f, Mathf.Min((z - f.z0) / ForkBlend, (f.z1 - z) / ForkBlend));
            }
            return 0f;
        }

        public bool ForkAt(float z, out Fork fork)
        {
            foreach (var f in forks)
                if (z > f.z0 && z < f.z1) { fork = f; return true; }
            fork = default;
            return false;
        }

        /// Altınların ve ipuçlarının izlediği çizgi: çatalda iyi şeridin ortası, başka yerde patika.
        public float GuideX(float z)
        {
            float w = ForkWeight(z);
            if (w <= 0f) return PathX(z);
            ForkAt(z, out var f);
            return PathX(z) + f.good * ForkOffset * w;
        }

        /// Çatal sırtının yüksekliği (yuvarlak kesit).
        float ForkHeight(float x, float z)
        {
            if (forks.Count == 0) return 0f;
            float w = ForkWeight(z);
            if (w <= 0f) return 0f;
            // Sırt şeritlerle birlikte daralır: giriş/çıkışta birbirine yaklaşan şeritler sırtın yamacına tırmanmasın.
            float r = Mathf.Abs(x - CenterX(z)) / (ForkRidgeHalf * Mathf.Max(w, 0.05f));
            return r >= 1f ? 0f : w * ForkRidge * 0.5f * (1f + Mathf.Cos(r * Mathf.PI));
        }
        public readonly List<SurfaceZone> surfaces = new List<SurfaceZone>();
        public readonly List<Flock> flocks = new List<Flock>();
        /// Roket tepelerinin zirve z'leri.
        public readonly List<float> rocketHills = new List<float>();
        /// Yükselen hava (kullanıcı 2026-10-19: hiç yükseğe süzülmüyoruz, uçuş hissi olsun): (z0, z1, güç m/s²). Kanadı açık
        /// kızak içinden geçerken yukarı itilir, kanadın taşıma süresi tükenmez, otomatik süzülme yamaca dalmaz.
        public readonly List<Vector3> updrafts = new List<Vector3>();

        /// z'deki yükselen havanın gücü (m/s², Dünya yerçekimine göre; yoksa 0). Başta 3 m'de tam güç (rampadan çıkan
        /// kızak hemen taşınsın, kullanıcı 2026-10-19), sonda 8 m'de söner.
        public float UpdraftAt(float z)
        {
            foreach (var u in updrafts)
                if (z > u.x && z < u.y) return u.z * Mathf.Clamp01(Mathf.Min((z - u.x) / 3f, (u.y - z) / 8f));
            return 0f;
        }
        /// Havada alınan roket hakkı: pistin %70'inden sonraki ilk rampanın uçuş yolunda, yerden yetişilemeyecek
        /// yükseklikte (her pistte en fazla bir tane). Kızağın merkezi (konum + 0.6 m) bu yarıçapa girerse alınır.
        public bool hasRocketPickup;
        public Vector3 rocketPickup;
        public const float RocketPickupRadius = 1.5f;

        /// Su birikintisinde sürtünmeye eklenen katsayı; buzda ve çimende sürtünme çarpanı.
        public const float PuddleDrag = 0.4f, MudDrag = 0.45f, IceFactor = 0.25f, GrassFactor = 1.2f;
        public const float PathHalfWidth = 1.7f;
        /// Yamaç: tabanın kenarından itibaren BankCurve·d² yükselir (d ≤ BankFlex), sonra aynı eğimle sürer.
        /// BankLimit: yamaçta kaya ve bitki yerleşiminin sınırı. BankRim: yamacın tepesi (taban kenarından); ötesi
        /// düzlüktür ve pistin dışıdır: oraya çıkan kızak düşer (görünmez duvar yok, kullanıcı kararı 2026-10-08).
        public const float BankCurve = 0.09f, BankFlex = 6f, BankLimit = 9f, BankRim = 10f;

        // Vadi kıvrımı ve patika (SetValley)
        float meanderAmp, meanderLength = 160f, pathAmp, pathLength = 55f, phaseA, phaseB, phaseC;

        readonly float step;
        readonly float[] heights;

        const float RoundLength = 14f;

        public TrackProfile(string name, Vector2[] points, float finishZ, float halfWidth,
                            IList<float> corners = null, IList<Vector2> sharpZones = null,
                            float step = 0.25f, float smoothRadius = 3f, IList<Vector2> rawZones = null)
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
            // Yumuşatılmayan bölgeler (köprülü derenin dik kıyıları): yumuşatma 5 m'lik kıyıyı 6 m'lik rampaya
            // çeviriyordu, tabladan kayan hızlı kızak karşı kıyıya atlayıp kurtuluyordu.
            if (rawZones != null)
                foreach (var zone in rawZones)
                    for (int i = Mathf.Max(0, Mathf.CeilToInt((zone.x - startZ) / step)); i < n && startZ + i * step <= zone.y; i++)
                        heights[i] = raw[i];

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
            k *= 1f - ForkWeight(z);   // çatalda patika vadinin ortasına gelir, şeritler ondan ayrılır
            k *= 1f - CenterWeight(z); // köprü yaklaşımında da: tabla vadinin ortasında, kenara açılı gelinmesin
            return CenterX(z) + k * pathAmp * Mathf.Sin(Bend(z) * 6.2832f / pathLength + phaseC);
        }

        /// Yolsuz harita (kullanıcı 2026-10-11): ortada toprak yol çizilmez, bütün zemin aynı hızda kayar (Orman'da çimen,
        /// Karlı Dağ'da açık kar). Güzergâh (PathX) altınlar ve otomatik pilot için yine vardır.
        public bool noPath;

        /// Güzergâhın (yol çizilmese de) üstünde mi: otomatik pilot ve altın dizilişi bunu izler.
        public bool NearRoute(float z, float x)
        {
            float w = ForkWeight(z), px = PathX(z);
            if (w <= 0f) return Mathf.Abs(x - px) <= PathHalfWidth;
            return Mathf.Abs(Mathf.Abs(x - px) - ForkOffset * w) <= PathHalfWidth;
        }

        public bool OnPath(float z, float x)
        {
            if (noPath) return true;
            float w = ForkWeight(z), px = PathX(z);
            if (w <= 0f) return Mathf.Abs(x - px) <= PathHalfWidth;
            float d = Mathf.Abs(Mathf.Abs(x - px) - ForkOffset * w);
            return d <= PathHalfWidth;
        }

        /// Yamaç yüksekliği; dx: vadi eksenine uzaklık.
        /// Yan yamaçların yüksekliği (1 = vadi; 0 = dümdüz açık arazi, Orman).
        public float bankScale = 1f;

        public float Bank(float dx)
        {
            if (bankScale <= 0f) return 0f;
            float d = Mathf.Min(Mathf.Abs(dx) - halfWidth, BankRim);   // tepeden sonra düz (görseldeki arazi gibi)
            if (d <= 0f) return 0f;
            if (d <= BankFlex) return bankScale * BankCurve * d * d;
            return bankScale * (BankCurve * BankFlex * BankFlex + 2f * BankCurve * BankFlex * (d - BankFlex));
        }

        /// Zemin yüksekliği (vadi tabanı + yamaç + tümsekler; köprü tablasının üstünde tabla).
        public float Height(float x, float z)
        {
            foreach (var b in bridges)
                if (z >= b.z0 && z <= b.z1 && Mathf.Abs(x - b.XAt(z)) <= b.HalfWidth) return DeckHeight(b, z);
            return TerrainHeight(x, z);
        }

        /// Arazi (köprüsüz): vadi, patika ve yüzey mesh'leri bunu kullanır.
        public float TerrainHeight(float x, float z) => Height(z) + Bank(x - CenterX(z)) + BumpHeight(x, z);

        public static float DeckHeight(Bridge b, float z) => Mathf.Lerp(b.y0, b.y1, Mathf.InverseLerp(b.z0, b.z1, z));

        /// (x, z) tablada ama z±d tablanın dışında mı (kıvrımlı tablada kenara yakın)?
        bool DeckEdge(float x, float z, float d, out Bridge bridge)
        {
            foreach (var b in bridges)
                if (z >= b.z0 && z <= b.z1 && Mathf.Abs(x - b.XAt(z)) <= b.HalfWidth)
                {
                    bridge = b;
                    bool inside(float zz) => zz >= b.z0 && zz <= b.z1 && Mathf.Abs(x - b.XAt(zz)) <= b.HalfWidth;
                    return (z + d <= b.z1 && !inside(z + d)) || (z - d >= b.z0 && !inside(z - d));
                }
            bridge = default;
            return false;
        }

        /// Köprü tablasının üstünde mi?
        public bool OnDeck(float x, float z)
        {
            foreach (var b in bridges)
                if (z >= b.z0 && z <= b.z1 && Mathf.Abs(x - b.XAt(z)) <= b.HalfWidth) return true;
            return false;
        }

        /// Köprülü derenin üstünde ama tablanın dışında mı (kızak dereye düşer)?
        public bool OverRavine(float x, float z)
        {
            // Derede birden çok köprü olabilir: herhangi birinin tablasındaysa düşmez.
            bool over = false;
            foreach (var b in bridges)
                if (z > b.r0 && z < b.r1)
                {
                    if (Mathf.Abs(x - b.XAt(z)) <= b.HalfWidth) return false;
                    over = true;
                }
            return over;
        }

        /// z'nin önündeki (range metre içinde başlayan) köprü; tablanın üstündeyken değil.
        public bool BridgeAhead(float z, float range, out Bridge bridge)
        {
            foreach (var b in bridges)
                if (b.z0 > z && b.z0 - z < range) { bridge = b; return true; }
            bridge = default;
            return false;
        }

        /// z bir köprü açıklığında mı (otomatik pilot ve coinler için)?
        public bool BridgeAt(float z, out Bridge bridge)
        {
            foreach (var b in bridges)
                if (z >= b.z0 - 0.5f && z <= b.z1 + 0.5f) { bridge = b; return true; }
            bridge = default;
            return false;
        }

        /// z'deki köprülerden x'e (tabla orta çizgisine) en yakını.
        public bool BridgeAt(float z, float x, out Bridge bridge)
        {
            bool found = false;
            bridge = default;
            float best = float.MaxValue;
            foreach (var b in bridges)
                if (z >= b.z0 - 0.5f && z <= b.z1 + 0.5f)
                {
                    float d = Mathf.Abs(x - b.XAt(z));
                    if (d < best) { best = d; bridge = b; found = true; }
                }
            return found;
        }

        /// Tümsek listesini z kovalarına dizer (her yükseklik sorgusu yalnızca yakındaki tümseklere bakar).
        public void IndexBumps()
        {
            bumpIndex = null;
            if (bumps.Count == 0) return;
            int n = Mathf.CeilToInt((EndZ - startZ) / BumpBucket) + 1;
            bumpIndex = new List<int>[n];
            for (int i = 0; i < bumps.Count; i++)
            {
                int a = Mathf.Clamp(Mathf.FloorToInt((bumps[i].z0 - startZ) / BumpBucket), 0, n - 1);
                int b = Mathf.Clamp(Mathf.FloorToInt((bumps[i].z0 + bumps[i].length - startZ) / BumpBucket), 0, n - 1);
                for (int k = a; k <= b; k++) (bumpIndex[k] ??= new List<int>()).Add(i);
            }
        }

        /// Bu noktadaki tümseklerin toplam yüksekliği.
        public float BumpHeight(float x, float z)
        {
            float fh = ForkHeight(x, z);
            if (bumpIndex == null) return fh;
            int k = Mathf.FloorToInt((z - startZ) / BumpBucket);
            if (k < 0 || k >= bumpIndex.Length || bumpIndex[k] == null) return fh;
            float h = fh, center = float.NaN;
            foreach (int i in bumpIndex[k])
            {
                var b = bumps[i];
                float u = (z - b.z0) / b.length;
                if (u <= 0f || u >= 1f) continue;
                float side = 1f;
                if (b.halfWidth < 30f)
                {
                    if (float.IsNaN(center)) center = CenterX(z);
                    float d = Mathf.Abs(x - center - b.xc) - b.halfWidth;
                    float fall = b.ramp ? 1.2f : 3f;
                    if (d >= fall) continue;
                    if (d > 0f) side = 1f - Mathf.SmoothStep(0f, 1f, d / fall);
                }
                if (b.ramp)
                {
                    const float Lip = 0.78f;   // 0.85'te dudak çok keskindi: kızak yatarak havalanıyor, süt bir anda dökülüyordu
                    float r = u < Lip ? (u / Lip) * (u / Lip) : 1f - Mathf.SmoothStep(0f, 1f, (u - Lip) / (1f - Lip));
                    h += b.height * side * r;
                }
                else h += b.height * side * 0.5f * (1f - Mathf.Cos(u * 6.2832f));
            }
            return h;
        }

        /// Yanal eğim dH/dx (yamaçta kızağı ortaya iten).
        public float SlopeX(float x, float z) => OnDeck(x, z) ? 0f : (Height(x + 0.1f, z) - Height(x - 0.1f, z)) / 0.2f;   // tabla enine düz

        /// Kızağın x konumunda ileri eğim dH/dz (kıvrımın yamaç etkisi dahil).
        /// Tablanın üstünde eğim tablanın kendisinden: kıvrımlı tablada z±step örneği tablanın dışına (5 m aşağıdaki dere
        /// yatağına) düşüp sahte bir duvar eğimi veriyor, kızak tablanın ortasında bir anda duruyordu.
        public float SlopeZ(float x, float z)
        {
            // Yalnız örneklerden biri tablanın dışına düşüyorsa (kıvrımlı tablanın kenarı): düz tablada eski hesap aynen.
            if (DeckEdge(x, z, step, out var b)) return (b.y1 - b.y0) / (b.z1 - b.z0);
            return (Height(x, z + step) - Height(x, z - step)) / (2f * step);
        }

        /// Kuş sürüsünün t anındaki merkezi (atışın kendi saatine göre: fizik ve görüntü aynı yeri kullanır).
        public Vector3 FlockCenter(Flock f, float t) =>
            new Vector3(CenterX(f.z) + f.x + f.sway * Mathf.Sin(t * 6.2832f / f.period + f.z), f.y, f.z);

        /// Yüzey normali (birim).
        public Vector3 Normal(float x, float z) => new Vector3(-SlopeX(x, z), 1f, -SlopeZ(x, z)).normalized;

        public float CurvatureAt(float x, float z)
        {
            if (DeckEdge(x, z, 0.5f, out _)) return 0f;   // tabla düz (bkz. SlopeZ)
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

        /// Arcade steering keeps low-speed control, but ice needs an earlier, wider turn.
        /// Decks retain full control so an overlapping surface patch cannot make a bridge unfair.
        public float LateralGrip(float z, float x)
        {
            if (OnDeck(x, z)) return 1f;
            switch (SurfaceAt(z, x))
            {
                case Surface.Ice: return 0.72f;
                case Surface.Mud: return 0.88f;
                case Surface.Puddle: return 0.9f;
                default: return 1f;
            }
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
        public const float PuddleResist = 0.028f, MudResist = 0.03f;

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
        /// Ağaç engeli (Orman): ince gövde, çarpılınca düşülür. variant 4.
        public void AddTree(float z, float x)
        {
            float s = 0.85f + 0.4f * Mathf.Repeat(z * 0.291f + x * 0.37f, 1f);
            obstacles.Add(new Obstacle { x = PathX(z) + x, z = z, isRock = true, variant = 4, halfWidth = 0.4f, halfDepth = 0.4f, height = 6f * s });
        }

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
