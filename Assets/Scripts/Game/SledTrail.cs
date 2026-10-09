using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Kızağın arkasında kalan iz (kullanıcı isteği 2026-10-09): ortada geniş, koyu bir oluk ve iki yanda kızak
    /// ayaklarının ince açık çizgileri. Kızak yerdeyken her Spacing metrede bir nokta alınır, şerit araziyi izler,
    /// eski kısım yavaşça solar. Havadayken iz kesilir (yeni parça inişte başlar).
    public class SledTrail
    {
        const float Spacing = 0.3f, Lift = 0.035f;
        const int MaxPoints = 360;   // ~108 m
        const float GrooveHalf = 0.42f, RunnerOffset = 0.3f, RunnerHalf = 0.035f;

        struct Point { public Vector3 pos; public Vector3 right; public bool cut; }

        readonly TrackProfile track;
        readonly Mesh mesh;
        readonly List<Point> points = new List<Point>();
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> tris = new List<int>();
        readonly Color groove, runner;
        bool wasGrounded;
        bool dirty;

        public SledTrail(Transform parent, TrackProfile track, TrackTheme theme)
        {
            this.track = track;
            // Karda mavi-gri oluk (videodaki gibi), çimen ve kumda zeminin koyu tonu.
            groove = theme.snowy ? new Color(0.42f, 0.56f, 0.8f, 0.42f)
                                 : new Color(theme.grass.r * 0.55f, theme.grass.g * 0.55f, theme.grass.b * 0.55f, 0.32f);
            runner = theme.snowy ? new Color(0.95f, 0.98f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.22f);
            mesh = new Mesh { name = "Sled Trail" };
            mesh.MarkDynamic();
            var go = new GameObject("Sled Trail");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { renderQueue = 2990 };   // yüzeylerin altında kalır
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        public void Clear()
        {
            points.Clear();
            wasGrounded = false;
            dirty = true;
        }

        public void Tick(SledPhysics sled, bool running)
        {
            bool grounded = running && sled.grounded && !sled.launching;
            if (grounded)
            {
                Vector3 p = sled.position;
                bool cut = !wasGrounded;
                if (cut || points.Count == 0 || (p - points[points.Count - 1].pos).sqrMagnitude >= Spacing * Spacing)
                {
                    float h = sled.heading;
                    var right = new Vector3(Mathf.Cos(h), 0f, -Mathf.Sin(h));
                    points.Add(new Point { pos = p, right = right, cut = cut });
                    if (points.Count > MaxPoints) points.RemoveAt(0);
                    dirty = true;
                }
            }
            wasGrounded = grounded;
            if (dirty) Rebuild();
        }

        void Rebuild()
        {
            dirty = false;
            verts.Clear();
            colors.Clear();
            tris.Clear();
            int n = points.Count;
            for (int i = 1; i < n; i++)
            {
                if (points[i].cut) continue;   // havadan sonraki ilk nokta yeni parça başlatır
                float fade = Mathf.Clamp01((float)i / n * 1.6f);   // en eski kısım solar
                fade *= Mathf.Clamp01((n - i) / 4f + 0.25f);      // kızağın hemen altı yumuşak başlar
                Strip(points[i - 1], points[i], 0f, GrooveHalf, groove, fade);
                Strip(points[i - 1], points[i], -RunnerOffset, RunnerHalf, runner, fade);
                Strip(points[i - 1], points[i], RunnerOffset, RunnerHalf, runner, fade);
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        /// İki nokta arasında, merkezden offset kayık, half yarı genişlikli dörtgen (köşeler zemine oturur).
        void Strip(Point a, Point b, float offset, float half, Color c, float fade)
        {
            int k = verts.Count;
            verts.Add(Ground(a.pos + a.right * (offset - half)));
            verts.Add(Ground(a.pos + a.right * (offset + half)));
            verts.Add(Ground(b.pos + b.right * (offset - half)));
            verts.Add(Ground(b.pos + b.right * (offset + half)));
            var col = new Color(c.r, c.g, c.b, c.a * fade);
            for (int j = 0; j < 4; j++) colors.Add(col);
            tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
            tris.Add(k + 1); tris.Add(k + 2); tris.Add(k + 3);
        }

        Vector3 Ground(Vector3 p) => new Vector3(p.x, track.Height(p.x, p.z) + Lift, p.z);
    }
}
