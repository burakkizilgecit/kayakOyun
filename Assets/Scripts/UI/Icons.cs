using UnityEngine;
using UnityEngine.UIElements;

namespace SledSurfers
{
    public enum IconKind { Coin, Flag, Gear, Back, Play, Slingshot, Runners, Wing, Glass, Hand, Swap, Wrench, Sound, Mute, Finish, Lock, Map, Check, Rocket, Income, Gem, Chest, Video, Flame }

    /// Görsel dosyası kullanmadan Painter2D ile çizilen ikon. Koordinatlar 0..1 kare içinde tanımlanır.
    public class Icon : VisualElement
    {
        static readonly Color Ink = new Color32(0x2B, 0x23, 0x40, 0xFF);
        static readonly Color Gold = new Color32(0xFF, 0xD3, 0x4D, 0xFF);
        static readonly Color GoldDark = new Color32(0xE0, 0x96, 0x10, 0xFF);
        static readonly Color Milk = new Color32(0xFF, 0xFD, 0xF5, 0xFF);

        readonly IconKind kind;
        Color tint;
        float fill = 0.7f;

        Vector2 origin;
        float size;

        public Icon(IconKind kind, float px, Color? tint = null)
        {
            this.kind = kind;
            this.tint = tint ?? Color.white;
            style.width = px;
            style.height = px;
            style.flexShrink = 0;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void SetTint(Color c)
        {
            tint = c;
            MarkDirtyRepaint();
        }

        /// Bardak ikonundaki süt seviyesi (0..1).
        public void SetFill(float f)
        {
            f = Mathf.Clamp01(f);
            if (Mathf.Abs(f - fill) < 0.01f) return;
            fill = f;
            MarkDirtyRepaint();
        }

        Vector2 P(float x, float y) => origin + new Vector2(x, y) * size;

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            size = Mathf.Min(r.width, r.height);
            if (size <= 1f) return;
            origin = new Vector2(r.x + (r.width - size) * 0.5f, r.y + (r.height - size) * 0.5f);
            var p = ctx.painter2D;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            switch (kind)
            {
                case IconKind.Coin:
                    Circle(p, 0.5f, 0.5f, 0.48f, GoldDark);
                    Circle(p, 0.5f, 0.46f, 0.4f, Gold);
                    Line(p, 0.07f, new Color(1f, 1f, 1f, 0.7f), (0.3f, 0.32f), (0.38f, 0.22f));
                    Line(p, 0.1f, GoldDark, (0.5f, 0.3f), (0.5f, 0.62f));
                    break;

                case IconKind.Flag:
                    Line(p, 0.09f, tint, (0.26f, 0.1f), (0.26f, 0.92f));
                    Poly(p, Gold, (0.3f, 0.12f), (0.86f, 0.3f), (0.3f, 0.52f));
                    break;

                case IconKind.Finish:
                    Line(p, 0.09f, tint, (0.22f, 0.1f), (0.22f, 0.92f));
                    Poly(p, Color.white, (0.26f, 0.1f), (0.86f, 0.1f), (0.86f, 0.52f), (0.26f, 0.52f));
                    for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 2; j++)
                    {
                        if ((i + j) % 2 == 1) continue;
                        float x = 0.26f + i * 0.2f, y = 0.1f + j * 0.21f;
                        Poly(p, Ink, (x, y), (x + 0.2f, y), (x + 0.2f, y + 0.21f), (x, y + 0.21f));
                    }
                    break;

                case IconKind.Gear:
                {
                    p.BeginPath();
                    const int teeth = 8;
                    for (int i = 0; i < teeth * 4; i++)
                    {
                        float a = (i + 0.5f) / (teeth * 4) * Mathf.PI * 2f;
                        float rad = (i % 4 == 0 || i % 4 == 1) ? 0.47f : 0.36f;
                        var pt = P(0.5f + Mathf.Cos(a) * rad, 0.5f + Mathf.Sin(a) * rad);
                        if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
                    }
                    p.ClosePath();
                    p.MoveTo(P(0.66f, 0.5f));
                    p.Arc(P(0.5f, 0.5f), 0.16f * size, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.ClosePath();
                    p.fillColor = tint;
                    p.Fill(FillRule.OddEven);
                    break;
                }

                case IconKind.Back:
                    Line(p, 0.17f, tint, (0.62f, 0.16f), (0.3f, 0.5f), (0.62f, 0.84f));
                    break;

                case IconKind.Play:
                    Poly(p, tint, (0.28f, 0.14f), (0.86f, 0.5f), (0.28f, 0.86f));
                    break;

                case IconKind.Slingshot:
                    Line(p, 0.15f, new Color32(0x9A, 0x5B, 0x2E, 0xFF), (0.5f, 0.92f), (0.5f, 0.56f), (0.2f, 0.14f));
                    Line(p, 0.15f, new Color32(0x9A, 0x5B, 0x2E, 0xFF), (0.5f, 0.56f), (0.8f, 0.14f));
                    Line(p, 0.07f, new Color32(0xE0, 0x3B, 0x2A, 0xFF), (0.2f, 0.18f), (0.5f, 0.4f), (0.8f, 0.18f));
                    break;

                case IconKind.Runners:
                {
                    var steel = new Color32(0x4A, 0x52, 0x60, 0xFF);
                    Line(p, 0.07f, steel, (0.32f, 0.72f), (0.32f, 0.5f));
                    Line(p, 0.07f, steel, (0.66f, 0.72f), (0.66f, 0.5f));
                    Poly(p, new Color32(0xE0, 0x3B, 0x2A, 0xFF), (0.16f, 0.36f), (0.82f, 0.36f), (0.82f, 0.52f), (0.16f, 0.52f));
                    p.strokeColor = steel;
                    p.lineWidth = 0.09f * size;
                    p.BeginPath();
                    p.MoveTo(P(0.08f, 0.72f));
                    p.LineTo(P(0.76f, 0.72f));
                    p.BezierCurveTo(P(0.9f, 0.72f), P(0.95f, 0.6f), P(0.9f, 0.46f));
                    p.Stroke();
                    break;
                }

                case IconKind.Wing:
                    p.BeginPath();
                    p.MoveTo(P(0.06f, 0.66f));
                    p.BezierCurveTo(P(0.24f, 0.24f), P(0.62f, 0.12f), P(0.95f, 0.22f));
                    p.LineTo(P(0.76f, 0.44f));
                    p.LineTo(P(0.84f, 0.48f));
                    p.LineTo(P(0.56f, 0.62f));
                    p.ClosePath();
                    p.fillColor = new Color32(0xE8, 0xF4, 0xFF, 0xFF);
                    p.Fill();
                    p.strokeColor = new Color32(0x3F, 0xA4, 0xFF, 0xFF);
                    p.lineWidth = 0.06f * size;
                    p.Stroke();
                    Line(p, 0.04f, new Color32(0x3F, 0xA4, 0xFF, 0xFF), (0.3f, 0.5f), (0.7f, 0.3f));
                    break;

                case IconKind.Glass:
                {
                    float level = 0.92f - 0.74f * fill;
                    float Lx(float y) => 0.25f + (y - 0.1f) / 0.82f * 0.07f;
                    Poly(p, Milk, (Lx(level), level), (1f - Lx(level), level), (0.68f, 0.92f), (0.32f, 0.92f));
                    p.strokeColor = tint;
                    p.lineWidth = 0.07f * size;
                    p.BeginPath();
                    p.MoveTo(P(0.25f, 0.1f));
                    p.LineTo(P(0.32f, 0.92f));
                    p.LineTo(P(0.68f, 0.92f));
                    p.LineTo(P(0.75f, 0.1f));
                    p.Stroke();
                    break;
                }

                case IconKind.Hand:
                    // Önce koyu, sonra beyaz çizerek dış hatlı bir parmak elde edilir.
                    Line(p, 0.28f, Ink, (0.5f, 0.2f), (0.5f, 0.62f));
                    Line(p, 0.5f, Ink, (0.45f, 0.72f), (0.6f, 0.74f));
                    Line(p, 0.2f, tint, (0.5f, 0.2f), (0.5f, 0.62f));
                    Line(p, 0.42f, tint, (0.45f, 0.72f), (0.6f, 0.74f));
                    break;

                case IconKind.Swap:
                    Line(p, 0.1f, tint, (0.12f, 0.33f), (0.86f, 0.33f));
                    Line(p, 0.1f, tint, (0.68f, 0.16f), (0.86f, 0.33f), (0.68f, 0.5f));
                    Line(p, 0.1f, tint, (0.88f, 0.67f), (0.14f, 0.67f));
                    Line(p, 0.1f, tint, (0.32f, 0.5f), (0.14f, 0.67f), (0.32f, 0.84f));
                    break;

                case IconKind.Wrench:
                    Line(p, 0.17f, tint, (0.2f, 0.8f), (0.6f, 0.4f));
                    p.strokeColor = tint;
                    p.lineWidth = 0.13f * size;
                    p.lineCap = LineCap.Butt;
                    p.BeginPath();
                    p.Arc(P(0.68f, 0.32f), 0.18f * size, Angle.Degrees(-10f), Angle.Degrees(260f));
                    p.Stroke();
                    break;

                case IconKind.Lock:
                    p.strokeColor = tint;
                    p.lineWidth = 0.11f * size;
                    p.BeginPath();
                    p.Arc(P(0.5f, 0.42f), 0.2f * size, Angle.Degrees(180f), Angle.Degrees(360f));
                    p.Stroke();
                    Line(p, 0.11f, tint, (0.3f, 0.42f), (0.3f, 0.5f));
                    Line(p, 0.11f, tint, (0.7f, 0.42f), (0.7f, 0.5f));
                    Poly(p, tint, (0.18f, 0.48f), (0.82f, 0.48f), (0.82f, 0.92f), (0.18f, 0.92f));
                    break;

                case IconKind.Map:
                    Poly(p, tint, (0.08f, 0.2f), (0.36f, 0.1f), (0.36f, 0.8f), (0.08f, 0.9f));
                    Poly(p, new Color(tint.r * 0.8f, tint.g * 0.8f, tint.b * 0.8f, tint.a), (0.38f, 0.1f), (0.62f, 0.2f), (0.62f, 0.9f), (0.38f, 0.8f));
                    Poly(p, tint, (0.64f, 0.2f), (0.92f, 0.1f), (0.92f, 0.8f), (0.64f, 0.9f));
                    Circle(p, 0.5f, 0.42f, 0.1f, new Color32(0xFF, 0x4D, 0x3D, 0xFF));
                    break;

                case IconKind.Flame:
                {
                    // Dış beyaz alev, içte sarı damla: turuncu butonun üstünde okunaklı
                    void FlamePath(Color c, float s, float dy)
                    {
                        p.fillColor = c;
                        p.BeginPath();
                        p.MoveTo(P(0.5f, 0.06f + dy));
                        p.BezierCurveTo(P(0.5f + 0.1f * s, 0.26f + dy), P(0.5f + 0.36f * s, 0.38f + dy), P(0.5f + 0.32f * s, 0.62f + dy));
                        p.BezierCurveTo(P(0.5f + 0.29f * s, 0.84f), P(0.5f + 0.12f * s, 0.94f), P(0.5f, 0.94f));
                        p.BezierCurveTo(P(0.5f - 0.12f * s, 0.94f), P(0.5f - 0.29f * s, 0.84f), P(0.5f - 0.32f * s, 0.62f + dy));
                        p.BezierCurveTo(P(0.5f - 0.36f * s, 0.42f + dy), P(0.5f - 0.18f * s, 0.34f + dy), P(0.5f - 0.12f * s, 0.18f + dy));
                        p.BezierCurveTo(P(0.5f - 0.04f * s, 0.32f + dy), P(0.5f, 0.2f + dy), P(0.5f, 0.06f + dy));
                        p.ClosePath();
                        p.Fill();
                    }
                    FlamePath(Color.white, 1f, 0f);
                    FlamePath(new Color32(0xFF, 0xD3, 0x4D, 0xFF), 0.55f, 0.28f);
                    break;
                }

                case IconKind.Rocket:
                {
                    var red = new Color32(0xE8, 0x3B, 0x2A, 0xFF);
                    Poly(p, new Color32(0xFF, 0x9A, 0x1F, 0xFF), (0.4f, 0.74f), (0.5f, 0.98f), (0.6f, 0.74f));
                    Poly(p, red, (0.36f, 0.52f), (0.18f, 0.78f), (0.36f, 0.74f));
                    Poly(p, red, (0.64f, 0.52f), (0.82f, 0.78f), (0.64f, 0.74f));
                    Poly(p, Color.white, (0.36f, 0.3f), (0.64f, 0.3f), (0.64f, 0.76f), (0.36f, 0.76f));
                    Poly(p, red, (0.5f, 0.04f), (0.64f, 0.3f), (0.36f, 0.3f));
                    Circle(p, 0.5f, 0.44f, 0.08f, new Color32(0x3F, 0xA4, 0xFF, 0xFF));
                    p.strokeColor = Ink;
                    p.lineWidth = 0.035f * size;
                    p.BeginPath();
                    p.MoveTo(P(0.5f, 0.04f));
                    p.LineTo(P(0.64f, 0.3f));
                    p.LineTo(P(0.64f, 0.76f));
                    p.LineTo(P(0.36f, 0.76f));
                    p.LineTo(P(0.36f, 0.3f));
                    p.ClosePath();
                    p.Stroke();
                    break;
                }

                case IconKind.Income:
                    Circle(p, 0.42f, 0.58f, 0.36f, GoldDark);
                    Circle(p, 0.42f, 0.55f, 0.29f, Gold);
                    Line(p, 0.08f, GoldDark, (0.42f, 0.42f), (0.42f, 0.68f));
                    Poly(p, new Color32(0x3F, 0xD1, 0x5B, 0xFF), (0.8f, 0.04f), (0.98f, 0.28f), (0.87f, 0.28f),
                         (0.87f, 0.5f), (0.73f, 0.5f), (0.73f, 0.28f), (0.62f, 0.28f));
                    break;

                case IconKind.Gem:
                {
                    // Pembe elmas: üstte düz tabla, altta sivri uç; yüzeyler farklı tonlarda.
                    var light = new Color32(0xFF, 0x9C, 0xD8, 0xFF);
                    var mid = new Color32(0xFF, 0x5C, 0xBE, 0xFF);
                    var dark = new Color32(0xD6, 0x2E, 0x96, 0xFF);
                    Poly(p, mid, (0.08f, 0.38f), (0.92f, 0.38f), (0.5f, 0.94f));
                    Poly(p, dark, (0.5f, 0.38f), (0.92f, 0.38f), (0.5f, 0.94f));
                    Poly(p, light, (0.26f, 0.12f), (0.74f, 0.12f), (0.92f, 0.38f), (0.08f, 0.38f));
                    Poly(p, new Color32(0xFF, 0xD6, 0xF0, 0xFF), (0.36f, 0.12f), (0.5f, 0.38f), (0.26f, 0.38f));
                    p.strokeColor = new Color32(0x9C, 0x1C, 0x6E, 0xFF);
                    p.lineWidth = 0.04f * size;
                    p.BeginPath();
                    p.MoveTo(P(0.26f, 0.12f));
                    p.LineTo(P(0.74f, 0.12f));
                    p.LineTo(P(0.92f, 0.38f));
                    p.LineTo(P(0.5f, 0.94f));
                    p.LineTo(P(0.08f, 0.38f));
                    p.ClosePath();
                    p.Stroke();
                    break;
                }

                case IconKind.Chest:
                {
                    var wood = new Color32(0xC9, 0x7A, 0x3A, 0xFF);
                    var woodDark = new Color32(0x8E, 0x4E, 0x22, 0xFF);
                    var band = new Color32(0xFF, 0xC9, 0x3C, 0xFF);
                    Poly(p, woodDark, (0.08f, 0.45f), (0.92f, 0.45f), (0.92f, 0.9f), (0.08f, 0.9f));
                    Poly(p, wood, (0.12f, 0.49f), (0.88f, 0.49f), (0.88f, 0.86f), (0.12f, 0.86f));
                    p.BeginPath();
                    p.MoveTo(P(0.08f, 0.45f));
                    p.BezierCurveTo(P(0.08f, 0.12f), P(0.92f, 0.12f), P(0.92f, 0.45f));
                    p.ClosePath();
                    p.fillColor = woodDark;
                    p.Fill();
                    Poly(p, band, (0.08f, 0.42f), (0.92f, 0.42f), (0.92f, 0.52f), (0.08f, 0.52f));
                    Poly(p, band, (0.42f, 0.36f), (0.58f, 0.36f), (0.58f, 0.66f), (0.42f, 0.66f));
                    Circle(p, 0.5f, 0.57f, 0.035f, woodDark);
                    break;
                }

                case IconKind.Video:
                    Poly(p, tint, (0.08f, 0.24f), (0.68f, 0.24f), (0.68f, 0.76f), (0.08f, 0.76f));
                    Poly(p, tint, (0.72f, 0.5f), (0.94f, 0.3f), (0.94f, 0.7f));
                    break;

                case IconKind.Check:
                    Line(p, 0.17f, tint, (0.18f, 0.52f), (0.42f, 0.76f), (0.84f, 0.26f));
                    break;

                case IconKind.Sound:
                case IconKind.Mute:
                    Poly(p, tint, (0.1f, 0.38f), (0.3f, 0.38f), (0.52f, 0.16f), (0.52f, 0.84f), (0.3f, 0.62f), (0.1f, 0.62f));
                    if (kind == IconKind.Sound)
                    {
                        p.strokeColor = tint;
                        p.lineWidth = 0.08f * size;
                        p.BeginPath();
                        p.Arc(P(0.52f, 0.5f), 0.18f * size, Angle.Degrees(-50f), Angle.Degrees(50f));
                        p.Stroke();
                        p.BeginPath();
                        p.Arc(P(0.52f, 0.5f), 0.34f * size, Angle.Degrees(-50f), Angle.Degrees(50f));
                        p.Stroke();
                    }
                    else
                    {
                        Line(p, 0.08f, tint, (0.66f, 0.36f), (0.92f, 0.64f));
                        Line(p, 0.08f, tint, (0.92f, 0.36f), (0.66f, 0.64f));
                    }
                    break;
            }
        }

        void Circle(Painter2D p, float x, float y, float r, Color c)
        {
            p.BeginPath();
            p.Arc(P(x, y), r * size, Angle.Degrees(0f), Angle.Degrees(360f));
            p.ClosePath();
            p.fillColor = c;
            p.Fill();
        }

        void Line(Painter2D p, float width, Color c, params (float x, float y)[] pts)
        {
            p.strokeColor = c;
            p.lineWidth = width * size;
            p.BeginPath();
            p.MoveTo(P(pts[0].x, pts[0].y));
            for (int i = 1; i < pts.Length; i++) p.LineTo(P(pts[i].x, pts[i].y));
            p.Stroke();
        }

        void Poly(Painter2D p, Color c, params (float x, float y)[] pts)
        {
            p.BeginPath();
            p.MoveTo(P(pts[0].x, pts[0].y));
            for (int i = 1; i < pts.Length; i++) p.LineTo(P(pts[i].x, pts[i].y));
            p.ClosePath();
            p.fillColor = c;
            p.Fill();
        }
    }
}
