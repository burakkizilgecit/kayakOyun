using UnityEngine;

namespace SlingRiders
{
    public enum UpgradeType { Slingshot, Runners, Glass, Income, Rocket }

    /// Yükseltmeler pist başınadır (kullanıcı kararı 2026-10-09): yeni piste geçince beş kart da en baştan başlar.
    /// Her kart pistin aşama sayısı kadar ana seviyedir (Çayır ve Orman 5, zor pistler 6), her ana seviye 5 kademe.
    /// Ana seviye bitince parça araçta görünür biçimde değişir (Stage). Kademenin gücü pistin güç aralığına eşlenir
    /// (TrackLibrary.PowerStart/PowerEnd): 0. kademe o pist için zayıf bir başlangıç, son kademe pisti rahat bitirir.
    public static class Upgrades
    {
        public const int Count = 5;
        public const int StageSize = 5;   // kullanıcı kararı (2026-10-10, referans oyun): her aşama 5 yükseltme
        public const int MaxStages = 6;

        /// Geçerli pist: aşama sayısı, fiyat ve güç buna göre. Oyun pisti yüklerken, simler pist başına ayarlar.
        public static int Track;

        public static int Stages => StagesFor(Track);
        public static int Max => MaxFor(Track);
        public static int StagesFor(int track) => TrackLibrary.UpgradeStages[Mathf.Clamp(track, 0, TrackLibrary.Count - 1)];
        public static int MaxFor(int track) => StagesFor(track) * StageSize;

        public static readonly string[] Names = { "Slingshot", "Sled", "Cup", "Income", "Rocket" };

        public static readonly string[] Descriptions =
        {
            "Stiffer band, faster launch",
            "Less friction · wings later on",
            "A new cup each stage: milk spills less",
            "More coins every run",
            "Tap once per run: strong boost · stronger each level",
        };

        /// Kart adı: Kızak kartı pistin aracının adını taşır (Kum Sörfü, Bobsled, Hover).
        public static string Name(int type) => type == (int)UpgradeType.Runners ? Vehicles.CardName(TrackLibrary.Vehicles[Track]) : Names[type];

        // Fiyat: kartın taban fiyatı × pistin fiyat ölçeği × iki aşamalı büyüme (kullanıcının referans oyunu, kızak kartı:
        // 50 70 100 135 190 | 270 375 525 740 1035 | 1345 ... 25. seviyede ~35 bin). İlk Knee kademede ×growth (1.4),
        // sonra ×lateGrowth (1.24). Kartlar arası oran eski tablodan (sapan kızağın %80'i, bardak 6 katı...).
        public static int[] baseCosts = { 40, 50, 300, 60, 120 };
        public static float[] growth = { 1.4f, 1.4f, 1.4f, 1.4f, 1.4f };
        public static float lateGrowth = 1.24f;
        public const int Knee = 10;

        // Güç tabloları (güç endeksi 0..20; 20'nin ötesi son eğimle sürer). Kademe pistin aralığına eşlenir.
        static readonly float[] slingFactor =
            { 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f, 5.25f, 5.5f, 5.75f, 6f, 6.25f, 6.5f, 6.75f, 7f, 7.5f, 8f, 8.5f, 9f };
        // Zemin sürtünmesi: toprak patika (çimen x1.2, buz x0.25); güç endeksi başına 0.004 azalır (0.25 → 0.17 → ...).
        static readonly float[] runnerFriction =
            { 0.25f, 0.246f, 0.242f, 0.238f, 0.234f, 0.23f, 0.226f, 0.222f, 0.218f, 0.214f, 0.21f, 0.206f, 0.202f, 0.198f, 0.194f, 0.19f, 0.186f, 0.182f, 0.178f, 0.174f, 0.17f };

        // Kanat (kullanıcı kararı 2026-10-10): önce tutma ipi (1. aşama), sonra minik kanat (2. aşama) ve her aşamada bir
        // boy büyür: minik → küçük → orta → büyük. Kalıcı kanat parçası (Çayır bitince) sırayı iki aşama öne alır: sonraki
        // pistler minik kanatla başlar.
        public static readonly float[] WingSpans = { 0f, 0.32f, 0.58f, 0.85f, 1.18f };
        public static readonly float[] WingAreas = { 0f, 0.7f, 1.4f, 2.2f, 2.6f };
        /// Kanat boyu adımı (0 yok, 1 minik ... 4 büyük).
        public static int WingStep(int runnerLevel) =>
            Mathf.Clamp(Stage(runnerLevel) - 1 + (Parts >= 1 ? 2 : 0), 0, WingSpans.Length - 1);
        /// Kanadın ilk geldiği kademe (bu pistte).
        public static int WingLevel => Parts >= 1 ? 0 : 2 * StageSize;
        /// Görünen kanadın bir tarafının boyu (m).
        public static float WingSpan(int runnerLevel) => WingSpans[WingStep(runnerLevel)];
        /// Kanadın bir kalkışta tam taşıdığı süre: ilk geldiğinde 1.3 s, her kademede +0.4 s.
        public static float GlideTime(int runnerLevel) => WingStep(runnerLevel) == 0 ? 0f : 1.3f + 0.4f * (runnerLevel - WingLevel);

