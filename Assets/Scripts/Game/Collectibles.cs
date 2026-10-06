using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Pistteki coinler ve nadir pembe elmaslar. Her atışta yeniden dizilir; kızak yanından geçerken toplanır.
    /// Düz kısımlarda sıra sıra coin, çukurların üstünde yay biçiminde coin ve yayın tepesinde elmas bulunur.
    public class Collectibles
    {
        public const int CoinValue = 2;
        const float PickRadius = 1.6f;
        const float CoinSize = 0.9f;
        const float GemSize = 1.1f;

        class Item
        {
            public Vector3 pos;
            public bool gem, taken;
            public Transform tf;
            public float phase, popClock = -1f;
        }

        readonly List<Item> items = new List<Item>();
        readonly Transform root;

        public int coins, gems;   // bu atışta toplananlar
        public int CoinCount { get; }
        public int GemCount { get; }

        public Collectibles(TrackProfile track, Transform parent)
        {
            root = new GameObject("Collectibles").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(track.name.GetHashCode() ^ 0x5eed);
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();

            bool NearWater(float z, float margin)
            {
                foreach (var w in track.water)
                    if (z > w.x - margin && z < w.y + margin) return true;
                return false;
            }

            // 1) Düz kısımlarda sıra sıra coin; engelin önündeki şeritten kaçınılır.
            float[] lanes = { -2.6f, 0f, 2.6f };
            for (float z = 28f; z < track.finishZ - 12f; z += R(26f, 42f))
            {
                int n = 5 + rng.Next(4);
                float length = n * 2.2f;
                if (NearWater(z, 10f) || NearWater(z + length, 10f)) continue;
                float lane = lanes[rng.Next(lanes.Length)];
                for (int tries = 0; tries < 3 && Blocked(track, lane, z, z + length); tries++)
                    lane = lanes[(System.Array.IndexOf(lanes, lane) + 1) % lanes.Length];
                if (Blocked(track, lane, z, z + length)) continue;
                for (int i = 0; i < n; i++)
                {
                    float zz = z + i * 2.2f;
                    float x = track.PathX(zz) + lane;
                    Add(new Vector3(x, track.Height(x, zz) + 0.8f, zz), false);
                }
                z += length;
            }

            // 2) Çukurların üstünde yay: atlayan kızağın yoluna benzer; tepesinde pembe elmas.
            foreach (var w in track.water)
            {
                float z0 = w.x - 2f, z1 = w.y + 2f;
                float width = z1 - z0;
                float lift = Mathf.Clamp(width * 0.12f, 2.5f, 14f);
                float h0 = track.Height(z0 - 6f), h1 = track.Height(z1 + 6f);
                int n = Mathf.Clamp(Mathf.RoundToInt(width / 3.2f), 4, 40);
                for (int i = 0; i <= n; i++)
                {
                    float t = (float)i / n;
                    float zz = Mathf.Lerp(z0, z1, t);
                    float y = Mathf.Lerp(h0, h1, t) + 1.4f + lift * Mathf.Sin(t * Mathf.PI);
                    Add(new Vector3(track.CenterX(zz), y, zz), i == n / 2);
                }
            }

            // 3) Pistin zor kısımlarında birer elmas (iyi atışların ödülü).
            foreach (float f in new[] { 0.45f, 0.82f })
            {
                float z = track.finishZ * f;
                for (int k = 0; k < 20 && NearWater(z, 8f); k++) z += 6f;
                float gx = track.PathX(z);
                Add(new Vector3(gx, track.Height(gx, z) + 1.1f, z), true);
            }

            foreach (var it in items) { if (it.gem) GemCount++; else CoinCount++; }
            Spawn();
        }

        static bool Blocked(TrackProfile track, float lane, float z0, float z1)
        {
            foreach (var o in track.obstacles)
                if (o.z > z0 - 4f && o.z < z1 + 4f && Mathf.Abs(o.x - (track.PathX(o.z) + lane)) < o.halfWidth + 1.4f) return true;
            return false;
        }

        /// Elmasların pistteki z konumları (ilerleme çubuğu için).
        public List<float> GemPositions()
        {
            var list = new List<float>();
            foreach (var it in items) if (it.gem) list.Add(it.pos.z);
            return list;
        }

        void Add(Vector3 pos, bool gem) => items.Add(new Item { pos = pos, gem = gem, phase = pos.z * 0.37f });

        void Spawn()
        {
            var coinPrefab = Resources.Load<GameObject>("Env/Platformer/coin-gold");
            var gemPrefab = Resources.Load<GameObject>("Env/Platformer/jewel");
            var gemMat = Mats.Solid(new Color(1f, 0.38f, 0.78f), 0.85f);
            gemMat.EnableKeyword("_EMISSION");
            gemMat.SetColor("_EmissionColor", new Color(0.9f, 0.15f, 0.55f) * 0.9f);

            foreach (var it in items)
            {
                var prefab = it.gem ? gemPrefab : coinPrefab;
                GameObject go;
                if (prefab != null)
                {
                    go = Object.Instantiate(prefab, root);
                    // Modeli istenen boya ölçekle ve ortasını noktaya al.
                    var b = MeasureBounds(go);
                    float size = it.gem ? GemSize : CoinSize;
                    float s = size / Mathf.Max(Mathf.Max(b.size.x, b.size.y), 0.01f);
                    var holder = new GameObject(it.gem ? "Gem" : "Coin").transform;
                    holder.SetParent(root, false);
                    go.transform.SetParent(holder, false);
                    go.transform.localScale *= s;
                    go.transform.localPosition = -b.center * s;
                    if (it.gem)
                        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = gemMat;
                    go = holder.gameObject;
                }
                else
                {
                    go = GameObject.CreatePrimitive(it.gem ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
                    Object.Destroy(go.GetComponent<Collider>());
                    go.transform.SetParent(root, false);
                    go.transform.localScale = it.gem ? Vector3.one * GemSize : new Vector3(CoinSize, 0.06f, CoinSize);
                    go.GetComponent<Renderer>().sharedMaterial = it.gem ? gemMat : Mats.Solid(new Color(1f, 0.8f, 0.2f), 0.7f);
                }
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.position = it.pos;
                it.tf = go.transform;
            }
        }

        static Bounds MeasureBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.one);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            b.center -= go.transform.position;
            return b;
        }

        /// Yeni atış: hepsi yerine döner.
        /// Çarpma sahnesinde kamera ile karakter arasında kalmasınlar diye yakındaki toplanabilirleri gizler.
        public void HideAround(Vector3 p, float radius)
        {
            foreach (var it in items)
                if (!it.taken && (it.pos - p).sqrMagnitude < radius * radius) it.tf.gameObject.SetActive(false);
        }

        public void ResetRun()
        {
            coins = gems = 0;
            foreach (var it in items)
            {
                it.taken = false;
                it.popClock = -1f;
                it.tf.gameObject.SetActive(true);
                it.tf.position = it.pos;
                it.tf.localScale = Vector3.one;
            }
        }

        /// Kızağın yanından geçtiği coin/elmasları toplar; bu karede toplanan coin ve elmas sayısını döner.
        public void Collect(Vector3 sledPos, float radiusScale, out int newCoins, out int newGems)
        {
            newCoins = newGems = 0;
            float radius = PickRadius * radiusScale;
            Vector3 c = sledPos + new Vector3(0f, 0.6f, 0f);
            foreach (var it in items)
            {
                if (it.taken || Mathf.Abs(it.pos.z - c.z) > radius) continue;
                if ((it.pos - c).sqrMagnitude > radius * radius) continue;
                it.taken = true;
                it.popClock = 0f;
                if (it.gem) { gems++; newGems++; }
                else { coins++; newCoins++; }
            }
        }

        /// Dönme ve süzülme; toplananlar küçülüp yukarı uçar. Sadece kızağın yakınındakiler güncellenir.
        public void Animate(float dt, float sledZ, float time)
        {
            foreach (var it in items)
            {
                if (it.pos.z < sledZ - 20f || it.pos.z > sledZ + 160f) continue;
                if (it.popClock >= 0f)
                {
                    if (!it.tf.gameObject.activeSelf) continue;
                    it.popClock += dt;
                    float t = it.popClock / 0.35f;
                    it.tf.position = it.pos + Vector3.up * (2.5f * t);
                    it.tf.localScale = Vector3.one * Mathf.Max(0f, 1f + 0.4f * t - 1.4f * t * t);
                    if (t >= 1f) it.tf.gameObject.SetActive(false);
                    continue;
                }
                float bob = it.gem ? 0.2f * Mathf.Sin(time * 2.5f + it.phase) : 0f;
                it.tf.position = it.pos + Vector3.up * bob;
                it.tf.rotation = Quaternion.Euler(0f, time * (it.gem ? 90f : 180f) + it.phase * 40f, 0f);
            }
        }
    }
}
