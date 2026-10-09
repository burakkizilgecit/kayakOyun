using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Son üç haritanın araçları (kullanıcı kararı 2026-10-09: aracın şekli tamamen değişir). Hepsi koddan üretilir;
    /// oturma yüksekliği kızakla aynı (~0.27 m), kanat ve roket bağlantıları RiderView'da ortak.
    /// Kum sörfü: raysız, kısa ve geniş tahta, ayak kayışları, kuyrukta yüzgeçler.
    /// Bobsled: kapalı obsidyen gövde, ön kubbe, dört kısa bıçak, lav şeritleri.
    /// Hover: oval platform, altında parlayan yastıklar, anten, yan iticiler.
    public static class VehicleModels
    {
        /// Aşamaya göre değişen parçalar (0..6).
        public class Parts
        {
            public readonly List<(Transform t, int stage)> unlocks = new List<(Transform, int)>();
            public Material body, trim, glow;
            public Color[] bodyColors, trimColors;

            public void SetStage(int stage)
            {
                foreach (var (t, s) in unlocks) t.gameObject.SetActive(stage >= s);
                int i = Mathf.Clamp(stage, 0, bodyColors.Length - 1);
                body.color = bodyColors[i];
                trim.color = trimColors[Mathf.Clamp(stage, 0, trimColors.Length - 1)];
            }
        }

        public static Parts Build(VehicleKind kind, Transform parent)
        {
            switch (kind)
            {
                case VehicleKind.Sandboard: return Sandboard(parent);
                case VehicleKind.Bobsled: return Bobsled(parent);
                default: return Hover(parent);
            }
        }

        static Material Glow(Color c, float power)
        {
            var m = Mats.Solid(c, 0.6f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * power);
            return m;
        }

        // ------------------------------------------------------------------ kum sörfü

        static Parts Sandboard(Transform parent)
        {
            var p = new Parts
            {
                body = Mats.Solid(new Color(0.72f, 0.5f, 0.3f), 0.35f),
                trim = Mats.Solid(new Color(0.95f, 0.85f, 0.6f), 0.3f),
                bodyColors = new[]
                {
                    new Color(0.72f, 0.5f, 0.3f), new Color(0.72f, 0.5f, 0.3f), new Color(0.98f, 0.5f, 0.25f), new Color(0.98f, 0.42f, 0.3f),
                    new Color(0.15f, 0.55f, 0.6f), new Color(0.2f, 0.25f, 0.55f), new Color(1f, 0.78f, 0.22f),
                },
                trimColors = new[]
                {
                    new Color(0.95f, 0.85f, 0.6f), new Color(0.95f, 0.85f, 0.6f), new Color(1f, 0.85f, 0.3f), new Color(1f, 0.9f, 0.4f),
                    new Color(1f, 0.6f, 0.2f), new Color(0.3f, 0.95f, 1f), new Color(1f, 0.95f, 0.75f),
                },
            };
            var board = new GameObject("Sandboard").transform;
            board.SetParent(parent, false);
            board.gameObject.AddComponent<MeshFilter>().sharedMesh = Plank(1.7f, 0.42f, 0.04f, 0.22f, 0.07f);
            board.gameObject.AddComponent<MeshRenderer>().sharedMaterial = p.body;
            // Orta şerit ve kenar bandı (boya)
            Mats.Prim(PrimitiveType.Cube, "Stripe", board, new Vector3(0f, 0.262f, 0f), new Vector3(0.1f, 0.006f, 1.25f), p.trim);
            for (int s = -1; s <= 1; s += 2)
                Mats.Prim(PrimitiveType.Cube, "Edge", board, new Vector3(0.36f * s, 0.255f, 0f), new Vector3(0.05f, 0.012f, 1.2f), p.trim);
            // Ayak kayışları (1. aşama)
            var straps = new GameObject("Straps").transform;
            straps.SetParent(board, false);
            var strapMat = Mats.Solid(new Color(0.15f, 0.15f, 0.18f), 0.3f);
            foreach (float z in new[] { -0.25f, 0.3f })
                Mats.Prim(PrimitiveType.Cube, "Strap", straps, new Vector3(0f, 0.3f, z), new Vector3(0.5f, 0.05f, 0.09f), strapMat);
            p.unlocks.Add((straps, 1));
            // Yüzgeçler: küçük (her zaman), büyük (3. aşama)
            var fins = new GameObject("Fins").transform;
            fins.SetParent(board, false);
            for (int s = -1; s <= 1; s++)
                Mats.Prim(PrimitiveType.Cube, "Fin", fins, new Vector3(0.18f * s, 0.12f, -0.7f), new Vector3(0.02f, 0.1f, 0.14f), p.trim);
            var bigFins = new GameObject("Big Fins").transform;
            bigFins.SetParent(board, false);
            for (int s = -1; s <= 1; s += 2)
                Mats.Prim(PrimitiveType.Cube, "Big Fin", bigFins, new Vector3(0.3f * s, 0.34f, -0.72f), new Vector3(0.03f, 0.22f, 0.26f), p.trim,
                          Quaternion.Euler(0f, 0f, 18f * s));
            p.unlocks.Add((bigFins, 3));
            p.SetStage(0);
            return p;
        }

        /// Uçları yuvarlak, burnu hafif kalkık düz tahta (kum sörfü).
        static Mesh Plank(float length, float halfWidth, float halfThick, float centerY, float noseLift)
        {
            const int N = 40, K = 16;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N;
                float end = Mathf.Min(t, 1f - t) / 0.12f;
                float w = halfWidth * Mathf.Sqrt(Mathf.Clamp01(end * (2f - Mathf.Min(end, 1f)))) + 0.002f;
                float y0 = centerY + noseLift * Mathf.Pow(Mathf.Max(0f, (t - 0.75f) / 0.25f), 2f);
                float z = (t - 0.5f) * length;
                for (int k = 0; k < K; k++)
                {
                    float a = 2f * Mathf.PI * k / K;
                    float cx = Mathf.Cos(a), sy = Mathf.Sin(a);
                    verts.Add(new Vector3(w * Mathf.Sign(cx) * Mathf.Sqrt(Mathf.Abs(cx)), y0 + halfThick * Mathf.Sign(sy) * Mathf.Sqrt(Mathf.Abs(sy)), z));
                }
            }
            for (int i = 0; i < N; i++)
                for (int k = 0; k < K; k++)
                {
                    int k2 = (k + 1) % K;
                    int a = i * K + k, b = i * K + k2, c = (i + 1) * K + k, d = (i + 1) * K + k2;
                    tris.AddRange(new[] { a, b, c, b, d, c });
                }
            var mesh = new Mesh { name = "Plank" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ obsidyen bobsled

        static Parts Bobsled(Transform parent)
        {
            var p = new Parts
            {
                body = Mats.Solid(new Color(0.08f, 0.07f, 0.1f), 0.92f),
                trim = Glow(new Color(1f, 0.4f, 0.08f), 1.4f),
                bodyColors = new[]
                {
                    new Color(0.08f, 0.07f, 0.1f), new Color(0.08f, 0.07f, 0.1f), new Color(0.1f, 0.08f, 0.12f), new Color(0.12f, 0.06f, 0.08f),
                    new Color(0.35f, 0.06f, 0.05f), new Color(0.2f, 0.05f, 0.25f), new Color(1f, 0.76f, 0.2f),
                },
                trimColors = new[]
                {
                    new Color(1f, 0.4f, 0.08f), new Color(1f, 0.45f, 0.1f), new Color(1f, 0.5f, 0.1f), new Color(1f, 0.55f, 0.12f),
                    new Color(1f, 0.75f, 0.2f), new Color(0.75f, 0.35f, 1f), new Color(1f, 0.95f, 0.7f),
                },
            };
            var hull = new GameObject("Bob Hull").transform;
            hull.SetParent(parent, false);
            hull.gameObject.AddComponent<MeshFilter>().sharedMesh = Pod(1.95f, 0.4f, 0.27f, 0.25f, 0.33f, 0f);
            hull.gameObject.AddComponent<MeshRenderer>().sharedMaterial = p.body;
            // Ön kubbe: kızağın burnunu ve sürücünün bacaklarını örter
            var cowl = new GameObject("Bob Cowl").transform;
            cowl.SetParent(parent, false);
            cowl.gameObject.AddComponent<MeshFilter>().sharedMesh = Pod(1.95f, 0.4f, 0.32f, 0.25f, 10f, 0.68f);
            cowl.gameObject.AddComponent<MeshRenderer>().sharedMaterial = p.body;
            // Bıçaklar
            var blade = Mats.Solid(new Color(0.75f, 0.78f, 0.82f), 0.9f);
            foreach (float z in new[] { -0.6f, 0.55f })
                for (int s = -1; s <= 1; s += 2)
                    Mats.Prim(PrimitiveType.Cube, "Blade", parent, new Vector3(0.22f * s, 0.04f, z), new Vector3(0.025f, 0.06f, 0.42f), blade);
            // Lav şeritleri (1. aşama): gövdenin iki yanında parlayan çizgi
            var stripes = new GameObject("Lava Stripes").transform;
            stripes.SetParent(parent, false);
            for (int s = -1; s <= 1; s += 2)
                Mats.Prim(PrimitiveType.Cube, "Stripe", stripes, new Vector3(0.385f * s, 0.25f, 0f), new Vector3(0.012f, 0.03f, 1.3f), p.trim);
            p.unlocks.Add((stripes, 1));
            // Ön siperlik (2. aşama)
            var shield = new GameObject("Windshield").transform;
            shield.SetParent(parent, false);
            var glass = Mats.Solid(new Color(0.55f, 0.75f, 0.9f), 0.95f);
            Mats.Prim(PrimitiveType.Cube, "Glass", shield, new Vector3(0f, 0.52f, 0.33f), new Vector3(0.46f, 0.18f, 0.015f), glass, Quaternion.Euler(-35f, 0f, 0f));
            p.unlocks.Add((shield, 2));
            // Kuyruk yüzgeci (3. aşama)
            var fin = new GameObject("Tail Fin").transform;
            fin.SetParent(parent, false);
            Mats.Prim(PrimitiveType.Cube, "Fin", fin, new Vector3(0f, 0.5f, -0.82f), new Vector3(0.03f, 0.3f, 0.32f), p.body, Quaternion.Euler(20f, 0f, 0f));
            Mats.Prim(PrimitiveType.Cube, "Fin Glow", fin, new Vector3(0f, 0.62f, -0.86f), new Vector3(0.035f, 0.05f, 0.2f), p.trim, Quaternion.Euler(20f, 0f, 0f));
            p.unlocks.Add((fin, 3));
            p.SetStage(0);
            return p;
        }

        /// Uzun, yuvarlak uçlu gövde (dönel elipsoid). topCut: bu yükseklikten sonrası kesilir (kokpit açıklığı);
        /// fromT > 0: yalnızca burun bölümü (ön kubbe).
        static Mesh Pod(float length, float halfWidth, float halfHeight, float centerY, float topCut, float fromT)
        {
            const int N = 36, K = 20;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int rows = 0;
            for (int i = 0; i <= N; i++)
            {
                float t = Mathf.Lerp(fromT, 1f, i / (float)N);
                // Profil: kuyrukta küt, burunda sivri yuvarlak
                float u = t * 2f - 1f;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(u), 2.4f)));
                if (u < 0f) r = Mathf.Max(r, 0.35f * (1f + u)) ;
                float z = (t - 0.5f) * length;
                for (int k = 0; k < K; k++)
                {
                    float a = 2f * Mathf.PI * k / K;
                    float y = centerY + Mathf.Sin(a) * halfHeight * r;
                    verts.Add(new Vector3(Mathf.Cos(a) * halfWidth * r, Mathf.Min(y, centerY + (topCut - centerY)), z));
                }
                rows++;
            }
            for (int i = 0; i < rows - 1; i++)
                for (int k = 0; k < K; k++)
                {
                    int k2 = (k + 1) % K;
                    int a = i * K + k, b = i * K + k2, c = (i + 1) * K + k, d = (i + 1) * K + k2;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }
            var mesh = new Mesh { name = "Pod" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ hover

        static Parts Hover(Transform parent)
        {
            var p = new Parts
            {
                body = Mats.Solid(new Color(0.95f, 0.95f, 0.97f), 0.7f),
                trim = Mats.Solid(new Color(0.9f, 0.25f, 0.2f), 0.5f),
                glow = Glow(new Color(0.3f, 0.95f, 1f), 2f),
                bodyColors = new[]
                {
                    new Color(0.95f, 0.95f, 0.97f), new Color(0.95f, 0.95f, 0.97f), new Color(0.92f, 0.92f, 0.95f), new Color(0.2f, 0.45f, 0.95f),
                    new Color(0.15f, 0.35f, 0.85f), new Color(0.1f, 0.1f, 0.14f), new Color(1f, 0.78f, 0.22f),
                },
                trimColors = new[]
                {
                    new Color(0.9f, 0.25f, 0.2f), new Color(0.9f, 0.25f, 0.2f), new Color(0.95f, 0.5f, 0.15f), new Color(0.95f, 0.95f, 1f),
                    new Color(0.95f, 0.95f, 1f), new Color(0.3f, 0.95f, 1f), new Color(1f, 0.95f, 0.7f),
                },
            };
            Mats.Prim(PrimitiveType.Cylinder, "Deck", parent, new Vector3(0f, 0.235f, 0f), new Vector3(0.82f, 0.035f, 1.85f), p.body);
            Mats.Prim(PrimitiveType.Cylinder, "Rim", parent, new Vector3(0f, 0.2f, 0f), new Vector3(0.86f, 0.012f, 1.9f), p.trim);
            Mats.Prim(PrimitiveType.Sphere, "Nose", parent, new Vector3(0f, 0.25f, 0.78f), new Vector3(0.4f, 0.12f, 0.35f), p.body);
            // Altta parlayan dört yastık
            foreach (float z in new[] { -0.55f, 0.5f })
                for (int s = -1; s <= 1; s += 2)
                    Mats.Prim(PrimitiveType.Sphere, "Pad", parent, new Vector3(0.24f * s, 0.12f, z), new Vector3(0.22f, 0.05f, 0.22f), p.glow);
            // Anten (1. aşama)
            var antenna = new GameObject("Antenna").transform;
            antenna.SetParent(parent, false);
            var pole = Mats.Prim(PrimitiveType.Cylinder, "Pole", antenna, Vector3.zero, Vector3.one, Mats.Solid(new Color(0.6f, 0.62f, 0.66f), 0.8f));
            Mats.PlaceBetween(pole, new Vector3(-0.28f, 0.25f, -0.75f), new Vector3(-0.32f, 0.75f, -0.85f), 0.015f);
            Mats.Prim(PrimitiveType.Sphere, "Tip", antenna, new Vector3(-0.32f, 0.77f, -0.85f), Vector3.one * 0.05f, p.glow);
            p.unlocks.Add((antenna, 1));
            // Yan iticiler (2. aşama)
            var thrusters = new GameObject("Thrusters").transform;
            thrusters.SetParent(parent, false);
            for (int s = -1; s <= 1; s += 2)
            {
                Mats.Prim(PrimitiveType.Capsule, "Thruster", thrusters, new Vector3(0.44f * s, 0.24f, -0.45f), new Vector3(0.1f, 0.22f, 0.1f), p.trim,
                          Quaternion.Euler(90f, 0f, 0f));
                Mats.Prim(PrimitiveType.Sphere, "Exhaust", thrusters, new Vector3(0.44f * s, 0.24f, -0.68f), Vector3.one * 0.08f, p.glow);
            }
            p.unlocks.Add((thrusters, 2));
            // Işık halkası (5. aşama)
            var halo = new GameObject("Halo").transform;
            halo.SetParent(parent, false);
            Mats.Prim(PrimitiveType.Cylinder, "Halo Ring", halo, new Vector3(0f, 0.17f, 0f), new Vector3(0.95f, 0.006f, 2.02f), p.glow);
            p.unlocks.Add((halo, 5));
            p.SetStage(0);
            return p;
        }
    }
}
