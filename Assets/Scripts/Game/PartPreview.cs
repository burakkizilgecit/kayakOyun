using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Yükseltme kartlarındaki 3D önizlemeler: her kart için parçanın o anki seviyedeki görünümü (sapan, kızak,
    /// bardak, para yığını, roket) ayrı bir sahne köşesinde kurulur, kendi kamerasıyla şeffaf zemine çizilir.
    /// Yalnızca seviye görünümü değişince yeniden çizilir.
    public class PartPreview
    {
        public static PartPreview Instance { get; private set; }

        const int Layer = 31;
        const int Width = 320, Height = 260;
        static readonly Vector3 Origin = new Vector3(0f, -600f, 0f);

        readonly Camera cam;
        readonly RenderTexture[] targets = new RenderTexture[Upgrades.Count];
        readonly int[] shown = new int[Upgrades.Count];
        readonly Mesh rocketMesh;

        public PartPreview()
        {
            Instance = this;
            var go = new GameObject("Part Preview Camera");
            Object.DontDestroyOnLoad(go);
            cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 26f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            cam.allowMSAA = true;
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i] = new RenderTexture(Width, Height, 16, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Preview " + i };
                targets[i].Create();
                shown[i] = -1;
            }
            rocketMesh = SledModel.RocketMesh();
        }

        public Texture Get(int type) => targets[type];

        /// Seviyeler değişince görünümü değişen kartları yeniden çizer.
        public void Refresh(int[] levels)
        {
            for (int i = 0; i < Upgrades.Count; i++)
            {
                int stage = Mathf.Clamp(Upgrades.Stage(levels[i]), 0, 5);
                int key = i == (int)UpgradeType.Runners ? levels[i] : stage;   // kızakta kanat boyu her kademede büyür
                if (key == shown[i] && targets[i].IsCreated()) continue;
                shown[i] = key;
                Render(i, stage, levels[i]);
            }
        }

        void Render(int type, int stage, int level)
        {
            var root = new GameObject("Preview " + type).transform;
            root.position = Origin;
            Vector3 view = new Vector3(0.75f, 0.45f, -1f);   // sağ-ön-üstten 3/4 bakış
            switch ((UpgradeType)type)
            {
                case UpgradeType.Slingshot:
                {
                    var sv = new SlingshotView(root, 0.95f, 0f, 0f, 1.75f);
                    sv.SetStage(stage);
                    sv.Update(sv.Rest + new Vector3(0f, -0.15f, -0.9f), 0.15f, PartVisuals.SlingRope[stage], 1f + 0.1f * stage);
                    view = new Vector3(0.45f, 0.35f, -1f);
                    break;
                }
                case UpgradeType.Runners:
                    BuildSled(root, stage, level);
                    view = new Vector3(0.95f, 0.6f, -0.85f);
                    break;
                case UpgradeType.Glass:
                {
                    var cfg = Upgrades.BuildConfig(new[] { 0, 0, level, 0, 0 });
                    float d = cfg.glassRadius * 2f * RiderView.GlassScale, h = cfg.glassHeight * RiderView.GlassScale;
                    var deco = new GameObject("Deco").transform;
                    deco.SetParent(root, false);
                    var glassMat = Mats.Glass();
                    glassMat.color = new Color(0.85f, 0.95f, 1f, 0.45f);
                    var shell = new GameObject("Shell");
                    shell.transform.SetParent(root, false);
                    shell.AddComponent<MeshFilter>().sharedMesh = GlassModels.Build(cfg.glassModel, d, h, 1f, deco, glassMat, Layer);
                    shell.AddComponent<MeshRenderer>().sharedMaterial = glassMat;
                    Mats.Prim(PrimitiveType.Cylinder, "Milk", root, new Vector3(0f, h * 0.33f, 0f), new Vector3(d * 0.9f, h * 0.33f, d * 0.9f),
                              Mats.Solid(new Color(0.98f, 0.97f, 0.92f), 0.5f));
                    view = new Vector3(0.5f, 0.45f, -1f);
                    break;
                }
                case UpgradeType.Income:
                    BuildCoins(root, stage);
                    view = new Vector3(0.6f, 0.65f, -1f);
                    break;
                default:
                {
                    if (level == 0) stage = 0;
                    var r = new GameObject("Rocket").transform;
                    r.SetParent(root, false);
                    r.localRotation = Quaternion.Euler(-35f, 25f, 0f);
                    var tail = PartVisuals.BuildRocket(r, stage, rocketMesh);
                    var flame = Mats.Solid(new Color(1f, 0.6f, 0.1f));
                    flame.EnableKeyword("_EMISSION");
                    flame.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.05f) * 1.6f);
                    Mats.Prim(PrimitiveType.Sphere, "Flame", r, tail - new Vector3(0f, 0f, 0.1f), new Vector3(0.1f, 0.1f, 0.26f) * (1f + 0.18f * stage), flame);
                    view = new Vector3(1f, 0.25f, -0.4f);
                    break;
                }
            }

            // Katman ve gölge: yalnızca önizleme kamerası görür.
            var renderers = root.GetComponentsInChildren<Renderer>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            if (renderers.Length == 0) { Object.Destroy(root.gameObject); return; }
            var b = renderers[0].bounds;
            foreach (var r in renderers)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                b.Encapsulate(r.bounds);
            }
            // Kamera: nesneyi kadraja sığdıran uzaklık.
            float radius = b.extents.magnitude;
            float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.82f;
            cam.transform.position = b.center + view.normalized * dist;
            cam.transform.LookAt(b.center);
            cam.targetTexture = targets[type];
            var fog = RenderSettings.fog;
            RenderSettings.fog = false;
            cam.Render();
            RenderSettings.fog = fog;
            cam.targetTexture = null;
            root.gameObject.SetActive(false);
            Object.Destroy(root.gameObject);
        }

        static void BuildSled(Transform root, int stage, int level)
        {
            var runner = Mats.Solid(RiderView.RunnerColors[stage >= 5 ? 3 : stage >= 4 ? 2 : stage >= 2 ? 1 : 0], stage >= 2 ? 0.9f : 0.6f);
            var parts = SledModel.Build(root, runner);
            parts.SetStage(stage);
            if (stage >= 1)
                Mats.PlaceBetween(Mats.Prim(PrimitiveType.Cylinder, "Rope", root, Vector3.zero, Vector3.one, Mats.Solid(new Color(1f, 0.76f, 0.2f), 0.2f)),
                                  SledModel.NoseEye, new Vector3(0.15f, 0.62f, 0.25f), 0.022f);
            var cfg = Upgrades.BuildConfig(new[] { 0, level, 0, 0, 0 });
            if (cfg.wingArea > 0f)
            {
                const float chord = 0.55f;
                float span = cfg.wingArea / (2f * chord);
                int ws = stage >= 5 ? 2 : 1;
                var body = Mats.Solid(RiderView.WingColors[ws], 0.3f);
                for (int s = -1; s <= 1; s += 2)
                {
                    var w = new GameObject("Wing").transform;
                    w.SetParent(root, false);
                    w.localPosition = new Vector3(0.3f * s, 0.29f, -0.08f);
                    w.gameObject.AddComponent<MeshFilter>().sharedMesh = SledModel.WingMesh(span, chord, s);
                    w.gameObject.AddComponent<MeshRenderer>().sharedMaterial = body;
                    Mats.Prim(PrimitiveType.Cube, "Winglet", w, new Vector3(span * s, 0.08f * span + 0.06f, -0.04f * chord),
                              new Vector3(0.025f, 0.14f, 0.42f * chord), Mats.Solid(RiderView.WingletColors[ws], 0.35f));
                }
            }
        }

        /// Gelir: seviye arttıkça büyüyen altın para yığınları (+ en üst seviyelerde para kesesi).
        static void BuildCoins(Transform root, int stage)
        {
            var gold = Mats.Solid(new Color(1f, 0.78f, 0.2f), 0.85f);
            var rim = Mats.Solid(new Color(0.85f, 0.55f, 0.1f), 0.7f);
            int stacks = Mathf.Min(1 + stage, 5);
            var rnd = new System.Random(stage * 13 + 1);
            for (int k = 0; k < stacks; k++)
            {
                float a = k * 2.4f, rr = k == 0 ? 0f : 0.22f + 0.04f * k;
                var basePos = new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                int coins = 3 + stage * 2 - k + rnd.Next(2);
                for (int c = 0; c < coins; c++)
                {
                    var off = new Vector3((float)rnd.NextDouble() * 0.015f, 0.025f * c + 0.0125f, (float)rnd.NextDouble() * 0.015f);
                    Mats.Prim(PrimitiveType.Cylinder, "Coin", root, basePos + off, new Vector3(0.2f, 0.0125f, 0.2f), c % 4 == 3 ? rim : gold);
                }
            }
            if (stage >= 3)
            {
                var bag = Mats.Solid(new Color(0.6f, 0.38f, 0.2f), 0.2f);
                Mats.Prim(PrimitiveType.Sphere, "Bag", root, new Vector3(-0.3f, 0.17f, 0.12f), new Vector3(0.34f, 0.34f, 0.3f), bag);
                Mats.Prim(PrimitiveType.Cylinder, "Tie", root, new Vector3(-0.3f, 0.34f, 0.12f), new Vector3(0.12f, 0.03f, 0.12f), gold);
            }
        }
    }
}
