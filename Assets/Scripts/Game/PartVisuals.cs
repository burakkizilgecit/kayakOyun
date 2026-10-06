using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Yükseltmelerin 6 görünümü (başlangıç + 5 tamamlanan seviye): roket, sapan süslemeleri ve seviye adları.
    /// Hem oyundaki kızak/sapan hem de kartlardaki 3D önizlemeler bu kurucuları kullanır.
    public static class PartVisuals
    {
        public static readonly string[] SlingNames = { "Tahta Sapan", "Kırmızı Sapan", "Destekli Sapan", "Çelik Sapan", "Hidrolik Sapan", "Altın Sapan" };
        public static readonly string[] SledNames = { "Sörf Kızağı", "İpli Kızak", "Gümüş Kayaklı", "Spoilerlı", "Kanatlı Kızak", "Jet Kanatlı" };
        public static readonly string[] IncomeNames = { "Kumbara", "Cüzdan", "Para Kesesi", "Hazine Sandığı", "Kasa", "Altın Hazine" };
        public static readonly string[] RocketNames = { "Havai Fişek", "Mavi Roket", "İkiz Roket", "Jet Motoru", "Turbo Roket", "Altın Roket" };

        /// Kartın gösterdiği seviye adı: tür ve tamamlanmış seviye (0..5).
        public static string StageName(int type, int stage)
        {
            stage = Mathf.Clamp(stage, 0, 5);
            switch ((UpgradeType)type)
            {
                case UpgradeType.Slingshot: return SlingNames[stage];
                case UpgradeType.Runners: return SledNames[stage];
                case UpgradeType.Glass: return Upgrades.GlassNames[Mathf.Min(stage, Upgrades.GlassNames.Length - 1)];
                case UpgradeType.Income: return IncomeNames[stage];
                default: return RocketNames[stage];
            }
        }

        static Material Mat(Color c, float gloss = 0.4f, bool glow = false)
        {
            var m = Mats.Solid(c, gloss);
            if (glow)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 1.4f);
            }
            return m;
        }

        static readonly Color White = new Color(0.95f, 0.95f, 0.97f), Red = new Color(0.9f, 0.22f, 0.15f),
                              Blue = new Color(0.16f, 0.45f, 0.95f), Navy = new Color(0.12f, 0.16f, 0.3f),
                              Black = new Color(0.1f, 0.1f, 0.12f), Orange = new Color(1f, 0.5f, 0.1f),
                              Silver = new Color(0.78f, 0.8f, 0.85f), Gold = new Color(1f, 0.76f, 0.2f),
                              Cyan = new Color(0.25f, 0.95f, 1f);

        /// Tek bir roket (z ileri, uzunluk ~0.5 m). Dönüş: alevin konacağı nokta (yerel).
        public static Vector3 BuildRocket(Transform r, int stage, Mesh body)
        {
            stage = Mathf.Clamp(stage, 0, 5);
            void Part(Mesh mesh, Material[] mats, Vector3 pos, Vector3 scale)
            {
                var go = new GameObject("Part");
                go.transform.SetParent(r, false);
                go.transform.localPosition = pos;
                go.transform.localScale = scale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            }
            void Fins(int count, Material m, float size, float z, float offset)
            {
                for (int f = 0; f < count; f++)
                {
                    var rot = Quaternion.Euler(0f, 0f, offset + 360f / count * f);
                    Mats.Prim(PrimitiveType.Cube, "Fin", r, rot * new Vector3(0f, 0.085f * size, 0f) + new Vector3(0f, 0f, z),
                              new Vector3(0.018f, 0.07f * size, 0.11f * size), m, rot);
                }
            }
            void Ring(float z, float radius, Material m, float thick = 0.02f)
            {
                Mats.Prim(PrimitiveType.Cylinder, "Ring", r, new Vector3(0f, 0f, z), new Vector3(radius * 2f, thick, radius * 2f), m,
                          Quaternion.Euler(90f, 0f, 0f));
            }
            switch (stage)
            {
                case 0:   // havai fişek: beyaz-kırmızı, 3 kanatçık
                    Part(body, new[] { Mat(White, 0.5f), Mat(Red) }, Vector3.zero, Vector3.one);
                    Fins(3, Mat(Red), 1f, -0.13f, 90f);
                    return new Vector3(0f, 0f, -0.33f);
                case 1:   // mavi roket: biraz büyük, 4 kanat, krom halka
                    Part(body, new[] { Mat(White, 0.6f), Mat(Blue, 0.5f) }, Vector3.zero, new Vector3(1.1f, 1.1f, 1.15f));
                    Fins(4, Mat(Blue, 0.5f), 1.15f, -0.14f, 45f);
                    Ring(0.05f, 0.08f, Mat(Silver, 0.85f));
                    return new Vector3(0f, 0f, -0.36f);
                case 2:   // ikiz roket: ana gövde + iki küçük yan yakıt tankı
                {
                    var main = new[] { Mat(Black, 0.5f), Mat(Orange, 0.5f) };
                    Part(body, main, Vector3.zero, new Vector3(1.15f, 1.15f, 1.2f));
                    for (int s = -1; s <= 1; s += 2)
                        Part(body, main, new Vector3(0.1f * s, -0.03f, -0.05f), new Vector3(0.55f, 0.55f, 0.75f));
                    Fins(4, Mat(Orange, 0.5f), 1.2f, -0.15f, 0f);
                    return new Vector3(0f, 0f, -0.38f);
                }
                case 3:   // jet motoru: kalın gövde, ön hava girişi halkası, egzoz konisi
                {
                    var shell = Mat(Silver, 0.85f);
                    Mats.Prim(PrimitiveType.Cylinder, "Nacelle", r, Vector3.zero, new Vector3(0.2f, 0.2f, 0.2f), shell, Quaternion.Euler(90f, 0f, 0f));
                    Ring(0.2f, 0.105f, Mat(Navy, 0.6f), 0.03f);
                    Mats.Prim(PrimitiveType.Sphere, "Spinner", r, new Vector3(0f, 0f, 0.19f), new Vector3(0.09f, 0.09f, 0.08f), Mat(Black, 0.7f));
                    Mats.Prim(PrimitiveType.Cylinder, "Nozzle", r, new Vector3(0f, 0f, -0.23f), new Vector3(0.15f, 0.04f, 0.15f), Mat(Black, 0.6f),
                              Quaternion.Euler(90f, 0f, 0f));
                    Ring(-0.1f, 0.104f, Mat(Blue, 0.5f), 0.04f);
                    Fins(2, Mat(Navy, 0.6f), 1.3f, -0.12f, 90f);
                    return new Vector3(0f, 0f, -0.32f);
                }
                case 4:   // turbo roket: uzun karbon gövde, parlayan camgöbeği halkalar
                {
                    Part(body, new[] { Mat(Black, 0.7f), Mat(Cyan, 0.6f, true) }, Vector3.zero, new Vector3(1.2f, 1.2f, 1.45f));
                    Ring(-0.12f, 0.087f, Mat(Cyan, 0.6f, true), 0.025f);
                    Ring(0.12f, 0.087f, Mat(Cyan, 0.6f, true), 0.025f);
                    Fins(4, Mat(Black, 0.7f), 1.35f, -0.2f, 45f);
                    return new Vector3(0f, 0f, -0.44f);
                }
                default:  // altın roket: altın gövde, kırmızı burun, parlayan turuncu halkalar, yıldız kanatlar
                {
                    Part(body, new[] { Mat(Gold, 0.9f), Mat(Red, 0.6f) }, Vector3.zero, new Vector3(1.3f, 1.3f, 1.5f));
                    Ring(-0.1f, 0.095f, Mat(Orange, 0.6f, true), 0.03f);
                    Ring(0.08f, 0.095f, Mat(Orange, 0.6f, true), 0.03f);
                    Fins(5, Mat(Gold, 0.9f), 1.4f, -0.2f, 90f);
                    return new Vector3(0f, 0f, -0.46f);
                }
            }
        }

        /// Sapan direğinin seviye süslemeleri (direk yerel ekseninde, y yukarı; h: direk boyu; side: -1 sol, +1 sağ).
        public static void BuildSlingDecor(Transform post, int stage, float h, float side)
        {
            stage = Mathf.Clamp(stage, 0, 5);
            var metal = Mat(stage >= 5 ? Gold : Silver, 0.85f);
            if (stage >= 1)
            {
                // Metal taban plakası ve cıvatalar
                Mats.Prim(PrimitiveType.Cylinder, "Base Plate", post, new Vector3(0f, 0.23f, 0f), new Vector3(0.5f, 0.02f, 0.5f), metal);
            }
            if (stage >= 2)
            {
                // Çapraz destek: dış yana ve geriye iki payanda
                foreach (var dir in new[] { new Vector3(side, 0f, 0f), new Vector3(0f, 0f, -1f) })
                {
                    var strut = Mats.Prim(PrimitiveType.Cylinder, "Brace", post, Vector3.zero, Vector3.one, Mat(stage >= 5 ? Gold : stage >= 3 ? Silver : new Color(0.55f, 0.35f, 0.2f), 0.5f));
                    Mats.PlaceBetween(strut, dir * 0.85f + Vector3.up * 0.05f, Vector3.up * (h * 0.6f), 0.07f);
                }
                // Tepe makarası
                Mats.Prim(PrimitiveType.Cylinder, "Pulley", post, new Vector3(-side * 0.17f, h - 0.3f, 0f), new Vector3(0.2f, 0.03f, 0.2f), metal,
                          Quaternion.Euler(0f, 0f, 90f));
            }
            if (stage >= 3)
            {
                // Yay: direğin üst yarısına sarılı helezon
                var pts = new List<Vector3>();
                for (int k = 0; k <= 60; k++)
                {
                    float a = k * 0.55f;
                    pts.Add(new Vector3(Mathf.Cos(a) * 0.19f, h * 0.45f + k * 0.006f, Mathf.Sin(a) * 0.19f));
                }
                var spring = new GameObject("Spring");
                spring.transform.SetParent(post, false);
                spring.AddComponent<MeshFilter>().sharedMesh = SledModel.Tube(pts, 0.018f, 6);
                spring.AddComponent<MeshRenderer>().sharedMaterial = Mat(stage >= 5 ? Gold : new Color(0.95f, 0.3f, 0.25f), 0.7f);
                // Bayrak
                Mats.Prim(PrimitiveType.Cylinder, "Flag Pole", post, new Vector3(0f, h + 0.35f, 0f), new Vector3(0.03f, 0.35f, 0.03f), metal);
                Mats.Prim(PrimitiveType.Cube, "Flag", post, new Vector3(side * 0.17f, h + 0.55f, 0f), new Vector3(0.32f, 0.2f, 0.015f),
                          Mat(stage >= 5 ? Red : stage >= 4 ? new Color(0.6f, 0.25f, 0.9f) : Orange, 0.3f));
            }
            if (stage >= 4)
            {
                // Hidrolik piston (önde) ve tepe ışığı
                Mats.Prim(PrimitiveType.Cylinder, "Piston", post, new Vector3(0f, h * 0.35f, 0.2f), new Vector3(0.1f, h * 0.25f, 0.1f), Mat(Black, 0.6f));
                Mats.Prim(PrimitiveType.Cylinder, "Rod", post, new Vector3(0f, h * 0.68f, 0.2f), new Vector3(0.05f, h * 0.12f, 0.05f), Mat(Silver, 0.95f));
                Mats.Prim(PrimitiveType.Sphere, "Light", post, new Vector3(0f, h + 0.1f, 0f), Vector3.one * 0.16f,
                          Mat(stage >= 5 ? Gold : Cyan, 0.8f, true));
            }
            if (stage >= 5)
            {
                // Altın yıldız taç
                Mats.Prim(PrimitiveType.Cube, "Star A", post, new Vector3(0f, h + 0.95f, 0f), new Vector3(0.22f, 0.22f, 0.05f), Mat(Gold, 0.9f, true),
                          Quaternion.Euler(0f, 0f, 45f));
                Mats.Prim(PrimitiveType.Cube, "Star B", post, new Vector3(0f, h + 0.95f, 0f), new Vector3(0.22f, 0.22f, 0.05f), Mat(Gold, 0.9f, true));
            }
        }

        /// Sapan seviyesi renkleri: direk, başlık metali, halat.
        public static readonly Color[] SlingPost = { new Color(0.6f, 0.38f, 0.2f), new Color(0.85f, 0.2f, 0.16f), new Color(0.2f, 0.42f, 0.85f),
                                                    new Color(0.7f, 0.72f, 0.76f), new Color(0.25f, 0.25f, 0.3f), new Color(1f, 0.92f, 0.75f) };
        public static readonly Color[] SlingCap = { new Color(0.62f, 0.66f, 0.72f), new Color(0.85f, 0.87f, 0.9f), new Color(0.85f, 0.87f, 0.9f),
                                                   new Color(0.95f, 0.3f, 0.25f), new Color(1f, 0.85f, 0.2f), Gold };
        public static readonly Color[] SlingRope = { new Color(0.3f, 0.3f, 0.32f), new Color(1f, 0.5f, 0.1f), new Color(0.2f, 0.55f, 1f),
                                                    new Color(0.95f, 0.25f, 0.3f), new Color(0.6f, 0.3f, 0.95f), Gold };
    }
}
