using UnityEngine;

namespace SledSurfers
{
    /// Basit malzeme ve primitif yardımcıları. Malzemeler Resources'taki şablonlardan
    /// kopyalanır, böylece Standard shader build'e dahil olur.
    public static class Mats
    {
        public const int GlassLayer = 4;   // Unity'nin hazır "Water" katmanı

        static Material solidTemplate;
        static Material glassTemplate;

        public static Material Solid(Color color, float gloss = 0.15f)
        {
            if (solidTemplate == null)
            {
                solidTemplate = Resources.Load<Material>("BaseMat");
                if (solidTemplate == null) solidTemplate = new Material(Shader.Find("Standard"));
            }
            var m = new Material(solidTemplate) { color = color };
            m.SetFloat("_Glossiness", gloss);
            return m;
        }

        static Material waterTemplate;
        static Texture2D ripples;

        /// Dalgacıklı su: şeffaf, çok parlak (gökyüzünü yansıtır), kayan normal haritası (bkz. WaterAnimator).
        public static Material Water(Color color)
        {
            if (waterTemplate == null) waterTemplate = Resources.Load<Material>("Env/Water");
            var m = waterTemplate != null ? new Material(waterTemplate) : Glass();
            m.color = color;
            m.SetFloat("_Glossiness", 0.96f);
            if (ripples == null) ripples = RippleNormals();
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_BumpMap", ripples);
            m.SetFloat("_BumpScale", 0.55f);
            WaterAnimator.Register(m);
            return m;
        }

        /// Üst üste binen halka dalgacıkların yükseklik alanından normal haritası (döşenebilir).
        static Texture2D RippleNormals()
        {
            const int N = 128;
            var h = new float[N * N];
            var rnd = new System.Random(11);
            var centers = new Vector3[14];
            for (int k = 0; k < centers.Length; k++)
                centers[k] = new Vector3((float)rnd.NextDouble(), (float)rnd.NextDouble(), 6f + 10f * (float)rnd.NextDouble());
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N, sum = 0f;
                    foreach (var c in centers)
                    {
                        // Döşenebilir olsun diye en yakın kopyaya uzaklık
                        float dx = Mathf.Abs(u - c.x), dy = Mathf.Abs(v - c.y);
                        dx = Mathf.Min(dx, 1f - dx);
                        dy = Mathf.Min(dy, 1f - dy);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        sum += Mathf.Sin(d * c.z * Mathf.PI * 2f) * Mathf.Exp(-d * 4f);
                    }
                    h[y * N + x] = sum;
                }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, name = "Ripples" };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float hx = h[y * N + (x + 1) % N] - h[y * N + (x + N - 1) % N];
                    float hy = h[((y + 1) % N) * N + x] - h[((y + N - 1) % N) * N + x];
                    var n = new Vector3(-hx * 0.6f, -hy * 0.6f, 1f).normalized;
                    px[y * N + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        public static Material Glass()
        {
            if (glassTemplate == null) glassTemplate = Resources.Load<Material>("GlassMat");
            return glassTemplate != null ? new Material(glassTemplate) : Solid(new Color(0.85f, 0.95f, 1f), 0.9f);
        }

        public static Transform Prim(PrimitiveType type, string name, Transform parent,
                                     Vector3 localPos, Vector3 localScale, Material mat,
                                     Quaternion? localRot = null, int layer = 0)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.layer = layer;
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = localRot ?? Quaternion.identity;
            t.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return t;
        }

        /// Silindiri iki yerel nokta arasına yerleştirir (kol, bacak gibi).
        public static void PlaceBetween(Transform cylinder, Vector3 a, Vector3 b, float thickness)
        {
            Vector3 d = b - a;
            cylinder.localPosition = (a + b) * 0.5f;
            cylinder.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            cylinder.localScale = new Vector3(thickness, d.magnitude * 0.5f, thickness);
        }

        public static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return mesh;
        }
    }
}
