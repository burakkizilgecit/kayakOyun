using System.Collections.Generic;
using UnityEngine;

namespace SlingRiders
{
    /// Parkurun renkleri, ışığı, gökyüzü ve bitki örtüsü.
    public class TrackTheme
    {
        public enum Landscape { Hills, Canyon, Dunes, Craters }
        public Landscape landscape;
        // Gökyüzü ve ışık
        public Color horizon, skyTint, skyGround, sunColor, ambientSky, ambientEquator, ambientGround;
        public Color skyTop = new Color(0.38f, 0.62f, 0.98f);   // stilize gökyüzünün tepe rengi
        public Vector3 sunEuler;
        public float exposure = 1.3f, atmosphere = 1f, sunIntensity = 1f, fogStart = 70f, fogEnd = 320f;

        // Renk ayarı (GradeEffect)
        public float contrast = 1.08f, saturation = 1.16f, vignette = 0.24f;
        public Color gradeTint = Color.white;

        // Zemin
        public Color grass, grassB, rock, trackA, trackB, kerb, water;
        public float hillHeight = 14f, mountainHeight = 45f, terrace;
        /// Karlı tema: zemin kar dokusu, kamera önünde kar yağışı.
        public bool snowy;

        // Yüzeylerin görünüşü (fizik aynı): çamur = bataklık kumu / kül / kızıl toz, buz = tuz / obsidyen / Mars buzu.
        public Color mudA = new Color(0.37f, 0.25f, 0.15f), mudB = new Color(0.27f, 0.17f, 0.09f), mudSpot = new Color(0.2f, 0.12f, 0.06f);
        public Color iceA = new Color(0.74f, 0.91f, 1f), iceB = new Color(0.56f, 0.81f, 0.97f), iceC = new Color(0.42f, 0.7f, 0.93f);
        public Color frost = Color.clear;   // şeffaf: varsayılan (karlı pistte beyaz, değilse açık buz mavisi)
        public Color puddleShallow = new Color(0.42f, 0.62f, 0.7f, 0.85f), puddleDeep = new Color(0.18f, 0.34f, 0.48f, 0.9f);
        /// Hazır kaya/bitki modellerinin renk tonu (Volkan koyu, Mars kızıl).
        public Color propTint = Color.white;
        /// Köprü altındaki "su" lav: parlayan turuncu.
        public bool lava;

        public Texture2D GroundTexture() => snowy ? TextureKit.Snow() : TextureKit.Grass();

        // Bitki örtüsü: pistin yanı (yakın), orta bant (ağaçlar), uzak bant
        public string[] near, mid, far;
        public Vector2 nearHeight, midHeight, farHeight;
        public float nearStep = 1.3f, nearChance = 0.6f, midStep = 6f, farStep = 12f;

        static Material skyTemplate, runtimeSky;

