using UnityEngine;

namespace SledSurfers
{
    /// Halatlı sapan: metal başlıklı yuvarlak ahşap direkler, aralarında örgü desenli kalın lastik halat
    /// ve kızağın arkasını saran deri cep. Halat her karede yeniden bükülür: gevşekken sarkar, gerilince düzleşip incelir.
    public class SlingshotView
    {
        const int Sides = 8, Segments = 14;

        readonly Material bandMat, woodMat, metalMat;

        readonly Transform[] decor = new Transform[2];
        readonly float postHeight;
        int builtStage = -1;

        /// Seviye (0..5): direk ve başlık boyası; her seviyede yeni parçalar (taban plakası, payandalar, makara,
        /// yay ve bayrak, hidrolik piston ve ışık, altın taç).
        public void SetStage(int stage)
        {
            stage = Mathf.Clamp(stage, 0, 5);
            woodMat.color = PartVisuals.SlingPost[stage];
            metalMat.color = PartVisuals.SlingCap[stage];
            metalMat.SetFloat("_Glossiness", stage >= 3 ? 0.9f : 0.75f);
            if (stage == builtStage) return;
            builtStage = stage;
            for (int s = 0; s < 2; s++)
            {
                for (int i = decor[s].childCount - 1; i >= 0; i--) Object.Destroy(decor[s].GetChild(i).gameObject);
                PartVisuals.BuildSlingDecor(decor[s], stage, postHeight, s == 0 ? -1f : 1f);
            }
        }
        readonly Band left, right;
        readonly Transform pouch;
        public readonly Vector3 leftTop, rightTop;

        public SlingshotView(Transform parent, float postX, float postZ, float groundY, float height)
        {
            var wood = woodMat = Mats.Solid(new Color(0.6f, 0.38f, 0.2f), 0.2f);
            var metal = metalMat = Mats.Solid(new Color(0.62f, 0.66f, 0.72f), 0.75f);
            var stone = Mats.Solid(new Color(0.62f, 0.6f, 0.58f), 0.1f);
            var postMesh = PostMesh(height);
            postHeight = height;
            for (int s = -1; s <= 1; s += 2)
            {
                var post = new GameObject("Sling Post").transform;
                post.SetParent(parent, false);
                post.localPosition = new Vector3(postX * s, groundY, postZ);
                post.gameObject.AddComponent<MeshFilter>().sharedMesh = postMesh;
                post.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[] { wood, metal };
                Mats.Prim(PrimitiveType.Cube, "Footing", post, new Vector3(0f, 0.08f, 0f), new Vector3(0.52f, 0.22f, 0.52f), stone);
                var d = new GameObject("Decor").transform;
                d.SetParent(post, false);
                decor[s < 0 ? 0 : 1] = d;
            }
            // Halat direğin başlığının hemen altından, içe bakan yandan çıkar.
            float attachY = groundY + height - 0.3f;
            leftTop = new Vector3(-postX + 0.1f, attachY, postZ);
            rightTop = new Vector3(postX - 0.1f, attachY, postZ);

            bandMat = Mats.Solid(Color.white, 0.25f);
            bandMat.mainTexture = RopeTexture();
            left = new Band(parent, bandMat);
            right = new Band(parent, bandMat);

            pouch = Mats.Prim(PrimitiveType.Capsule, "Pouch", parent, Vector3.zero, new Vector3(0.2f, 0.3f, 0.12f),
                              Mats.Solid(new Color(0.42f, 0.24f, 0.13f), 0.3f));
        }

        public Vector3 Rest => (leftTop + rightTop) * 0.5f;

        /// stretch 0..1: gerginlik; color: lastiğin o anki rengi; thickness: yükseltme kalınlık çarpanı.
        public void Update(Vector3 pouchPos, float stretch, Color color, float thickness)
        {
            float radius = Mathf.Lerp(0.055f, 0.028f, stretch) * thickness;
            float sag = 0.22f * (1f - Mathf.Clamp01(stretch * 4f));
            left.Set(leftTop, pouchPos, radius, sag);
            right.Set(rightTop, pouchPos, radius, sag);
            bandMat.color = color;

            pouch.localPosition = pouchPos + Vector3.down * 0.02f;   // yerel: önizleme sahnesinde de doğru yerde
            Vector3 toRest = Rest - pouchPos;
            Vector3 fwd = toRest.sqrMagnitude > 0.01f ? toRest.normalized : Vector3.forward;
            // Kapsül yatay uzanır (yerel y ekseni iki direğe doğru), cebin açık yüzü sapana bakar.
            pouch.localRotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(0f, 0f, 90f);
        }

