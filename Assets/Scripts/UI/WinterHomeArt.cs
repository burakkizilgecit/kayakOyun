using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SledSurfers
{
    /// Shared illustrated home art, with lightweight vector details for the existing add buttons and snow accents.
    public static class WinterHomeArt
    {
        static readonly Dictionary<(Color32, Color32), Texture2D> gradients = new Dictionary<(Color32, Color32), Texture2D>();

        static Texture2D navigationAtlas;
        static readonly Sprite[] navigationSprites = new Sprite[4];

        public static VisualElement Chest(float pixels) => NavigationIcon(0, pixels);
        public static VisualElement Map(float pixels) => NavigationIcon(1, pixels);
        public static VisualElement Clipboard(float pixels) => NavigationIcon(2, pixels);
        public static VisualElement Shop(float pixels) => NavigationIcon(3, pixels);
        public static VisualElement Plus(float pixels) => new PlusGlyph(pixels);
        public static VisualElement SnowAccent(float width, float height) => new SnowLip(width, height);

        static VisualElement NavigationIcon(int index, float pixels)
        {
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.width = icon.style.height = pixels;
            icon.style.flexShrink = 0;
            if (navigationAtlas == null) navigationAtlas = Resources.Load<Texture2D>("UI/HomeWinter/navigation-icons");
            if (navigationAtlas == null)
            {
                Debug.LogError("[HOME] Missing illustrated navigation atlas.");
                return icon;
            }
            if (navigationSprites[index] == null)
            {
                float cellWidth = navigationAtlas.width * 0.5f, cellHeight = navigationAtlas.height * 0.5f;
                // The source is a 2x2 atlas. Keep the chest's outer gold edge: it reaches 94% of its cell.
                // Trim transparent margins through the sprite rectangle only; preserve the shared image pixels.
                float marginX = cellWidth * 0.06f, marginY = cellHeight * 0.06f;
                int column = index % 2, rowFromBottom = 1 - index / 2;
                var rect = new Rect(column * cellWidth + marginX, rowFromBottom * cellHeight + marginY,
                    cellWidth - 2f * marginX, cellHeight - 2f * marginY);
                navigationSprites[index] = Sprite.Create(navigationAtlas, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                navigationSprites[index].name = "Home " + new[] { "chest", "map", "clipboard", "shop" }[index];
                navigationSprites[index].hideFlags = HideFlags.HideAndDontSave;
            }
            icon.style.backgroundImage = Background.FromSprite(navigationSprites[index]);
            icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            return icon;
        }
        /// Borders follow Unity's sprite order: left, bottom, right, top, measured in source pixels.
        public static bool ApplySprite(VisualElement target, string assetName, Vector4 borders)
        {
            string path = "UI/HomeWinter/" + assetName;
            var sprite = Resources.Load<Sprite>(path);
            bool sliced = borders.sqrMagnitude > 0f;
            var texture = sliced ? Resources.Load<Texture2D>(path) : null;
            if (sliced && texture != null)
            {
                // Use source pixels directly; Sprite PPU must not change the size of painted snow caps.
                target.style.backgroundImage = Background.FromTexture2D(texture);
            }
            else if (sprite != null) target.style.backgroundImage = Background.FromSprite(sprite);
            else
            {
                texture = Resources.Load<Texture2D>(path);
                if (texture == null) return false;
                target.style.backgroundImage = Background.FromTexture2D(texture);
            }
            target.style.unitySliceLeft = Mathf.RoundToInt(borders.x);
            target.style.unitySliceBottom = Mathf.RoundToInt(borders.y);
            target.style.unitySliceRight = Mathf.RoundToInt(borders.z);
            target.style.unitySliceTop = Mathf.RoundToInt(borders.w);
            if (sliced)
            {
                float imageWidth = texture != null ? texture.width : sprite.rect.width;
                float imageHeight = texture != null ? texture.height : sprite.rect.height;
                void FitSlices(Vector2 elementSize)
                {
                    if (elementSize.x <= 0f || elementSize.y <= 0f) return;
                    // A large source has large pixel borders. Keeping scale=1 hides the panel interior
                    // under its children; shrink the corner paint with the image, then stretch only its centre.
                    target.style.unitySliceScale = Mathf.Max(0.01f,
                        Mathf.Min(elementSize.x / imageWidth, elementSize.y / imageHeight));
                }
                target.RegisterCallback<GeometryChangedEvent>(evt => FitSlices(evt.newRect.size));
                FitSlices(target.contentRect.size);
            }
            return true;
        }

        /// Tiny cached vertical surface for button shine; it is not a replacement for the illustrated panels.
        public static Texture2D Gradient(Color top, Color bottom)
        {
            var key = ((Color32)top, (Color32)bottom);
            if (gradients.TryGetValue(key, out var texture) && texture != null) return texture;
            const int height = 64;
            texture = new Texture2D(2, height, TextureFormat.RGBA32, false)
            {
                name = "Home button gradient",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var colors = new Color32[height * 2];
            for (int y = 0; y < height; y++)
            {
                Color32 color = Color.Lerp(bottom, top, y / (float)(height - 1));
                colors[y * 2] = colors[y * 2 + 1] = color;
            }
            texture.SetPixels32(colors);
            texture.Apply(false, true);
            gradients[key] = texture;
            return texture;
        }

        sealed class PlusGlyph : VisualElement
        {
            Vector2 origin;
            float size;

            public PlusGlyph(float pixels)
            {
                style.width = pixels;
                style.height = pixels;
                style.flexShrink = 0;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            Vector2 P(float x, float y) => origin + new Vector2(x, y) * size;

            void Draw(MeshGenerationContext context)
            {
                var rect = contentRect;
                size = Mathf.Min(rect.width, rect.height);
                if (size <= 1f) return;
                origin = new Vector2(rect.x + (rect.width - size) * 0.5f, rect.y + (rect.height - size) * 0.5f);
                var p = context.painter2D;
                p.lineJoin = LineJoin.Round;
                p.lineCap = LineCap.Round;
                Box(p, 0.06f, 0.1f, 0.88f, 0.86f, 0.25f, new Color32(0x16, 0x8D, 0x35, 0xFF));
                Box(p, 0.08f, 0.04f, 0.84f, 0.82f, 0.24f, new Color32(0x4C, 0xD8, 0x54, 0xFF));
                p.strokeColor = Color.white;
                p.lineWidth = size * 0.12f;
                p.BeginPath();
                p.MoveTo(P(0.5f, 0.26f)); p.LineTo(P(0.5f, 0.67f));
                p.MoveTo(P(0.29f, 0.47f)); p.LineTo(P(0.71f, 0.47f));
                p.Stroke();
            }

            void Box(Painter2D p, float x, float y, float w, float h, float radius, Color color)
            {
                float r = Mathf.Min(radius, Mathf.Min(w, h) * 0.5f);
                const float k = 0.55228475f;
                p.BeginPath();
                p.MoveTo(P(x + r, y));
                p.LineTo(P(x + w - r, y));
                p.BezierCurveTo(P(x + w - r + r * k, y), P(x + w, y + r - r * k), P(x + w, y + r));
                p.LineTo(P(x + w, y + h - r));
                p.BezierCurveTo(P(x + w, y + h - r + r * k), P(x + w - r + r * k, y + h), P(x + w - r, y + h));
                p.LineTo(P(x + r, y + h));
                p.BezierCurveTo(P(x + r - r * k, y + h), P(x, y + h - r + r * k), P(x, y + h - r));
                p.LineTo(P(x, y + r));
                p.BezierCurveTo(P(x, y + r - r * k), P(x + r - r * k, y), P(x + r, y));
                p.ClosePath();
                p.fillColor = color;
                p.Fill();
            }
        }
        sealed class SnowLip : VisualElement
        {
            public SnowLip(float width, float height)
            {
                style.width = width;
                style.height = height;
                style.flexShrink = 0;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext context)
            {
                var r = contentRect;
                if (r.width <= 1f || r.height <= 1f) return;
                var p = context.painter2D;
                Vector2 P(float x, float y) => new Vector2(r.x + r.width * x, r.y + r.height * y);
                p.BeginPath();
                p.MoveTo(P(0f, 0.68f));
                p.BezierCurveTo(P(0f, 0.35f), P(0.07f, 0.19f), P(0.16f, 0.3f));
                p.BezierCurveTo(P(0.24f, 0.08f), P(0.35f, 0.18f), P(0.4f, 0.29f));
                p.BezierCurveTo(P(0.49f, 0.13f), P(0.59f, 0.21f), P(0.64f, 0.35f));
                p.BezierCurveTo(P(0.77f, 0.1f), P(0.95f, 0.23f), P(1f, 0.58f));
                p.BezierCurveTo(P(0.94f, 0.9f), P(0.89f, 0.73f), P(0.83f, 0.67f));
                p.BezierCurveTo(P(0.6f, 0.78f), P(0.37f, 0.57f), P(0.18f, 0.7f));
                p.BezierCurveTo(P(0.07f, 0.96f), P(0.02f, 0.92f), P(0f, 0.68f));
                p.ClosePath();
                p.fillColor = new Color32(0xEF, 0xFC, 0xFF, 0xFF);
                p.Fill();
                p.strokeColor = new Color32(0xB0, 0xE3, 0xF3, 0xFF);
                p.lineWidth = 1.5f;
                p.Stroke();
            }
        }
    }
}
