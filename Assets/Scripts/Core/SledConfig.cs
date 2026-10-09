using System;

namespace SledSurfers
{
    /// Bir atış için kızak, sapan ve bardağın fiziksel değerleri.
    /// Yükseltmeler bu değerleri değiştirir (bkz. Upgrades.BuildConfig).
    [Serializable]
    public class SledConfig
    {
        // Kızak + sürücü
        public float riderMass = 40f;          // kg
        /// Hava direnci Cd·A (m²): hız karesiyle büyür, hızlı kızak enerji kaybetmeden taşınamaz (kullanıcı: "enerji
        /// kaybı 0 gibi davranamayız"). 2026-10-08: 0.18 → 0.08. Güçlü direnç fırlatma hızını ilk saniyelerde yakıyordu;
        /// kullanıcı kararı "fırlatma hızı, roket ve rota belirleyici" + Sled Surfers gibi sürtünme ağırlıklı düzgün yavaşlama.
        public static float DefaultDragArea = 0.08f;
        public float dragArea = DefaultDragArea;
        public float groundFriction = 0.138f;  // kinetik sürtünme katsayısı
        public float crashImpact = 12f;        // m/s, bundan sert iniş = kaza

        // Sapan (yay): ½·k·x² enerji depolar
        // 2026-10-08 (kullanıcı: fırlatma yavaş ivmeleniyor gibi): lastik 6 m yerine 3 m gerilir, 4 kat serttir.
        // Depolanan enerji aynı (½·134·6² = ½·536·3²): çıkış hızı aynı kalır, itiş iki kat kısa sürer (1. seviye
        // ~0.43 s, 20. seviye ~0.14 s), sapan gerçekteki gibi şaklar.
        public const float SlingStretch = 3f;
        public float bandStiffness = 536f;     // N/m
        public float maxStretch = SlingStretch;   // m

        // Kanat
        public float wingArea = 0f;            // m²
        public float propThrust;               // kalıcı pervane itişi (m/s², hızla açılır)
        public int parts;                      // açılan kalıcı parça sayısı (görünüş)
        public float wingSpan = 0f;            // m, görünen kanadın bir tarafının boyu (yalnız görünüş)
        public float wingMass = 0f;            // kg
        public float glideTime = 0f;           // s: kanadın bir kalkışta taşıdığı süre (kızak seviyesiyle uzar)

        // Bardak (silindir)
        public float glassRadius = 0.035f;     // m
        public float glassHeight = 0.12f;      // m
        /// Bardak ağzına kalan boşluk (m). Yol haritası aşama 2: bardak ağzına kadar dolu başlar, süt gerçekten taşabilir;
        /// iyi bardak daha çok boşluk bırakır (Upgrades.glassHeadroom). Eskiden hep 7.5 cm doldurulurdu, ilk bardakta
        /// bile 4.5 cm boşluk kalıyor, süt hiç dökülmüyordu.
        public float milkHeadroom = 0.019f;
        public float InitialFill => UnityEngine.Mathf.Max(glassHeight - milkHeadroom, 0.02f);   // m, başlangıç sıvı yüksekliği
        public float sloshDamping = 0.12f;     // çalkalanma sönümü (ζ)
        public float glassReflex = 0.4f;       // karakterin savrulmayı kendiliğinden dengeleme oranı (0..1)
        public int glassModel;                 // görsel: hangi bardak (Upgrades.GlassNames)
        public int glassStage;                 // bardak aşaması (0..6): kristal / altın sürüm ve renk
        public VehicleKind vehicle;            // pistin aracı (görünüş)
        public bool glassGold;                 // görsel: 20. kademe, son bardağın altın sürümü
        public float lidDistance = 15f;        // m, kapak bu mesafede açılır: yalnızca sapan itişini korur (eskiden 100 m)

        // Roket: koşuda butonla ateşlenir, rocketBurn saniye boyunca rocketThrust N iter.
        public float rocketThrust;             // N
        public float rocketBurn;               // s
        public int rocketCharges;              // koşu başına ateşleme hakkı

        // Görünüm: tamamlanmış aşamalar (0..3), kızakta boya/spoiler/renk değişimi için.
        public int runnerStage, rocketStage, slingStage;

        public float TotalMass => riderMass + wingMass;
    }
}
