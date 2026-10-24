using UnityEngine;
using UnityEngine.UIElements;

namespace SlingRiders
{
    /// Fırlatma nişanı (kullanıcı 2026-10-23, örnek görsele göre): kızaktan ileri kavisli bir yol üzerinde akan köşeli oklar,
    /// iki ince iz çizgisi, ucunda büyük ok başı ve kıvılcımlar. Güç arttıkça yol uzar, renk sarıdan turuncuya, pembeye ve
    /// mora döner; nişan yönüne göre sağa-sola kıvrılır.
    public class AimArrows : VisualElement
    {
        public float power, aim, time;
        public Vector2 origin = new Vector2(0.5f, 0.64f);   // ekran oranı: kızağın üstü

        static readonly Color[] Ramp =
        {
            new Color32(0xFF, 0xD8, 0x4A, 0xFF), new Color32(0xFF, 0x9A, 0x2E, 0xFF),
            new Color32(0xFF, 0x4F, 0x8B, 0xFF), new Color32(0x9B, 0x5B, 0xFF, 0xFF),
        };

        public AimArrows()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.top = style.right = style.bottom = 0;
            generateVisualContent += Draw;
        }

        static Color RampAt(float t)
        {
            t = Mathf.Clamp01(t) * (Ramp.Length - 1);
            int i = Mathf.Min(Ramp.Length - 2, (int)t);
            return Color.Lerp(Ramp[i], Ramp[i + 1], t - i);
        }

        Vector2 Point(float u, Vector2 a, Vector2 c, Vector2 b)
            => (1 - u) * (1 - u) * a + 2 * (1 - u) * u * c + u * u * b;

        void Draw(MeshGenerationContext ctx)
        {
            if (power <= 0.01f) return;
            var r = contentRect;
            float W = r.width, H = r.height;
            if (W < 10f || H < 10f) return;
            var p = ctx.painter2D;
            float len = Mathf.Lerp(0.24f, 0.44f, power) * H;
            float side = Mathf.Sin(aim) * W * 0.55f;
            var a = new Vector2(origin.x * W, origin.y * H);
            var b = a + new Vector2(side, -len);
            var c = a + new Vector2(side * 0.15f, -len * 0.7f);   // önce dik kalkar, sonra yana kıvrılır
            Color end = RampAt(0.25f + 0.75f * power);

            // İki ince iz çizgisi
            for (int s = -1; s <= 1; s += 2)
            {
                p.lineWidth = 5f;
                p.lineCap = LineCap.Round;
                p.BeginPath();
                for (int i = 0; i <= 24; i++)
                {
                    float u = i / 24f;
                    var q = Point(u, a, c, b) + new Vector2(s * (10f + 26f * u), 0f);
                    if (i == 0) p.MoveTo(q); else p.LineTo(q);
                }
                p.strokeColor = new Color(1f, 1f, 1f, 0.55f);
                p.Stroke();
            }

            // Akan köşeli oklar (zamanla yola doğru ilerler)
            const int N = 6;
            for (int k = 0; k < N; k++)
            {
                float u = Mathf.Repeat(k / (float)N + time * 0.55f, 1f) * 0.86f;
                var q = Point(u, a, c, b);
                var dir = (Point(u + 0.02f, a, c, b) - q).normalized;
                var nrm = new Vector2(-dir.y, dir.x);
                float size = Mathf.Lerp(26f, 46f, u) * (0.8f + 0.4f * power);
                var col = RampAt(u * (0.25f + 0.75f * power));
                col.a = Mathf.Clamp01(Mathf.Min(u * 6f, (0.86f - u) * 6f));
                p.lineWidth = size * 0.42f;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;
                p.strokeColor = col;
                p.BeginPath();
                p.MoveTo(q - dir * size * 0.5f + nrm * size * 0.6f);
                p.LineTo(q + dir * size * 0.5f);
                p.LineTo(q - dir * size * 0.5f - nrm * size * 0.6f);
                p.Stroke();
            }

            // Ok gövdesi (son kısım) ve büyük ok başı
            var tail = Point(0.72f, a, c, b);
            var tip = b;
            var d = (tip - Point(0.97f, a, c, b)).normalized;
            var n = new Vector2(-d.y, d.x);
            float head = Mathf.Lerp(80f, 120f, power);
            p.lineWidth = head * 0.42f;
            p.lineCap = LineCap.Round;
            p.strokeColor = Color.Lerp(RampAt(0.5f * power), end, 0.5f);
            p.BeginPath();
            p.MoveTo(tail);
            for (int i = 1; i <= 8; i++) p.LineTo(Point(Mathf.Lerp(0.72f, 0.95f, i / 8f), a, c, b));
            p.Stroke();
            p.fillColor = end;
            p.BeginPath();
            p.MoveTo(tip + d * head * 0.55f);
            p.LineTo(tip - d * head * 0.35f + n * head * 0.62f);
            p.LineTo(tip - d * head * 0.1f);
            p.LineTo(tip - d * head * 0.35f - n * head * 0.62f);
            p.ClosePath();
            p.Fill();
            // Parlak iç çizgi
            p.lineWidth = 4f;
            p.strokeColor = new Color(1f, 1f, 1f, 0.7f);
            p.BeginPath();
            p.MoveTo(tip - d * head * 0.18f + n * head * 0.36f);
            p.LineTo(tip + d * head * 0.4f);
            p.Stroke();

            // Kıvılcımlar
            for (int k = 0; k < 14; k++)
            {
                float seed = k * 12.9898f;
                float u = Mathf.Repeat(Mathf.Sin(seed) * 43758.5f, 1f);
                float tw = 0.5f + 0.5f * Mathf.Sin(time * 6f + k * 1.7f);
                var q = Point(Mathf.Lerp(0.3f, 1f, u), a, c, b)
                        + new Vector2(Mathf.Sin(seed * 3.1f) * 40f, Mathf.Cos(seed * 2.3f) * 30f);
                var col = Color.Lerp(Color.white, RampAt(u), 0.4f);
                col.a = tw * Mathf.Clamp01(power * 1.5f);
                p.fillColor = col;
                p.BeginPath();
                p.Arc(q, 4f + 5f * tw, 0f, 360f);
                p.Fill();
            }
        }
    }
}
