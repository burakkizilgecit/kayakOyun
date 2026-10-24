using UnityEngine;

namespace SlingRiders
{
    // Shared by live equipment and shop previews. Geometry is built only when a look changes.
    public static class EquipmentStyle
    {
        public static readonly Color[] Exhaust = {
            new Color(1f, .27f, .045f), new Color(.08f, .52f, 1f),
            new Color(.25f, 1f, .32f), new Color(.72f, .2f, 1f),
            new Color(.05f, .96f, 1f), new Color(1f, .72f, .08f)
        };

        public static Mesh PlumeMesh()
        {
            var mesh = GlassModels.Lathe(new[] { new Vector2(0, 0), new Vector2(.32f, -.04f),
                new Vector2(.48f, -.2f), new Vector2(.3f, -.55f), new Vector2(.14f, -.8f), new Vector2(0, -1f) }, 14);
            var vertices = mesh.vertices; var normals = mesh.normals;
            var rotation = Quaternion.Euler(90, 0, 0);
            for (int i = 0; i < vertices.Length; i++) { vertices[i] = rotation * vertices[i]; normals[i] = rotation * normals[i]; }
            mesh.vertices = vertices; mesh.normals = normals; mesh.RecalculateBounds();
            return mesh;
        }

        public static Transform Surface(Transform parent, string name, Mesh mesh, Material material, Vector3 pos, Quaternion rotation)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = rotation;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            RuntimeResources.For(t).Own(mesh);
            return t;
        }

        // Open annulus, not a solid cylinder: intake/nozzle interiors remain visibly recessed.
        public static Transform Ring(Transform parent, string name, Vector3 pos, float radius, float width, Material mat)
        {
            float inner = radius - width;
            return Surface(parent, name, GlassModels.Lathe(new[] {
                new Vector2(inner, -.012f), new Vector2(radius, -.012f),
                new Vector2(radius + .003f, 0f), new Vector2(radius, .012f),
                new Vector2(inner, .012f), new Vector2(inner, -.012f)
            }, 20), mat, pos, Quaternion.Euler(90f, 0f, 0f));
        }

        public static void RocketDetails(Transform root, int stage)
        {
            var resources = RuntimeResources.For(root);
            var steel = resources.Solid(new Color(.65f, .74f, .83f), .8f);
            var dark = resources.Solid(new Color(.065f, .09f, .14f), .5f);
            var accent = resources.Glow(Exhaust[stage], Exhaust[stage] * .45f);
            float tail = new[] { -.33f, -.36f, -.38f, -.32f, -.44f, -.46f }[stage];
            float radius = stage >= 3 ? .088f : .065f;
            Surface(root, "Bell nozzle", GlassModels.Lathe(new[] {
                new Vector2(radius * .6f, .055f), new Vector2(radius * .72f, .02f),
                new Vector2(radius, -.025f), new Vector2(radius * .82f, -.025f),
                new Vector2(radius * .48f, .025f)
            }, 20), dark, new Vector3(0, 0, tail + .025f), Quaternion.Euler(90, 0, 0));
            Ring(root, "Exhaust lip", new Vector3(0, 0, tail), radius, .012f, steel);
            Ring(root, "Ignition ring", new Vector3(0, 0, tail + .018f), radius * .65f, .01f, accent);
            for (int side = -1; side <= 1; side += 2)
            {
                Mats.Prim(PrimitiveType.Cube, "Side panel", root, new Vector3(side * .079f, 0, -.025f),
                    new Vector3(.012f, .045f, .14f), dark);
                for (int j = 0; j < 3; j++)
                    Mats.Prim(PrimitiveType.Cube, "Panel vent", root, new Vector3(side * .087f, 0, -.07f + j * .04f),
                        new Vector3(.008f, .03f, .012f), steel, Quaternion.Euler(25, 0, 0));
            }
        }