        public static readonly string[] GlassNames = { "Plastic Cup", "Mug", "Thermos", "Straw Bottle", "Space Cup", "Crystal Thermos" };
        // Bardak modeli (mesh) 0..4; 5. aşama 4. modelin kristal sürümü, son kademe altın sürümü.
        static readonly float[] glassRadius = { 0.035f, 0.036f, 0.034f, 0.031f, 0.03f };
        static readonly float[] glassHeight = { 0.12f, 0.155f, 0.18f, 0.21f, 0.24f };
        // Aşamaya göre (0..6): her yeni bardak daha sönümlü ve ağzına daha çok boşluk bırakır.
        static readonly float[] glassDamping = { 0.12f, 0.24f, 0.32f, 0.4f, 0.5f, 0.56f, 0.62f };
        public static float[] glassHeadroom = { 0.019f, 0.023f, 0.027f, 0.032f, 0.037f, 0.04f, 0.043f };

        public static int MaxLevel(int type) => Max;

        /// Kademenin güç endeksi: pistin aralığında doğrusal (0. kademe PowerStart, son kademe PowerEnd).
        public static float Power(int level) => Mathf.Lerp(TrackLibrary.PowerStart[Track], TrackLibrary.PowerEnd[Track], (float)level / Max);

        /// Tabloyu kesirli endeksle okur; tablo sonundan sonra son eğimle devam eder.
        static float Sample(float[] table, float p)
        {
            int n = table.Length - 1;
            if (p <= 0f) return table[0];
            if (p >= n) return table[n] + (table[n] - table[n - 1]) * (p - n);
            int i = (int)p;
            return Mathf.Lerp(table[i], table[i + 1], p - i);
        }

        // Keep existing within-stage bonuses. A new model must at least match the previous
        // stage's last purchase; this also covers the crystal/gold stages with smaller table gaps.
        static float CupValue(float[] table, int level, float perLevel)
        {
            int stage = Mathf.Clamp(Stage(level), 0, table.Length - 1);
            float floor = table[0];
            for (int i = 1; i <= stage; i++)
                floor = Mathf.Max(table[i], floor + perLevel * (StageSize - 1));
            return floor + perLevel * Mathf.Max(0, level - stage * StageSize);
        }

        public static int Cost(int type, int level)
        {
            float price = baseCosts[type] * TrackLibrary.CostScale[Track] * Mathf.Pow(growth[type], Mathf.Min(level, Knee))
                          * Mathf.Pow(lateGrowth, Mathf.Max(0, level - Knee));
            return Mathf.Max(5, Mathf.RoundToInt(price / 5f) * 5);
        }

        /// Elmasla yükseltme (kullanıcı kararı 2026-10-24: "elması olan sürekli geliştirme yapamamalı"): yalnız aşamanın son
        /// kademesi (aracı yeni görünüme geçiren) coin yetmezse GemUpgradePrice elmasla alınabilir; diğer kademeler yalnız coin.
        public const int GemUpgradePrice = 2;
        public static bool GemPayable(int level) => level % StageSize == StageSize - 1;
        public static int GemCost(int type, int level) => GemPayable(level) ? GemUpgradePrice : 0;

        public static float IncomeMultiplier(int level) => 1f + 0.1f * level;   // 25 kademede ×3.5 (eski 20 × 0.125 ile aynı)

        public static float RocketThrust(float power) => 150f + 20f * power;
        public static float RocketBurn(float power) => 0.8f + 0.03f * power;

        /// Tamamlanmış ana seviye sayısı (0..Stages): araçtaki görünümü belirler.
        public static int Stage(int level) => level / StageSize;

        /// Bardak modeli (mesh): her ana seviye yeni bir bardak, 4'ten sonra aynı model (kristal / altın sürüm).
        public static int GlassModel(int level) => Mathf.Min(level / StageSize, 4);