        public static TrackTheme Get(int index)
        {
            switch (index)
            {
                case 1:   // Orman: sıcak ikindi ışığı, çamlar
                    return new TrackTheme
                    {
                        horizon = new Color(0.78f, 0.86f, 0.8f), skyTint = new Color(0.58f, 0.74f, 0.82f), skyGround = new Color(0.3f, 0.42f, 0.3f),
                        exposure = 1.4f, sunColor = new Color(1f, 0.86f, 0.66f), sunEuler = new Vector3(32f, -60f, 0f), sunIntensity = 1.05f,
                        // Gölgeler daha açık (aşama 6): koyu gölgede engel ve su okunmuyordu.
                        ambientSky = new Color(0.63f, 0.73f, 0.75f), ambientEquator = new Color(0.54f, 0.59f, 0.5f), ambientGround = new Color(0.33f, 0.38f, 0.29f),
                        skyTop = new Color(0.4f, 0.6f, 0.78f), fogEnd = 280f, contrast = 1.1f, saturation = 1.1f, vignette = 0.3f, gradeTint = new Color(0.98f, 1f, 0.97f),
                        grass = new Color(0.27f, 0.52f, 0.27f), grassB = new Color(0.22f, 0.45f, 0.24f), rock = new Color(0.47f, 0.47f, 0.5f),
                        trackA = new Color(0.66f, 0.47f, 0.31f), trackB = new Color(0.58f, 0.41f, 0.27f), kerb = new Color(0.98f, 0.72f, 0.15f),
                        water = new Color(0.2f, 0.5f, 0.55f, 0.8f), hillHeight = 20f, mountainHeight = 60f,
                        near = new[] { "Nature/mushroom_red", "Nature/mushroom_redGroup", "Nature/mushroom_tanGroup", "Nature/grass_large", "Nature/grass",
                                       "Nature/plant_bushSmall", "Nature/stump_round", "Nature/log", "Nature/rock_smallA", "Nature/plant_bush" },
                        mid = new[] { "Nature/tree_pineDefaultA", "Nature/tree_pineDefaultB", "Nature/tree_pineRoundA", "Nature/tree_pineRoundC",
                                      "Nature/tree_pineRoundE", "Nature/tree_pineTallA", "Nature/tree_pineTallB", "Nature/tree_pineTallC",
                                      "Nature/tree_pineSmallA", "Nature/tree_pineSmallB", "Nature/log_large", "Nature/stump_old" },
                        far = new[] { "Nature/tree_pineTallA", "Nature/tree_pineTallB", "Nature/tree_pineTallC", "Nature/tree_pineRoundA" },
                        nearHeight = new Vector2(0.4f, 1.1f), midHeight = new Vector2(6f, 13f), farHeight = new Vector2(10f, 18f),
                        midStep = 4.5f,
                    };
                case 2:   // Kanyon: gün batımı, kızıl kayalar, kaktüsler
                    return new TrackTheme
                    {
                        horizon = new Color(1f, 0.8f, 0.62f), skyTint = new Color(0.8f, 0.52f, 0.42f), skyGround = new Color(0.6f, 0.38f, 0.28f),
                        exposure = 1.2f, atmosphere = 1.6f, sunColor = new Color(1f, 0.72f, 0.46f), sunEuler = new Vector3(16f, 160f, 0f), sunIntensity = 1.2f,
                        ambientSky = new Color(0.78f, 0.66f, 0.62f), ambientEquator = new Color(0.7f, 0.52f, 0.42f), ambientGround = new Color(0.42f, 0.3f, 0.22f),
                        skyTop = new Color(0.36f, 0.44f, 0.8f), fogStart = 110f, fogEnd = 460f, contrast = 1.08f, saturation = 1.06f, vignette = 0.26f, gradeTint = new Color(1.01f, 0.99f, 0.96f),
                        // Görsel analiz (2026-10-10): zemin ve yol aynı turuncuydu: zemin kızıl, yol açık krem.
                        grass = new Color(0.8f, 0.5f, 0.33f), grassB = new Color(0.74f, 0.44f, 0.29f), rock = new Color(0.7f, 0.36f, 0.24f),
                        trackA = new Color(0.97f, 0.84f, 0.64f), trackB = new Color(0.9f, 0.76f, 0.56f), kerb = new Color(0.16f, 0.62f, 0.64f),
                        water = new Color(0.25f, 0.62f, 0.7f, 0.82f), hillHeight = 10f, mountainHeight = 75f, terrace = 7f, landscape = Landscape.Canyon,
                        near = new[] { "Nature/rock_smallA", "Nature/rock_smallB", "Nature/rock_smallC", "Nature/cactus_short", "Nature/grass_leafs",
                                       "Nature/plant_bushSmall" },
                        mid = new[] { "Nature/cactus_tall", "Nature/cactus_short", "Nature/rock_tallA", "Nature/rock_tallB", "Nature/rock_tallC",
                                      "Nature/rock_largeA", "Nature/rock_largeC", "Nature/tree_palmTall", "Nature/cactus_tall" },
                        far = new[] { "Nature/rock_tallD", "Nature/rock_tallE", "Nature/rock_tallF", "Nature/rock_tallA", "Nature/rock_largeD" },
                        nearHeight = new Vector2(0.4f, 1.3f), midHeight = new Vector2(3f, 9f), farHeight = new Vector2(18f, 45f),
                        nearChance = 0.4f, midStep = 9f, farStep = 18f,
                    };
                case 3:   // Karlı Dağ: berrak kış sabahı, karlı zemin, çamlar ve gri kayalar, yüksek dağlar
                    return new TrackTheme
                    {
                        horizon = new Color(0.86f, 0.91f, 0.97f), skyTint = new Color(0.6f, 0.74f, 0.95f), skyGround = new Color(0.82f, 0.86f, 0.92f),
                        exposure = 1.12f, sunColor = new Color(1f, 0.96f, 0.9f), sunEuler = new Vector3(30f, -40f, 0f), sunIntensity = 0.78f,
                        ambientSky = new Color(0.59f, 0.67f, 0.8f), ambientEquator = new Color(0.5f, 0.58f, 0.7f), ambientGround = new Color(0.35f, 0.42f, 0.53f),
                        skyTop = new Color(0.36f, 0.56f, 0.9f), fogStart = 75f, fogEnd = 320f, contrast = 1.02f, saturation = 1.02f, vignette = 0.22f,
                        gradeTint = new Color(0.97f, 0.99f, 1f),
                        grass = new Color(0.8f, 0.86f, 0.94f), grassB = new Color(0.7f, 0.79f, 0.89f), rock = new Color(0.36f, 0.42f, 0.51f),
                        trackA = new Color(0.6f, 0.71f, 0.84f), trackB = new Color(0.55f, 0.67f, 0.81f), kerb = new Color(0.2f, 0.5f, 0.9f),
                        water = new Color(0.35f, 0.66f, 0.85f, 0.8f), hillHeight = 22f, mountainHeight = 90f, snowy = true,
                        near = new[] { "Nature/stone_smallA", "Nature/stone_smallB", "Nature/stone_smallC", "Nature/tree_pineSmallA",
                                       "Nature/tree_pineSmallB" },
                        mid = new[] { "Nature/tree_pineDefaultA", "Nature/tree_pineDefaultB", "Nature/tree_pineRoundA", "Nature/tree_pineRoundC",
                                      "Nature/tree_pineRoundE", "Nature/tree_pineTallA", "Nature/tree_pineTallB", "Nature/tree_pineTallC",
                                      "Nature/stone_largeA", "Nature/stone_largeB", "Nature/stone_tallA" },
                        far = new[] { "Nature/tree_pineTallA", "Nature/tree_pineTallB", "Nature/tree_pineTallC", "Nature/rock_tallB", "Nature/rock_tallD" },
                        nearHeight = new Vector2(0.4f, 1.2f), midHeight = new Vector2(5f, 12f), farHeight = new Vector2(10f, 20f),
                        nearChance = 0.35f, midStep = 5.5f,
                    };
                case 4:   // Çöl Kumulları: sıcak öğle, kum tepeleri, kaktüs ve palmiyeler
                    return new TrackTheme
                    {
                        // Görsel analiz (2026-10-10): her şey aynı soluk sarıydı. Gök mavi, ufuk açık mavi-krem, parlaklık düşük;
                        // zemin sıcak kum, yol ondan belirgin koyu sıkışmış toprak.
                        horizon = new Color(0.86f, 0.9f, 0.93f), skyTint = new Color(0.6f, 0.72f, 0.95f), skyGround = new Color(0.8f, 0.68f, 0.5f),
                        skyTop = new Color(0.22f, 0.5f, 0.92f), exposure = 1.15f, sunColor = new Color(1f, 0.94f, 0.82f), sunEuler = new Vector3(55f, -30f, 0f),
                        sunIntensity = 1f, ambientSky = new Color(0.66f, 0.72f, 0.82f), ambientEquator = new Color(0.66f, 0.58f, 0.46f),
                        ambientGround = new Color(0.44f, 0.36f, 0.26f), fogStart = 110f, fogEnd = 460f, contrast = 1.08f, saturation = 1.05f,
                        gradeTint = new Color(1.01f, 1f, 0.98f),
                        grass = new Color(0.9f, 0.72f, 0.44f), grassB = new Color(0.84f, 0.65f, 0.38f), rock = new Color(0.72f, 0.5f, 0.34f),
                        trackA = new Color(0.72f, 0.52f, 0.33f), trackB = new Color(0.66f, 0.47f, 0.3f), kerb = new Color(0.2f, 0.6f, 0.7f),
                        water = new Color(0.2f, 0.7f, 0.75f, 0.85f), hillHeight = 22f, mountainHeight = 30f, landscape = Landscape.Dunes,
                        mudA = new Color(0.78f, 0.6f, 0.38f), mudB = new Color(0.66f, 0.5f, 0.3f), mudSpot = new Color(0.55f, 0.4f, 0.24f),
                        iceA = new Color(0.98f, 0.96f, 0.91f), iceB = new Color(0.95f, 0.93f, 0.87f), iceC = new Color(0.91f, 0.89f, 0.83f),
                        frost = new Color(1f, 0.98f, 0.94f),
                        puddleShallow = new Color(0.3f, 0.75f, 0.75f, 0.85f), puddleDeep = new Color(0.1f, 0.45f, 0.55f, 0.9f),
                        near = new[] { "Nature/cactus_short", "Nature/rock_smallA", "Nature/rock_smallB", "Nature/rock_smallC", "Nature/plant_bushSmall",
                                       "Nature/grass_leafs" },
                        mid = new[] { "Nature/cactus_tall", "Nature/cactus_short", "Nature/tree_palmTall", "Nature/tree_palmBend", "Nature/rock_largeA",
                                      "Nature/rock_tallA", "Nature/cactus_tall" },
                        far = new[] { "Nature/rock_largeD", "Nature/rock_largeA", "Nature/tree_palmTall" },
                        nearHeight = new Vector2(0.4f, 1.2f), midHeight = new Vector2(3f, 8f), farHeight = new Vector2(7f, 12f),
                        nearChance = 0.35f, midStep = 9f, farStep = 28f,
                    };
                case 5:   // Volkan: dumanlı kızıl gökyüzü, bazalt zemin, lav nehirleri, koyu kayalar
                    return new TrackTheme
                    {
                        horizon = new Color(0.55f, 0.35f, 0.3f), skyTint = new Color(0.4f, 0.25f, 0.22f), skyGround = new Color(0.2f, 0.12f, 0.1f),
                        skyTop = new Color(0.22f, 0.15f, 0.2f), exposure = 1.3f, sunColor = new Color(1f, 0.68f, 0.45f), sunEuler = new Vector3(38f, 140f, 0f),
                        sunIntensity = 1.25f, ambientSky = new Color(0.75f, 0.58f, 0.54f), ambientEquator = new Color(0.64f, 0.48f, 0.43f),
                        ambientGround = new Color(0.4f, 0.26f, 0.2f), fogStart = 60f, fogEnd = 300f, contrast = 1.1f, saturation = 1.1f,
                        vignette = 0.32f, gradeTint = new Color(1.05f, 0.97f, 0.94f),
                        grass = new Color(0.36f, 0.31f, 0.3f), grassB = new Color(0.3f, 0.26f, 0.26f), rock = new Color(0.34f, 0.28f, 0.27f),
                        trackA = new Color(0.55f, 0.45f, 0.4f), trackB = new Color(0.49f, 0.4f, 0.36f), kerb = new Color(1f, 0.4f, 0.1f),
                        water = new Color(1f, 0.35f, 0.05f, 1f), lava = true, hillHeight = 16f, mountainHeight = 110f,
                        mudA = new Color(0.45f, 0.45f, 0.47f), mudB = new Color(0.35f, 0.35f, 0.37f), mudSpot = new Color(0.27f, 0.27f, 0.29f),
                        iceA = new Color(0.24f, 0.22f, 0.3f), iceB = new Color(0.15f, 0.13f, 0.21f), iceC = new Color(0.08f, 0.07f, 0.12f),
                        frost = new Color(0.55f, 0.38f, 0.62f), propTint = new Color(0.45f, 0.38f, 0.38f),
                        near = new[] { "Nature/rock_smallA", "Nature/rock_smallB", "Nature/rock_smallC", "Nature/stone_smallA", "Nature/stone_smallB",
                                       "Nature/stump_old" },
                        mid = new[] { "Nature/rock_tallA", "Nature/rock_tallB", "Nature/rock_tallC", "Nature/rock_largeA", "Nature/rock_largeD",
                                      "Nature/stone_tallA", "Nature/stone_tallB", "Nature/stump_round" },
                        far = new[] { "Nature/rock_tallD", "Nature/rock_tallE", "Nature/rock_tallF" },
                        nearHeight = new Vector2(0.4f, 1.2f), midHeight = new Vector2(3f, 9f), farHeight = new Vector2(18f, 45f),
                        nearChance = 0.4f, midStep = 8f, farStep = 16f,
                    };
                case 6:   // Mars: kızıl toz, soluk pembe gökyüzü, kızıl kayalar, buzlu kraterler
                    return new TrackTheme
                    {
                        horizon = new Color(0.92f, 0.66f, 0.52f), skyTint = new Color(0.8f, 0.55f, 0.42f), skyGround = new Color(0.6f, 0.35f, 0.25f),
                        skyTop = new Color(0.62f, 0.44f, 0.4f), exposure = 1.25f, sunColor = new Color(1f, 0.9f, 0.78f), sunEuler = new Vector3(35f, -50f, 0f),
                        sunIntensity = 1f, ambientSky = new Color(0.78f, 0.58f, 0.5f), ambientEquator = new Color(0.68f, 0.48f, 0.4f),
                        ambientGround = new Color(0.42f, 0.27f, 0.22f), fogStart = 80f, fogEnd = 380f, contrast = 1.06f, saturation = 1.08f,
                        gradeTint = new Color(1.04f, 0.98f, 0.95f),
                        grass = new Color(0.74f, 0.4f, 0.24f), grassB = new Color(0.68f, 0.36f, 0.21f), rock = new Color(0.56f, 0.3f, 0.2f),
                        trackA = new Color(0.84f, 0.54f, 0.36f), trackB = new Color(0.78f, 0.49f, 0.32f), kerb = new Color(0.95f, 0.95f, 1f),
                        water = new Color(0.35f, 0.18f, 0.12f, 1f), hillHeight = 8f, mountainHeight = 30f, landscape = Landscape.Craters,
                        mudA = new Color(0.6f, 0.3f, 0.18f), mudB = new Color(0.5f, 0.24f, 0.14f), mudSpot = new Color(0.42f, 0.2f, 0.12f),
                        iceA = new Color(0.92f, 0.96f, 1f), iceB = new Color(0.82f, 0.9f, 0.98f), iceC = new Color(0.72f, 0.84f, 0.96f),
                        frost = new Color(0.95f, 0.95f, 1f), propTint = new Color(0.9f, 0.55f, 0.42f),
                        near = new[] { "Nature/rock_smallA", "Nature/rock_smallB", "Nature/rock_smallC", "Nature/stone_smallA", "Nature/stone_smallC" },
                        mid = new[] { "Nature/rock_largeA", "Nature/rock_largeB", "Nature/rock_largeE", "Nature/rock_tallA", "Nature/rock_tallC",
                                      "Nature/stone_tallC" },
                        far = new[] { "Nature/rock_largeD", "Nature/rock_largeF", "Nature/stone_largeA" },
                        nearHeight = new Vector2(0.3f, 1f), midHeight = new Vector2(3f, 7f), farHeight = new Vector2(5f, 11f),
                        nearChance = 0.25f, midStep = 14f, farStep = 30f,
                    };
                case 7:   // Canyon Run: Sunset Canyon'un kayaları, daha yüksek güneş (dar boğaz karanlık kalmasın), kısa sis
                {
                    var g = Get(2);
                    g.sunEuler = new Vector3(48f, 150f, 0f);
                    g.sunIntensity = 1.3f;
                    g.fogStart = 40f;
                    g.fogEnd = 220f;
                    g.rock = new Color(0.66f, 0.38f, 0.26f);
                    return g;
                }
                case 8:   // Frozen Lake: kuzey ışıklı gece, donmuş göl, seyrek çamlar
                    return new TrackTheme
                    {
                        horizon = new Color(0.36f, 0.62f, 0.72f), skyTint = new Color(0.2f, 0.36f, 0.6f), skyGround = new Color(0.6f, 0.72f, 0.86f),
                        exposure = 1.05f, sunColor = new Color(0.72f, 0.86f, 1f), sunEuler = new Vector3(28f, 30f, 0f), sunIntensity = 0.62f,
                        ambientSky = new Color(0.46f, 0.58f, 0.8f), ambientEquator = new Color(0.4f, 0.52f, 0.68f), ambientGround = new Color(0.3f, 0.37f, 0.5f),
                        skyTop = new Color(0.07f, 0.11f, 0.3f), fogStart = 60f, fogEnd = 300f, contrast = 1.06f, saturation = 1.12f, vignette = 0.32f,
                        gradeTint = new Color(0.94f, 1f, 1.06f),
                        grass = new Color(0.74f, 0.85f, 0.95f), grassB = new Color(0.64f, 0.78f, 0.92f), rock = new Color(0.3f, 0.36f, 0.47f),
                        trackA = new Color(0.58f, 0.74f, 0.92f), trackB = new Color(0.52f, 0.69f, 0.89f), kerb = new Color(0.3f, 0.9f, 0.8f),
                        water = new Color(0.08f, 0.22f, 0.38f, 0.92f), hillHeight = 16f, mountainHeight = 85f, snowy = true,
                        near = new[] { "Nature/stone_smallA", "Nature/stone_smallB", "Nature/stone_smallC" },
                        mid = new[] { "Nature/tree_pineTallA", "Nature/tree_pineTallB", "Nature/tree_pineRoundA", "Nature/stone_largeA", "Nature/stone_tallA" },
                        far = new[] { "Nature/tree_pineTallA", "Nature/tree_pineTallB", "Nature/tree_pineTallC", "Nature/rock_tallB" },
                        nearHeight = new Vector2(0.3f, 0.9f), midHeight = new Vector2(5f, 11f), farHeight = new Vector2(10f, 20f),
                        nearChance = 0.2f, midStep = 9f,
                    };
                case 9:   // Mushroom Grove: pembe-mor masal gökyüzü, ağaç boyunda mantarlar, çiçekler
                    return new TrackTheme
                    {
                        horizon = new Color(1f, 0.82f, 0.9f), skyTint = new Color(0.82f, 0.62f, 0.92f), skyGround = new Color(0.5f, 0.6f, 0.45f),
                        exposure = 1.3f, sunColor = new Color(1f, 0.88f, 0.95f), sunEuler = new Vector3(36f, -50f, 0f), sunIntensity = 1f,
                        ambientSky = new Color(0.72f, 0.62f, 0.85f), ambientEquator = new Color(0.62f, 0.58f, 0.66f), ambientGround = new Color(0.36f, 0.4f, 0.32f),
                        skyTop = new Color(0.44f, 0.34f, 0.86f), fogStart = 60f, fogEnd = 280f, contrast = 1.08f, saturation = 1.2f, vignette = 0.26f,
                        gradeTint = new Color(1.03f, 0.98f, 1.04f),
                        grass = new Color(0.4f, 0.62f, 0.4f), grassB = new Color(0.34f, 0.54f, 0.4f), rock = new Color(0.52f, 0.46f, 0.6f),
                        trackA = new Color(0.74f, 0.58f, 0.5f), trackB = new Color(0.66f, 0.51f, 0.45f), kerb = new Color(1f, 0.45f, 0.75f),
                        water = new Color(0.3f, 0.5f, 0.7f, 0.8f), hillHeight = 18f, mountainHeight = 55f,
                        near = new[] { "Nature/mushroom_red", "Nature/mushroom_tan", "Nature/flower_purpleA", "Nature/flower_purpleB", "Nature/flower_redA",
                                       "Nature/grass_large", "Nature/mushroom_redGroup" },
                        mid = new[] { "Nature/mushroom_redGroup", "Nature/mushroom_tanGroup", "Nature/mushroom_red", "Nature/mushroom_tan", "Nature/tree_fat",
                                      "Nature/mushroom_redGroup" },
                        far = new[] { "Nature/mushroom_redGroup", "Nature/mushroom_tanGroup", "Nature/tree_fat", "Nature/tree_tall" },
                        nearHeight = new Vector2(0.4f, 1.3f), midHeight = new Vector2(4f, 11f), farHeight = new Vector2(8f, 18f),
                        midStep = 5.5f,
                    };
                case 10:  // Jungle Temple: nemli yağmur ormanı, palmiyeler, iri yapraklar, tapınak taşları
                    return new TrackTheme
                    {
                        horizon = new Color(0.76f, 0.88f, 0.8f), skyTint = new Color(0.55f, 0.76f, 0.7f), skyGround = new Color(0.3f, 0.45f, 0.3f),
                        exposure = 1.25f, sunColor = new Color(1f, 0.95f, 0.8f), sunEuler = new Vector3(42f, 120f, 0f), sunIntensity = 1.05f,
                        ambientSky = new Color(0.6f, 0.74f, 0.68f), ambientEquator = new Color(0.5f, 0.6f, 0.46f), ambientGround = new Color(0.28f, 0.36f, 0.24f),
                        skyTop = new Color(0.3f, 0.55f, 0.68f), fogStart = 40f, fogEnd = 230f, contrast = 1.1f, saturation = 1.18f, vignette = 0.3f,
                        gradeTint = new Color(0.98f, 1.03f, 0.97f),
                        grass = new Color(0.24f, 0.5f, 0.22f), grassB = new Color(0.19f, 0.42f, 0.2f), rock = new Color(0.5f, 0.49f, 0.43f),
                        trackA = new Color(0.62f, 0.5f, 0.36f), trackB = new Color(0.55f, 0.44f, 0.31f), kerb = new Color(0.95f, 0.75f, 0.2f),
                        water = new Color(0.2f, 0.45f, 0.36f, 0.85f), hillHeight = 20f, mountainHeight = 60f,
                        near = new[] { "Nature/grass_leafsLarge", "Nature/grass_leafs", "Nature/plant_bushLarge", "Nature/flower_redA", "Nature/flower_yellowA" },
                        mid = new[] { "Nature/tree_palmTall", "Nature/tree_palmBend", "Nature/tree_detailed", "Nature/plant_bushLarge", "Nature/stone_tallA",
                                      "Nature/stone_tallB", "Nature/stone_tallC", "Nature/tree_palmTall" },
                        far = new[] { "Nature/tree_palmTall", "Nature/tree_tall", "Nature/tree_fat", "Nature/tree_palmBend" },
                        nearHeight = new Vector2(0.5f, 1.4f), midHeight = new Vector2(6f, 14f), farHeight = new Vector2(10f, 20f),
                        nearChance = 0.75f, midStep = 4.5f,
                    };
                default:  // Çayır: parlak öğle, meşeler, çiçekler
                    return new TrackTheme
                    {
                        horizon = new Color(0.72f, 0.87f, 1f), skyTint = new Color(0.62f, 0.76f, 1f), skyGround = new Color(0.45f, 0.6f, 0.4f),
                        exposure = 1.5f, atmosphere = 1.05f, sunColor = new Color(1f, 0.96f, 0.88f), sunEuler = new Vector3(50f, -35f, 0f), sunIntensity = 1.05f,
                        ambientSky = new Color(0.6f, 0.7f, 0.85f), ambientEquator = new Color(0.55f, 0.6f, 0.5f), ambientGround = new Color(0.3f, 0.34f, 0.26f),
                        grass = new Color(0.45f, 0.72f, 0.32f), grassB = new Color(0.39f, 0.64f, 0.29f), rock = new Color(0.58f, 0.54f, 0.5f),
                        trackA = new Color(0.78f, 0.57f, 0.36f), trackB = new Color(0.7f, 0.5f, 0.31f), kerb = new Color(0.93f, 0.3f, 0.22f),
                        water = new Color(0.25f, 0.6f, 0.92f, 0.78f),
                        near = new[] { "Nature/flower_purpleA", "Nature/flower_purpleB", "Nature/flower_redA", "Nature/flower_redB",
                                       "Nature/flower_yellowA", "Nature/flower_yellowB", "Nature/grass", "Nature/grass_large", "Nature/plant_bushSmall",
                                       "Nature/rock_smallA", "Nature/mushroom_red" },
                        mid = new[] { "Nature/tree_default", "Nature/tree_oak", "Nature/tree_detailed", "Nature/tree_fat", "Nature/tree_simple",
                                      "Nature/tree_tall", "Nature/tree_small", "Nature/tree_oak_fall", "Nature/plant_bushLarge", "Nature/plant_bushDetailed",
                                      "Nature/tree_default", "Nature/tree_oak" },
                        far = new[] { "Nature/tree_default", "Nature/tree_oak", "Nature/tree_tall", "Nature/tree_fat", "Nature/tree_default_fall" },
                        nearHeight = new Vector2(0.4f, 1f), midHeight = new Vector2(4f, 8.5f), farHeight = new Vector2(8f, 14f),
                    };
            }
        }

