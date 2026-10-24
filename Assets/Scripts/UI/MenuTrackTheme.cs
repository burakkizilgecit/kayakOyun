using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SlingRiders
{
    /// One shared landscape per track for menu surfaces, never the live 3D character stage.
    public sealed class MenuTrackTheme
    {
        sealed class Surface
        {
            public VisualElement art, shade;
            public float darkness;
            public bool fullSafeArea;
        }

        readonly List<Surface> surfaces = new List<Surface>();
        readonly Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();
        readonly List<int> recent = new List<int>(3);
        int track = -1;

        public int CachedCount => textures.Count;
        public static string AssetPath(int index) => "UI/MenuThemes/" + HomeTrackTheme.Slug(index) + "/background";

        public void Register(VisualElement host, string name, float darkness)
        {
            var art = new VisualElement { name = "menu-bg-" + name, pickingMode = PickingMode.Ignore };
            art.AddToClassList("fill");
            art.AddToClassList("menu-theme-background");
            art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            art.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            art.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            art.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            if (name == "heroes-top" || name == "heroes-grid")
            {
                art.style.borderTopLeftRadius = art.style.borderTopRightRadius = 28;
                art.style.borderBottomLeftRadius = art.style.borderBottomRightRadius = 28;
                art.style.overflow = Overflow.Hidden;
                // A short header should show calm sky instead of a tightly cropped strip of foliage.
                if (name == "heroes-top")
                    art.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top);
            }
            var shade = new VisualElement { pickingMode = PickingMode.Ignore };
            shade.AddToClassList("fill");
            art.Add(shade);
            // Absolute positioning keeps the host's existing layout and callbacks intact.
            host.Insert(0, art);
            surfaces.Add(new Surface { art = art, shade = shade, darkness = darkness,
                fullSafeArea = name != "ad" && name != "heroes-top" && name != "heroes-grid" });
        }

        // Controls stay inside safe, while menu scenery continues behind notches and gesture areas.
        public void SetSafeInsets(float left, float top, float right, float bottom)
        {
            foreach (var surface in surfaces)
            {
                if (!surface.fullSafeArea) continue;
                surface.art.style.left = -left;
                surface.art.style.top = -top;
                surface.art.style.right = -right;
                surface.art.style.bottom = -bottom;
            }
        }

        public void Apply(int selectedTrack)
        {
            if (track == selectedTrack) return;
            var palette = HomeTrackTheme.Get(selectedTrack);
            string path = AssetPath(selectedTrack);
            if (!textures.TryGetValue(selectedTrack, out var texture))
            {
                texture = Resources.Load<Texture2D>(path);
                if (texture == null) throw new InvalidOperationException("[MENU THEME] Missing landscape: Resources/" + path);
                textures.Add(selectedTrack, texture);
            }
            foreach (var surface in surfaces)
            {
                surface.art.userData = path;
                surface.art.style.backgroundImage = Background.FromTexture2D(texture);
                surface.art.style.backgroundColor = palette.panel;
                for (int i = 0; i < TrackLibrary.Count; i++)
                    surface.art.EnableInClassList("menu-theme-" + HomeTrackTheme.Slug(i), i == selectedTrack);
                Color shade = Color.Lerp(palette.panel, Color.black, 0.55f);
                shade.a = surface.darkness;
                surface.shade.style.backgroundColor = shade;
            }
            track = selectedTrack;
            recent.Remove(track);
            recent.Insert(0, track);
            // All registered surfaces now reference the new texture before an old one is released.
            while (recent.Count > 2)
            {
                int old = recent[recent.Count - 1];
                recent.RemoveAt(recent.Count - 1);
                Resources.UnloadAsset(textures[old]);
                textures.Remove(old);
            }
        }
    }
}
