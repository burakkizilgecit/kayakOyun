using System.IO;
using UnityEngine;

namespace SledSurfers
{
    // Uses the production builders, only in an explicitly requested isolated screenshot session.
    public static class EquipmentReview
    {
        public static void Capture(string folder)
        {
            if (!AutoShot.Requested || SaveData.Prefix != "autotest.") throw new System.InvalidOperationException("Isolated equipment review required");
            var cameraObject = new GameObject("Equipment review camera");
            var cam = cameraObject.AddComponent<Camera>(); cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.065f, .11f, .18f);
            cam.cullingMask = 1 << 30; cam.fieldOfView = 30; cam.nearClipPlane = .01f; cam.farClipPlane = 100;
            var sceneLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var masks = new int[sceneLights.Length];
            for (int i = 0; i < masks.Length; i++) { masks[i] = sceneLights[i].cullingMask; sceneLights[i].cullingMask &= ~(1 << 30); }
            var ambient = RenderSettings.ambientLight; var ambientMode = RenderSettings.ambientMode;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.3f, .35f, .42f);
            var lightObject = new GameObject("Equipment review light");
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1f;
            light.cullingMask = 1 << 30; light.transform.rotation = Quaternion.Euler(35, -45, 0);
            var rt = new RenderTexture(768, 768, 24) { antiAliasing = 4 }; rt.Create();
            var rocketMesh = SledModel.RocketMesh();
            bool fog = RenderSettings.fog; RenderSettings.fog = false;
            int count = 0;
            try
            {
                for (int stage = 0; stage < 6; stage++)
                {
                    var root = new GameObject("Rocket review").transform;
                    var tail = PartVisuals.BuildRocket(root, stage, rocketMesh);
                    var flame = EquipmentStyle.Surface(root, "Visible plume", EquipmentStyle.PlumeMesh(),
                        Mats.Glow(EquipmentStyle.Exhaust[stage], EquipmentStyle.Exhaust[stage]), tail, Quaternion.identity);
                    flame.localScale = new Vector3(.13f, .13f, .4f);
                    Shoot(root, "rocket_" + stage, new Vector3(.9f, .5f, -1));
                }
                for (int parts = 2; parts <= 6; parts++)
                {
                    var root = new GameObject("Addon review").transform;
                    new PartAddons(root).Show(parts);
                    Shoot(root, "addon_" + parts, new Vector3(.65f, .35f, -1));
                }
                for (int stage = 0; stage < 6; stage++)
                {
                    var root = new GameObject("Sling review").transform;
                    var sling = new SlingshotView(root, .95f, 0, 0, 1.75f); sling.SetStage(stage);
                    sling.Update(sling.Rest + new Vector3(0, -.1f, -.8f), .3f, PartVisuals.SlingRope[stage], 1);
                    Shoot(root, "sling_" + stage, new Vector3(.7f, .4f, -1));
                }
                var sledRoot = new GameObject("Sled review").transform;
                SledModel.Build(sledRoot, Mats.Solid(new Color(.6f, .7f, .8f), .8f)).SetStage(3);
                new GripRope(sledRoot).Update(SledModel.NoseEye, new Vector3(.05f, .65f, .2f));
                Shoot(sledRoot, "sled_rope", new Vector3(.8f, .8f, 1));
                File.WriteAllText(Path.Combine(folder, "equipment-gallery.txt"), "PASS: " + count + " production-model renders; finite geometry and unique exhaust palette.\n");
                for (int i = 0; i < 6; i++) for (int j = i + 1; j < 6; j++)
                    if (EquipmentStyle.Exhaust[i] == EquipmentStyle.Exhaust[j]) throw new System.Exception("Duplicate exhaust colors");
            }
            finally
            {
                RenderSettings.fog = fog;
                RenderSettings.ambientLight = ambient; RenderSettings.ambientMode = ambientMode;
                for (int i = 0; i < masks.Length; i++) if (sceneLights[i] != null) sceneLights[i].cullingMask = masks[i];
                cam.targetTexture = null; rt.Release(); Object.Destroy(rt); Object.Destroy(rocketMesh);
                Object.Destroy(cameraObject); Object.Destroy(lightObject);
            }

            void Shoot(Transform root, string name, Vector3 view)
            {
                root.position = new Vector3(0, -900, 0);
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
                var renderers = root.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                    foreach (var vertex in filter.sharedMesh.vertices)
                        if (float.IsNaN(vertex.sqrMagnitude) || float.IsInfinity(vertex.sqrMagnitude)) throw new System.Exception("Invalid equipment mesh: " + name);
                cam.transform.position = bounds.center + view.normalized * bounds.extents.magnitude / Mathf.Sin(15 * Mathf.Deg2Rad) * 1.1f;
                cam.transform.LookAt(bounds.center); cam.targetTexture = rt; cam.Render();
                var previous = RenderTexture.active; RenderTexture.active = rt;
                var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
                RenderTexture.active = previous; Object.Destroy(image);
                root.gameObject.SetActive(false); Object.Destroy(root.gameObject); count++;
            }
        }
    }
}
