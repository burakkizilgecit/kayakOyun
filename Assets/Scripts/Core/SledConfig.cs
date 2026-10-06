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
        public float dragArea = 0.02f;         // Cd·A (m²), hava direnci (oyun için gerçekten düşük: hız korunur)
        public float groundFriction = 0.138f;  // kinetik sürtünme katsayısı
        public float crashImpact = 12f;        // m/s, bundan sert iniş = kaza

        // Sapan (yay): ½·k·x² enerji depolar
        public float bandStiffness = 134f;     // N/m
        public float maxStretch = 6f;          // m

        // Kanat
        public float wingArea = 0f;            // m²
        public float wingMass = 0f;            // kg

        // Bardak (silindir)
        public float glassRadius = 0.035f;     // m
        public float glassHeight = 0.12f;      // m
        public float initialFill = 0.075f;     // m, başlangıç sıvı yüksekliği
        public float sloshDamping = 0.12f;     // çalkalanma sönümü (ζ)
        public int glassModel;                 // görsel: hangi bardak (Upgrades.GlassNames)
        public float lidDistance = 100f;       // m, kapak bu mesafede açılır (fırlatma sarsıntısında dökülmesin)

        // Roket: koşuda butonla ateşlenir, rocketBurn saniye boyunca rocketThrust N iter.
        public float rocketThrust;             // N
        public float rocketBurn;               // s
        public int rocketCharges;              // koşu başına ateşleme hakkı

        // Görünüm: tamamlanmış aşamalar (0..3), kızakta boya/spoiler/renk değişimi için.
        public int runnerStage, rocketStage, slingStage;

        public float TotalMass => riderMass + wingMass;
    }
}