        public static void WingDetails(Transform wing, float span, float chord, float side, Material accent)
        {
            var old = wing.Find("Wing fittings");
            if (old != null) { old.gameObject.SetActive(false); Object.Destroy(old.gameObject); }
            var fittings = new GameObject("Wing fittings").transform;
            fittings.SetParent(wing, false);
            var dark = RuntimeResources.For(fittings).Solid(new Color(.12f, .19f, .27f), .7f);
            var hinge = Mats.Prim(PrimitiveType.Cylinder, "Hinge", fittings, new Vector3(0, .015f, 0),
                new Vector3(.075f, chord * .36f, .075f), dark, Quaternion.Euler(90, 0, 0));
            for (int i = 0; i < 2; i++)
                Mats.Prim(PrimitiveType.Cube, "Racing inlay", fittings,
                    new Vector3(span * side * (.55f + i * .15f), .08f * span * (.55f + i * .15f) + .025f, -.025f),
                    new Vector3(.035f, .012f, chord * .62f), accent, Quaternion.Euler(0, side * -12f, 0));
        }
    }

    // Two flexible braided cords and a rubber grip. Fixed buffers: no per-frame mesh/material creation.
    public sealed class GripRope
    {
        public readonly Transform Root;
        readonly Transform handle;
        readonly Mesh mesh;
        const int Segments = 14, Sides = 8;
        readonly Vector3[] vertices = new Vector3[2 * (Segments + 1) * Sides];
        readonly Vector3[] normals = new Vector3[2 * (Segments + 1) * Sides];

        public GripRope(Transform parent)
        {
            Root = new GameObject("Braided tow rope").transform;
            Root.SetParent(parent, false);
            var resources = RuntimeResources.For(Root);
            var cord = resources.Solid(new Color(.95f, .72f, .3f), .2f);
            var texture = resources.Own(new Texture2D(32, 32, TextureFormat.RGB24, true));
            texture.wrapMode = TextureWrapMode.Repeat;
            var colors = new Color[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float stripe = Mathf.Abs(Mathf.Sin((x + y * 2) * Mathf.PI / 8f));
                colors[y * 32 + x] = Color.Lerp(new Color(.28f, .23f, .16f), Color.white, stripe);
            }
            texture.SetPixels(colors); texture.Apply(); cord.mainTexture = texture;
            mesh = resources.Own(new Mesh { name = "Flexible braided pair" }); mesh.MarkDynamic();
            Root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            Root.gameObject.AddComponent<MeshRenderer>().sharedMaterial = cord;
            var uv = new Vector2[vertices.Length];
            var triangles = new int[2 * Segments * Sides * 6]; int at = 0;
            for (int rope = 0; rope < 2; rope++) for (int j = 0; j <= Segments; j++) for (int k = 0; k < Sides; k++)
            {
                int a = (rope * (Segments + 1) + j) * Sides + k;
                uv[a] = new Vector2(k / (float)Sides, j * .55f);
                if (j == Segments) continue;
                int b = (rope * (Segments + 1) + j) * Sides + (k + 1) % Sides;
                triangles[at++] = a; triangles[at++] = b; triangles[at++] = a + Sides;
                triangles[at++] = b; triangles[at++] = b + Sides; triangles[at++] = a + Sides;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            var rubber = resources.Solid(new Color(.11f, .16f, .22f), .25f);
            var metal = resources.Solid(new Color(.78f, .85f, .9f), .8f);
            handle = new GameObject("Tow handle").transform; handle.SetParent(Root, false);
            Mats.Prim(PrimitiveType.Capsule, "Rubber grip", handle, Vector3.zero, new Vector3(.045f, .11f, .045f), rubber, Quaternion.Euler(0, 0, 90));
            for (int i = -1; i <= 1; i += 2)
                Mats.Prim(PrimitiveType.Cylinder, "End ferrule", handle, new Vector3(i * .095f, 0, 0), new Vector3(.058f, .013f, .058f), metal, Quaternion.Euler(0, 0, 90));
        }

        public void Update(Vector3 anchor, Vector3 hand)
        {
            handle.localPosition = hand;
            for (int rope = 0; rope < 2; rope++)
            {
                float side = rope == 0 ? -1f : 1f;
                var a = anchor + Vector3.right * (side * .025f);
                var b = hand + Vector3.right * (side * .095f);
                for (int j = 0; j <= Segments; j++)
                {
                    float t = j / (float)Segments;
                    var center = Vector3.Lerp(a, b, t) + Vector3.down * (.065f * Mathf.Sin(t * Mathf.PI));
                    var tangent = (b - a + Vector3.down * (.065f * Mathf.PI * Mathf.Cos(t * Mathf.PI))).normalized;
                    var right = Vector3.Cross(tangent, Vector3.right).normalized;
                    var up = Vector3.Cross(tangent, right);
                    for (int k = 0; k < Sides; k++)
                    {
                        float angle = k * Mathf.PI * 2 / Sides;
                        var n = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                        int index = (rope * (Segments + 1) + j) * Sides + k;
                        vertices[index] = center + n * .011f; normals[index] = n;
                    }
                }
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.RecalculateBounds();
        }
    }
}
