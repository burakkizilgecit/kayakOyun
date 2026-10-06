using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Parkurun renkleri, ışığı, gökyüzü ve bitki örtüsü.
    public class TrackTheme
    {
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

        public Texture2D GroundTexture() => snowy ? TextureKit.Snow() : TextureKit.Grass();

        // Bitki örtüsü: pistin yanı (yakın), orta bant (ağaçlar), uzak bant
        public string[] near, mid, far;
        public Vector2 nearHeight, midHeight, farHeight;
        public float nearStep = 1.3f, nearChance = 0.6f, midStep = 6f, farStep = 12f;

        static Material skyTemplate;

        public static TrackTheme Get(int index)
        {
            switch (index)
            {
                case 1:   // Orman: sıcak ikindi ışığı, çamlar
                    return new TrackTheme
                    {
                        horizon = new Color(0.78f, 0.86f, 0.8f), skyTint = new Color(0.58f, 0.74f, 0.82f), skyGround = new Color(0.3f, 0.42f, 0.3f),
                        exposure = 1.4f, sunColor = new Color(1f, 0.86f, 0.66f), sunEuler = new Vector3(32f, -60f, 0f), sunIntensity = 1.05f,
                        ambientSky = new Color(0.55f, 0.65f, 0.68f), ambientEquator = new Color(0.45f, 0.5f, 0.42f), ambientGround = new Color(0.25f, 0.3f, 0.22f),
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
                        skyTop = new Color(0.4f, 0.46f, 0.76f), fogStart = 90f, fogEnd = 420f, contrast = 1.06f, saturation = 1.12f, vignette = 0.28f, gradeTint = new Color(1.03f, 0.98f, 0.93f),
                        grass = new Color(0.9f, 0.66f, 0.42f), grassB = new Color(0.85f, 0.6f, 0.38f), rock = new Color(0.74f, 0.4f, 0.26f),
                        trackA = new Color(0.9f, 0.72f, 0.5f), trackB = new Color(0.83f, 0.64f, 0.43f), kerb = new Color(0.16f, 0.62f, 0.64f),
                        water = new Color(0.25f, 0.62f, 0.7f, 0.82f), hillHeight = 10f, mountainHeight = 75f, terrace = 7f,
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
                        exposure = 1.3f, sunColor = new Color(1f, 0.95f, 0.86f), sunEuler = new Vector3(30f, -40f, 0f), sunIntensity = 1.0f,
                        ambientSky = new Color(0.72f, 0.8f, 0.94f), ambientEquator = new Color(0.68f, 0.72f, 0.82f), ambientGround = new Color(0.52f, 0.56f, 0.64f),
                        skyTop = new Color(0.36f, 0.56f, 0.9f), fogStart = 60f, fogEnd = 300f, contrast = 1.07f, saturation = 1.08f, vignette = 0.22f,
                        gradeTint = new Color(0.97f, 1f, 1.04f),
                        grass = new Color(0.94f, 0.96f, 0.99f), grassB = new Color(0.86f, 0.9f, 0.97f), rock = new Color(0.5f, 0.53f, 0.6f),
                        trackA = new Color(0.78f, 0.84f, 0.93f), trackB = new Color(0.72f, 0.79f, 0.9f), kerb = new Color(0.2f, 0.5f, 0.9f),
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
                default:  // Çayır: parlak öğle, meşeler, çiçekler
                    return new TrackTheme
                    {
                        horizon = new Color(0.8f, 0.9f, 0.98f), skyTint = new Color(0.62f, 0.76f, 1f), skyGround = new Color(0.45f, 0.6f, 0.4f),
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
                RenderSettings.skybox = sky;
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
                RenderSettings.skybox = sky;
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

        public void Build(Transform parent)
        {
            foreach (var kv in groups)
            {
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                var go = new GameObject("Props " + kv.Key.Item1 + " " + kv.Key.Item2.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = kv.Key.Item2;
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
        const float BankTop = 10f;
        // Vadi kesiti: tabandan yamaç tepesine sütunlar (taban kenarından uzaklık; eksi = taban içi)
        // Taban içi ~1,1 m aralıklı: yana kaymış tümsekler fizikteki yüzeyle aynı görünsün.
        static readonly float[] ValleyCols = { -6.5f, -5.4f, -4.3f, -3.2f, -2.2f, -1.1f, 0f, 0.8f, 1.7f, 2.7f, 3.8f, 5f, 6.3f, 8f, 10f };

        readonly TrackProfile track;
        readonly TrackTheme theme;
        readonly Transform root;
        readonly PropBatcher props = new PropBatcher();
        readonly System.Random rng;
        readonly float hw, seedX, seedZ;

        public static void Build(Transform root, TrackProfile track, TrackTheme theme) => new Scenery(root, track, theme).BuildAll();

        Scenery(Transform root, TrackProfile track, TrackTheme theme)
        {
            this.root = root;
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
            BuildSurfaces();
            BuildTerrain();
            BuildWater();
            BuildVegetation();
            BuildObstacles();
            BuildStartArea();
            BuildFinish();
            BuildClouds();
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
            float n = Mathf.PerlinNoise(x * 0.018f + seedX, z * 0.018f + seedZ) * 0.8f
                    + Mathf.PerlinNoise(x * 0.06f + 31f, z * 0.06f + 17f) * 0.35f;
            h += Smooth(BankTop, BankTop + 45f, d) * theme.hillHeight * n;
            float m = Smooth(80f, 170f, d) * theme.mountainHeight * Mathf.PerlinNoise(x * 0.007f + 5f, z * 0.007f + 9f + seedZ);
            if (theme.terrace > 0f) m = Mathf.Floor(m / theme.terrace) * theme.terrace;
            return h + m;
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
            var mat = Textured(Mats.Solid(theme.grass, 0.06f), theme.GroundTexture());
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
                        float x = c + side * (hw + off);
                        if (j == 0) x = c + side * 0f;   // orta çizgi
                        verts.Add(new Vector3(x, track.Height(x, z), z));
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
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
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
            var mat = Textured(Mats.Solid(theme.trackA, 0.1f), TextureKit.Dirt());
            float first = track.startZ, last = track.EndZ;
            const float step = 0.5f;
            for (float c0 = first; c0 < last; c0 += 150f)
            {
                float c1 = Mathf.Min(last, c0 + 150f);
                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();
                for (float z = c0; z <= c1 + 0.01f; z += step)
                {
                    float px = track.PathX(z);
                    float dir = (track.PathX(z + 0.5f) - track.PathX(z - 0.5f));   // kıvrımda genişlik sabit kalsın
                    float w = TrackProfile.PathHalfWidth * Mathf.Sqrt(1f + dir * dir);
                    float wob = 0.12f * Mathf.Sin(z * 0.9f) + 0.06f * Mathf.Sin(z * 2.3f);   // kenar hafif düzensiz (yumuşak dalga)
                    float xa = px - w - wob, xb = px + w + wob;
                    // Şerit enine 4 parça: tümseklerin üstünde zemini izler (gömülmez, havada kalmaz).
                    for (int k = 0; k <= PathSegs; k++)
                    {
                        float x = Mathf.Lerp(xa, xb, k / (float)PathSegs);
                        verts.Add(new Vector3(x, track.Height(x, z) + 0.025f, z));
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
                var mesh = new Mesh();
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject("Path " + c0);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// Su, çamur ve buz lekeleri: yumuşak loblu düzgün ovaller, araziyi izler (fizikteki kenarla aynı).
        /// Su ve çamur: altındaki zeminin (çimen/patika) koyu tonunda ince kıyı bandı + açık renkli ince kenar parlaması
        /// + kenarda birkaç çakıl. Buz: ince beyaz don kenarı; yanında birkaç yumuşak, kabarık kar tepeciği.
        void BuildSurfaces()
        {
            if (track.surfaces.Count == 0) return;
            var puddle = Mats.Water(new Color(0.3f, 0.45f, 0.55f, 0.85f));
            var mud = Mats.Water(new Color(0.33f, 0.22f, 0.13f, 0.97f));
            mud.SetFloat("_Glossiness", 0.7f);
            var foam = Mats.Solid(new Color(0.78f, 0.9f, 0.96f), 0.6f);
            var mudRim = Mats.Solid(new Color(0.5f, 0.36f, 0.24f), 0.35f);
            var shoreGrass = Mats.Solid(Color.Lerp(theme.grass, Color.black, theme.snowy ? 0.12f : 0.28f), 0.2f);
            var shorePath = Mats.Solid(Color.Lerp(theme.trackA, Color.black, 0.3f), 0.3f);
            var ice = Mats.Solid(new Color(0.7f, 0.86f, 1f), 0.95f);
            ice.mainTexture = FrostTexture();
            var frost = Mats.Solid(new Color(0.95f, 0.97f, 1f), 0.3f);
            var snow = Textured(Mats.Solid(new Color(0.97f, 0.98f, 1f), 0.15f), TextureKit.Snow());
            foreach (var zone in track.surfaces)
            {
                // Lekenin ortası patikada mı (kıyı rengi altındaki zemine uysun)?
                float pz = zone.cz, px = (zone.onPath ? track.PathX(pz) : track.CenterX(pz)) + zone.cx;
                bool overPath = zone.onPath || Mathf.Abs(px - track.PathX(pz)) < TrackProfile.PathHalfWidth;
                switch (zone.type)
                {
                    case Surface.Puddle:
                    case Surface.Mud:
                        bool isMud = zone.type == Surface.Mud;
                        Blob(zone, 1.07f, 0.03f, overPath ? shorePath : shoreGrass, "Shore");
                        Blob(zone, 1f, 0.045f, isMud ? mud : puddle, isMud ? "Mud" : "Puddle");
                        Ring(zone, 0.9f, 1f, 0.05f, isMud ? mudRim : foam, "Rim");
                        for (int k = 0; k < 2 + rng.Next(3); k++)
                        {
                            float a = R(0f, 6.28f);
                            PlaceSurfaceProp(zone, a, zone.Edge(a) * R(1.05f, 1.12f), "Nature/rock_smallA", R(0.04f, 0.08f));   // çakıl
                        }
                        break;
                    case Surface.Ice:
                        Blob(zone, 1f, 0.045f, ice, "Ice");
                        Ring(zone, 0.93f, 1.05f, 0.05f, frost, "Frost Rim");
                        if (theme.snowy) break;   // karlı pistte zemin zaten kar
                        // Buzun yanında birkaç yumuşak kar tepeciği
                        for (int k = 0; k < 2 + rng.Next(2); k++)
                        {
                            float a = R(0f, 6.28f), r = zone.Edge(a) * R(1.15f, 1.35f);
                            var mound = SurfaceZone.Blob(Surface.Ice, zone.cz + Mathf.Cos(a) * r * zone.rz, zone.cx + Mathf.Sin(a) * r * zone.rx,
                                                         R(0.7f, 1.3f), R(0.6f, 1.1f), R(0f, 50f), zone.onPath);
                            Blob(mound, 1f, 0.03f, snow, "Snow Mound", R(0.12f, 0.22f));
                        }
                        break;
                }
            }
        }

        /// Lekenin kenarına kadar uzanan, araziyi izleyen dairesel ağ. scale: kenarı büyütür (kıyı bandı için);
        /// dome: ortanın kenara göre ne kadar kabarık olduğu (kar tepeciği).
        void Blob(SurfaceZone zone, float scale, float lift, Material mat, string name, float dome = 0f)
        {
            const int rings = 6, sectors = 40;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            verts.Add(SurfacePoint(zone, 0f, 0f, lift + dome));
            for (int r = 1; r <= rings; r++)
                for (int j = 0; j < sectors; j++)
                {
                    float a = j * 6.2832f / sectors, f = r / (float)rings;
                    float k = scale * zone.Edge(a) * f;
                    verts.Add(SurfacePoint(zone, Mathf.Cos(a) * k, Mathf.Sin(a) * k, lift + dome * (1f - f * f)));
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
            SurfaceMesh(verts, tris, mat, name);
        }

        /// Lekenin kenarı boyunca ince şerit (inner..outer, yarıçap biriminde): kenar parlaması ya da don kenarı.
        void Ring(SurfaceZone zone, float inner, float outer, float lift, Material mat, string name)
        {
            const int sectors = 40;
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
            return new Vector3(x, track.Height(x, z) + lift, z);
        }

        void SurfaceMesh(List<Vector3> verts, List<int> tris, Material mat, string name)
        {
            var uvs = new List<Vector2>();
            foreach (var v in verts) uvs.Add(new Vector2(v.x * 0.35f, v.z * 0.35f));
            var mesh = new Mesh();
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

        /// Buz: açık zemin üstünde ince, dallanan don çatlakları ve parlak benekler.
        static Texture2D FrostTexture()
        {
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Frost", anisoLevel = 4 };
            var px = new Color[N * N];
            var rnd = new System.Random(7);
            for (int i = 0; i < px.Length; i++) px[i] = Color.white * (0.92f + 0.05f * (float)rnd.NextDouble());
            // Rastgele yürüyüşle dallanan çatlaklar
            for (int k = 0; k < 14; k++)
            {
                float x = rnd.Next(N), y = rnd.Next(N), dir = (float)rnd.NextDouble() * 6.28f;
                int len = 20 + rnd.Next(50);
                for (int j = 0; j < len; j++)
                {
                    dir += ((float)rnd.NextDouble() - 0.5f) * 0.7f;
                    x = Mathf.Repeat(x + Mathf.Cos(dir), N);
                    y = Mathf.Repeat(y + Mathf.Sin(dir), N);
                    px[(int)y * N + (int)x] = Color.white * 0.78f;
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        // ---------------------------------------------------------------- arazi

        void BuildTerrain()
        {
            const float step = 2.5f;
            const int rowsPerChunk = 60;
            float z0 = track.startZ - 200f, z1 = track.EndZ + 140f;
            int rows = Mathf.CeilToInt((z1 - z0) / step);
            var mats = new[] { Textured(Mats.Solid(theme.grass, 0.05f), theme.GroundTexture()), Textured(Mats.Solid(theme.grassB, 0.05f), theme.GroundTexture()),
                               Textured(Mats.Solid(theme.rock, 0.1f), TextureKit.Rock()) };

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

                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
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
            var mat = Mats.Water(theme.water);
            foreach (var w in track.water)
            {
                const float half = 230f;
                var mesh = new Mesh();
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

        void BuildObstacles()
        {
            string[] rocks = { "Nature/rock_largeA", "Nature/rock_largeB", "Nature/rock_largeC", "Nature/rock_largeD", "Nature/stone_largeA",
                               "Nature/stone_largeB", "Nature/stone_largeC" };
            string[] crates = { "Platformer/crate", "Platformer/crate-strong", "Platformer/barrel" };
            foreach (var o in track.obstacles)
            {
                float y = track.Height(o.x, o.z);
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
            var white = Mats.Solid(Color.white, 0.3f);
            for (int side = -1; side <= 1; side += 2)
                Mats.Prim(PrimitiveType.Cylinder, "Post", gate, new Vector3(side * half, 2.9f, 0f), new Vector3(0.28f, 2.9f, 0.28f), white);
            Mats.Prim(PrimitiveType.Cube, "Banner", gate, new Vector3(0f, 5.1f, 0f), new Vector3(2f * half, 1.15f, 0.16f), Mats.Solid(banner, 0.2f));
            Mats.Prim(PrimitiveType.Cube, "Trim", gate, new Vector3(0f, 4.47f, 0f), new Vector3(2f * half, 0.12f, 0.2f), white);
            Mats.Prim(PrimitiveType.Cube, "Trim", gate, new Vector3(0f, 5.73f, 0f), new Vector3(2f * half, 0.12f, 0.2f), white);
            if (checkered)
            {
                // Damalı şerit (bitiş)
                var black = Mats.Solid(new Color(0.12f, 0.12f, 0.14f));
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
            Gate(z, "BİTİŞ", new Color(0.22f, 0.7f, 0.32f), true);
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
            var mat = Mats.Solid(Color.white, 0f);
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
