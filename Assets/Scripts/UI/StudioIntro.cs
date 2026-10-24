using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SlingRiders
{
    /// RALP GAMES stüdyo açılışı (kullanıcı 2026-10-23, marka paketindeki senaryo): kömür zemin; R simgesi %85'ten %100'e,
    /// 8 birim aşağıdan yerine gelir; RALP soldan 12 birimle, GAMES daha hafif belirir; logo sabit durur, 250 ms solar.
    /// Toplam ~2.65 s, bir kez çalışır. Katmanlar SVG'den (2080x760 tuval, 2x) ayrı ayrı çizilmiş saydam görüntülerdir;
    /// tuvaldeki yerleri korunur, simge geometrisi animasyonda bozulmaz (yalnız ölçek, kayma ve saydamlık).
    public class StudioIntro : VisualElement
    {
        const float CanvasW = 2080f, CanvasH = 760f;
        const float HoldEnd = 2.4f, FadeTime = 0.25f;
        static readonly Color Charcoal = new Color32(0x14, 0x14, 0x16, 0xFF);

        readonly VisualElement logo, emblem, wordmark, descriptor;
        readonly Action onFadeStart, onDone;
        float t;
        bool fading;
        IVisualElementScheduledItem tick;

        public StudioIntro(Action onFadeStart, Action onDone)
        {
            this.onFadeStart = onFadeStart;
            this.onDone = onDone;
            name = "studio-intro";
            style.position = Position.Absolute;
            style.left = style.top = style.right = style.bottom = 0;
            style.backgroundColor = Charcoal;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            pickingMode = PickingMode.Position;   // altındaki ekrana dokunulmasın

            logo = new VisualElement { pickingMode = PickingMode.Ignore };
            logo.style.width = Length.Percent(82);
            logo.RegisterCallback<GeometryChangedEvent>(e => logo.style.height = e.newRect.width * CanvasH / CanvasW);
            Add(logo);
            emblem = Layer("ralp-emblem", new RectInt(112, 116, 522, 522));
            wordmark = Layer("ralp-wordmark", new RectInt(733, 174, 1205, 310));
            descriptor = Layer("ralp-descriptor", new RectInt(788, 528, 1065, 98));
            Apply(0f);
            tick = schedule.Execute(() => Step(0.016f)).Every(16);
        }

        /// Tuvaldeki yeri (2x piksel) yüzde olarak korunan katman.
        VisualElement Layer(string file, RectInt r)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            var tex = Resources.Load<Texture2D>("UI/Studio/" + file);
            if (tex != null) e.style.backgroundImage = Background.FromTexture2D(tex);
            e.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            e.style.position = Position.Absolute;
            e.style.left = Length.Percent(r.x / CanvasW * 100f);
            e.style.top = Length.Percent(r.y / CanvasH * 100f);
            e.style.width = Length.Percent(r.width / CanvasW * 100f);
            e.style.height = Length.Percent(r.height / CanvasH * 100f);
            logo.Add(e);
            return e;
        }

        static float EaseOut(float u) => 1f - Mathf.Pow(1f - Mathf.Clamp01(u), 3f);

        void Apply(float time)
        {
            // Senaryodaki "px" SVG birimidir (1040 genişlik): ekranda logonun genişliğine göre ölçeklenir.
            float unit = logo.resolvedStyle.width > 1f ? logo.resolvedStyle.width / 1040f : 1f;
            float e = EaseOut((time - 0.25f) / 0.6f);
            emblem.style.opacity = e;
            emblem.style.scale = new Scale(Vector2.one * Mathf.Lerp(0.85f, 1f, e));
            emblem.style.translate = new Translate(0f, (1f - e) * 8f * unit);
            float w = EaseOut((time - 0.65f) / 0.6f);
            wordmark.style.opacity = w;
            wordmark.style.translate = new Translate(-(1f - w) * 12f * unit, 0f);
            float d = EaseOut((time - 1.05f) / 0.5f);
            descriptor.style.opacity = d;
        }

        void Step(float dt)
        {
            t += dt;
            Apply(t);
            if (!fading && t >= HoldEnd)
            {
                fading = true;
                onFadeStart?.Invoke();
            }
            if (fading)
            {
                float f = Mathf.Clamp01(1f - (t - HoldEnd) / FadeTime);
                style.opacity = f;
                if (f <= 0f)
                {
                    tick.Pause();
                    RemoveFromHierarchy();
                    onDone?.Invoke();
                }
            }
        }
    }
}
