using UnityEngine;
using UnityEngine.UIElements;

namespace SledSurfers
{
    /// Ödül arkasında yavaşça dönen ışın patlaması: sıcak sarı dilimler, ortada yumuşak parıltı (sandık açılışı).
    public class RayBurst : VisualElement
    {
        float angle;

        public RayBurst()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.top = style.right = style.bottom = 0;
            generateVisualContent += Draw;
            schedule.Execute(() => { angle += 0.35f; MarkDirtyRepaint(); }).Every(16);
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 10f) return;
            var p = ctx.painter2D;
            var c = r.center;
            float R = Mathf.Max(r.width, r.height) * 0.75f;
            const int Rays = 14;
            for (int i = 0; i < Rays; i++)
            {
                float a0 = (angle + i * 360f / Rays) * Mathf.Deg2Rad, a1 = a0 + 0.5f * 360f / Rays * Mathf.Deg2Rad;
                p.fillColor = new Color(1f, 0.93f, 0.55f, 0.28f);
                p.BeginPath();
                p.MoveTo(c);
                p.LineTo(c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * R);
                p.LineTo(c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * R);
                p.ClosePath();
                p.Fill();
            }
            for (int k = 6; k >= 1; k--)
            {
                p.fillColor = new Color(1f, 0.95f, 0.65f, 0.09f);
                p.BeginPath();
                p.Arc(c, R * 0.12f * k, 0f, 360f);
                p.Fill();
            }
        }
    }

    /// Ekranın üstünden dönerek düşen renkli kâğıt parçaları (sürekli döngü).
    public class RewardConfetti : VisualElement
    {
        float t;
        static readonly Color[] Colors =
        {
            new Color32(0xFF, 0x4F, 0x8B, 0xFF), new Color32(0xFF, 0xC9, 0x3C, 0xFF), new Color32(0x3F, 0xC0, 0x7A, 0xFF),
            new Color32(0x3F, 0xA9, 0xF5, 0xFF), new Color32(0x9B, 0x6B, 0xFF, 0xFF), new Color32(0xFF, 0x8A, 0x3D, 0xFF),
        };

        public RewardConfetti()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.top = style.right = style.bottom = 0;
            generateVisualContent += Draw;
            schedule.Execute(() => { t += 0.016f; MarkDirtyRepaint(); }).Every(16);
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 10f) return;
            var p = ctx.painter2D;
            for (int i = 0; i < 46; i++)
            {
                float h = Mathf.Repeat(Mathf.Sin(i * 91.7f) * 4375.85f, 1f);
                float speed = 0.08f + 0.06f * h;
                float y = Mathf.Repeat(h + t * speed, 1.1f) - 0.05f;
                float x = Mathf.Repeat(i * 0.6180339f, 1f) + 0.03f * Mathf.Sin(t * 2f + i);
                var c = new Vector2(x * r.width, y * r.height);
                float rot = t * (2f + 3f * h) + i;
                var d = new Vector2(Mathf.Cos(rot), Mathf.Sin(rot));
                var n = new Vector2(-d.y, d.x) * Mathf.Abs(Mathf.Cos(t * 4f + i));
                float w = 14f, l = 26f;
                p.fillColor = Colors[i % Colors.Length];
                p.BeginPath();
                p.MoveTo(c - d * l * 0.5f - n * w * 0.5f);
                p.LineTo(c + d * l * 0.5f - n * w * 0.5f);
                p.LineTo(c + d * l * 0.5f + n * w * 0.5f);
                p.LineTo(c - d * l * 0.5f + n * w * 0.5f);
                p.ClosePath();
                p.Fill();
            }
        }
    }
}