        /// Gökyüzü, sis, güneş ve ortam ışığını uygular.
        public void Apply(Camera cam, Light sun)
        {
            // Every track switch creates a sky instance; only release the instance we created.
            if (runtimeSky != null)
            {
                if (RenderSettings.skybox == runtimeSky) RenderSettings.skybox = null;
                Object.Destroy(runtimeSky);
                runtimeSky = null;
            }
            sun.transform.rotation = Quaternion.Euler(sunEuler);
            var skyShader = Resources.Load<Shader>("Shaders/Sky");
            if (skyShader != null && skyShader.isSupported)
            {
                // Stilize gökyüzü: ufuk sis rengiyle aynı, güneş yönünde yumuşak parıltı.
                var sky = new Material(skyShader);
                sky.SetColor("_Top", skyTop);
                sky.SetColor("_Horizon", horizon);
                sky.SetColor("_Ground", skyGround);
                sky.SetColor("_SunColor", sunColor);
                sky.SetVector("_SunDir", -sun.transform.forward);
                RenderSettings.skybox = runtimeSky = sky;
                RenderSettings.sun = sun;
                cam.clearFlags = CameraClearFlags.Skybox;
            }
            else if ((skyTemplate = skyTemplate != null ? skyTemplate : Resources.Load<Material>("Env/Sky")) != null)
            {
                var sky = new Material(skyTemplate);
                sky.SetColor("_SkyTint", skyTint);
                sky.SetColor("_GroundColor", skyGround);
                sky.SetFloat("_Exposure", exposure);
                sky.SetFloat("_AtmosphereThickness", atmosphere);
                sky.SetFloat("_SunSize", 0.06f);
                RenderSettings.skybox = runtimeSky = sky;
                RenderSettings.sun = sun;
                cam.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = horizon;
            }
            var grade = cam.GetComponent<GradeEffect>();
            if (grade != null)
            {
                grade.contrast = contrast;
                grade.saturation = saturation;
                grade.vignette = vignette;
                grade.tint = gradeTint;
            }
            RenderSettings.fogColor = horizon;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            sun.color = sunColor;
            sun.intensity = sunIntensity;
            sun.transform.rotation = Quaternion.Euler(sunEuler);
            Snowfall.Set(cam, snowy);
        }
    }

    /// Bir Kenney modelinin birleştirmeye hazır parçaları ve sınır kutusu (Resources/Env altından).
    public class PropModel
    {
        public struct Part
        {
            public Mesh mesh;
            public int sub;
            public Material mat;
            public Matrix4x4 local;
        }

        public readonly List<Part> parts = new List<Part>();
        public Bounds bounds;

        /// Model yatayda en çok X ekseni boyunca mı uzanıyor (kemer, çit gibi)?
        public bool LongAxisX => bounds.size.x >= bounds.size.z;

        static readonly Dictionary<string, PropModel> cache = new Dictionary<string, PropModel>();

