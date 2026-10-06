using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Beş bardak modelinin görselleri. Kabuk, iç ve dış duvarı, kalın ağzı ve tabanı olan döndürülmüş profil;
    /// süt (çapı d·0.92 olan silindir) her modelde iç duvara sığar. Süslemeler (kulp, bantlar, kapak, pipet, ayaklar)
    /// ayrı parçalar olarak verilen kökün altına kurulur.
    public static class GlassModels
    {
        /// d: ağız dış çapı, h: yükseklik (ikisi de görsel ölçekte). side: bardağın tutulduğu taraf (+1 sağ).
        public static Mesh Build(int model, float d, float h, float side, Transform deco, Material glass, int layer)
        {
            float r = d * 0.5f;
            Mesh shell;
            switch (model)
            {
                default:
                case 0:
                {
                    // Plastik bardak: yukarı doğru açılan ince duvar, altta kabartma halkalar, ağızda kırmızı bant.
                    shell = Shell(new[] { V(r * 1.0f, 0f), V(r * 1.02f, h * 0.1f), V(r * 1.05f, h * 0.11f), V(r * 1.05f, h * 0.14f),
                                          V(r * 1.03f, h * 0.15f), V(r * 1.04f, h * 0.2f), V(r * 1.07f, h * 0.21f), V(r * 1.07f, h * 0.24f),
                                          V(r * 1.05f, h * 0.25f), V(r * 1.14f, h) }, r * 0.04f, h * 0.04f);
                    Band(deco, r * 1.125f, h * 0.9f, h * 0.995f, r * 0.03f, Mats.Solid(new Color(0.95f, 0.25f, 0.25f), 0.4f), layer);
                    break;
                }
                case 1:
                {
                    // Cam kupa: kalın duvar ve taban, yan tarafta kalın kulp.
                    shell = Shell(new[] { V(r * 1.04f, 0f), V(r * 1.06f, h * 0.02f), V(r * 1.06f, h * 0.98f), V(r * 1.04f, h) },
                                  r * 0.12f, h * 0.1f);
                    var pts = new List<Vector3>();
                    for (int k = 0; k <= 14; k++)
                    {
                        float a = Mathf.Lerp(-80f, 80f, k / 14f) * Mathf.Deg2Rad;
                        pts.Add(new Vector3(side * (r * 1.0f + Mathf.Cos(a) * h * 0.3f), h * 0.52f + Mathf.Sin(a) * h * 0.32f, 0f));
                    }
                    Part(deco, "Handle", SledModel.Tube(pts, r * 0.16f, 10), glass, layer);
                    break;
                }
                case 2:
                {
                    // Termos: çelik taban ve boyun, ortası şeffaf; kauçuk tutma halkası ve yanda taşıma tokası.
                    shell = Shell(new[] { V(r * 1.04f, 0f), V(r * 1.08f, h * 0.03f), V(r * 1.08f, h * 0.97f), V(r * 1.04f, h) },
                                  r * 0.06f, h * 0.05f);
                    var steel = Mats.Solid(new Color(0.78f, 0.8f, 0.84f), 0.85f);
                    var rubber = Mats.Solid(new Color(0.18f, 0.2f, 0.26f), 0.2f);
                    Band(deco, r * 1.06f, -0.002f, h * 0.2f, r * 0.08f, steel, layer);
                    Band(deco, r * 1.06f, h * 0.84f, h * 1.0f, r * 0.08f, steel, layer);
                    Band(deco, r * 1.07f, h * 0.2f, h * 0.24f, r * 0.1f, rubber, layer);
                    Band(deco, r * 1.07f, h * 0.8f, h * 0.84f, r * 0.1f, rubber, layer);
                    var clip = Mats.Prim(PrimitiveType.Cube, "Clip", deco, new Vector3(side * r * 1.2f, h * 0.55f, 0f),
                                         new Vector3(r * 0.14f, h * 0.5f, r * 0.45f), steel, null, layer);
                    clip.name = "Clip";
                    break;
                }
                case 3:
                {
                    // Pipetli şişe: uzun, yuvarlak omuzlu değil düz gövde; renkli kapak halkası, iki ince tutma bandı, bükük pipet.
                    shell = Shell(new[] { V(r * 0.98f, 0f), V(r * 1.06f, h * 0.03f), V(r * 1.06f, h * 0.97f), V(r * 1.02f, h) },
                                  r * 0.06f, h * 0.04f);
                    var coral = Mats.Solid(new Color(1f, 0.38f, 0.45f), 0.45f);
                    var teal = Mats.Solid(new Color(0.1f, 0.7f, 0.66f), 0.35f);
                    Band(deco, r * 1.04f, h * 0.88f, h * 1.0f, r * 0.12f, coral, layer);
                    Band(deco, r * 1.06f, h * 0.36f, h * 0.39f, r * 0.07f, teal, layer);
                    Band(deco, r * 1.06f, h * 0.45f, h * 0.48f, r * 0.07f, teal, layer);
                    var straw = new List<Vector3> { new Vector3(r * 0.25f, h * 0.5f, 0f), new Vector3(r * 0.32f, h + r * 0.4f, 0f),
                                                    new Vector3(r * 0.4f, h + r * 0.75f, 0f), new Vector3(r * 0.7f, h + r * 0.95f, 0f),
                                                    new Vector3(r * 1.2f, h + r * 1.05f, 0f) };
                    Part(deco, "Straw", SledModel.Tube(straw, r * 0.1f, 8), coral, layer);
                    break;
                }
                case 4:
                {
                    // Uzay bardağı: mor cam, parlayan camgöbeği halkalar, metal taban ve üç küçük kanatçık.
                    shell = Shell(new[] { V(r * 0.9f, 0f), V(r * 1.04f, h * 0.06f), V(r * 1.04f, h * 0.94f), V(r * 1.12f, h) },
                                  r * 0.05f, h * 0.06f);
                    var glow = Mats.Solid(new Color(0.3f, 0.95f, 1f), 0.6f);
                    glow.EnableKeyword("_EMISSION");
                    glow.SetColor("_EmissionColor", new Color(0.2f, 0.85f, 1f) * 1.4f);
                    var metal = Mats.Solid(new Color(0.75f, 0.72f, 0.85f), 0.85f);
                    Band(deco, r * 1.03f, h * 0.12f, h * 0.15f, r * 0.08f, glow, layer);
                    Band(deco, r * 1.03f, h * 0.5f, h * 0.52f, r * 0.06f, glow, layer);
                    Band(deco, r * 1.03f, h * 0.86f, h * 0.89f, r * 0.08f, glow, layer);
                    Band(deco, r * 0.85f, -0.01f, h * 0.05f, r * 0.25f, metal, layer);
                    for (int f = 0; f < 3; f++)
                    {
                        float a = (f * 120f + 30f) * Mathf.Deg2Rad;
                        var fin = Mats.Prim(PrimitiveType.Cube, "Fin", deco,
                                            new Vector3(Mathf.Cos(a) * r * 1.15f, h * 0.06f, Mathf.Sin(a) * r * 1.15f),
                                            new Vector3(r * 0.5f, h * 0.12f, r * 0.06f), metal,
                                            Quaternion.Euler(0f, -f * 120f - 30f, 0f), layer);
                        fin.name = "Fin";
                    }
                    break;
                }
            }
            return shell;
        }

        static Vector2 V(float radius, float y) => new Vector2(radius, y);

        /// Dış profil (aşağıdan yukarı) + duvar kalınlığı → dış duvar, yuvarlatılmamış ağız, iç duvar ve iç taban.
        static Mesh Shell(Vector2[] outer, float wall, float bottom)
        {
            var prof = new List<Vector2> { V(0.0005f, 0f) };
            prof.AddRange(outer);
            var top = outer[outer.Length - 1];
            prof.Add(V(top.x - wall, top.y));
            // İç duvar: dış profili içeriden izler, tabanın üstünde biter.
            for (int i = outer.Length - 2; i >= 0; i--)
            {
                var o = outer[i];
                if (o.y < bottom) break;
                prof.Add(V(o.x - wall, o.y));
            }
            float innerBottomR = Mathf.Min(outer[0].x, outer[1].x) - wall;
            prof.Add(V(innerBottomR, bottom));
            prof.Add(V(0.0005f, bottom));
            return Lathe(prof, 20);
        }

        /// Profili y ekseni etrafında döndürür; her profil parçası ayrı köşelerle (keskin kenarlar).
        public static Mesh Lathe(IList<Vector2> prof, int sides)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < prof.Count - 1; i++)
            {
                int start = verts.Count;
                for (int e = 0; e < 2; e++)
                    for (int k = 0; k <= sides; k++)
                    {
                        float a = 2f * Mathf.PI * k / sides;
                        verts.Add(new Vector3(Mathf.Cos(a) * prof[i + e].x, prof[i + e].y, Mathf.Sin(a) * prof[i + e].x));
                    }
                for (int k = 0; k < sides; k++)
                {
                    int a = start + k, b = a + 1, c = start + sides + 1 + k, dd = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, dd });
                }
            }
            var mesh = new Mesh { name = "Lathe" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// Gövdeyi saran halka bant (yarıçap r'den dışa doğru t kalınlığında, y0..y1 arası).
        static void Band(Transform deco, float r, float y0, float y1, float t, Material mat, int layer)
        {
            var mesh = Lathe(new[] { V(r, y0), V(r + t, y0), V(r + t, y1), V(r, y1) }, 20);
            Part(deco, "Band", mesh, mat, layer);
        }

        static void Part(Transform deco, string name, Mesh mesh, Material mat, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(deco, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
