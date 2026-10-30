using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SlingRiders
{
    /// Açılış ekranı (kullanıcı 2026-10-23: "oyunun açılışına ufak bir animasyonlu arka plan, ortada logo"): koyu maviden
    /// mora gökyüzü, yavaş dönen ışık huzmeleri, çapraz kayan hız çizgileri ve parıldayan yıldızlar; ortada Sling Riders
    /// logosu yaylanarak büyür, yerine oturur, hafifçe süzülür. ~2.8 s sonra (ya da dokununca) solarak kaybolur.
    public class SplashScreen : VisualElement
    {
        const float Duration = 2.8f, FadeTime = 0.45f, SkipAfter = 0.6f;
        readonly VisualElement logo;
        readonly Action onPop, onDone;
        float t;
        bool popped, closing;
        IVisualElementScheduledItem tick;

        public SplashScreen(Action onPop, Action onDone)
        {
            this.onPop = onPop;
            this.onDone = onDone;
            name = "splash";
            style.position = Position.Absolute;
            style.left = style.top = style.right = style.bottom = 0;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.backgroundColor = (Color)new Color32(0x1B, 0x2A, 0x6B, 0xFF);
            pickingMode = PickingMode.Position;
            generateVisualContent += Draw;

            logo = new VisualElement { pickingMode = PickingMode.Ignore };
            var tex = Resources.Load<Texture2D>("UI/Logo/sling-riders");
            if (tex != null)
            {
                logo.style.backgroundImage = Background.FromTexture2D(tex);
                logo.style.width = Length.Percent(88);
                float aspect = (float)tex.width / tex.height;
                logo.RegisterCallback<GeometryChangedEvent>(e => logo.style.height = e.newRect.width / aspect);
            }
            logo.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            logo.style.scale = new Scale(Vector2.zero);
            Add(logo);

            RegisterCallback<PointerDownEvent>(_ => { if (t > SkipAfter) Close(); });
            tick = schedule.Execute(() => Step(0.016f)).Every(16);
        }

        void Step(float dt)
        {
            t += dt;
            // Logo: 0.15 s bekler, sonra yaylı büyüme (aşar, geri oturur), ardından hafif süzülme ve nabız
            float u = Mathf.Clamp01((t - 0.15f) / 0.75f);
            float s = u <= 0f ? 0f : 1f + Mathf.Sin(u * Mathf.PI * 2.4f) * Mathf.Exp(-u * 4.5f) * 0.35f - (1f - u) * (1f - u) * (1f - u);
            if (u > 0f && !popped) { popped = true; onPop?.Invoke(); }
            float bob = Mathf.Sin(t * 2.2f) * 10f, pulse = 1f + 0.02f * Mathf.Sin(t * 4.4f);
            logo.style.scale = new Scale(Vector2.one * Mathf.Max(0f, s * pulse));
            logo.style.translate = new Translate(0f, bob * u);
            logo.style.rotate = new Rotate(Mathf.Sin(u * Mathf.PI * 3f) * Mathf.Exp(-u * 3f) * 8f);
            if (!closing && t > Duration) Close();
            if (closing)
            {
                float f = Mathf.Clamp01(1f - (t - closeAt) / FadeTime);
                style.opacity = f;
                if (f <= 0f)
                {
                    tick.Pause();
                    RemoveFromHierarchy();
                    onDone?.Invoke();
                    return;
                }
            }
            MarkDirtyRepaint();
        }

        float closeAt;
        void Close()
        {
            if (closing) return;
            closing = true;
            closeAt = t;
            pickingMode = PickingMode.Ignore;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float W = r.width, H = r.height;
            if (W < 10f) return;
            var p = ctx.painter2D;
            // Gökyüzü: yukarıdan aşağıya koyu lacivert → mor → pembe ufuk (yatay şeritler)
            Color top = new Color32(0x14, 0x22, 0x5E, 0xFF), mid = new Color32(0x4A, 0x2C, 0x8C, 0xFF), low = new Color32(0xE2, 0x5C, 0x9A, 0xFF);
            const int Bands = 24;
            for (int i = 0; i < Bands; i++)
            {
                float a = i / (float)Bands, b = (i + 1) / (float)Bands;
                float m = (a + b) * 0.5f;
                p.fillColor = m < 0.6f ? Color.Lerp(top, mid, m / 0.6f) : Color.Lerp(mid, low, (m - 0.6f) / 0.4f);
                p.BeginPath();
                p.MoveTo(new Vector2(0f, a * H - 1f));
                p.LineTo(new Vector2(W, a * H - 1f));
                p.LineTo(new Vector2(W, b * H + 1f));
                p.LineTo(new Vector2(0f, b * H + 1f));
                p.ClosePath();
                p.Fill();
            }
            var c = new Vector2(W * 0.5f, H * 0.47f);
            // Dönen ışık huzmeleri
            float R = Mathf.Max(W, H);
            const int Rays = 16;
            float rot = t * 9f;
            for (int i = 0; i < Rays; i++)
            {
                float a0 = (rot + i * 360f / Rays) * Mathf.Deg2Rad, a1 = a0 + 0.45f * 360f / Rays * Mathf.Deg2Rad;
                p.fillColor = new Color(1f, 0.9f, 0.6f, 0.07f);
                p.BeginPath();
                p.MoveTo(c);
                p.LineTo(c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * R);
                p.LineTo(c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * R);
                p.ClosePath();
                p.Fill();
            }
            // Ortada yumuşak parıltı
            for (int k = 7; k >= 1; k--)
            {
                p.fillColor = new Color(1f, 0.85f, 0.55f, 0.05f);
                p.BeginPath();
                p.Arc(c, W * 0.09f * k, 0f, 360f);
                p.Fill();
            }
            // Hız çizgileri: sol alttan sağ üste kayar (kızak hızı)
            for (int i = 0; i < 22; i++)
            {
                float h = Mathf.Repeat(Mathf.Sin(i * 78.233f) * 43758.5f, 1f);
                float speed = 0.5f + 0.7f * h;
                float prog = Mathf.Repeat(h * 3f + t * speed, 1.4f) - 0.2f;
                var dir = new Vector2(0.86f, -0.5f);
                var start = new Vector2(Mathf.Lerp(-0.3f, 1f, Mathf.Repeat(i * 0.618f, 1f)) * W, H * (0.55f + 0.55f * h));
                var a = start + dir * prog * W;
                float len = (0.12f + 0.18f * h) * W;
                p.strokeColor = i % 3 == 0 ? new Color(1f, 0.82f, 0.3f, 0.35f) : new Color(0.55f, 0.9f, 1f, 0.3f);
                p.lineWidth = 3f + 5f * h;
                p.lineCap = LineCap.Round;
                p.BeginPath();
                p.MoveTo(a);
                p.LineTo(a + dir * len);
                p.Stroke();
            }
            // Parıldayan yıldızlar (dört köşeli)
            for (int i = 0; i < 26; i++)
            {
                float h = Mathf.Repeat(Mathf.Sin(i * 12.9898f + 3f) * 43758.5f, 1f);
                var q = new Vector2(Mathf.Repeat(i * 0.381966f + 0.05f, 1f) * W, Mathf.Repeat(h * 1.7f, 1f) * H * 0.92f);
                float tw = 0.5f + 0.5f * Mathf.Sin(t * (2f + 3f * h) + i * 1.3f);
                float s = (6f + 14f * h) * (0.4f + 0.6f * tw);
                p.fillColor = new Color(1f, 0.95f, 0.7f, 0.35f + 0.6f * tw);
                p.BeginPath();
                p.MoveTo(q + new Vector2(0f, -s));
                p.LineTo(q + new Vector2(s * 0.25f, -s * 0.25f));
                p.LineTo(q + new Vector2(s, 0f));
                p.LineTo(q + new Vector2(s * 0.25f, s * 0.25f));
                p.LineTo(q + new Vector2(0f, s));
                p.LineTo(q + new Vector2(-s * 0.25f, s * 0.25f));
                p.LineTo(q + new Vector2(-s, 0f));
                p.LineTo(q + new Vector2(-s * 0.25f, -s * 0.25f));
                p.ClosePath();
                p.Fill();
            }
        }
    }
}
