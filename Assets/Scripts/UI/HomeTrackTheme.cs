using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace SledSurfers
{
    /// Loads only the selected home-menu artwork; gameplay and the world behind the UI stay unchanged.
    public static class HomeTrackTheme
    {
        public struct Palette
        {
            public readonly string slug;
            public readonly Color panel, edge, accent, text;
            public Palette(string slug, Color panel, Color edge, Color accent)
            {
                this.slug = slug;
                this.panel = panel;
                this.edge = edge;
                this.accent = accent;
                text = new Color32(245, 252, 255, 255);
            }
        }

        static readonly Palette[] palettes =
        {
            new Palette("meadow", new Color32(23, 77, 49, 255), new Color32(177, 220, 104, 255), new Color32(255, 209, 74, 255)),
            new Palette("forest", new Color32(15, 53, 42, 255), new Color32(113, 183, 111, 255), new Color32(225, 183, 88, 255)),
            new Palette("canyon", new Color32(85, 44, 31, 255), new Color32(238, 158, 102, 255), new Color32(255, 198, 83, 255)),
            new Palette("winter", new Color32(12, 71, 112, 255), new Color32(140, 221, 251, 255), new Color32(255, 191, 43, 255)),
            new Palette("desert", new Color32(107, 70, 34, 255), new Color32(245, 209, 134, 255), new Color32(255, 186, 62, 255)),
            new Palette("volcano", new Color32(49, 35, 44, 255), new Color32(233, 107, 68, 255), new Color32(255, 154, 50, 255)),
            new Palette("mars", new Color32(54, 45, 69, 255), new Color32(214, 131, 100, 255), new Color32(111, 221, 226, 255))
        };

        static readonly string[] files = { "track-plaque", "play-button", "upgrade-panel" };
        static readonly string[] elementNames = { "home-art-banner", "home-art-play", "home-art-tray" };
        static readonly Dictionary<int, ThemeAssets> cache = new Dictionary<int, ThemeAssets>();
        static readonly List<int> recent = new List<int>(3);
        static readonly ConditionalWeakTable<VisualElement, CanvasBinding> canvases = new ConditionalWeakTable<VisualElement, CanvasBinding>();
        static readonly ConditionalWeakTable<VisualElement, ArtBinding> bindings = new ConditionalWeakTable<VisualElement, ArtBinding>();

        sealed class CanvasBinding { public int track = -1; }
        sealed class ThemeAssets
        {
            public readonly Sprite[] sprites = new Sprite[3];
            public readonly Texture2D[] textures = new Texture2D[3];
            public readonly string[] paths = new string[3];
        }

        // Each element owns one handler for its entire lifetime. Switching tracks changes its inputs,
        // never adds another closure; ConditionalWeakTable does not keep destroyed UI trees alive.
        sealed class ArtBinding
        {
            public readonly VisualElement element;
            public bool sliced;
            public float sourceWidth, sourceHeight;
            public int callbacks;
            public ArtBinding(VisualElement element) { this.element = element; }
            public void SetSliced(bool value)
            {
                sliced = value;
                if (!value || callbacks != 0) return;
                element.RegisterCallback<GeometryChangedEvent>(OnGeometry);
                callbacks = 1;
            }
            void OnGeometry(GeometryChangedEvent evt) => Fit(evt.newRect.size);
            public void Fit(Vector2 size)
            {
                if (!sliced || sourceWidth <= 0f || sourceHeight <= 0f || size.x <= 0f || size.y <= 0f) return;
                element.style.unitySliceScale = Mathf.Max(0.01f, Mathf.Min(size.x / sourceWidth, size.y / sourceHeight));
            }
        }

        public static Palette Get(int track)
        {
            if (track < 0 || track >= palettes.Length) throw new ArgumentOutOfRangeException(nameof(track));
            return palettes[track];
        }
        public static string Slug(int track) => Get(track).slug;
        public static int CachedThemeCount => cache.Count;
        public static int AppliedTrack(VisualElement canvas) => canvases.TryGetValue(canvas, out var value) ? value.track : -1;
        public static int SliceCallbackCount(VisualElement art) => bindings.TryGetValue(art, out var value) ? value.callbacks : 0;
        public static string AssetPath(int track, string file) => track == 3 ? "UI/HomeWinter/" + file : "UI/HomeThemes/" + Slug(track) + "/" + file;

        public static void Apply(int track, VisualElement banner, VisualElement play, VisualElement tray, VisualElement homeCanvas)
        {
            if (banner == null || play == null || tray == null || homeCanvas == null)
                throw new ArgumentNullException("Home theme artwork layers must be created before applying a theme.");
            var palette = Get(track);
            var state = canvases.GetValue(homeCanvas, _ => new CanvasBinding());
            if (state.track == track) return;

            var assets = Load(track); // Fail explicitly before changing any currently visible art.
            var layers = new[] { banner, play, tray };
            for (int i = 0; i < layers.Length; i++)
            {
                var target = layers[i];
                target.name = elementNames[i];
                target.userData = assets.paths[i];
                target.pickingMode = PickingMode.Ignore;
                target.parent?.AddToClassList("home-has-art");
                var binding = bindings.GetValue(target, element => new ArtBinding(element));
                binding.SetSliced(i == 2);
                binding.sourceWidth = assets.textures[i].width;
                binding.sourceHeight = assets.textures[i].height;
                target.style.unitySliceLeft = target.style.unitySliceRight = target.style.unitySliceTop = target.style.unitySliceBottom = 0;
                target.style.unitySliceScale = 1f;
                if (i == 2)
                {
                    target.style.backgroundImage = Background.FromTexture2D(assets.textures[i]);
                    Vector4 border = track == 3 ? new Vector4(180f, 170f, 180f, 250f)
                        : new Vector4(binding.sourceWidth * 0.1f, binding.sourceHeight * 0.19f, binding.sourceWidth * 0.1f, binding.sourceHeight * 0.28f);
                    target.style.unitySliceLeft = Mathf.RoundToInt(border.x);
                    target.style.unitySliceBottom = Mathf.RoundToInt(border.y);
                    target.style.unitySliceRight = Mathf.RoundToInt(border.z);
                    target.style.unitySliceTop = Mathf.RoundToInt(border.w);
                    binding.Fit(target.contentRect.size);
                }
                else target.style.backgroundImage = Background.FromSprite(assets.sprites[i]);
            }
            for (int i = 0; i < palettes.Length; i++)
                homeCanvas.EnableInClassList("home-theme-" + palettes[i].slug, i == track);
            // Banner crests contain transparent space around their silhouette; never fill it with a rectangle.
            if (banner.parent != null)
            {
                banner.parent.style.backgroundColor = Color.clear;
                banner.parent.style.borderTopColor = banner.parent.style.borderLeftColor = banner.parent.style.borderRightColor
                    = banner.parent.style.borderBottomColor = Color.clear;
            }
            // Preserve Frostpeak's established tray USS exactly; other trays receive theme backing colors.
            ApplyPanelColors(tray.parent, palette, track == 3);
            state.track = track;
            recent.Remove(track);
            recent.Insert(0, track);
            // All live artwork has been replaced before releasing the oldest, now unused theme.
            while (recent.Count > 2)
            {
                int oldTrack = recent[recent.Count - 1];
                recent.RemoveAt(recent.Count - 1);
                Release(cache[oldTrack]);
                cache.Remove(oldTrack);
            }
        }

        static void ApplyPanelColors(VisualElement panel, Palette palette, bool winter)
        {
            if (panel == null) return;
            panel.style.backgroundColor = winter ? new StyleColor(StyleKeyword.Null) : new StyleColor(palette.panel);
            StyleColor edge = winter ? new StyleColor(StyleKeyword.Null) : new StyleColor(palette.edge);
            panel.style.borderTopColor = panel.style.borderLeftColor = panel.style.borderRightColor = edge;
            panel.style.borderBottomColor = winter ? new StyleColor(StyleKeyword.Null) : new StyleColor(Color.Lerp(palette.panel, Color.black, 0.25f));
        }

        static ThemeAssets Load(int track)
        {
            if (cache.TryGetValue(track, out var existing)) return existing;
            var assets = new ThemeAssets();
            try
            {
                for (int i = 0; i < files.Length; i++)
                {
                    string path = assets.paths[i] = AssetPath(track, files[i]);
                    if (i != 2) assets.sprites[i] = Resources.Load<Sprite>(path);
                    assets.textures[i] = i == 2 ? Resources.Load<Texture2D>(path) : assets.sprites[i] != null ? assets.sprites[i].texture : null;
                    if (assets.textures[i] == null)
                        throw new InvalidOperationException("[HOME THEME] Missing imported " + (i == 2 ? "texture" : "sprite") + ": Resources/" + path);
                }
                cache.Add(track, assets);
                return assets;
            }
            catch
            {
                Release(assets);
                throw;
            }
        }

        static void Release(ThemeAssets assets)
        {
            for (int i = 0; i < files.Length; i++)
            {
                if (assets.sprites[i] != null) Resources.UnloadAsset(assets.sprites[i]);
                if (assets.textures[i] != null) Resources.UnloadAsset(assets.textures[i]);
            }
        }
    }
}
