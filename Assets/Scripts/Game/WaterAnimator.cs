using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Su malzemelerinin dalgacık haritasını yavaşça kaydırır (rüzgârda kıpırdayan su).
    public class WaterAnimator : MonoBehaviour
    {
        static readonly List<Material> materials = new List<Material>();
        static WaterAnimator instance;

        public static void Register(Material m)
        {
            materials.Add(m);
            if (instance == null)
            {
                var go = new GameObject("Water Animator");
                Object.DontDestroyOnLoad(go);
                instance = go.AddComponent<WaterAnimator>();
            }
        }

        void Update()
        {
            materials.RemoveAll(m => m == null);
            float t = Time.time;
            var offset = new Vector2(t * 0.021f, t * 0.013f);
            foreach (var m in materials) m.SetTextureOffset("_BumpMap", offset);
        }
    }
}
