using UnityEngine;

namespace SledSurfers
{
    public enum UpgradeType { Slingshot, Runners, Glass, Income, Rocket }

    /// Her kart 5 ana seviyedir, her ana seviye 4 kademe: toplam 20 geliştirme. Ana seviye bitince o parça kızakta
    /// görünür biçimde değişir (Stage). Parkurlar seviye açar: Çayır ilk 2 seviyeyi (8 kademe), Orman 4'ünü (16),
    /// Kanyon 5'ini (20). Kızak kartı kanadı da içerir: 4. seviyede küçük kanat gelir, 5.'de büyür.
    public static class Upgrades
    {
        public const int Count = 5;
        public const int StageSize = 4;
        public const int Stages = 5;
        public const int Max = StageSize * Stages;   // 20

        public static readonly string[] Names = { "Sapan", "Kızak", "Bardak", "Gelir", "Roket" };

        public static readonly string[] Descriptions =
        {
            "Daha sert lastik, daha hızlı çıkış",
            "Daha az sürtünme · 5. seviyede kanat, 16'da büyük kanat",
            "Her seviye yeni bardak: süt daha zor dökülür",
            "Her atışta daha çok coin",
            "Koşuda bir kez dokun: güçlü itiş · her seviye daha güçlü",
        };

        /// Kızağın ana seviyelerinde gelen görünüm (seviye bitince).
        public static readonly string[] RunnerStageNames = { "Tutma ipi", "Gümüş kayaklar", "Arka tampon kanadı", "Büyük kanatlar", "Altın kanatlar" };

        /// Açılmış en ileri parkura göre en yüksek kademe: 20 kademenin tamamı baştan açıktır (kullanıcı kararı).
        /// Fiyat eğrisi için Çayır kademeleri ilk 8 sayılır.
        static readonly int[] tierCaps = { 20, 20, 20, 20 };
        const int EarlyLevels = 8;

        public static int[] baseCosts = { 68, 85, 510, 100, 205 };
        // Fiyat büyümesi: Çayır kademelerinde hızlı (ilk kademeler neredeyse her atışta alınır, sonrakiler birkaç atış
        // ister), sonraki parkurlarda daha yavaş (Orman/Kanyon kazancı mesafeyle zaten büyür).
        public static float[] growth = { 1.65f, 1.65f, 1.9f, 1.75f, 1.7f };
        public static float[] laterGrowth = { 1.3f, 1.35f, 1.5f, 1.35f, 1.3f };
        // Son aşama (17-20): Kanyon ve Karlı Dağ'ın istediği seviyeler, kazancı yüksek pistlerde alınır.
        public static float[] topGrowth = { 1.3f, 1.8f, 1.5f, 1.35f, 1.8f };
        const int LateLevels = 16;
        /// Son seviye (20) yalnızca final pist Karlı Dağ için gerekir: ek çarpanla pahalıdır.
        public static float FinalMultiplier = 1.7f;

