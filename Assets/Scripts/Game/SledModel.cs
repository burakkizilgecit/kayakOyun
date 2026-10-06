using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Sörf tahtalı kızak: kalkık burunlu, desenli bir tahta ve altında ucu kıvrık boru raylar.
    /// Tüm parçalar koddan üretilir; raylar dışarıdan verilen malzemeyi kullanır (kızak altı yükseltmesi rengini değiştirir).
    public static class SledModel
    {
        public const float Length = 1.9f;
        public const float HalfWidth = 0.36f;
        const float HalfThick = 0.035f;
        const float CenterY = 0.235f;   // tahtanın orta yüksekliği; üstü DeckTop (0.27)
        const float RunnerY = 0.06f, RunnerX = 0.24f, RunnerRadius = 0.028f;

        static readonly AnimationCurve Width = Smooth(new AnimationCurve(
            new Keyframe(0f, 0.25f), new Keyframe(0.12f, 0.31f), new Keyframe(0.42f, 0.36f),
            new Keyframe(0.7f, 0.32f), new Keyframe(0.86f, 0.22f), new Keyframe(0.95f, 0.12f), new Keyframe(1f, 0f)));

        /// Kızak aşamalarının boyaları: zemin, kenar bandı + orta çizgi, iç şerit, dış şerit, burun-kuyruk.
        /// Dalga: burun/kuyruk sınırının dalgası (genlik, sıklık) — alevli boyada sivri ve sık.
        struct Livery
        {
            public Color field, rail, stripeA, stripeB, tips;
            public float waveAmp, waveFreq;
        }

        static readonly Livery[] Liveries =
        {
            // 0: başlangıç — turkuaz, mercan uçlar
            new Livery { field = new Color(0.07f, 0.68f, 0.74f), rail = new Color(1f, 0.97f, 0.9f), stripeA = new Color(1f, 0.5f, 0.15f),
                         stripeB = new Color(1f, 0.82f, 0.2f), tips = new Color(0.98f, 0.33f, 0.36f), waveAmp = 0.025f, waveFreq = 4f },
            // 1: alevli kırmızı
            new Livery { field = new Color(0.88f, 0.16f, 0.14f), rail = new Color(1f, 0.97f, 0.9f), stripeA = new Color(1f, 0.85f, 0.2f),
                         stripeB = Color.white, tips = new Color(1f, 0.6f, 0.08f), waveAmp = 0.06f, waveFreq = 14f },
            // 2: neon mor
            new Livery { field = new Color(0.42f, 0.2f, 0.78f), rail = new Color(0.2f, 0.92f, 1f), stripeA = new Color(0.2f, 0.92f, 1f),
                         stripeB = Color.white, tips = new Color(1f, 0.3f, 0.72f), waveAmp = 0.035f, waveFreq = 8f },
            // 3: siyah-altın şampiyon
            new Livery { field = new Color(0.1f, 0.1f, 0.12f), rail = new Color(1f, 0.78f, 0.22f), stripeA = new Color(1f, 0.78f, 0.22f),
                         stripeB = new Color(1f, 0.93f, 0.6f), tips = new Color(1f, 0.78f, 0.22f), waveAmp = 0.02f, waveFreq = 6f },
        };

        public static int LiveryCount => Liveries.Length;
        public static Color StripeColor(int livery) => Liveries[Mathf.Clamp(livery, 0, Liveries.Length - 1)].stripeA;
        public static Color FieldColor(int livery) => Liveries[Mathf.Clamp(livery, 0, Liveries.Length - 1)].field;

        static readonly Texture2D[] boardTextures = new Texture2D[4];

        /// Aşamaya göre değişen parçalar.
        public class Parts
        {
            public Material top, spoilerMat;
            public Transform spoiler;
            int livery = -1;

            /// stage 0..5 (kızak seviyesi): tahta boyası her iki seviyede bir yenilenir; 3. seviyeden sonra spoiler.
            public void SetStage(int stage)
            {
                int l = Mathf.Clamp(stage <= 1 ? 0 : stage <= 3 ? 1 : stage == 4 ? 2 : 3, 0, Liveries.Length - 1);
                spoiler.gameObject.SetActive(stage >= 3);
                if (l == livery) return;
                livery = l;
                if (boardTextures[l] == null) boardTextures[l] = BoardTexture(Liveries[l]);
                top.mainTexture = boardTextures[l];
                spoilerMat.color = Liveries[l].stripeA;
            }
        }

        static AnimationCurve Smooth(AnimationCurve c)
        {
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        static float Sq(float x) => x * x;

        /// t: kuyruktan (0) buruna (1). Burun yukarı kalkar, kuyruk hafifçe kalkar.
        static float Rocker(float t) => 0.2f * Sq(Mathf.Max(0f, (t - 0.68f) / 0.32f)) + 0.035f * Sq(Mathf.Max(0f, (0.1f - t) / 0.1f));

        /// Burun ucundaki ip halkasının yeri (kızak koordinatı).
        public static Vector3 NoseEye => new Vector3(0f, CenterY + Rocker(0.95f) + HalfThick, Length * 0.45f);

        public static Parts Build(Transform parent, Material runnerMat)
        {
            var parts = new Parts();
            var board = new GameObject("Board").transform;
            board.SetParent(parent, false);
            board.gameObject.AddComponent<MeshFilter>().sharedMesh = BoardMesh();
            parts.top = Mats.Solid(Color.white, 0.45f);
            board.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[] { parts.top, Mats.Solid(new Color(1f, 0.93f, 0.82f), 0.3f) };

            // Spoiler (2. aşamadan sonra): kuyrukta iki ayak üstünde, hafif geriye yatık kanatçık ve yan plakalar.
            parts.spoilerMat = Mats.Solid(Color.white, 0.6f);
            var dark = Mats.Solid(new Color(0.15f, 0.15f, 0.18f), 0.5f);
            parts.spoiler = new GameObject("Spoiler").transform;
            parts.spoiler.SetParent(parent, false);
            float tailY = CenterY + Rocker(0.06f) + HalfThick;
            parts.spoiler.localPosition = new Vector3(0f, tailY, -Length * 0.44f);
            for (int s = -1; s <= 1; s += 2)
                Mats.Prim(PrimitiveType.Cube, "Spoiler Leg", parts.spoiler, new Vector3(0.16f * s, 0.13f, 0.02f),
                          new Vector3(0.035f, 0.26f, 0.08f), dark, Quaternion.Euler(-12f, 0f, 0f));
            Mats.Prim(PrimitiveType.Cube, "Spoiler Wing", parts.spoiler, new Vector3(0f, 0.27f, -0.02f),
                      new Vector3(0.66f, 0.035f, 0.2f), parts.spoilerMat, Quaternion.Euler(-10f, 0f, 0f));
            for (int s = -1; s <= 1; s += 2)
                Mats.Prim(PrimitiveType.Cube, "Spoiler Plate", parts.spoiler, new Vector3(0.335f * s, 0.27f, -0.02f),
                          new Vector3(0.02f, 0.12f, 0.24f), dark);
            parts.SetStage(0);

            // Raylar: düz alt kısım, önde yukarı ve geriye kıvrılan uç.
            for (int s = -1; s <= 1; s += 2)
            {
                float x = RunnerX * s;
                var pts = new List<Vector3> { new Vector3(x, RunnerY + 0.05f, -0.86f), new Vector3(x, RunnerY, -0.72f) };
                for (float z = -0.6f; z < 0.7f; z += 0.2f) pts.Add(new Vector3(x, RunnerY, z));
                var c = new Vector3(x, RunnerY + 0.1f, 0.74f);
                for (int k = 0; k <= 10; k++)
                {
                    float a = Mathf.Lerp(-90f, 120f, k / 10f) * Mathf.Deg2Rad;
                    pts.Add(c + new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a)) * 0.1f);
                }
                var rail = new GameObject("Runner").transform;
                rail.SetParent(parent, false);
                rail.gameObject.AddComponent<MeshFilter>().sharedMesh = Tube(pts, RunnerRadius, 10);
                rail.gameObject.AddComponent<MeshRenderer>().sharedMaterial = runnerMat;

                foreach (float z in new[] { -0.55f, 0.05f, 0.55f })
                {
                    float t = (z + Length * 0.5f) / Length;
                    float bottom = CenterY + Rocker(t) - HalfThick;
                    var strut = Mats.Prim(PrimitiveType.Cylinder, "Strut", parent, Vector3.zero, Vector3.one, runnerMat);
                    Mats.PlaceBetween(strut, new Vector3(x, RunnerY, z), new Vector3(x * 0.9f, bottom + 0.01f, z), 0.035f);
                }
            }
            return parts;
        }

        static Mesh BoardMesh()
        {
            const int N = 60, K = 20;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var topTris = new List<int>();
            var bottomTris = new List<int>();

            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N;
                float w = Mathf.Max(Width.Evaluate(t), 0.002f);
                float h = HalfThick * (1f - 0.8f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, t)));
                float y0 = CenterY + Rocker(t), z = (t - 0.5f) * Length;
                for (int k = 0; k < K; k++)
                {
                    float a = 2f * Mathf.PI * k / K;
                    float cx = Mathf.Cos(a), sy = Mathf.Sin(a);
                    float x = w * Mathf.Sign(cx) * Mathf.Sqrt(Mathf.Abs(cx));
                    float y = h * Mathf.Sign(sy) * Mathf.Sqrt(Mathf.Abs(sy));
                    if (sy > 0f) y += 0.012f * (1f - Sq(x / w)) * (w / HalfWidth);   // üstte hafif kubbe
                    verts.Add(new Vector3(x, y0 + y, z));
                    uvs.Add(new Vector2(0.5f + x / (2f * HalfWidth), t));
                }
            }
            for (int i = 0; i < N; i++)
                for (int k = 0; k < K; k++)
                {
                    int k2 = (k + 1) % K;
                    int a = i * K + k, b = i * K + k2, c = (i + 1) * K + k, d = (i + 1) * K + k2;
                    float mid = Mathf.Sin(2f * Mathf.PI * (k + 0.5f) / K);
                    var list = mid > 0f ? topTris : bottomTris;
                    list.AddRange(new[] { a, b, c, b, d, c });
                }

            // Kuyruk kapağı (ayrı köşeler: keskin kenar)
            int center = verts.Count;
            verts.Add(new Vector3(0f, CenterY + Rocker(0f), -0.5f * Length));
            uvs.Add(new Vector2(0.5f, 0f));
            for (int k = 0; k < K; k++)
            {
                verts.Add(verts[k]);
                uvs.Add(uvs[k]);
            }
            for (int k = 0; k < K; k++)
                bottomTris.AddRange(new[] { center, center + 1 + (k + 1) % K, center + 1 + k });

            var mesh = new Mesh { name = "Surf Board" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(topTris, 0);
            mesh.SetTriangles(bottomTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// Tahtanın üst deseni: turkuaz zemin, krem kenar bandı ve orta çizgi, turuncu-sarı yarış şeritleri,
        /// dalga kenarlı mercan burun ve kuyruk.
        static Texture2D BoardTexture(Livery l)
        {
            const int W = 128, H = 512;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, anisoLevel = 4, name = "Board" };
            var px = new Color[W * H];
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    Color sum = Color.clear;
                    for (int sj = 0; sj < 2; sj++)
                        for (int si = 0; si < 2; si++)
                            sum += Pattern(l, (i + 0.25f + 0.5f * si) / W, (j + 0.25f + 0.5f * sj) / H);
                    px[j * W + i] = sum * 0.25f;
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        static Color Pattern(Livery l, float u, float v)
        {
            float edge = Mathf.Max(Width.Evaluate(v) / HalfWidth, 0.02f);
            float dn = Mathf.Abs(u - 0.5f) * 2f / edge;   // 0 = orta çizgi, 1 = kenar
            if (dn > 0.88f) return l.rail;
            // Alev/dalga: sinüsün mutlak değeri sivri uçlar verir.
            float wave = l.waveAmp * (l.waveFreq > 10f ? 1f - 2f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * l.waveFreq * 0.5f)) : Mathf.Sin(u * Mathf.PI * l.waveFreq));
            if (v > 0.8f + wave || v < 0.1f - wave) return dn < 0.03f ? l.rail : l.tips;
            if (dn < 0.03f) return l.rail;
            if (dn > 0.62f && dn < 0.72f) return l.stripeA;
            if (dn > 0.75f && dn < 0.8f) return l.stripeB;
            return l.field;
        }

        /// Kanat: kökten uca sivrilen, geriye açılı, hafif yukarı kalkık (dihedral) ince levha.
        /// side +1 sağ, -1 sol; mesh kanadın kökünde başlar, ölçüler metre cinsinden.
        public static Mesh WingMesh(float span, float chord, float side)
        {
            var outline = new[]
            {
                new Vector2(0f, 0.5f * chord), new Vector2(span * 0.96f, 0.22f * chord), new Vector2(span, 0.08f * chord),
                new Vector2(span, -0.14f * chord), new Vector2(span * 0.94f, -0.28f * chord), new Vector2(0f, -0.5f * chord),
            };
            var verts = new List<Vector3>();
            var tris = new List<int>();
            Vector3 P(Vector2 o, float sgn)
            {
                float u = o.x / span;
                float half = Mathf.Lerp(0.028f, 0.012f, u);
                return new Vector3(o.x * side, sgn * half + 0.08f * o.x, o.y);
            }
            void Face(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                tris.AddRange(new[] { i, i + 1, i + 2 });
            }
            int n = outline.Length;
            for (int k = 1; k < n - 1; k++)
            {
                // Üst ve alt yüzler (yelpaze), yan yüzler; sol kanatta sıra ters çevrilir.
                if (side > 0f)
                {
                    Face(P(outline[0], 1), P(outline[k], 1), P(outline[k + 1], 1));
                    Face(P(outline[0], -1), P(outline[k + 1], -1), P(outline[k], -1));
                }
                else
                {
                    Face(P(outline[0], 1), P(outline[k + 1], 1), P(outline[k], 1));
                    Face(P(outline[0], -1), P(outline[k], -1), P(outline[k + 1], -1));
                }
            }
            for (int k = 0; k < n; k++)
            {
                var a = outline[k];
                var b = outline[(k + 1) % n];
                Vector3 at = P(a, 1), ab = P(a, -1), bt = P(b, 1), bb = P(b, -1);
                if (side > 0f) { Face(at, ab, bt); Face(bt, ab, bb); }
                else { Face(at, bt, ab); Face(bt, bb, ab); }
            }
            var mesh = new Mesh { name = "Wing" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// Roket gövdesi: (yarıçap, z) profilinin z ekseni etrafında döndürülmesi. Burun ve orta bant kırmızı (alt mesh 1).
        public static Mesh RocketMesh()
        {
            var profile = new[]
            {
                new Vector2(0.001f, -0.2f), new Vector2(0.05f, -0.2f), new Vector2(0.062f, -0.16f), new Vector2(0.07f, -0.14f),
                new Vector2(0.07f, -0.02f), new Vector2(0.071f, 0f), new Vector2(0.071f, 0.04f), new Vector2(0.07f, 0.06f),
                new Vector2(0.07f, 0.12f), new Vector2(0.064f, 0.17f), new Vector2(0.05f, 0.215f), new Vector2(0.028f, 0.25f),
                new Vector2(0.001f, 0.27f),
            };
            const int sides = 14;
            var verts = new List<Vector3>();
            var white = new List<int>();
            var red = new List<int>();
            for (int i = 0; i < profile.Length; i++)
                for (int k = 0; k <= sides; k++)
                {
                    float a = 2f * Mathf.PI * k / sides;
                    verts.Add(new Vector3(Mathf.Cos(a) * profile[i].x, Mathf.Sin(a) * profile[i].x, profile[i].y));
                }
            for (int i = 0; i < profile.Length - 1; i++)
            {
                float zMid = (profile[i].y + profile[i + 1].y) * 0.5f;
                var list = zMid > 0.12f || (zMid > -0.02f && zMid < 0.06f) || zMid < -0.17f ? red : white;
                for (int k = 0; k < sides; k++)
                {
                    int a = i * (sides + 1) + k, b = a + 1, c = a + sides + 1, d = c + 1;
                    list.AddRange(new[] { a, b, c, b, d, c });
                }
            }
            var mesh = new Mesh { name = "Rocket" };
            mesh.SetVertices(verts);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(white, 0);
            mesh.SetTriangles(red, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// Nokta dizisi boyunca boru (paralel taşınan çerçeveyle).
        public static Mesh Tube(List<Vector3> pts, float radius, int sides)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            Vector3 prevNormal = Vector3.zero;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 tangent = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 n = i == 0 ? Vector3.Cross(tangent, Vector3.right).normalized : Vector3.ProjectOnPlane(prevNormal, tangent).normalized;
                if (n.sqrMagnitude < 0.5f) n = Vector3.up;
                prevNormal = n;
                Vector3 b = Vector3.Cross(tangent, n);
                for (int k = 0; k < sides; k++)
                {
                    float a = 2f * Mathf.PI * k / sides;
                    verts.Add(pts[i] + (n * Mathf.Cos(a) + b * Mathf.Sin(a)) * radius);
                }
            }
            for (int i = 0; i < pts.Count - 1; i++)
                for (int k = 0; k < sides; k++)
                {
                    int k2 = (k + 1) % sides;
                    int a = i * sides + k, b = i * sides + k2, c = (i + 1) * sides + k, d = (i + 1) * sides + k2;
                    tris.AddRange(new[] { a, b, c, b, d, c });
                }
            // Uç kapakları
            for (int e = 0; e < 2; e++)
            {
                int ring = e == 0 ? 0 : (pts.Count - 1) * sides;
                int center = verts.Count;
                verts.Add(pts[e == 0 ? 0 : pts.Count - 1]);
                for (int k = 0; k < sides; k++)
                {
                    int k2 = (k + 1) % sides;
                    if (e == 0) tris.AddRange(new[] { center, ring + k2, ring + k });
                    else tris.AddRange(new[] { center, ring + k, ring + k2 });
                }
            }
            var mesh = new Mesh { name = "Tube" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
