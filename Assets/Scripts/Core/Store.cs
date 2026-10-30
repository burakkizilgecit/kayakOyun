using System.Collections.Generic;

namespace SlingRiders
{
    /// Gerçek parayla satılan ürünlerin fiyatları ve günlük fırsatlar. Fiyatlar mağazadan gelir: ödeme sistemi (Unity IAP /
    /// Google Play) bağlanınca SetPrice ile mağazanın yerel fiyat metni (ör. "₺264,99") yazılır; o zamana kadar aşağıdaki
    /// katalog yalnızca önizlemedir; satın alma düğmeleri kapalıdır. Arayüz fiyatı hep Store.Price(id) üzerinden alır.
    public static class Store
    {
        /// Remains false in test builds. An actual purchase integration must explicitly enable it after initialization.
        public static bool PurchasesAvailable { get; private set; }
        public static void SetPurchasesAvailable(bool available) => PurchasesAvailable = available;

        static readonly Dictionary<string, string> prices = new Dictionary<string, string>
        {
            { "noads_pack", "$4.99" }, { "noads", "$3.49" }, { "double_rocket", "$5.99" }, { "auto_x3", "$2.49" },
            { "gems_10", "$1.99" }, { "gems_30", "$4.99" }, { "gems_100", "$9.99" }, { "gems_150", "$13.99" }, { "gems_200", "$17.99" },
            { "gems_500", "$19.99" }, { "coins_7500", "$0.99" }, { "coins_20000", "$1.99" }, { "coins_50000", "$3.99" }, { "coins_100000", "$6.99" },
            { "coins_150000", "$8.99" }, { "coins_200000", "$9.99" },
            { "launches_20", "$0.99" }, { "launches_80", "$2.49" }, { "launches_200", "$4.99" },
            { "deal_auto_x3", "$1.99" }, { "deal_double_rocket", "$3.99" }, { "deal_noads_pack", "$3.49" }, { "deal_gems_100", "$6.99" },
            { "deal_starter", "$1.99" },
        };

        /// Mağazadan gelen yerel fiyat metni (ödeme sistemi bağlanınca çağrılır).
        public static void SetPrice(string productId, string localizedPrice) => prices[productId] = localizedPrice;

        public static string Price(string productId) => prices.TryGetValue(productId, out var p) ? p : "—";

        /// Günlük fırsat: her gün biri öne çıkar (oyun açılınca gösterilir), süresi oyun verisinden (SaveData.dealEndTicks).
        public struct Deal
        {
            public string productId;      // fiyat Store.Price(productId)
            public string regularId;      // indirimsiz ürün (üstü çizili "eski fiyat" bundan); boş olabilir
            public string title;          // kurdele başlığı
            public string headline;       // büyük metin; {…} arası vurgulanır (sarı)
            public string info;           // alttaki bilgi şeridi
            public string buttonCaption;  // satın alma düğmesinde fiyatın üstündeki satır
            public string art;            // illüstrasyon katmanı: Resources/UI/Offers/<art> (yoksa oyun içi çizim)
            public int hours;             // teklifin süresi (saat)
        }

        public static readonly Deal[] Deals =
        {
            new Deal { productId = "deal_auto_x3", regularId = "auto_x3", title = "AUTO X3 COINS", headline = "GET {X3} COINS\nWITHOUT WATCHING ADS\nFOR 12 HOURS",
                       info = "After 12 hours, activate X3 coins by watching a rewarded ad", buttonCaption = "12-HOUR BOOST", art = "coins", hours = 12 },
            new Deal { productId = "deal_gems_100", regularId = "gems_100", title = "GEM RUSH", headline = "{100} GEMS\nUNLOCK HEROES\nAND SKIP THE GRIND",
                       info = "Gems buy heroes, coins and upgrades", buttonCaption = "TODAY ONLY", art = "gems", hours = 24 },
            new Deal { productId = "deal_double_rocket", regularId = "double_rocket", title = "DOUBLE BOOST", headline = "FIRE YOUR ROCKET\n{TWICE} EVERY RUN\nFOREVER",
                       info = "A permanent upgrade for every track", buttonCaption = "PERMANENT", art = "rocket", hours = 24 },
            new Deal { productId = "deal_noads_pack", regularId = "noads_pack", title = "NO ADS PACK", headline = "{NO} FORCED ADS\n+ 30 GEMS",
                       info = "Rewarded ads stay available", buttonCaption = "ONE-TIME OFFER", art = "noads", hours = 24 },
            new Deal { productId = "deal_starter", regularId = "", title = "STARTER PACK", headline = "{50} GEMS\n+ COINS + 20 LAUNCHES",
                       info = "A flying start for new heroes", buttonCaption = "BEST VALUE", art = "starter", hours = 24 },
        };

        /// Günün fırsatı: gün numarasına göre sırayla döner; yeni oyuncuya (ilk parkur) ilk gün başlangıç paketi.
        public static int DealOfDay(int day, int unlocked) => unlocked == 0 && day % 3 == 0 ? Deals.Length - 1 : day % (Deals.Length - 1);
    }
}