        // Kademe başına değerler (0..20). Çayır'ın sonu (8) önceki dengedeki Çayır maksimumuyla aynı güçtedir.
        static readonly float[] slingFactor =
            { 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f, 5.25f, 5.5f, 5.75f, 6f, 6.25f, 6.5f, 6.75f, 7f, 7.5f, 8f, 8.5f, 9f };
        // Zemin sürtünmesi: toprak patika (çimen x1.2, buz x0.25). 2026-10-08 kullanıcı kararı: orta sürtünme
        // (kızak tekerlekli gibi kaymasın, ama pistte bol yokuş kalsın); kızak geliştikçe ölçülü azalır,
        // seviye başına 0.004 (0.25 → 0.17). Belirleyici olan sapanın verdiği hız, roket ve rota (su, çamur, çimen).
        public static readonly float[] runnerFriction =
            { 0.25f, 0.246f, 0.242f, 0.238f, 0.234f, 0.23f, 0.226f, 0.222f, 0.218f, 0.214f, 0.21f, 0.206f, 0.202f, 0.198f, 0.194f, 0.19f, 0.186f, 0.182f, 0.178f, 0.174f, 0.17f };
        // Kanat (2026-10-08, kullanıcı kararı): kızağın 5. seviyesinde gelir, her seviyede büyür. Kural: vadi kanatsız
        // geçilemez, süzülen kızak (sürtünmesiz) daha uzağa gider. 40 kg'ı 20 m/s'de taşımak ~2 m² ister: ilk kanat
        // bile gerçekten süzülür (1.6 m²), büyüdükçe süzülme oranı ve hızı artar.
        // 2026-10-08 kullanıcı kararı: kanat iki boy — küçük (Kızak 5-15) ve büyük (16-20); görünen boy eskisinin en
        // fazla yarısı (WingSpan), taşıma gücü eski düzeyde kalır (alan 2.2 / 2.6 m²). Seviye farkı süzülme süresinden.
        public static readonly float[] wingAreas =
            { 0f, 0f, 0f, 0f, 0f, 2.2f, 2.2f, 2.2f, 2.2f, 2.2f, 2.2f, 2.2f, 2.2f, 2.2f, 2.2f, 2.2f, 2.6f, 2.6f, 2.6f, 2.6f, 2.6f };
        static readonly float[] wingMasses =
            { 0f, 0f, 0f, 0f, 0f, 3f, 3f, 2.9f, 2.8f, 2.7f, 2.6f, 2.5f, 2.4f, 2.3f, 2.2f, 2.1f, 2f, 1.9f, 1.8f, 1.7f, 1.5f };
        /// Kanadın geldiği ve büyüdüğü kızak seviyeleri.
        public const int WingLevel = 5, BigWingLevel = 16;

        /// Görünen kanadın bir tarafının boyu (m): küçük 0.85, büyük 1.18 (eski en uzun kanadın yarısı).
        public static float WingSpan(int runnerLevel) => runnerLevel < WingLevel ? 0f : runnerLevel < BigWingLevel ? 0.85f : 1.18f;

        public static readonly string[] GlassNames = { "Plastik Bardak", "Kupa", "Termos", "Pipetli Şişe", "Uzay Bardağı" };
        // Her yeni bardak daha yüksek (ağza daha çok pay) ve daha sönümlü (çalkantı çabuk söner).
        static readonly float[] glassRadius = { 0.035f, 0.036f, 0.034f, 0.031f, 0.03f };
        static readonly float[] glassHeight = { 0.12f, 0.155f, 0.18f, 0.21f, 0.24f };
        static readonly float[] glassDamping = { 0.12f, 0.24f, 0.32f, 0.4f, 0.5f };
        // Bardak ağzına kalan boşluk (m), bardak modeline göre; aynı bardakta her kademe +1 mm (simle ayarlanır).
        public static float[] glassHeadroom = { 0.019f, 0.023f, 0.027f, 0.032f, 0.037f };

        public static int MaxLevel(int type) => Max;

        /// Açılmış parkura göre alınabilecek en yüksek kademe.
        public static int Cap(int type, int tier) => tierCaps[Mathf.Clamp(tier, 0, tierCaps.Length - 1)];

        /// Bu kademeyi açan parkur (yoksa -1).
        public static int UnlockTier(int type, int level)
        {
            for (int t = 0; t < tierCaps.Length; t++) if (tierCaps[t] >= level) return t;
            return -1;
        }

        public static int Cost(int type, int level)
        {
            int first = EarlyLevels;
            float price = baseCosts[type] * Mathf.Pow(growth[type], Mathf.Min(level, first))
                          * Mathf.Pow(laterGrowth[type], Mathf.Clamp(level - first, 0, LateLevels - first))
                          * Mathf.Pow(topGrowth[type], Mathf.Max(0, level - LateLevels))
                          * (level == Max - 1 ? FinalMultiplier : 1f);
            return Mathf.RoundToInt(price / 5f) * 5;
        }