        /// Ahşap direk (alt mesh 0) + metal halkalar ve kubbeli başlık (alt mesh 1), y ekseni etrafında döndürülmüş profil.
        static Mesh PostMesh(float h)
        {
            // (yarıçap, yükseklik, metal mi)
            var profile = new (float r, float y, bool metal)[]
            {
                (0.001f, 0f, false), (0.15f, 0f, false), (0.15f, 0.2f, false), (0.168f, 0.2f, true), (0.168f, 0.3f, true),
                (0.148f, 0.3f, false), (0.138f, h - 0.4f, false), (0.162f, h - 0.4f, true), (0.162f, h - 0.32f, true),
                (0.136f, h - 0.32f, false), (0.134f, h - 0.16f, false), (0.175f, h - 0.16f, true), (0.175f, h - 0.08f, true),
                (0.15f, h - 0.03f, true), (0.1f, h + 0.02f, true), (0.001f, h + 0.04f, true),
            };
            const int sides = 16;
            var verts = new System.Collections.Generic.List<Vector3>();
            var woodTris = new System.Collections.Generic.List<int>();
            var metalTris = new System.Collections.Generic.List<int>();
            // Her halka iki kez yazılır (aşağıdan ve yukarıdan gelen yüze ayrı): keskin kenarlı, köşeli görünüm.
            for (int i = 0; i < profile.Length - 1; i++)
            {
                int start = verts.Count;
                for (int e = 0; e < 2; e++)
                {
                    var p = profile[i + e];
                    for (int k = 0; k <= sides; k++)
                    {
                        float a = 2f * Mathf.PI * k / sides;
                        verts.Add(new Vector3(Mathf.Cos(a) * p.r, p.y, Mathf.Sin(a) * p.r));
                    }
                }
                var list = profile[i].metal || profile[i + 1].metal ? metalTris : woodTris;
                for (int k = 0; k < sides; k++)
                {
                    int a = start + k, b = a + 1, c = start + sides + 1 + k, d = c + 1;
                    list.AddRange(new[] { a, c, b, b, c, d });
                }
            }
            var mesh = new Mesh { name = "Sling Post" };
            mesh.SetVertices(verts);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(woodTris, 0);
            mesh.SetTriangles(metalTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// Örgü halat dokusu: çapraz şeritler (renk malzemeden gelir).
        static Texture2D RopeTexture()
        {
            const int W = 32, H = 32;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Rope", anisoLevel = 2 };
            var px = new Color[W * H];
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    float f = Mathf.Repeat(i / (float)W * 2f + j / (float)H, 1f);
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.Abs(f - 0.5f) * 2f);   // şerit ortası açık, kenarları koyu
                    float v = Mathf.Lerp(1f, 0.62f, edge);
                    px[j * W + i] = new Color(v, v, v, 1f);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// Tek halat parçası: iki nokta arasında, ortası sarkan ikinci derece eğri boyunca boru.
        class Band
        {
            readonly Mesh mesh;
            readonly Vector3[] verts = new Vector3[(Segments + 1) * (Sides + 1)];
            readonly Vector2[] uvs = new Vector2[(Segments + 1) * (Sides + 1)];

            public Band(Transform parent, Material mat)
            {
                var go = new GameObject("Band");
                go.transform.SetParent(parent, false);
                mesh = new Mesh { name = "Band" };
                mesh.MarkDynamic();
                mesh.vertices = verts;
                var tris = new int[Segments * Sides * 6];
                int t = 0;
                for (int i = 0; i < Segments; i++)
                    for (int k = 0; k < Sides; k++)
                    {
                        int a = i * (Sides + 1) + k, b = a + 1, c = a + Sides + 1, d = c + 1;
                        tris[t++] = a; tris[t++] = b; tris[t++] = c;
                        tris[t++] = b; tris[t++] = d; tris[t++] = c;
                    }
                mesh.triangles = tris;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            public void Set(Vector3 a, Vector3 b, float radius, float sag)
            {
                Vector3 ctrl = (a + b) * 0.5f + Vector3.down * sag * 2f;
                float length = 0f;
                Vector3 prev = a, normal = Vector3.zero;
                for (int i = 0; i <= Segments; i++)
                {
                    float t = i / (float)Segments;
                    Vector3 p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * ctrl + t * t * b;
                    Vector3 tangent = (2 * (1 - t) * (ctrl - a) + 2 * t * (b - ctrl)).normalized;
                    if (tangent.sqrMagnitude < 0.5f) tangent = Vector3.forward;
                    normal = i == 0 ? Vector3.Cross(tangent, Vector3.up) : Vector3.ProjectOnPlane(normal, tangent);
                    if (normal.sqrMagnitude < 1e-4f) normal = Vector3.Cross(tangent, Vector3.right);
                    normal.Normalize();
                    Vector3 binormal = Vector3.Cross(tangent, normal);
                    length += Vector3.Distance(prev, p);
                    prev = p;
                    for (int k = 0; k <= Sides; k++)
                    {
                        float ang = 2f * Mathf.PI * k / Sides;
                        verts[i * (Sides + 1) + k] = p + (normal * Mathf.Cos(ang) + binormal * Mathf.Sin(ang)) * radius;
                        uvs[i * (Sides + 1) + k] = new Vector2(k / (float)Sides, length / 0.16f);
                    }
                }
                mesh.vertices = verts;
                mesh.uv = uvs;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }
        }
    }
}
