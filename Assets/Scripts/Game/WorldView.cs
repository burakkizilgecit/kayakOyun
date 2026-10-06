using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Parkur profilinden zemin, korkuluk, çimen, ağaçlar, engeller, tabelalar ve sapanı üretir.
    public class WorldView
    {
        const float SlingPostX = 1.7f;
        const float SlingPostZ = 0.3f;
        const float SlingTop = 1.75f;

        const int GuideDots = 14;

        readonly Transform root;
        readonly SlingshotView sling;
        readonly Transform[] guide = new Transform[GuideDots];
        Vector3 pouchPos, pouchVel;
        Color bandBase = new Color(0.3f, 0.3f, 0.32f);
        float bandThickness = 1f;
        bool pouchAttached = true;
        readonly Transform bestFlag;
        readonly TextMesh bestText;
        readonly Transform stopFlag;
        readonly TextMesh stopText;
        float stopClock = -1f;
        readonly TrackProfile track;
        readonly List<Transform> flockRoots = new List<Transform>();
        readonly List<Transform[]> wings = new List<Transform[]>();
        readonly TrackTheme theme;

        public WorldView(TrackProfile track, TrackTheme theme)
        {
            this.track = track;
            this.theme = theme;
            root = new GameObject("World").transform;

            Scenery.Build(root, track, theme);

            // Sapan
            sling = new SlingshotView(root, SlingPostX, SlingPostZ, track.Height(SlingPostZ), SlingTop);
            pouchPos = sling.Rest;

            var dotMat = Mats.Solid(Color.white, 0.6f);
            dotMat.EnableKeyword("_EMISSION");
            dotMat.SetColor("_EmissionColor", new Color(0.6f, 0.6f, 0.6f));
            for (int i = 0; i < GuideDots; i++)
                guide[i] = Mats.Prim(PrimitiveType.Cylinder, "Guide", root, Vector3.zero, new Vector3(0.28f, 0.02f, 0.28f), dotMat);
            ShowGuide(0f, 0f);

            // Kuş sürüleri: her biri ~10 kuş (gövde + iki kanat), sürünün merkezi fizikle aynı formülle salınır.
            var birdMat = Mats.Solid(new Color(0.18f, 0.18f, 0.22f), 0.2f);
            var rnd = new System.Random(5);
            foreach (var f in track.flocks)
            {
                var flock = new GameObject("Flock").transform;
                flock.SetParent(root, false);
                var flaps = new List<Transform>();
                for (int b = 0; b < 10; b++)
                {
                    var bird = new GameObject("Bird").transform;
                    bird.SetParent(flock, false);
                    float a = (float)rnd.NextDouble() * 6.28f, r = (float)rnd.NextDouble() * f.radius * 0.75f;
                    bird.localPosition = new Vector3(Mathf.Cos(a) * r, ((float)rnd.NextDouble() - 0.5f) * 1.4f, Mathf.Sin(a) * r * 0.6f);
                    bird.localRotation = Quaternion.Euler(0f, 180f + ((float)rnd.NextDouble() - 0.5f) * 40f, 0f);
                    Mats.Prim(PrimitiveType.Sphere, "Body", bird, Vector3.zero, new Vector3(0.18f, 0.16f, 0.42f), birdMat);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var pivot = new GameObject("Wing").transform;
                        pivot.SetParent(bird, false);
                        Mats.Prim(PrimitiveType.Cube, "Feathers", pivot, new Vector3(0.28f * s, 0f, 0f), new Vector3(0.52f, 0.03f, 0.22f), birdMat);
                        flaps.Add(pivot);
                    }
                }
                flockRoots.Add(flock);
                wings.Add(flaps.ToArray());
            }

            // Rekor bayrağı
            bestFlag = new GameObject("Best Flag").transform;
            bestFlag.SetParent(root, false);
            var gold = Mats.Solid(new Color(1f, 0.78f, 0.1f), 0.5f);
            Mats.Prim(PrimitiveType.Cylinder, "Pole", bestFlag, new Vector3(0f, 1.5f, 0f), new Vector3(0.08f, 1.5f, 0.08f), Mats.Solid(Color.white));
            Mats.Prim(PrimitiveType.Cube, "Flag", bestFlag, new Vector3(0.45f, 2.6f, 0f), new Vector3(0.8f, 0.5f, 0.04f), gold);
            bestText = MakeText(bestFlag, new Vector3(0f, 3.1f, 0f), "REKOR", 0.06f, new Color(1f, 0.85f, 0.2f));
            bestFlag.gameObject.SetActive(false);

            // Atışın bittiği noktaya dikilen mesafe bayrağı
            stopFlag = new GameObject("Stop Flag").transform;
            stopFlag.SetParent(root, false);
            var red = Mats.Solid(new Color(0.95f, 0.3f, 0.2f), 0.4f);
            Mats.Prim(PrimitiveType.Cylinder, "Pole", stopFlag, new Vector3(0f, 1.25f, 0f), new Vector3(0.09f, 1.25f, 0.09f), Mats.Solid(Color.white));
            Mats.Prim(PrimitiveType.Cube, "Flag", stopFlag, new Vector3(0.55f, 2.15f, 0f), new Vector3(1.05f, 0.6f, 0.05f), red);
            Mats.Prim(PrimitiveType.Sphere, "Knob", stopFlag, new Vector3(0f, 2.55f, 0f), Vector3.one * 0.18f, Mats.Solid(new Color(1f, 0.8f, 0.2f), 0.6f));
            stopText = MakeText(stopFlag, new Vector3(0.5f, 2.15f, -0.04f), "", 0.05f, Color.white);
            stopFlag.gameObject.SetActive(false);
        }

        /// Atış bitince en uzak noktaya bayrak dikilir ve mesafe yazılır.
        public void PlantFlag(Vector3 groundPos, int meters)
        {
            stopFlag.position = groundPos;
            stopText.text = meters + " m";
            stopFlag.localScale = Vector3.zero;
            stopFlag.gameObject.SetActive(true);
            stopClock = 0f;
        }

        public void ClearFlag()
        {
            stopFlag.gameObject.SetActive(false);
            stopClock = -1f;
        }

        /// Bayrak yere saplanıyormuş gibi yukarıdan iner ve hafifçe sallanır.
        public void UpdateFlag(float dt, Vector3 cameraPos)
        {
            if (stopClock < 0f) return;
            stopClock += dt;
            float t = Mathf.Clamp01(stopClock / 0.35f);
            float s = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
            stopFlag.localScale = Vector3.one * Mathf.Max(s, 0f);
            float wobble = 6f * Mathf.Exp(-stopClock * 3f) * Mathf.Sin(stopClock * 18f);
            Vector3 toCam = cameraPos - stopFlag.position;
            toCam.y = 0f;
            float yaw = toCam.sqrMagnitude > 0.01f ? Quaternion.LookRotation(-toCam).eulerAngles.y : 0f;
            stopFlag.rotation = Quaternion.Euler(0f, yaw, wobble);
        }

        public Transform Root => root;

        /// Kuş sürülerini atışın saatine göre konumlar, kanatları çırptırır.
        public void AnimateBirds(float clock, float time)
        {
            for (int i = 0; i < flockRoots.Count; i++)
            {
                flockRoots[i].position = track.FlockCenter(track.flocks[i], clock);
                var w = wings[i];
                for (int k = 0; k < w.Length; k++)
                {
                    float side = k % 2 == 0 ? -1f : 1f;
                    w[k].localRotation = Quaternion.Euler(0f, 0f, side * 35f * Mathf.Sin(time * 14f + k * 0.7f));
                }
            }
        }

        /// Parkur değişince eski dünya silinir.
        public void Destroy() => Object.Destroy(root.gameObject);

        public void SetBest(int meters)
        {
            bestFlag.gameObject.SetActive(meters > 2);
            float z = meters;
            float x = track.CenterX(z) - track.halfWidth - 0.6f;
            bestFlag.position = new Vector3(x, track.Height(x, z), z);
            bestText.text = "REKOR " + meters + " m";
        }

        /// attachPoint: lastiğin kızağa bağlandığı nokta. Kızak bırakılınca lastik (kütleli bir yay gibi)
        /// ileri şaklar, sapanın önüne taşar ve titreşerek durulur.
        public void UpdateSlingshot(Vector3 attachPoint, Vector3 sledVelocity, bool attached, float dt)
        {
            Vector3 rest = sling.Rest;
            if (attached)
            {
                pouchPos = attachPoint;
                pouchVel = sledVelocity;
            }
            else
            {
                if (pouchAttached) pouchVel = sledVelocity;   // bırakıldığı an
                const float w = 28f, zeta = 0.1f;
                Vector3 acc = -w * w * (pouchPos - rest) - 2f * zeta * w * pouchVel;
                pouchVel += acc * dt;
                pouchPos += pouchVel * dt;
            }
            pouchAttached = attached;

            // Gerildikçe lastik incelir ve kızarır.
            float stretch = Mathf.Clamp01((Vector3.Distance(pouchPos, rest) - 0.3f) / 6f);
            sling.Update(pouchPos, stretch, Color.Lerp(bandBase, new Color(0.95f, 0.2f, 0.12f), stretch), bandThickness);
        }

        /// Sapan yükseltmesi: lastik kalınlaşır ve rengi koyu griden turuncuya döner (0..1).
        /// Sapan seviyesi: her aşamada direkler boyanır ve halat rengi değişir, seviyeyle halat kalınlaşır.
        public void SetSlingLevel(int level)
        {
            int stage = Mathf.Clamp(Upgrades.Stage(level), 0, 5);
            sling.SetStage(stage);
            bandBase = PartVisuals.SlingRope[stage];
            bandThickness = 1f + 0.5f * level / Upgrades.MaxLevel((int)UpgradeType.Slingshot);
        }

        /// Zeminde fırlatma yönü ve gücünü gösteren noktalar.
        public void ShowGuide(float aim, float pull)
        {
            bool visible = pull > 0.03f;
            var dir = new Vector3(Mathf.Sin(aim), 0f, Mathf.Cos(aim));
            float length = 4f + 22f * pull;
            for (int i = 0; i < GuideDots; i++)
            {
                guide[i].gameObject.SetActive(visible);
                if (!visible) continue;
                float t = (i + 1f) / GuideDots;
                Vector3 p = new Vector3(0f, 0f, 1.2f) + dir * (length * t);
                p.y = track.Height(p.x, p.z) + 0.04f;
                guide[i].position = p;
                float s = Mathf.Lerp(0.6f, 0.3f, t);
                guide[i].localScale = new Vector3(s, 0.02f, s);
            }
        }

        public static TextMesh MakeText(Transform parent, Vector3 localPos, string text, float size, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            return tm;
        }
    }
}