        /// Kademe elmasla alınırsa fiyatı (kullanıcı kararı 2026-10-09): coin fiyatıyla logaritmik büyür, en az 2.
        /// ~70 coin → 2, 1 bin → 5, 10 bin → 8, 100 bin → 11, 500 bin → 14 elmas. Elmas takılınan yerde kestirme olur
        /// ama pahalı kademeleri bedavalaştırmaz (eskiden tüm kademeler baştan açık olduğu için hepsi 2 elmastı).
        public static int GemCost(int type, int level) =>
            Mathf.Max(2, 2 + Mathf.FloorToInt(Mathf.Log(Mathf.Max(Cost(type, level), 1) / 100f, 2f)));

        public static float IncomeMultiplier(int level) => 1f + 0.125f * level;

        /// Kanadın bir kalkışta taşıdığı süre: Kızak 5'te 1.5 s, her seviyede +0.35 s (Kızak 20: 6.75 s). Seviyeler
        /// arası fark belirgin: her seviye vadide ~7 m daha ileri süzülür (vadiler seviye seviye kapı olur).
        public static float GlideTime(int runnerLevel) => runnerLevel < WingLevel ? 0f : 1.5f + 0.35f * (runnerLevel - WingLevel);

        public static float RocketThrust(int level) => 150f + 20f * level;
        public static float RocketBurn(int level) => 0.8f + 0.03f * level;

        /// Tamamlanmış ana seviye sayısı (0..5): kızaktaki görünümü belirler.
        public static int Stage(int level) => level / StageSize;

        /// Bardak modeli: her ana seviye yeni bir bardak.
        public static int GlassModel(int level) => Mathf.Min(level / StageSize, GlassNames.Length - 1);

        /// Bardak kartının aşama adı (0..5): son aşama aynı bardağın altın sürümü.
        public static string GlassStageName(int stage) => stage >= Stages ? "Altın " + GlassNames[GlassNames.Length - 1]
                                                         : GlassNames[Mathf.Clamp(stage, 0, GlassNames.Length - 1)];

        public static SledConfig BuildConfig(int[] levels, Perk perk = Perk.None)
        {
            var c = new SledConfig();
            int sl = levels[(int)UpgradeType.Slingshot], ru = levels[(int)UpgradeType.Runners];
            c.bandStiffness *= slingFactor[sl];
            c.groundFriction = runnerFriction[ru];
            c.wingArea = wingAreas[ru];
            c.wingMass = wingMasses[ru];
            c.wingSpan = WingSpan(ru);
            c.glideTime = GlideTime(ru);

            int gl = levels[(int)UpgradeType.Glass];
            int g = GlassModel(gl);
            c.glassRadius = glassRadius[g];
            c.glassHeight = glassHeight[g];
            // Aynı bardakta her kademe biraz daha sönümlü (kapak contası, tutuş...). Son bardakta 20. kademe de
            // sayılır (eskiden gl % 4 ile 19→20'de sönüm 0.56'dan 0.50'ye düşüyordu).
            c.sloshDamping = glassDamping[g] + 0.02f * (gl - g * StageSize);
            c.glassGold = gl >= Max;
            c.milkHeadroom = glassHeadroom[g] + 0.001f * (gl - g * StageSize);
            c.glassModel = g;
            c.glassReflex = 0.4f + 0.35f * gl / Max;   // iyi bardak = daha kolay dengelenir

            int r = levels[(int)UpgradeType.Rocket];
            // Sürtünmeli pistte roket belirleyici (kullanıcı kararı): 1. seviye ~+3 m/s, 8. ~+7.5, 16. ~+14, 20. ~+18 m/s.
            c.rocketThrust = r == 0 ? 0f : RocketThrust(r);
            c.rocketBurn = r == 0 ? 0f : RocketBurn(r);
            c.rocketCharges = r == 0 ? 0 : 1;   // her koşuda tek ateşleme (kullanıcı kararı); seviye itişi ve süreyi artırır
            c.slingStage = Stage(sl);
            c.runnerStage = Stage(ru);
            c.rocketStage = Stage(r);
            AvatarLibrary.ApplyPerk(perk, c);
            return c;
        }
    }
}
