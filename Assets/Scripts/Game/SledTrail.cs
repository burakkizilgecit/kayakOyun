using System.Collections.Generic;
using UnityEngine;

namespace SlingRiders
{
    /// Kızağın arkasında kalan iz (kullanıcı isteği 2026-10-09): ortada geniş, koyu bir oluk ve iki yanda kızak
    /// ayaklarının ince açık çizgileri. Kızak yerdeyken her Spacing metrede bir nokta alınır, şerit araziyi izler,
    /// eski kısım yavaşça solar. Havadayken iz kesilir (yeni parça inişte başlar).
    public class SledTrail
    {
        const float Spacing = 0.3f, Lift = 0.035f;
        const int MaxPoints = 200;   // ~60 m
        const float GrooveHalf = 0.36f, RunnerOffset = 0.27f, RunnerHalf = 0.022f;   // ince: iz zemini bastırmasın

        /// Noktanın zemine oturmuş 6 köşesi (oluk sol/sağ, sol ray sol/sağ, sağ ray sol/sağ): eklenirken bir kez
        /// hesaplanır. Her karede zemin sorgusu yapmak (binlerce Height çağrısı) telefonda kare hızını düşürüyordu.
        struct Point { public Vector3 g0, g1, a0, a1, b0, b1; public bool cut; }

        readonly TrackProfile track;
        readonly Mesh mesh;
        readonly List<Point> points = new List<Point>();
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> tris = new List<int>();
        readonly Color groove, runner;
        bool wasGrounded;
        Vector3 lastPos;
        bool dirty;

        public SledTrail(Transform parent, TrackProfile track, TrackTheme theme)
        {
            this.track = track;
            // Karda mavi-gri oluk (videodaki gibi), çimen ve kumda zeminin koyu tonu.
            groove = theme.snowy ? new Color(0.42f, 0.56f, 0.8f, 0.42f)
                                 : new Color(theme.trackA.r * 0.62f, theme.trackA.g * 0.62f, theme.trackA.b * 0.62f, 0.24f);
            runner = theme.snowy ? new Color(0.95f, 0.98f, 1f, 0.5f) : new Color(1f, 1f, 1f, 0.16f);
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
                if (cut || points.Count == 0 || (p - lastPos).sqrMagnitude >= Spacing * Spacing)
                {
                    float h = sled.heading;
                    var right = new Vector3(Mathf.Cos(h), 0f, -Mathf.Sin(h));
                    points.Add(new Point
                    {
                        g0 = Ground(p - right * GrooveHalf), g1 = Ground(p + right * GrooveHalf),
                        a0 = Ground(p + right * (-RunnerOffset - RunnerHalf)), a1 = Ground(p + right * (-RunnerOffset + RunnerHalf)),
                        b0 = Ground(p + right * (RunnerOffset - RunnerHalf)), b1 = Ground(p + right * (RunnerOffset + RunnerHalf)),
                        cut = cut,
                    });
                    lastPos = p;
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
                var a = points[i - 1];
                var b = points[i];
                Quad(a.g0, a.g1, b.g0, b.g1, groove, fade);
                Quad(a.a0, a.a1, b.a0, b.a1, runner, fade);
                Quad(a.b0, a.b1, b.b0, b.b1, runner, fade);
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color c, float fade)
        {
            int k = verts.Count;
            verts.Add(p0);
            verts.Add(p1);
            verts.Add(p2);
            verts.Add(p3);
            var col = new Color(c.r, c.g, c.b, c.a * fade);
            for (int j = 0; j < 4; j++) colors.Add(col);
            tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
            tris.Add(k + 1); tris.Add(k + 2); tris.Add(k + 3);
        }

        Vector3 Ground(Vector3 p) => new Vector3(p.x, track.Height(p.x, p.z) + Lift, p.z);
    }
}
