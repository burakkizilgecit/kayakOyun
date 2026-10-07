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
            "Daha az sürtünme · 4. seviyede kanat",
            "Her seviye yeni bardak: süt daha zor dökülür",
            "Her atışta daha çok coin",
            "Koşuda bir kez dokun: güçlü itiş · her seviye daha güçlü",
        };

        /// Kızağın ana seviyelerinde gelen görünüm (seviye bitince).
        public static readonly string[] RunnerStageNames = { "Tutma ipi", "Gümüş kayaklar", "Arka tampon kanadı", "Küçük kanatlar", "Büyük kanatlar" };

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
        // Zemin sürtünmesi (patika; çimen x1.5, buz x0.25). 2026-10-06: kullanıcı isteğiyle tümü yüzde 20 artırıldı.
        static readonly float[] runnerFriction =
            { 0.1656f, 0.144f, 0.1248f, 0.108f, 0.0936f, 0.0816f, 0.0708f, 0.0624f, 0.0552f, 0.0528f, 0.0504f, 0.048f, 0.0456f,
              0.0432f, 0.0408f, 0.0384f, 0.036f, 0.0336f, 0.0312f, 0.0288f, 0.0264f };
        // Kanat: kızağın 4. seviyesinde (kademe 13-16) küçük, 5.'de (17-20) büyük.
        static readonly float[] wingAreas =
            { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0.7f, 1.1f, 1.6f, 2f, 2.4f, 2.8f, 3.2f, 3.6f };
        static readonly float[] wingMasses =
            { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 4f, 4f, 3.5f, 3f, 2.6f, 2.2f, 1.8f, 1.5f };

        public static readonly string[] GlassNames = { "Plastik Bardak", "Kupa", "Termos", "Pipetli Şişe", "Uzay Bardağı" };
        // Her yeni bardak daha yüksek (ağza daha çok pay) ve daha sönümlü (çalkantı çabuk söner).
        static readonly float[] glassRadius = { 0.035f, 0.036f, 0.034f, 0.031f, 0.03f };
        static readonly float[] glassHeight = { 0.12f, 0.155f, 0.18f, 0.21f, 0.24f };
        static readonly float[] glassDamping = { 0.12f, 0.24f, 0.32f, 0.4f, 0.5f };

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

        /// Coin yetmediğinde kademe elmasla da alınabilir: 2 elmas + kademenin ait olduğu parkur.
        public static int GemCost(int type, int level) => 2 + Mathf.Max(0, UnlockTier(type, level + 1));

        public static float IncomeMultiplier(int level) => 1f + 0.125f * level;

        /// Tamamlanmış ana seviye sayısı (0..5): kızaktaki görünümü belirler.
        public static int Stage(int level) => level / StageSize;

        /// Bardak modeli: her ana seviye yeni bir bardak.
        public static int GlassModel(int level) => Mathf.Min(level / StageSize, GlassNames.Length - 1);

        public static SledConfig BuildConfig(int[] levels, Perk perk = Perk.None)
        {
            var c = new SledConfig();
            int sl = levels[(int)UpgradeType.Slingshot], ru = levels[(int)UpgradeType.Runners];
            c.bandStiffness *= slingFactor[sl];
            c.groundFriction = runnerFriction[ru];
            c.wingArea = wingAreas[ru];
            c.wingMass = wingMasses[ru];

            int gl = levels[(int)UpgradeType.Glass];
            int g = GlassModel(gl);
            c.glassRadius = glassRadius[g];
            c.glassHeight = glassHeight[g];
            // Aynı bardakta her kademe biraz daha sönümlü (kapak contası, tutuş...)
            c.sloshDamping = glassDamping[g] + 0.02f * (gl % StageSize);
            c.glassModel = g;
            c.glassReflex = 0.4f + 0.35f * gl / Max;   // iyi bardak = daha kolay dengelenir

            int r = levels[(int)UpgradeType.Rocket];
            c.rocketThrust = r == 0 ? 0f : 80f + 12f * r;
            c.rocketBurn = r == 0 ? 0f : 0.6f + 0.035f * r;
            c.rocketCharges = r == 0 ? 0 : 1;   // her koşuda tek ateşleme (kullanıcı kararı); seviye itişi ve süreyi artırır
            c.slingStage = Stage(sl);
            c.runnerStage = Stage(ru);
            c.rocketStage = Stage(r);
            AvatarLibrary.ApplyPerk(perk, c);
            return c;
        }
    }
}
