using System.Collections.Generic;
using UnityEngine;

namespace SlingRiders
{
    /// Owns explicitly registered runtime assets; imported models and shared texture caches stay untouched.
    public sealed class RuntimeResources : MonoBehaviour
    {
        readonly HashSet<Object> owned = new HashSet<Object>();

        public static RuntimeResources For(Transform root)
        {
            var resources = root.GetComponent<RuntimeResources>();
            return resources != null ? resources : root.gameObject.AddComponent<RuntimeResources>();
        }

        public T Own<T>(T asset) where T : Object
        {
            if (asset != null) owned.Add(asset);
            return asset;
        }

        public Material Solid(Color color, float gloss = 0.15f) => Own(Mats.Solid(color, gloss));
        public Material Ground(Color color, float gloss, Texture2D texture, Texture2D macro, float tiling) =>
            Own(Mats.Ground(color, gloss, texture, macro, tiling));
        public Material Water(Color color) => Own(Mats.Water(color));
        public Material Glow(Color color, Color emission, float gloss = 0.3f) => Own(Mats.Glow(color, emission, gloss));

        void OnDestroy()
        {
            foreach (var asset in owned)
                if (asset != null) Destroy(asset);
            owned.Clear();
        }
    }
}