        /// Bardak kartının aşama adı: son aşama altın sürüm.
        public static string GlassStageName(int stage) => stage >= Stages ? "Golden " + GlassNames[Mathf.Min(Stages - 1, GlassNames.Length - 1)]
                                                         : GlassNames[Mathf.Clamp(stage, 0, GlassNames.Length - 1)];

        // ---------------------------------------------------------------- kalıcı parçalar (kullanıcı isteği 2026-10-09)
        // Her parkur bitince kızağa kalıcı bir parça eklenir (geliştirmeler sıfırlansa da kalır):
        // Çayır → kanat (her pistte en baştan), Orman → aero kuyruk, Kanyon → pervane, Karlı Dağ → turbo pervane,
        // Çöl → çift turbo pervane, Volkan → jet motoru.

        /// Bitirilen parkur sayısı (açılan parça kademesi). Oyunda kayıttan (save.unlocked); -1 iken simler için o anki pist
        /// (pist t'ye gelen oyuncu önceki t parkuru bitirmiştir).
        public static int PartTier = -1;
        public static int Parts => PartTier >= 0 ? PartTier : Track;

        public static readonly string[] PartNames = { "Wings", "Aero Tail", "Propeller", "Turbo Propeller", "Twin Turbo Propeller", "Jet Engine" };
        public static readonly string[] PartTexts =
        {
            "Every track starts with tiny wings, and they grow sooner",
            "12% less air drag: keeps speed on long glides",
            "A gentle push while sliding: carries a slowing sled further",
            "A stronger push: takes slopes further",
            "Two turbo propellers: a big help on long flats",
            "Jet engine: the strongest push for the long flats of Mars",
        };

        /// Pervane itişi (m/s²) parça kademesine göre.
        public static float PropThrust(int parts) => parts >= 6 ? 0.45f : parts >= 5 ? 0.36f : parts >= 4 ? 0.26f : parts >= 3 ? 0.16f : 0f;

        public static SledConfig BuildConfig(int[] levels, Perk perk = Perk.None)
        {
            var c = new SledConfig();
            int sl = levels[(int)UpgradeType.Slingshot], ru = levels[(int)UpgradeType.Runners];
            c.bandStiffness *= Sample(slingFactor, Power(sl));
            c.groundFriction = Mathf.Max(0.08f, Sample(runnerFriction, Power(ru)));
            int wing = WingStep(ru);
            c.wingArea = WingAreas[wing];
            c.wingMass = wing == 0 ? 0f : Mathf.Lerp(1.2f, 3f, wing / 4f) * Mathf.Lerp(1f, 0.6f, (float)ru / Max);
            c.wingSpan = WingSpans[wing];
            c.glideTime = GlideTime(ru);
            if (Parts >= 2) c.dragArea *= 0.88f;
            c.dragArea *= Mathf.Lerp(TrackLibrary.DragStart[Track], TrackLibrary.DragEnd[Track], (float)ru / Max);
            c.propThrust = PropThrust(Parts);
            c.parts = Parts;
            c.vehicle = TrackLibrary.Vehicles[Track];

            int gl = levels[(int)UpgradeType.Glass];
            int stage = Mathf.Min(Stage(gl), glassDamping.Length - 1);
            int g = GlassModel(gl);
            c.glassRadius = glassRadius[g];
            c.glassHeight = glassHeight[g];
            // Aynı bardakta her kademe biraz daha sönümlü ve ağzına 1 mm daha boşluk.
            c.sloshDamping = CupValue(glassDamping, gl, 0.02f);
            c.milkHeadroom = CupValue(glassHeadroom, gl, 0.001f) * TrackLibrary.MilkHeadroomScale[Track];
            c.glassGold = gl >= Max;
            c.glassStage = stage;
            c.glassModel = g;
            // İyi bardak = daha kolay dengelenir. 2026-10-19: başlangıç 0.4 -> 0.5 (ortalama oyuncu ilk haritada sütün
            // %38'ini döküyordu; kayıp birkaç olayda toplanıyordu: çatal çıkışı, rampa).
            c.glassReflex = 0.5f + 0.35f * gl / Max;

            int r = levels[(int)UpgradeType.Rocket];
            c.rocketThrust = r == 0 ? 0f : RocketThrust(Power(r));
            c.rocketBurn = r == 0 ? 0f : RocketBurn(Power(r));
            c.rocketCharges = r == 0 ? 0 : 1;   // her koşuda tek ateşleme (kullanıcı kararı); seviye itişi ve süreyi artırır
            c.slingStage = Stage(sl);
            c.runnerStage = Stage(ru);
            c.rocketStage = Stage(r);
            AvatarLibrary.ApplyPerk(perk, c);
            return c;
        }
    }
}
