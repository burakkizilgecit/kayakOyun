using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Pistteki coin: ince (kalınlık çapın ~%12'si) altın para. Kenarı pahlı, dış halkası kabarık, ortası hafif
    /// çukur ve iki yüzünde kabartma yıldız. Çap 1, yüzler ±Z'ye bakar (Y ekseninde döner). Tek mesh, iki malzeme:
    /// 0 = gövde altını, 1 = yıldız (daha açık, parlak).
    public static class CoinModel
    {
        const float R = 0.5f;          // dış yarıçap
        const float Edge = 0.034f;     // kenar bandının yarı kalınlığı
        const float RimTop = 0.06f;    // halka tepesinin yüzeyden yüksekliği (yarı kalınlık)
        const float Field = 0.036f;    // ortadaki çukur alanın yüksekliği
        const float StarTop = 0.062f;  // yıldızın tepesi
        const int Sides = 48;

        static Mesh mesh;
        static Material[] materials;

        public static Mesh Mesh => mesh != null ? mesh : mesh = Build();

        public static Material[] Materials
        {
            get
            {
                if (materials != null) return materials;
                var gold = Mats.Solid(new Color(1f, 0.74f, 0.16f), 0.78f);
                gold.SetFloat("_Metallic", 0.35f);
                gold.EnableKeyword("_EMISSION");
                gold.SetColor("_EmissionColor", new Color(0.55f, 0.32f, 0.02f) * 0.55f);   // gölgede de sıcak parlasın
                var star = Mats.Solid(new Color(1f, 0.9f, 0.45f), 0.9f);
                star.SetFloat("_Metallic", 0.25f);
                star.EnableKeyword("_EMISSION");
                star.SetColor("_EmissionColor", new Color(0.75f, 0.5f, 0.08f) * 0.6f);
                return materials = new[] { gold, star };
            }
        }

        /// Sahnede bir coin: paylaşılan mesh ve malzemeler, verilen çapta.
        public static GameObject Create(Transform parent, float diameter)
        {
            var go = new GameObject("Coin");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * diameter;
            go.AddComponent<MeshFilter>().sharedMesh = Mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = Materials;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static Mesh Build()
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var body = new List<int>();
            var star = new List<int>();

            // Gövde: profil (yarıçap, z) boyunca torna. Her profil parçası ayrı şerit: köşeler keskin,
            // çevre boyunca yumuşak gölgelenir.
            var profile = new[]
            {
                new Vector2(0f, Field), new Vector2(0.37f, Field),          // çukur alan
                new Vector2(0.385f, RimTop), new Vector2(0.455f, RimTop),   // iç duvar, halka tepesi
                new Vector2(R, Edge), new Vector2(R, -Edge),                // pah, kenar bandı
                new Vector2(0.455f, -RimTop), new Vector2(0.385f, -RimTop),
                new Vector2(0.37f, -Field), new Vector2(0f, -Field),
            };
            for (int p = 0; p < profile.Length - 1; p++)
            {
                Vector2 a = profile[p], b = profile[p + 1];
                Vector2 d = b - a;
                // Profil saat yönünde dolaşır (önde dışa, kenarda aşağı, arkada içe): dışa bakan normal yönün solu.
                var pn = new Vector2(-d.y, d.x).normalized;
                int start = v.Count;
                for (int i = 0; i <= Sides; i++)
                {
                    float ang = i * Mathf.PI * 2f / Sides;
                    float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
                    v.Add(new Vector3(c * a.x, s * a.x, a.y));
                    v.Add(new Vector3(c * b.x, s * b.x, b.y));
                    var nn = new Vector3(c * pn.x, s * pn.x, pn.y);
                    n.Add(nn);
                    n.Add(nn);
                }
                for (int i = 0; i < Sides; i++)
                {
                    int k = start + i * 2;
                    Tri(body, v, n, k, k + 1, k + 3);
                    Tri(body, v, n, k, k + 3, k + 2);
                }
            }

            // Kabartma yıldız: iki yüzde, çukur alandan StarTop'a yükselen beş köşeli yıldız.
            for (int side = 0; side < 2; side++)
            {
                float sg = side == 0 ? 1f : -1f;
                var outline = new List<Vector3>();
                for (int i = 0; i < 10; i++)
                {
                    float ang = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                    float r = i % 2 == 0 ? 0.25f : 0.105f;
                    outline.Add(new Vector3(Mathf.Cos(ang) * r * sg, Mathf.Sin(ang) * r, 0f));
                }
                // Üst yüz: hafif kubbeli (merkez biraz yüksek), düz normalli parçalar ışığı yıldız gibi kırar.
                int centre = v.Count;
                v.Add(new Vector3(0f, 0f, (StarTop + 0.008f) * sg));
                n.Add(new Vector3(0f, 0f, sg));
                for (int i = 0; i < 10; i++)
                {
                    var a = outline[i] + new Vector3(0f, 0f, StarTop * sg);
                    var b = outline[(i + 1) % 10] + new Vector3(0f, 0f, StarTop * sg);
                    var fn = Vector3.Cross(a - v[centre], b - v[centre]).normalized;
                    if (fn.z * sg < 0f) fn = -fn;
                    int k = v.Count;
                    v.Add(v[centre]); v.Add(a); v.Add(b);
                    n.Add(fn); n.Add(fn); n.Add(fn);
                    Tri(star, v, n, k, k + 1, k + 2);
                }
                // Yan duvarlar
                for (int i = 0; i < 10; i++)
                {
                    var a = outline[i];
                    var b = outline[(i + 1) % 10];
                    var lo = new Vector3(0f, 0f, Field * sg);
                    var hi = new Vector3(0f, 0f, StarTop * sg);
                    var wn = Vector3.Cross(b - a, Vector3.forward * sg).normalized;
                    if (Vector3.Dot(wn, (a + b) * 0.5f) < 0f) wn = -wn;
                    int k = v.Count;
                    v.Add(a + lo); v.Add(b + lo); v.Add(b + hi); v.Add(a + hi);
                    n.Add(wn); n.Add(wn); n.Add(wn); n.Add(wn);
                    Tri(star, v, n, k, k + 1, k + 2);
                    Tri(star, v, n, k, k + 2, k + 3);
                }
            }

            var m = new Mesh { name = "Coin" };
            m.SetVertices(v);
            m.SetNormals(n);
            m.subMeshCount = 2;
            m.SetTriangles(body, 0);
            m.SetTriangles(star, 1);
            m.RecalculateBounds();
            return m;
        }

        /// Üçgeni, yüzü normalin baktığı yöne dönecek sırayla ekler (Unity: saat yönü önden görünür).
        static void Tri(List<int> tris, List<Vector3> v, List<Vector3> n, int a, int b, int c)
        {
            var face = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(face, n[a] + n[b] + n[c]) < 0f) { tris.Add(a); tris.Add(c); tris.Add(b); }
            else { tris.Add(a); tris.Add(b); tris.Add(c); }
        }
    }
}
