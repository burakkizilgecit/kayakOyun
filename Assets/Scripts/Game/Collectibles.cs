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
            public bool gem, taken, small;   // small: köprü tablasında (görüşü kapatmasın)
            public bool gone;                // önceki denemelerde toplandı: pistte yok (kayıttan)
            public Transform tf;
            public float phase, popClock = -1f;
        }

        readonly List<Item> items = new List<Item>();
        readonly Transform root;

        public int coins, gems;   // bu atışta toplananlar
        public int CoinCount { get; }
        public int GemCount { get; }

        // Havada asılı roket hakkı (pistte varsa)
        Transform rocketPickup;
        Vector3 rocketHome;
        float rocketPop = -1f;

        public Collectibles(TrackProfile track, Transform parent)
        {
            root = new GameObject("Collectibles").transform;
            root.SetParent(parent, false);
            if (track.hasRocketPickup) BuildRocketPickup(track.rocketPickup);
            var rng = new System.Random(track.name.GetHashCode() ^ 0x5eed);
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();

            bool NearFork(float z) => track.ForkAt(z, out _);

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
                if (NearFork(z - 4f) || NearFork(z + length + 4f)) continue;   // çatalda altınlar şeridine dizilir (aşağıda)
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

            // 1b) Çatalın iyi şeridi boyunca altın sırası: hangi yolun seçileceğini gösterir.
            foreach (var f in track.forks)
                for (float zz = f.z0 + TrackProfile.ForkBlend; zz <= f.z1 - TrackProfile.ForkBlend; zz += 2.4f)
                {
                    float x = track.GuideX(zz);
                    Add(new Vector3(x, track.Height(x, zz) + 0.8f, zz), false);
                }

            // 2) Köprülerin tablası boyunca coin sırası (ortada pembe elmas): düz ve tam ortadan geçmenin ödülü.
            foreach (var b in track.bridges)
            {
                int n = Mathf.Max(3, Mathf.RoundToInt((b.r1 - b.r0) / 2.2f));
                for (int i = 0; i <= n; i++)
                {
                    float zz = Mathf.Lerp(b.r0, b.r1, (float)i / n);
                    Add(new Vector3(b.x, TrackProfile.DeckHeight(b, zz) + 0.6f, zz), i == n / 2);
                    items[items.Count - 1].small = true;
                }
            }

            // 3) Köprüsüz çukurların üstünde yay: atlayan kızağın yoluna benzer; tepesinde pembe elmas.
            foreach (var w in track.water)
            {
                if (track.BridgeAt((w.x + w.y) * 0.5f, out _)) continue;
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

            // 4) Pistin zor kısımlarında birer elmas (iyi atışların ödülü).
            foreach (float f in new[] { 0.45f, 0.82f })
            {
                float z = track.finishZ * f;
                for (int k = 0; k < 20 && NearWater(z, 8f); k++) z += 6f;
                float gx = track.GuideX(z);
                Add(new Vector3(gx, track.Height(gx, z) + 1.1f, z), true);
            }

            foreach (var it in items) { if (it.gem) GemCount++; else CoinCount++; }
            Spawn();
        }

        /// Pistteki elmasların z konumları, kurulumla aynı kural (köprü ortası, köprüsüz çukur yayının tepesi,
        /// %45 ve %82). Simler elmas gelirini bununla hesaplar; kurulum kuralı değişirse bu da değişmeli.
        public static List<float> GemZs(TrackProfile track)
        {
            var list = new List<float>();
            foreach (var b in track.bridges)
            {
                int n = Mathf.Max(3, Mathf.RoundToInt((b.r1 - b.r0) / 2.2f));
                list.Add(Mathf.Lerp(b.r0, b.r1, (float)(n / 2) / n));
            }
            foreach (var w in track.water)
            {
                if (track.BridgeAt((w.x + w.y) * 0.5f, out _)) continue;
                float z0 = w.x - 2f, z1 = w.y + 2f;
                int n = Mathf.Clamp(Mathf.RoundToInt((z1 - z0) / 3.2f), 4, 40);
                list.Add(Mathf.Lerp(z0, z1, (float)(n / 2) / n));
            }
            foreach (float f in new[] { 0.45f, 0.82f })
            {
                float z = track.finishZ * f;
                for (int k = 0; k < 20 && NearWaterZ(track, z, 8f); k++) z += 6f;
                list.Add(z);
            }
            list.Sort();
            return list;
        }

        static bool NearWaterZ(TrackProfile track, float z, float margin)
        {
            foreach (var w in track.water)
                if (z > w.x - margin && z < w.y + margin) return true;
            return false;
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
            foreach (var it in items) if (it.gem && !it.gone) list.Add(it.pos.z);
            return list;
        }

        void Add(Vector3 pos, bool gem) => items.Add(new Item { pos = pos, gem = gem, phase = pos.z * 0.37f });

        void Spawn()
        {
            GameObject coinPrefab = null;   // coinler CoinModel ile kurulur (hazır model çok kalın duruyordu)
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
                else if (!it.gem)
                {
                    var holder = new GameObject("Coin").transform;
                    holder.SetParent(root, false);
                    CoinModel.Create(holder, CoinSize);
                    go = holder.gameObject;
                }
                else
                {
                    go = Mats.Primitive(it.gem ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
                    go.transform.SetParent(root, false);
                    go.transform.localScale = it.gem ? Vector3.one * GemSize : new Vector3(CoinSize, 0.06f, CoinSize);
                    go.GetComponent<Renderer>().sharedMaterial = it.gem ? gemMat : Mats.Solid(new Color(1f, 0.8f, 0.2f), 0.7f);
                }
                if (it.small) go.transform.localScale *= 0.6f;
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

        /// Altın roket + etrafında parlayan turuncu halka: uzaktan seçilsin diye büyük ve parlak.
        void BuildRocketPickup(Vector3 pos)
        {
            rocketHome = pos;
            rocketPickup = new GameObject("RocketPickup").transform;
            rocketPickup.SetParent(root, false);
            rocketPickup.position = pos;
            var body = new GameObject("Rocket").transform;
            body.SetParent(rocketPickup, false);
            body.localRotation = Quaternion.Euler(-60f, 0f, 0f);   // burnu yukarı-ileri
            body.localScale = Vector3.one * 2.2f;
            var tail = PartVisuals.BuildRocket(body, 5, SledModel.RocketMesh());
            body.localPosition = body.localRotation * (-tail * 0.5f * 2.2f);   // roketin ortası halkanın merkezinde
            var glow = Mats.Solid(new Color(1f, 0.62f, 0.15f), 0.6f);
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.05f) * 1.2f);
            const int seg = 24;
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var bead = Mats.Primitive(PrimitiveType.Cube);
                bead.transform.SetParent(rocketPickup, false);
                bead.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 1.25f;
                bead.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                bead.transform.localScale = new Vector3(0.12f, 0.34f, 0.12f);
                bead.GetComponent<Renderer>().sharedMaterial = glow;
            }
            foreach (var r in rocketPickup.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// Roket hakkı alındı: küçülüp yukarı uçar.
        public void TakeRocket() { if (rocketPickup != null) rocketPop = 0f; }

        /// Yeni atış: hepsi yerine döner.
        /// Çarpma sahnesinde kamera ile karakter arasında kalmasınlar diye yakındaki toplanabilirleri gizler.
        public void HideAround(Vector3 p, float radius)
        {
            foreach (var it in items)
                if (!it.taken && (it.pos - p).sqrMagnitude < radius * radius) it.tf.gameObject.SetActive(false);
        }

        /// Kayıttaki toplanmışları uygular ('1' = toplanmış; uzunluk uymuyorsa — pist değişmiş — yok sayılır).
        public void LoadTaken(string mask)
        {
            bool ok = !string.IsNullOrEmpty(mask) && mask.Length == items.Count;
            for (int i = 0; i < items.Count; i++) items[i].gone = ok && mask[i] == '1';
        }

        /// Bu denemede toplananları kalıcı sayar ve kayıt dizisini döner.
        public string CommitTaken()
        {
            var sb = new System.Text.StringBuilder(items.Count);
            foreach (var it in items)
            {
                if (it.taken) it.gone = true;
                sb.Append(it.gone ? '1' : '0');
            }
            return sb.ToString();
        }

        public void ResetRun()
        {
            coins = gems = 0;
            if (rocketPickup != null)
            {
                rocketPop = -1f;
                rocketPickup.gameObject.SetActive(true);
                rocketPickup.position = rocketHome;
                rocketPickup.localScale = Vector3.one;
            }
            foreach (var it in items)
            {
                it.taken = it.gone;
                it.popClock = -1f;
                it.tf.gameObject.SetActive(!it.gone);
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
            if (rocketPickup != null && rocketPickup.gameObject.activeSelf)
            {
                if (rocketPop >= 0f)
                {
                    rocketPop += dt;
                    float t = rocketPop / 0.4f;
                    rocketPickup.position = rocketHome + Vector3.up * (3f * t);
                    rocketPickup.localScale = Vector3.one * Mathf.Max(0f, 1f + 0.5f * t - 1.5f * t * t);
                    if (t >= 1f) rocketPickup.gameObject.SetActive(false);
                }
                else
                {
                    rocketPickup.position = rocketHome + Vector3.up * (0.15f * Mathf.Sin(time * 2.2f));
                    rocketPickup.rotation = Quaternion.Euler(0f, time * 70f, 0f);
                }
            }
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
                // Kızağın gerisinde kalan (toplanmamış) altın kameranın dibinde dev görünüyordu: geride kaldıkça küçülür.
                float behind = sledZ - it.pos.z;
                if (behind > 0.5f) it.tf.localScale = Vector3.one * Mathf.Clamp01(1f - (behind - 0.5f) / 3f);
                else if (it.tf.localScale.x < 1f) it.tf.localScale = Vector3.one;
                it.tf.rotation = Quaternion.Euler(0f, time * (it.gem ? 90f : 180f) + it.phase * 40f, 0f);
            }
        }
    }
}