        public static PropModel Get(string path)
        {
            if (cache.TryGetValue(path, out var m)) return m;
            var go = Resources.Load<GameObject>("Env/" + path);
            if (go == null)
            {
                Debug.LogWarning("[ENV] Model bulunamadı: " + path);
                cache[path] = null;
                return null;
            }
            m = new PropModel();
            bool first = true;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || mr == null) continue;
                var local = mf.transform.localToWorldMatrix;
                var mats = mr.sharedMaterials;
                for (int i = 0; i < mf.sharedMesh.subMeshCount; i++)
                    m.parts.Add(new Part { mesh = mf.sharedMesh, sub = i, mat = mats[Mathf.Min(i, mats.Length - 1)], local = local });
                var b = mf.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var corner = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                    var p = local.MultiplyPoint3x4(corner);
                    if (first) { m.bounds = new Bounds(p, Vector3.zero); first = false; }
                    else m.bounds.Encapsulate(p);
                }
            }
            if (first) m = null;
            cache[path] = m;
            return m;
        }
    }

    /// Çok sayıda model örneğini malzemeye ve pist bölgesine göre tek mesh'te birleştirir:
    /// binlerce ağaç birkaç düzine çizim çağrısıyla çizilir, uzak bölgeler kamera dışında kalınca atlanır.
    public class PropBatcher
    {
        const float ChunkLength = 150f;
        readonly Dictionary<(int, Material), List<CombineInstance>> groups = new Dictionary<(int, Material), List<CombineInstance>>();

        List<CombineInstance> Group(float z, Material mat)
        {
            var key = (Mathf.FloorToInt(z / ChunkLength), mat);
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<CombineInstance>();
            return list;
        }

        public void Add(PropModel m, Matrix4x4 world)
        {
            if (m == null) return;
            float z = world.m23;
            foreach (var p in m.parts)
                Group(z, p.mat).Add(new CombineInstance { mesh = p.mesh, subMeshIndex = p.sub, transform = world * p.local });
        }

        public void Add(Mesh mesh, Material mat, Matrix4x4 world) =>
            Group(world.m23, mat).Add(new CombineInstance { mesh = mesh, subMeshIndex = 0, transform = world });

        /// Hazır modellerin renk tonu (tema): beyaz değilse malzemenin tonlu kopyası kullanılır.
        public Color tint = Color.white;
        readonly Dictionary<Material, Material> tinted = new Dictionary<Material, Material>();

        Material Tinted(Material m, RuntimeResources resources)
        {
            if (tint == Color.white || m == null) return m;
            if (!tinted.TryGetValue(m, out var t))
            {
                t = resources.Own(new Material(m) { color = m.color * tint });
                tinted[m] = t;
            }
            return t;
        }

        public void Build(Transform parent)
        {
            var resources = RuntimeResources.For(parent);
            foreach (var kv in groups)
            {
                var mesh = resources.Own(new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 });
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                var go = new GameObject("Props " + kv.Key.Item1 + " " + kv.Key.Item2.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = Tinted(kv.Key.Item2, resources);
            }
            groups.Clear();
        }
    }

    /// Parkurun görsel sahnesini kurar: pist yüzeyi ve bordürler, yanlarda tepelere/dağlara yükselen
    /// köşeli arazi, dere ve kanyon diplerinde su, çitler, bitki örtüsü, engeller, kemerler, başlangıç alanı ve bulutlar.
    public class Scenery
    {
        // Arazi sütunları: yamacın tepesinden (BankTop) dışarı doğru uzaklıklar.
        static readonly float[] Cols = { -0.6f, 2f, 5f, 9f, 14f, 21f, 30f, 42f, 58f, 78f, 102f, 130f, 165f, 210f };
        /// Vadi mesh'inin yamaçta kapladığı genişlik (taban kenarından); ötesini arazi devralır.
        const float BankTop = TrackProfile.BankRim;
        // Vadi kesiti: tabandan yamaç tepesine sütunlar (taban kenarından uzaklık; eksi = taban içi)
        // Taban içi ~1,1 m aralıklı: yana kaymış tümsekler fizikteki yüzeyle aynı görünsün.
        static readonly float[] ValleyCols = { -6.5f, -5.4f, -4.3f, -3.2f, -2.2f, -1.1f, 0f, 0.8f, 1.7f, 2.7f, 3.8f, 5f, 6.3f, 8f, 10f };

        readonly TrackProfile track;
        readonly TrackTheme theme;
        readonly Transform root;
        readonly RuntimeResources resources;
        readonly PropBatcher props = new PropBatcher();
        readonly System.Random rng;
        readonly float hw, seedX, seedZ;

        public static void Build(Transform root, TrackProfile track, TrackTheme theme) => new Scenery(root, track, theme).BuildAll();

        Scenery(Transform root, TrackProfile track, TrackTheme theme)
        {
            this.root = root;
            resources = RuntimeResources.For(root);
            this.track = track;
            this.theme = theme;
            hw = track.halfWidth;
            rng = new System.Random(track.name.GetHashCode());
            seedX = R(0f, 100f);
            seedZ = R(0f, 100f);
        }

        void BuildAll()
        {
            BuildValley();
            BuildPath();
            BuildForkRidges();
            BuildSurfaces();
            BuildTerrain();
            BuildWater();
            BuildCanyonWalls();
            BuildBridges();
            BuildVegetation();
            BuildObstacles();
            BuildStartArea();
            BuildFinish();
            BuildClouds();
            BuildOpenTrackBoundary();
            props.tint = theme.propTint;
            props.Build(root);
        }

        // ---------------------------------------------------------------- yardımcılar

        float R() => (float)rng.NextDouble();
        float R(float a, float b) => a + (b - a) * R();
        T Pick<T>(T[] list) => list[rng.Next(list.Length)];

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// Yan arazinin yüksekliği: pistin hemen yanında pist seviyesi, uzaklaştıkça tepeler, en dışta dağlar.
        /// Çukurlarda (dere, kanyon) arazi de pistle birlikte iner: çukur pisti enlemesine kesen bir vadi olur.
        float GroundHeight(float x, float z)
        {
            float c = track.CenterX(z);
            float d = Mathf.Abs(x - c) - hw;
            // Vadi içi ve yamaç pistin kendi yüksekliği; yamacın tepesinden sonra tepeler başlar.
            float h = track.Height(z) + track.Bank(hw + Mathf.Clamp(d, 0f, BankTop));
            // These silhouettes begin outside the playable strip. Never change collision terrain here.
            if (theme.landscape == TrackTheme.Landscape.Dunes)
            {
                float ridge = 0.5f + 0.5f * Mathf.Sin(x * 0.05f + z * 0.027f + seedZ);
                float broad = Mathf.PerlinNoise(x * 0.008f + seedX, z * 0.01f + seedZ);
                return h + Smooth(BankTop, BankTop + 40f, d) * theme.hillHeight * ridge * ridge
                         + Smooth(60f, 180f, d) * theme.mountainHeight * broad;
            }
            if (theme.landscape == TrackTheme.Landscape.Craters)
            {
                // Broad shallow bowls with raised rims; sparse rocks leave the horizon readable.
                float cx = Mathf.Round((x + seedX) / 95f) * 95f - seedX;
                float cz = Mathf.Round((z + seedZ) / 100f) * 100f - seedZ;
                float radius = new Vector2((x - cx) / 43f, (z - cz) / 40f).magnitude;
                float rim = 13f * Mathf.Exp(-Mathf.Pow((radius - 0.75f) / 0.17f, 2f));
                float bowl = -4f * (1f - Smooth(0.2f, 0.7f, radius));
                float rolling = theme.hillHeight * Mathf.PerlinNoise(x * 0.012f + seedX, z * 0.012f + seedZ);
                return h + Smooth(BankTop, BankTop + 35f, d) * (rolling + rim + bowl);
            }
            float n = Mathf.PerlinNoise(x * 0.018f + seedX, z * 0.018f + seedZ) * 0.8f
                    + Mathf.PerlinNoise(x * 0.06f + 31f, z * 0.06f + 17f) * 0.35f;
            h += Smooth(BankTop, BankTop + 45f, d) * theme.hillHeight * n;
            float m = Smooth(80f, 170f, d) * theme.mountainHeight * Mathf.PerlinNoise(x * 0.007f + 5f, z * 0.007f + 9f + seedZ);
            if (theme.terrace > 0f) m = Mathf.Floor(m / theme.terrace) * theme.terrace;
            if (theme.landscape == TrackTheme.Landscape.Canyon)
                m += Smooth(BankTop + 3f, BankTop + 16f, d) * (20f + 10f * Mathf.PerlinNoise(z * 0.012f + seedZ, 3f));
            return h + m;
        }

        /// The flat forest has no bank to explain its off-track limit. Low amber rails mark that exact edge.
        void BuildOpenTrackBoundary()
        {
            if (track.bankScale > 0f) return;
            var wood = resources.Solid(new Color(0.24f, 0.2f, 0.14f));
            var amber = resources.Solid(new Color(0.94f, 0.66f, 0.15f), 0.08f);
            var cube = Mats.PrimitiveMesh(PrimitiveType.Cube);
            const float spacing = 5f;
            for (int side = -1; side <= 1; side += 2)
                for (float z = track.startZ; z < track.EndZ; z += spacing)
                {
                    Vector3 Edge(float at)
                    {
                        float x = track.CenterX(at) + side * (hw + TrackProfile.BankRim);
                        return new Vector3(x, track.TerrainHeight(x, at), at);
                    }
                    Vector3 a = Edge(z), b = Edge(Mathf.Min(z + spacing, track.EndZ));
                    if (InWater(z, a.y) || InWater(b.z, b.y)) continue;
                    props.Add(cube, wood, Matrix4x4.TRS(a + Vector3.up * 0.62f, Quaternion.identity, new Vector3(0.14f, 1.24f, 0.14f)));
                    props.Add(cube, amber, Matrix4x4.TRS(a + Vector3.up * 1.13f, Quaternion.identity, new Vector3(0.17f, 0.2f, 0.17f)));
                    Vector3 span = b - a;
                    props.Add(cube, amber, Matrix4x4.TRS((a + b) * 0.5f + Vector3.up * 0.92f,
                        Quaternion.FromToRotation(Vector3.forward, span.normalized), new Vector3(0.05f, 0.07f, span.magnitude)));
                }
        }

        bool InWater(float z, float y)
        {
            foreach (var w in track.water)
                if (z > w.x && z < w.y && y < w.z + 0.4f) return true;
            return false;
        }

        /// Modeli tabanı verilen noktaya oturacak, yatay ortası o noktada olacak şekilde yerleştirir.
        void Place(PropModel m, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            if (m == null) return;
            var b = m.bounds;
            props.Add(m, Matrix4x4.TRS(pos, rot, scale) * Matrix4x4.Translate(new Vector3(-b.center.x, -b.min.y, -b.center.z)));
        }

        /// Modeli istenen yüksekliğe ölçekleyip yerleştirir.
        void PlaceHeight(string path, Vector3 pos, float height, float yaw)
        {
            var m = PropModel.Get(path);
            if (m == null) return;
            float s = height / Mathf.Max(m.bounds.size.y, 0.01f);
            Place(m, pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one * s);
        }

        // ---------------------------------------------------------------- pist

        /// Vadi: çimenli düz taban ve iki yanda yumuşakça yükselen yamaçlar (çit yok). 1 m aralıkla örneklenir
        /// ki rampalar ve tepeler fizikteki yüzeyle birebir aynı görünsün. Yumuşak gölgeli (yamaçlar yuvarlak).
        void BuildValley()
        {
            int first = Mathf.FloorToInt(track.startZ), last = Mathf.CeilToInt(track.EndZ);
            // Büyük lekeler ~70 m'de bir tekrar eder (UV 1 = 2.5 m)
            var mat = resources.Ground(theme.grass, 0.06f, theme.GroundTexture(), TextureKit.Macro(), 0.036f);
            int cols = ValleyCols.Length * 2 - 1;
            for (int c0 = first; c0 < last; c0 += 150)
            {
                int c1 = Mathf.Min(last, c0 + 150);
                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();
                for (int z = c0; z <= c1; z++)
                {
                    float c = track.CenterX(z);
                    for (int k = 0; k < cols; k++)
                    {
                        // Soldaki yamacın tepesinden sağdakinin tepesine
                        int j = k < ValleyCols.Length ? ValleyCols.Length - 1 - k : k - ValleyCols.Length + 1;
                        float side = k < ValleyCols.Length ? -1f : 1f;
                        float off = ValleyCols[j];
                        if (off < 0f) off *= hw / 6.5f;   // taban içi sütunlar vadi genişliğiyle ölçeklenir
                        float x = c + side * (hw + off);
                        if (j == 0) x = c + side * 0f;   // orta çizgi
                        verts.Add(new Vector3(x, track.TerrainHeight(x, z), z));
                        uvs.Add(new Vector2(x * 0.4f, z * 0.4f));
                    }
                    if (z > c0)
                    {
                        int b = verts.Count - 2 * cols;
                        for (int k = 0; k < cols - 1; k++)
                        {
                            int a = b + k;
                            tris.AddRange(new[] { a, a + cols, a + 1, a + 1, a + cols, a + cols + 1 });
                        }
                    }
                }
                var mesh = resources.Own(new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 });
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject("Valley " + c0);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        /// Vadinin tabanında S çizen toprak patika (normal hız; dışındaki çimen yavaşlatır).
        const int PathSegs = 4;

        void BuildPath()
        {
            if (track.noPath) return;   // yolsuz harita: yalnız zemin
            var mat = resources.Ground(theme.trackA, 0.1f, TextureKit.Dirt(), TextureKit.Macro(), 0.03f);
            // Kenar: çimenle toprak arası tonda biraz daha geniş alt şerit; geçiş tek sert çizgi olmasın (aşınmış çim).
            var edge = resources.Ground(Color.Lerp(theme.grass, theme.trackA, 0.5f), 0.08f, theme.GroundTexture(), TextureKit.Macro(), 0.03f);
            float first = track.startZ, last = track.EndZ;
            // Çatalda patika iki şeride ayrılır: ana şerit sola, ikinci şerit (yalnızca çatal boyunca) sağa açılır.
            float Lane(float z, float side) => track.PathX(z) + side * TrackProfile.ForkOffset * track.ForkWeight(z);
            for (float c0 = first; c0 < last; c0 += 150f)
            {
                BuildStrip(mat, c0, Mathf.Min(last, c0 + 150f), z => Lane(z, -1f), "Path " + c0);
                BuildStrip(edge, c0, Mathf.Min(last, c0 + 150f), z => Lane(z, -1f), "PathEdge " + c0, 0.45f, 0.012f, 1.7f);
            }
            foreach (var f in track.forks)
            {
                BuildStrip(mat, f.z0, f.z1, z => Lane(z, 1f), "Fork " + f.z0);
                BuildStrip(edge, f.z0, f.z1, z => Lane(z, 1f), "ForkEdge " + f.z0, 0.45f, 0.012f, 1.7f);
            }
        }

        /// extra: şeride eklenen yarı genişlik; lift: zeminden yükseklik; phase: kenar dalgasının kayması.
        void BuildStrip(Material mat, float c0, float c1, System.Func<float, float> center, string name,
                        float extra = 0f, float lift = 0.025f, float phase = 0f)
        {
            const float step = 0.5f;
            {
                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();
                for (float z = c0; z <= c1 + 0.01f; z += step)
                {
                    float px = center(z);
                    float dir = (center(z + 0.5f) - center(z - 0.5f));   // kıvrımda genişlik sabit kalsın
                    float w = (TrackProfile.PathHalfWidth + extra) * Mathf.Sqrt(1f + dir * dir);
                    float wob = 0.12f * Mathf.Sin(z * 0.9f + phase) + 0.06f * Mathf.Sin(z * 2.3f + phase * 1.7f);   // kenar hafif düzensiz (yumuşak dalga)
                    float xa = px - w - wob, xb = px + w + wob;
                    // Şerit enine 4 parça: tümseklerin üstünde zemini izler (gömülmez, havada kalmaz).
                    for (int k = 0; k <= PathSegs; k++)
                    {
                        float x = Mathf.Lerp(xa, xb, k / (float)PathSegs);
                        verts.Add(new Vector3(x, track.TerrainHeight(x, z) + lift, z));
                        uvs.Add(new Vector2(x * 0.3f, z * 0.3f));
                    }
                    if (verts.Count >= 2 * (PathSegs + 1))
                    {
                        int b = verts.Count - 2 * (PathSegs + 1);
                        for (int k = 0; k < PathSegs; k++)
                        {
                            int i = b + k, j = i + PathSegs + 1;
                            tris.AddRange(new[] { i, j, i + 1, i + 1, j, j + 1 });
                        }
                    }
                }
                var mesh = resources.Own(new Mesh());
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// Çatal sırtı: tepesinde alçak çalılar ve çiçekler (iki yolu ayıran yeşil ada; çarpışma yok, sırt yumuşak).
        void BuildForkRidges()
        {
            foreach (var f in track.forks)
                for (float z = f.z0 + TrackProfile.ForkBlend * 0.8f; z < f.z1 - TrackProfile.ForkBlend * 0.8f; z += R(1.6f, 2.6f))
                {
                    float x = track.CenterX(z) + R(-0.35f, 0.35f);
                    var pos = new Vector3(x, track.TerrainHeight(x, z) - 0.05f, z);
                    // Alçak kalır (en çok 0.4 m): kızak sırtın üstünden geçerse süsün içinden geçiyormuş gibi görünmesin.
                    PlaceHeight(Pick(theme.near), pos, Mathf.Min(theme.nearHeight.x, 0.4f) * R(0.6f, 1f), R(0f, 360f));
                }
        }

        /// Su, çamur ve buz lekeleri: yumuşak loblu düzgün ovaller, araziyi izler (fizikteki kenarla aynı).
        /// Su ve çamur: altındaki zeminin (çimen/patika) koyu tonunda ince kıyı bandı + açık renkli ince kenar parlaması
        /// + kenarda birkaç çakıl. Buz: ince beyaz don kenarı; yanında birkaç yumuşak, kabarık kar tepeciği.
        void BuildSurfaces()
        {
            if (track.surfaces.Count == 0) return;
            // Su: kenarda sığ ve açık, ortada derin ve koyu; ince köpük kenarı.
            // Su birikintisi: Meshy taban dokusu (çakıllı çamur, ışık desenleri) üstünde yarı saydam su.
            var bedTex = Resources.Load<Texture2D>("Surfaces/PuddleBed");
            var bed = Textured(resources.Solid(Color.Lerp(theme.mudB, theme.puddleDeep, 0.35f), 0.2f), bedTex);
            // Sığ su koyu ve saydam, yansıma ölçülü: gökyüzünü yansıtan soluk levha gibi durmasın, çamurlu taban görünsün.
            var sc = Color.Lerp(theme.puddleShallow, theme.puddleDeep, 0.5f);
            var shallow = resources.Water(new Color(sc.r, sc.g, sc.b, bedTex != null ? 0.32f : theme.puddleShallow.a));
            var deep = resources.Water(new Color(theme.puddleDeep.r, theme.puddleDeep.g, theme.puddleDeep.b, bedTex != null ? 0.5f : theme.puddleDeep.a));
            if (bedTex != null) { shallow.SetFloat("_Glossiness", 0.42f); deep.SetFloat("_Glossiness", 0.5f); }   // yatık açıda gökyüzü yansıması parlaklıkla artar
            var foam = resources.Solid(new Color(0.86f, 0.95f, 0.98f), 0.6f);
            // Çamur: kenarı açık kahve, ortası koyu ve ıslak parlak; üstünde birikinti benekleri.
            // Meshy çamur dokusu (ıslak kıvrımlar, su gözleri; gri ton, renk temadan). ~5.7 m'de bir tekrar.
            var mudTex = Resources.Load<Texture2D>("Surfaces/Mud");
            var mudOuter = Textured(resources.Solid(theme.mudA, 0.35f), mudTex);
            var mudCore = Textured(resources.Solid(theme.mudB, 0.55f), mudTex);
            if (mudTex != null) { mudOuter.mainTextureScale = mudCore.mainTextureScale = Vector2.one * 0.5f; }
            var mudSpot = resources.Solid(theme.mudSpot, 0.8f);
            var shoreGrass = resources.Solid(Color.Lerp(theme.grass, Color.black, theme.snowy ? 0.12f : 0.28f), 0.2f);
            var shorePath = resources.Solid(Color.Lerp(theme.trackA, Color.black, 0.3f), 0.3f);
            // Buz: donmuş göl; dıştan içe koyulaşan mavi, çapraz ışık yansımaları ve ince çatlaklar.
            var iceTex = Resources.Load<Texture2D>("Surfaces/Ice") ?? IceTexture();   // Meshy: çatlaklar ve hava kabarcıkları
            float gloss = theme.lava ? 0.35f : 1f;   // obsidyen: gökyüzünü yansıtıp soluk cam levha gibi durmasın
            var iceOuter = Textured(resources.Solid(theme.iceA, 0.95f * gloss), iceTex);
            var iceMid = Textured(resources.Solid(theme.iceB, 0.97f * gloss), iceTex);
            var iceCore = Textured(resources.Solid(theme.iceC, 0.98f * gloss), iceTex);
            // Don kenarı: saf beyaz değil, açık buz mavisi (çimenin üstünde beyaz leke gibi durmasın); karlı pistte beyaz.
            var frost = resources.Solid(theme.frost.a > 0f ? theme.frost : theme.snowy ? new Color(0.96f, 0.98f, 1f) : new Color(0.8f, 0.93f, 1f), 0.5f);
            foreach (var zone in track.surfaces)
            {
                // Lekenin ortası patikada mı (kıyı rengi altındaki zemine uysun)?
                float pz = zone.cz, px = (zone.onPath ? track.PathX(pz) : track.CenterX(pz)) + zone.cx;
                bool overPath = zone.onPath || Mathf.Abs(px - track.PathX(pz)) < TrackProfile.PathHalfWidth;
                switch (zone.type)
                {
                    case Surface.Puddle:
                        Blob(zone, 1.07f, 0.03f, overPath ? shorePath : shoreGrass, "Shore");
                        if (bedTex != null) Blob(zone, 1f, 0.038f, bed, "Puddle Bed", 0f, true);
                        Blob(zone, 1f, 0.045f, shallow, "Puddle");
                        Blob(zone.Inner(R(-0.12f, 0.12f), R(-0.1f, 0.1f), 13f), 0.5f, 0.05f, deep, "Puddle Deep");
                        Ring(zone, 0.96f, 1f, 0.055f, foam, "Foam");
                        Pebbles(zone);
                        break;
                    case Surface.Mud:
                        // Yol haritası aşama 6: kalın açık kenar halkası zeminden ayrı bir nesne gibi duruyordu.
                        Blob(zone, 1.03f, 0.03f, overPath ? shorePath : shoreGrass, "Shore");
                        Blob(zone, 1f, 0.045f, mudOuter, "Mud");
                        Blob(zone.Inner(R(-0.1f, 0.1f), R(-0.08f, 0.08f), 7f), 0.6f, 0.05f, mudCore, "Mud Wet");
                        // Islak benekler (küçük birikintiler)
                        for (int k = 0; k < 2 + rng.Next(3); k++)
                        {
                            float a = R(0f, 6.28f), r = zone.Edge(a) * R(0.15f, 0.55f);
                            var spot = SurfaceZone.Blob(Surface.Mud, zone.cz + Mathf.Cos(a) * r * zone.rz, zone.cx + Mathf.Sin(a) * r * zone.rx,
                                                        R(0.35f, 0.7f), R(0.3f, 0.6f), R(0f, 50f), zone.onPath);
                            Blob(spot, 1f, 0.058f, mudSpot, "Mud Spot");   // koyu ıslak çamur (mavi su beneği çamura uymuyordu)
                        }
                        Pebbles(zone);
                        break;
                    case Surface.Ice:
                        Blob(zone, 1f, 0.045f, iceOuter, "Ice", 0f, true);
                        Blob(zone.Inner(R(-0.1f, 0.1f), R(-0.08f, 0.08f), 5f), 0.58f, 0.05f, iceMid, "Ice Mid", 0f, true);
                        Blob(zone.Inner(R(-0.12f, 0.12f), R(-0.1f, 0.1f), 11f), 0.3f, 0.055f, iceCore, "Ice Core", 0f, true);
                        Ring(zone, 0.97f, 1.03f, 0.058f, frost, "Frost Rim");
                        break;   // çayırda beyaz kar tepeciği yok (kullanıcı: beyaz lekeler kötü duruyor)
                }
            }
        }

        void Pebbles(SurfaceZone zone)
        {
            for (int k = 0; k < 2 + rng.Next(3); k++)
            {
                float a = R(0f, 6.28f);
                PlaceSurfaceProp(zone, a, zone.Edge(a) * R(1.05f, 1.12f), "Nature/rock_smallA", R(0.04f, 0.08f));   // çakıl
            }
        }

        /// Lekenin kenarına kadar uzanan, araziyi izleyen dairesel ağ. scale: kenarı büyütür (kıyı bandı için);
        /// dome: ortanın kenara göre ne kadar kabarık olduğu (kar tepeciği).
        void Blob(SurfaceZone zone, float scale, float lift, Material mat, string name, float dome = 0f, bool localUV = false)
        {
            const int rings = 6, sectors = 64;   // girintili kenar için sık örnekleme
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var luv = localUV ? new List<Vector2>() : null;   // lekeye göre UV: doku leke boyunca bir kez uzanır
            verts.Add(SurfacePoint(zone, 0f, 0f, lift + dome));
            luv?.Add(new Vector2(0.5f, 0.5f));
            for (int r = 1; r <= rings; r++)
                for (int j = 0; j < sectors; j++)
                {
                    float a = j * 6.2832f / sectors, f = r / (float)rings;
                    float k = scale * zone.Edge(a) * f;
                    verts.Add(SurfacePoint(zone, Mathf.Cos(a) * k, Mathf.Sin(a) * k, lift + dome * (1f - f * f)));
                    luv?.Add(new Vector2(0.5f + 0.42f * Mathf.Sin(a) * k, 0.5f + 0.42f * Mathf.Cos(a) * k));
                }
            for (int j = 0; j < sectors; j++)
            {
                int j2 = (j + 1) % sectors;
                Tri(tris, verts, 0, 1 + j, 1 + j2);
                for (int r = 1; r < rings; r++)
                {
                    int a = 1 + (r - 1) * sectors + j, b = 1 + (r - 1) * sectors + j2;
                    int c = 1 + r * sectors + j, d = 1 + r * sectors + j2;
                    Tri(tris, verts, a, c, b);
                    Tri(tris, verts, b, c, d);
                }
            }
            SurfaceMesh(verts, tris, mat, name, luv);
        }

        /// Lekenin kenarı boyunca ince şerit (inner..outer, yarıçap biriminde): kenar parlaması ya da don kenarı.
        void Ring(SurfaceZone zone, float inner, float outer, float lift, Material mat, string name)
        {
            const int sectors = 64;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int j = 0; j < sectors; j++)
            {
                float a = j * 6.2832f / sectors, e = zone.Edge(a);
                verts.Add(SurfacePoint(zone, Mathf.Cos(a) * e * inner, Mathf.Sin(a) * e * inner, lift));
                verts.Add(SurfacePoint(zone, Mathf.Cos(a) * e * outer, Mathf.Sin(a) * e * outer, lift));
            }
            for (int j = 0; j < sectors; j++)
            {
                int a = 2 * j, b = 2 * ((j + 1) % sectors);
                Tri(tris, verts, a, a + 1, b);
                Tri(tris, verts, b, a + 1, b + 1);
            }
            SurfaceMesh(verts, tris, mat, name);
        }

        /// u: z yönü, v: yanal (lekenin yarıçap biriminde); arazinin lift kadar üstü.
        Vector3 SurfacePoint(SurfaceZone zone, float u, float v, float lift)
        {
            float z = zone.cz + u * zone.rz;
            float x = (zone.onPath ? track.PathX(z) : track.CenterX(z)) + zone.cx + v * zone.rx;
            return new Vector3(x, track.TerrainHeight(x, z) + lift, z);
        }

        void SurfaceMesh(List<Vector3> verts, List<int> tris, Material mat, string name, List<Vector2> localUV = null)
        {
            var uvs = localUV;
            if (uvs == null)
            {
                uvs = new List<Vector2>();
                foreach (var v in verts) uvs.Add(new Vector2(v.x * 0.35f, v.z * 0.35f));
            }
            var mesh = resources.Own(new Mesh());
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// Üçgeni yukarı bakacak şekilde ekler (köşe sırası açıya göre değişebildiği için).
        static void Tri(List<int> tris, List<Vector3> v, int a, int b, int c)
        {
            if (Vector3.Cross(v[b] - v[a], v[c] - v[a]).y < 0f) { int t = b; b = c; c = t; }
            tris.Add(a); tris.Add(b); tris.Add(c);
        }

        void PlaceSurfaceProp(SurfaceZone zone, float angle, float r, string model, float height)
        {
            float z = zone.cz + Mathf.Cos(angle) * r * zone.rz;
            float x = (zone.onPath ? track.PathX(z) : track.CenterX(z)) + zone.cx + Mathf.Sin(angle) * r * zone.rx;
            if (Mathf.Abs(x - track.CenterX(z)) > hw + 2f) return;
            PlaceHeight(model, new Vector3(x, track.Height(x, z), z), height, R(0f, 360f));
        }

        static Material Textured(Material m, Texture2D tex)
        {
            m.mainTexture = tex;
            return m;
        }

        /// Buz (lekeye göre UV): iki çapraz yumuşak ışık yansıması, ince dallanan çatlaklar ve hafif benekler.
        /// Malzeme rengiyle çarpılır (1 = rengin kendisi; yansımalar 1'in üstüne çıkamaz, taban 0.82).
        static Texture2D iceTexture;
        static Texture2D IceTexture()
        {
            if (iceTexture != null) return iceTexture;
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Ice", anisoLevel = 4 };
            var px = new Color[N * N];
            var rnd = new System.Random(11);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    float d = u + v;   // çapraz
                    float streak = Mathf.Exp(-Mathf.Pow((d - 0.78f) / 0.06f, 2f)) + 0.6f * Mathf.Exp(-Mathf.Pow((d - 0.98f) / 0.025f, 2f));
                    float b = 0.82f + 0.18f * Mathf.Clamp01(streak) + 0.02f * (float)rnd.NextDouble();
                    px[y * N + x] = new Color(b, b, b, 1f);
                }
            // Rastgele yürüyüşle dallanan ince çatlaklar (açık renk: buzun içindeki kırılma yüzeyleri)
            for (int k = 0; k < 9; k++)
            {
                float x = rnd.Next(N / 5, N * 4 / 5), y = rnd.Next(N / 5, N * 4 / 5), dir = (float)rnd.NextDouble() * 6.28f;
                int len = 40 + rnd.Next(70);
                for (int j = 0; j < len; j++)
                {
                    dir += ((float)rnd.NextDouble() - 0.5f) * 0.5f;
                    x = Mathf.Clamp(x + Mathf.Cos(dir), 0, N - 1);
                    y = Mathf.Clamp(y + Mathf.Sin(dir), 0, N - 1);
                    px[(int)y * N + (int)x] = Color.white;
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return iceTexture = tex;
        }

        // ---------------------------------------------------------------- arazi

        /// Dar kanyonun duvarları (Canyon Run; kullanıcı 2026-10-23: "duvarlar çok düz, arada kayalar, kanyon hissi"):
        /// iç yüz tabandan dik çıkar, katman katman (her katmanın altında içe giren saçak, üstünde raf) dışa açılarak
        /// arkadaki yüksek platoya bağlanır. Katmanlar iki tonlu kaya; yüzey z boyunca girintili çıkıntılı, yer yer
        /// büyük kaya burunları öne çıkar. Dipte ve raflarda kaya blokları. Fizikteki duvar iç yüzün kendisi (WallHalfAt).
        void BuildCanyonWalls()
        {
            if (track.wallHalf <= 0f) return;
            var rockA = Textured(resources.Solid(theme.rock, 0.12f), TextureKit.Rock());
            var rockB = Textured(resources.Solid(Color.Lerp(theme.rock, new Color(0.92f, 0.72f, 0.52f), 0.35f), 0.1f), TextureKit.Rock());
            // (dışa, yukarı) profil: katmanlar (dik yüz + raf); her noktanın katmanı (renk) ve girinti genliği
            Vector2[] prof =
            {
                new Vector2(-0.05f, -0.6f), new Vector2(0f, 1.6f), new Vector2(-0.15f, 2.2f), new Vector2(0.55f, 2.5f),
                new Vector2(0.6f, 4.6f), new Vector2(0.45f, 5.1f), new Vector2(1.4f, 5.4f), new Vector2(1.5f, 7.8f),
                new Vector2(1.3f, 8.3f), new Vector2(2.6f, 8.7f), new Vector2(2.8f, 11.4f), new Vector2(2.6f, 11.9f),
                new Vector2(4.2f, 12.3f), new Vector2(4.5f, 14.6f), new Vector2(6.5f, 16f),
            };
            int[] band = { 0, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0 };
            float[] jagAmp = { 0f, 0.25f, 0.3f, 0.4f, 0.45f, 0.5f, 0.55f, 0.6f, 0.65f, 0.7f, 0.75f, 0.8f, 0.8f, 0.9f, 0.6f };
            int first = Mathf.FloorToInt(track.startZ) - 10, last = Mathf.CeilToInt(track.EndZ) + 10;
            for (int side = -1; side <= 1; side += 2)
                for (int c0 = first; c0 < last; c0 += 150)
                {
                    int c1 = Mathf.Min(last, c0 + 150);
                    var verts = new List<Vector3>();
                    var uvs = new List<Vector2>();
                    var trisA = new List<int>();
                    var trisB = new List<int>();
                    int cols = prof.Length + 1;
                    float seed = side * 37.1f;
                    for (int z = c0; z <= c1; z++)
                    {
                        float c = track.CenterX(z), w = track.WallHalfAt(z);
                        float floor = track.Height(z);
                        // Büyük kaya burnu: ~25-40 m'de bir, birkaç metre boyunca duvar öne/arkaya kabarır (alt kısım fiziğe dokunmaz)
                        float bulge = Mathf.Max(0f, Mathf.Sin(z * 0.19f + seed) * Mathf.Sin(z * 0.047f + seed * 0.3f)) * 1.1f;
                        for (int k = 0; k < prof.Length; k++)
                        {
                            float jag = jagAmp[k] * (0.6f * Mathf.Sin(z * 0.43f + k * 1.9f + seed) + 0.4f * Mathf.Sin(z * 0.11f + k * 0.7f + seed));
                            float lift = k >= 3 ? bulge * Mathf.Clamp01((prof[k].y - 2f) / 4f) : 0f;
                            float x = c + side * (w + prof[k].x + Mathf.Max(-0.1f, jag) * (k == 0 ? 0f : 1f) - lift);
                            if (k <= 1) x = c + side * Mathf.Max(w, Mathf.Abs(x - c));   // alt kısım fizik duvarının içine taşmaz
                            verts.Add(new Vector3(x, floor + prof[k].y + 0.25f * jag, z));
                            uvs.Add(new Vector2(prof[k].y * 0.3f, z * 0.3f));
                        }
                        float xo = c + side * (track.halfWidth + TrackProfile.BankRim);
                        verts.Add(new Vector3(xo, Mathf.Max(track.TerrainHeight(xo, z), floor + 16f) - 0.2f, z));
                        uvs.Add(new Vector2(5f, z * 0.3f));
                        if (z > c0)
                        {
                            int b = verts.Count - 2 * cols;
                            for (int k = 0; k < cols - 1; k++)
                            {
                                int a = b + k;
                                var tl = k < band.Length && band[k] == 1 ? trisB : trisA;
                                if (side < 0) tl.AddRange(new[] { a, a + 1, a + cols, a + 1, a + cols + 1, a + cols });
                                else tl.AddRange(new[] { a, a + cols, a + 1, a + 1, a + cols, a + cols + 1 });
                            }
                        }
                    }
                    var mesh = resources.Own(new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 });
                    mesh.SetVertices(verts);
                    mesh.SetUVs(0, uvs);
                    mesh.subMeshCount = 2;
                    mesh.SetTriangles(trisA, 0);
                    mesh.SetTriangles(trisB, 1);
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    var go = new GameObject("Canyon Wall");
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterials = new[] { rockA, rockB };
                }

            // Kaya blokları: duvar dibinde (oyun alanına taşmadan) ve raflarda
            string[] rocks = { "Nature/rock_largeA", "Nature/rock_largeB", "Nature/rock_largeC", "Nature/rock_largeD", "Nature/rock_tallA", "Nature/rock_tallB" };
            for (float z = track.startZ + 20f; z < track.EndZ; z += R(5f, 11f))
            {
                if (track.BridgeAt(z, out _)) continue;
                int side = R() < 0.5f ? -1 : 1;
                float c = track.CenterX(z), w = track.WallHalfAt(z), floor = track.Height(z);
                if (R() < 0.6f)
                {
                    float hgt = R(1f, 2.4f);
                    PlaceHeight(rocks[rng.Next(rocks.Length)], new Vector3(c + side * (w + hgt * 0.35f), floor - 0.2f, z), hgt, R(0f, 360f));
                }
                else
                {
                    // Rafta: 2.5 / 5.4 / 8.7 m katmanlarının üstünde
                    var shelf = prof[new[] { 3, 6, 9 }[rng.Next(3)]];
                    float hgt = R(0.8f, 1.8f);
                    PlaceHeight(rocks[rng.Next(rocks.Length)], new Vector3(c + side * (w + shelf.x + 0.6f), floor + shelf.y - 0.1f, z), hgt, R(0f, 360f));
                }
            }
        }

        /// Uçurumun üstündeki ince doğal kaya yol (Canyon Run): tabla boyunca kalın kaya dilimleri, altında aşağı sarkan
        /// sivri kaya; korkuluk yok.
        void BuildStoneLedge(TrackProfile.Bridge b, int index)
        {
            var rockA = Textured(resources.Solid(theme.rock, 0.12f), TextureKit.Rock());
            var rockB = Textured(resources.Solid(Color.Lerp(theme.rock, new Color(0.92f, 0.72f, 0.52f), 0.35f), 0.1f), TextureKit.Rock());
            var go = new GameObject("Bridge " + index).transform;
            go.SetParent(root, false);
            Vector3 At(float z) => new Vector3(b.XAt(z), TrackProfile.DeckHeight(b, z), z);
            float z0 = b.z0 - 0.3f, len = b.z1 - b.z0 + 0.6f;
            int n = Mathf.Max(3, Mathf.RoundToInt(len / 1.8f));   // uzun, düzensiz dilimler (kısa dilimler tahta köprü gibi görünüyordu)
            for (int k = 0; k < n; k++)
            {
                float z = z0 + (k + 0.5f) * len / n;
                var dir = Quaternion.LookRotation(At(z + 0.2f) - At(z - 0.2f));
                float thick = 1.1f + 0.4f * Mathf.Sin(z * 1.3f);
                Mats.Prim(PrimitiveType.Cube, "Ledge", go, At(z) + Vector3.down * (thick * 0.5f + 0.02f),
                          new Vector3(b.HalfWidth * 2f + R(0.1f, 0.4f), thick, len / n + 0.5f), rockA,
                          dir * Quaternion.Euler(R(-1f, 1f), R(-4f, 4f), R(-2f, 2f)));
                // Kenarlarda yuvarlak kaya topakları (doğal kenar)
                for (int e = -1; e <= 1; e += 2)
                    Mats.Prim(PrimitiveType.Sphere, "Ledge Edge", go, At(z) + dir * Vector3.right * (e * b.HalfWidth * 0.95f) + Vector3.down * 0.32f,
                              new Vector3(R(0.28f, 0.42f), R(0.25f, 0.35f), len / n * R(0.4f, 0.7f)), rockA, dir);
                {
                    float hang = R(2f, 6f);
                    Mats.Prim(PrimitiveType.Cube, "Ledge Root", go, At(z) + Vector3.down * (thick + hang * 0.5f),
                              new Vector3(b.HalfWidth * R(0.8f, 1.6f), hang, R(0.4f, 0.9f)), rockA, dir * Quaternion.Euler(0f, R(0f, 40f), R(-6f, 6f)));
                }
            }
        }

        void BuildTerrain()
        {
            const float step = 2.5f;
            const int rowsPerChunk = 60;
            float z0 = track.startZ - 200f, z1 = track.EndZ + 140f;
            int rows = Mathf.CeilToInt((z1 - z0) / step);
            var mats = new[] { Textured(resources.Solid(theme.grass, 0.05f), theme.GroundTexture()), Textured(resources.Solid(theme.grassB, 0.05f), theme.GroundTexture()),
                               Textured(resources.Solid(theme.rock, 0.1f), TextureKit.Rock()) };

            for (int side = -1; side <= 1; side += 2)
            for (int r0 = 0; r0 < rows; r0 += rowsPerChunk)
            {
                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new[] { new List<int>(), new List<int>(), new List<int>() };

                // Her üçgen kendi köşelerine sahip: düz (köşeli, low-poly) gölgeleme. Renk eğime göre seçilir.
                void Tri(Vector3 a, Vector3 b, Vector3 c)
                {
                    var n = Vector3.Cross(b - a, c - a);
                    if (n.y < 0f) { var t = b; b = c; c = t; n = -n; }
                    n.Normalize();
                    Vector3 mid = (a + b + c) / 3f;
                    int sub = n.y < 0.6f ? 2 : Mathf.PerlinNoise(mid.x * 0.05f + seedZ, mid.z * 0.05f + seedX) > 0.5f ? 1 : 0;
                    int i = verts.Count;
                    verts.Add(a); verts.Add(b); verts.Add(c);
                    // Dik yüzlerde (kaya) doku yandan, düzlerde yukarıdan yansıtılır.
                    bool steep = sub == 2;
                    uvs.Add(steep ? new Vector2(a.z * 0.12f, a.y * 0.12f) : new Vector2(a.x * 0.15f, a.z * 0.15f));
                    uvs.Add(steep ? new Vector2(b.z * 0.12f, b.y * 0.12f) : new Vector2(b.x * 0.15f, b.z * 0.15f));
                    uvs.Add(steep ? new Vector2(c.z * 0.12f, c.y * 0.12f) : new Vector2(c.x * 0.15f, c.z * 0.15f));
                    tris[sub].Add(i); tris[sub].Add(i + 1); tris[sub].Add(i + 2);
                }

                Vector3 P(float x, float z) => new Vector3(x, GroundHeight(x, z), z);

                int r1 = Mathf.Min(rows, r0 + rowsPerChunk);
                for (int r = r0; r < r1; r++)
                {
                    float za = z0 + r * step, zb = za + step;
                    for (int c = 0; c < Cols.Length - 1; c++)
                    {
                        float ca = track.CenterX(za), cb = track.CenterX(zb);
                        float da = hw + BankTop + Cols[c], db = hw + BankTop + Cols[c + 1];
                        Vector3 p00 = P(ca + side * da, za), p10 = P(ca + side * db, za), p01 = P(cb + side * da, zb), p11 = P(cb + side * db, zb);
                        Tri(p00, p01, p10);
                        Tri(p10, p01, p11);
                    }
                }

                var mesh = resources.Own(new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 });
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = 3;
                for (int i = 0; i < 3; i++) mesh.SetTriangles(tris[i], i);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject("Terrain " + side + " " + r0);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            }
        }

        void BuildWater()
        {
            if (track.water.Count == 0) return;
            Material mat;
            if (theme.lava)
            {
                // Lav: parlayan turuncu, dalgacıksız
                mat = resources.Glow(theme.water, new Color(1f, 0.32f, 0.04f) * 1.6f);
            }
            else mat = resources.Water(theme.water);
            foreach (var w in track.water)
            {
                const float half = 230f;
                var mesh = resources.Own(new Mesh());
                mesh.vertices = new[]
                {
                    new Vector3(-half, w.z, w.x), new Vector3(-half, w.z, w.y), new Vector3(half, w.z, w.x), new Vector3(half, w.z, w.y),
                };
                mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject("Water");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // ---------------------------------------------------------------- çit, bitki, engel

        bool StartArea(float z, float d) => z > -40f && z < 30f && d < 26f;

        void TryPlant(int side, float d, float z, string[] list, Vector2 heightRange, bool tree)
        {
            if (tree && StartArea(z, d)) return;
            float x = track.CenterX(z) + side * (hw + d);
            float y = GroundHeight(x, z);
            if (InWater(z, y)) return;
            if (tree)
            {
                float slope = Mathf.Abs(GroundHeight(x + 1.5f * side, z) - y) + Mathf.Abs(GroundHeight(x, z + 1.5f) - y);
                if (slope > 1.6f) return;
            }
            string path = Pick(list);
            // Kızağın erişebildiği yere (taban + yamaç) sert dekor konmaz: oradaki kayalar pistin gerçek engelleridir.
            if (!tree && d < TrackProfile.BankLimit + 1f && IsSolid(path)) return;
            var m = PropModel.Get(path);
            if (m == null) return;
            float s = HeightFor(path, heightRange) / Mathf.Max(m.bounds.size.y, 0.01f);
            Place(m, new Vector3(x, y - 0.05f, z), Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one * s);
        }

        static bool IsSolid(string path) =>
            path.Contains("rock") || path.Contains("stone") || path.Contains("stump") || path.Contains("log")
            || path.Contains("cactus") || path.Contains("mushroom") || path.Contains("bush");

        /// Küçük nesneler (kütük, çalı, kaya) bandın ağaç boyuna değil kendi gerçek boylarına ölçeklenir.
        float HeightFor(string path, Vector2 range)
        {
            if (path.Contains("log") || path.Contains("stump")) return R(0.6f, 1.3f);
            if (path.Contains("bush")) return R(1f, 2.2f);
            if (path.Contains("rock_large") || path.Contains("stone_large")) return R(1.5f, 3.2f);
            if (path.Contains("cactus_short")) return R(1f, 2.2f);
            return R(range.x, range.y);
        }

        void BuildVegetation()
        {
            float z0 = track.startZ - 195f, z1 = track.EndZ + 135f;
            for (int side = -1; side <= 1; side += 2)
            {
                for (float z = z0; z < z1; z += theme.nearStep)
                    if (R() < theme.nearChance) TryPlant(side, R(1.2f, BankTop + 4f), z + R(0f, theme.nearStep), theme.near, theme.nearHeight, false);
                for (float z = z0; z < z1; z += theme.midStep)
                    for (int k = 0; k < 2; k++)
                        TryPlant(side, BankTop + 1f + Mathf.Pow(R(), 1.4f) * 52f, z + R(0f, theme.midStep), theme.mid, theme.midHeight, true);
                for (float z = z0; z < z1; z += theme.farStep)
                    TryPlant(side, R(60f, 205f), z + R(0f, theme.farStep), theme.far, theme.farHeight, true);
            }
        }

        /// İnce tahta köprüler: enine tahtalar, altında iki kiriş, dereye inen direkler (çim kıyıda direk yok:
        /// giriş/çıkış işaret direkleri ve kıyı ayakları 2026-10-30'da kaldırıldı). Tablanın üstü fizikteki tabla yüksekliğindedir.
        void BuildBridges()
        {
            if (track.bridges.Count == 0) return;
            var plankA = resources.Solid(new Color(0.64f, 0.44f, 0.25f), 0.12f);
            var plankB = resources.Solid(new Color(0.55f, 0.36f, 0.2f), 0.12f);
            var beam = resources.Solid(new Color(0.38f, 0.25f, 0.14f), 0.1f);
            foreach (var b in track.bridges)
            {
                if (track.wallHalf > 0f) { BuildStoneLedge(b, track.bridges.IndexOf(b)); continue; }
                float hwB = b.HalfWidth;
                // Ad sonundaki sıra: çöken köprünün parçalarını WorldView bulur (yan köprüler ayrı çöker).
                var go = new GameObject("Bridge " + track.bridges.IndexOf(b)).transform;
                go.SetParent(root, false);
                // Tabla parça parça kurulur: kıvrımlı köprüde (Bridge.XAt) her tahta ve korkuluk parçası tablanın o
                // noktadaki yönüne döner.
                Vector3 At(float z) => new Vector3(b.XAt(z), TrackProfile.DeckHeight(b, z), z);
                Quaternion Dir(float z) => Quaternion.LookRotation(At(z + 0.2f) - At(z - 0.2f));
                Vector3 Side(float z) => Dir(z) * Vector3.right;
                float z0 = b.z0 - 0.2f, len = b.z1 - b.z0 + 0.4f;
                int n = Mathf.RoundToInt(len / 0.34f);
                for (int k = 0; k < n; k++)
                {
                    float z = z0 + (k + 0.5f) * len / n;
                    Mats.Prim(PrimitiveType.Cube, "Plank", go, At(z) + Side(z) * R(-0.03f, 0.03f) + Vector3.down * 0.05f,
                              new Vector3(hwB * 2f + R(-0.04f, 0.1f), 0.07f, 0.29f), k % 2 == 0 ? plankA : plankB,
                              Dir(z) * Quaternion.Euler(0f, R(-2.5f, 2.5f), 0f));
                }
                int segs = Mathf.Max(1, Mathf.CeilToInt(len / 1.2f));
                for (int s = -1; s <= 1; s += 2)
                {
                    float off = s * (hwB - 0.14f);
                    for (int k = 0; k < segs; k++)
                    {
                        float za = z0 + len * k / segs, zb = z0 + len * (k + 1) / segs;
                        Vector3 a = At(za) + Side(za) * off + Vector3.down * 0.17f, c = At(zb) + Side(zb) * off + Vector3.down * 0.17f;
                        Mats.Prim(PrimitiveType.Cube, "Beam", go, (a + c) * 0.5f, new Vector3(0.14f, 0.16f, (c - a).magnitude + 0.04f), beam,
                                  Quaternion.LookRotation(c - a));
                    }
                }
                // Direkler: yalnız derenin iki duvarında ve ortada (kullanıcı 2026-10-30: çim kıyıdaki ayaklar kaldırıldı)
                foreach (float z in new[] { b.r0 + 0.4f, (b.r0 + b.r1) * 0.5f, b.r1 - 0.4f })
                {
                    float top = TrackProfile.DeckHeight(b, z) - 0.2f, floor = track.TerrainHeight(b.XAt(z), z) - 0.3f;
                    if (top - floor < 0.3f) continue;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 foot = At(z) + Side(z) * (s * (hwB - 0.14f));
                        var post = Mats.Prim(PrimitiveType.Cylinder, "Post", root, Vector3.zero, Vector3.one, beam);
                        Mats.PlaceBetween(post, new Vector3(foot.x, floor, z), new Vector3(foot.x, top, z), 0.16f);
                    }
                }
            }
        }

        void BuildObstacles()
        {
            string[] rocks = { "Nature/rock_largeA", "Nature/rock_largeB", "Nature/rock_largeC", "Nature/rock_largeD", "Nature/stone_largeA",
                               "Nature/stone_largeB", "Nature/stone_largeC" };
            string[] crates = { "Platformer/crate", "Platformer/crate-strong", "Platformer/barrel" };
            string[] trees = { "Nature/tree_pineTallA", "Nature/tree_pineTallB", "Nature/tree_pineRoundA", "Nature/tree_pineRoundC",
                               "Nature/tree_pineDefaultA", "Nature/tree_pineDefaultB" };
            foreach (var o in track.obstacles)
            {
                float y = track.Height(o.x, o.z);
                if (o.variant == 4)
                {
                    // Ağaç engeli: gövdesi çarpışma kutusunda, boyu oranını koruyarak
                    PlaceHeight(Pick(trees), new Vector3(o.x, y - 0.1f, o.z), o.height, R(0f, 360f));
                    continue;
                }
                string path = !o.isRock ? crates[o.variant % crates.Length]
                            : o.variant == 2 ? "Nature/log_stack" : o.variant == 3 ? "Nature/stump_old" : Pick(rocks);
                var m = PropModel.Get(path);
                if (m == null) continue;
                var size = o.isRock
                    ? new Vector3(o.halfWidth * 2.2f, o.height * 1.15f, o.halfDepth * 2.2f)
                    : new Vector3(o.halfWidth * 2f, o.height, o.halfDepth * 2f);
                var b = m.bounds.size;
                var scale = new Vector3(size.x / Mathf.Max(b.x, 0.01f), size.y / Mathf.Max(b.y, 0.01f), size.z / Mathf.Max(b.z, 0.01f));
                Place(m, new Vector3(o.x, y - 0.06f, o.z), Quaternion.Euler(0f, o.isRock ? R(0f, 360f) : R(-12f, 12f), 0f), scale);
            }
        }

        // ---------------------------------------------------------------- kemerler, başlangıç, bitiş, tabelalar

        bool OverWater(float z)
        {
            foreach (var w in track.water)
                if (z > w.x - 6f && z < w.y + 6f) return true;
            return false;
        }

        /// Pisti enlemesine geçen kapı: iki direk, üstte mesafe yazılı pankart, direk tepelerinde bayraklar.
        void Gate(float z, string label, Color banner, bool checkered)
        {
            float y = track.Height(z);
            float half = hw + 0.7f;
            var gate = new GameObject("Gate " + label).transform;
            gate.SetParent(root, false);
            float gc = track.CenterX(z);
            gate.position = new Vector3(gc, y, z);
            var white = resources.Solid(Color.white, 0.3f);
            for (int side = -1; side <= 1; side += 2)
                Mats.Prim(PrimitiveType.Cylinder, "Post", gate, new Vector3(side * half, 2.9f, 0f), new Vector3(0.28f, 2.9f, 0.28f), white);
            Mats.Prim(PrimitiveType.Cube, "Banner", gate, new Vector3(0f, 5.1f, 0f), new Vector3(2f * half, 1.15f, 0.16f), resources.Solid(banner, 0.2f));
            Mats.Prim(PrimitiveType.Cube, "Trim", gate, new Vector3(0f, 4.47f, 0f), new Vector3(2f * half, 0.12f, 0.2f), white);
            Mats.Prim(PrimitiveType.Cube, "Trim", gate, new Vector3(0f, 5.73f, 0f), new Vector3(2f * half, 0.12f, 0.2f), white);
            if (checkered)
            {
                // Damalı şerit (bitiş)
                var black = resources.Solid(new Color(0.12f, 0.12f, 0.14f));
                int cells = Mathf.RoundToInt(2f * half / 0.5f);
                for (int i = 0; i < cells; i++)
                for (int j = 0; j < 2; j++)
                    Mats.Prim(PrimitiveType.Cube, "Check", gate, new Vector3(-half + 0.25f + i * 0.5f, 6.05f + j * 0.5f, 0f),
                              new Vector3(0.5f, 0.5f, 0.16f), (i + j) % 2 == 0 ? black : white);
            }
            WorldView.MakeText(gate, new Vector3(0f, 5.1f, -0.1f), label, 0.11f, Color.white);
            for (int side = -1; side <= 1; side += 2)
                PlaceHeight(side < 0 ? "Racing/flagRed" : "Racing/flagGreen", new Vector3(gc + side * half, y + 5.8f + (checkered ? 1f : 0f), z), 1.8f, 0f);
        }

        void BuildStartArea()
        {
            // Tribünler ve çadırlar yamacın tepesinde; kuleler ve direkler yamacın eteğinde.
            float top = hw + BankTop + 4f;
            void At(string path, float x, float z, float height, float yaw) => PlaceHeight(path, new Vector3(x, GroundHeight(x, z), z), height, yaw);
            At("Racing/grandStand", -top, -8f, 5f, 90f);
            At("Racing/grandStandCovered", top, -8f, 6f, -90f);
            At("Racing/bannerTowerRed", -hw - 1.8f, 8f, 5.5f, 0f);
            At("Racing/bannerTowerGreen", hw + 1.8f, 8f, 5.5f, 0f);
            At("Racing/billboard", top - 1f, 22f, 5f, -25f);
            At("Racing/tentLong", -top + 1f, 18f, 3f, 90f);
            At("Racing/tent", -top + 2f, -26f, 3f, 20f);
            At("Racing/lightPostLarge", hw + 2.5f, -18f, 7f, 0f);
            At("Racing/lightPostLarge", -hw - 2.5f, -18f, 7f, 0f);
        }

        void BuildFinish()
        {
            float z = track.finishZ, y = track.Height(z);
            Gate(z, "FINISH", new Color(0.22f, 0.7f, 0.32f), true);
            for (int side = -1; side <= 1; side += 2)
            {
                float c = track.CenterX(z);
                float xf = c + side * (hw + 1f), xt = c + side * (hw + 4f), xs = c + side * (hw + BankTop + 5f);
                PlaceHeight("Racing/flagCheckers", new Vector3(xf, GroundHeight(xf, z - 3f), z - 3f), 4.5f, 0f);
                PlaceHeight("Racing/flagCheckers", new Vector3(xf, GroundHeight(xf, z + 3f), z + 3f), 4.5f, 0f);
                PlaceHeight(side < 0 ? "Racing/bannerTowerRed" : "Racing/bannerTowerGreen", new Vector3(xt, GroundHeight(xt, z + 8f), z + 8f), 6f, 0f);
                PlaceHeight("Racing/grandStand", new Vector3(xs, GroundHeight(xs, z + 4f), z + 4f), 5f, side < 0 ? 90f : -90f);
            }
        }

        // ---------------------------------------------------------------- bulutlar

        void BuildClouds()
        {
            var sphere = Mats.PrimitiveMesh(PrimitiveType.Sphere);
            var mat = resources.Solid(Color.white, 0f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.Lerp(theme.horizon, Color.white, 0.5f) * 0.45f);
            for (float z = track.startZ - 120f; z < track.EndZ + 220f; z += 32f)
            {
                if (R() > 0.65f) continue;
                float side = R() < 0.5f ? -1f : 1f;
                var c = new Vector3(side * R(20f, 230f), track.Height(z) + R(38f, 85f), z + R(0f, 30f));
                int puffs = 4 + rng.Next(4);
                float size = R(5f, 10f);
                for (int i = 0; i < puffs; i++)
                {
                    var offset = new Vector3(R(-1.6f, 1.6f) * size, R(-0.2f, 0.5f) * size, R(-0.8f, 0.8f) * size);
                    var scale = new Vector3(R(1f, 1.6f), R(0.6f, 0.9f), R(0.9f, 1.4f)) * size;
                    props.Add(sphere, mat, Matrix4x4.TRS(c + offset, Quaternion.identity, scale));
                }
            }
        }
    }
}
