using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Pistteki coinler ve nadir pembe elmaslar. Her atışta yeniden dizilir; kızak yanından geçerken toplanır.
    /// Düz kısımlarda sıra sıra coin, çukurların üstünde yay biçiminde coin ve yayın tepesinde elmas bulunur.
    public class Collectibles
    {
        public const int CoinValue = 120;   // her altın 120 (kullanıcı kararı 2026-10-11); gelir çarpanları uygulanmaz
        const float PickRadius = 1.6f;
        const float CoinSize = 0.9f;
        const float GemSize = 1.1f;

        class Item
        {
            public Vector3 pos;
            public bool gem, taken, small;   // small: köprü tablasında (görüşü kapatmasın)
            public bool gone;                // önceki denemelerde toplandı: pistte yok (kayıttan)
            public Transform tf;
            public Vector3 baseScale;
            public float phase, popClock = -1f;
        }

        readonly List<Item> items;
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
            items = CreateItems(track);
            foreach (var it in items) { if (it.gem) GemCount++; else CoinCount++; }
            Spawn();
        }

        // Runtime and economy checks share the same finite inventory and item order.
        static List<Item> CreateItems(TrackProfile track)
        {
            var items = new List<Item>();
            void Add(Vector3 pos, bool gem) => items.Add(new Item { pos = pos, gem = gem, phase = pos.z * 0.37f });
            var rng = new System.Random(track.name.GetHashCode() ^ 0x5eed);
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();

            bool NearFork(float z) => track.ForkAt(z, out _);

            bool NearWater(float z, float margin)
            {
                foreach (var w in track.water)
                    if (z > w.x - margin && z < w.y + margin) return true;
                return false;
            }

            // 1) Altınlar tek tek, pistin her yerine dağılmış (kullanıcı kararı 2026-10-11): art arda sıra yok, her altın
            // CoinValue (120) eder ve çoğu bir bedel ister: güzergâhın dışında (yol uzar, çimen yavaşlatır), su/çamurun
            // ortasında (fren) ya da tam karşıda (bedelsiz). Engelin dibine konmaz.
            void Coin(float z, float x) => Add(new Vector3(x, track.Height(x, z) + 0.8f, z), false);
            bool Free(float z, float x)
            {
                foreach (var o in track.obstacles)
                    if (Mathf.Abs(o.z - z) < 5f && Mathf.Abs(o.x - x) < o.halfWidth + 1.6f) return false;
                return true;
            }
            float reach = track.halfWidth + (track.bankScale <= 0f ? 5f : 1.5f);
            for (float z = 45f; z < track.finishZ - 15f; z += R(34f, 62f))
            {
                if (NearWater(z, 14f) || NearFork(z)) continue;
                double roll = rng.NextDouble();
                float x;
                if (roll < 0.35) x = track.PathX(z) + R(-0.6f, 0.6f);                       // tam karşıda
                else if (roll < 0.7)                                                          // yolun dışında: yol uzar
                {
                    float side = rng.Next(2) == 0 ? -1f : 1f;
                    x = Mathf.Clamp(track.PathX(z) + side * R(3.2f, 5.6f), track.CenterX(z) - reach, track.CenterX(z) + reach);
                }
                else x = track.PathX(z) + (rng.Next(2) == 0 ? -1f : 1f) * R(1.6f, 2.6f);    // yol kenarında
                if (Free(z, x)) Coin(z, x);
            }
            // Su ve çamurun ortasında: almak için içinden geçmek (frenlenmek) gerekir. Lekelerin yarısında.
            foreach (var zone in track.surfaces)
            {
                if (zone.type == Surface.Ice || rng.NextDouble() < 0.5 || zone.cz < 40f || zone.cz > track.finishZ - 15f) continue;
                float x = (zone.onPath ? track.PathX(zone.cz) : track.CenterX(zone.cz)) + zone.cx;
                if (Free(zone.cz, x)) Coin(zone.cz, x);
            }

            // 1b) Çatalın altınlı şeridinde iki altın (hangi şeridin ödüllü olduğunu gösterir).
            foreach (var f in track.forks)
                foreach (float u in new[] { 0.38f, 0.62f })
                {
                    float zz = Mathf.Lerp(f.z0, f.z1, u);
                    Coin(zz, track.GuideX(zz));
                }

            // Elmaslar düz rotada değil (kullanıcı 2026-10-18): almak için hızdan ya da güvenden ödün verilir.
            // 2) Köprüde elmas tablanın dışında, derenin üstünde (sırayla sağda, solda): toplama yarıçapı (1.6 m) ortadan
            // geçen kızağa yetmez, tablanın en kenarından gitmek gerekir.
            int bridgeSide = 1;
            foreach (var b in track.bridges)
            {
                // Yan (S) köprü: tablanın orta çizgisinde coin dizisi, ortasında elmas (riskli yolun ödülü). Aynı deredeki
                // düz köprüye ödül konmaz.
                if (b.bonus)
                {
                    int m = Mathf.Max(4, Mathf.RoundToInt((b.r1 - b.r0) / 2.4f));
                    for (int k = 0; k <= m; k++)
                    {
                        float zb = Mathf.Lerp(b.r0, b.r1, (float)k / m);
                        bool mid = k == m / 2;
                        Add(new Vector3(b.XAt(zb), TrackProfile.DeckHeight(b, zb) + (mid ? 0.7f : 0.5f), zb), false);   // 2026-10-24: elmas yerine coin
                        if (mid) items[items.Count - 1].small = true;
                    }
                    continue;
                }
                if (track.bridges.Exists(o => o.bonus && o.r0 == b.r0)) continue;
                int n = Mathf.Max(3, Mathf.RoundToInt((b.r1 - b.r0) / 2.2f));
                float zz = Mathf.Lerp(b.r0, b.r1, (float)(n / 2) / n);
                Add(new Vector3(b.XAt(zz) + bridgeSide * (b.HalfWidth + 0.9f), TrackProfile.DeckHeight(b, zz) + 0.6f, zz), false);   // 2026-10-24: coin
                items[items.Count - 1].small = true;
                bridgeSide = -bridgeSide;
            }

            // 3) Köprüsüz çukurların üstünde, atlayışın tepesinde pembe elmas; iki yanında birer altın.
            foreach (var w in track.water)
            {
                if (track.BridgeAt((w.x + w.y) * 0.5f, out _)) continue;
                float z0 = w.x - 2f, z1 = w.y + 2f;
                float width = z1 - z0;
                float lift = Mathf.Clamp(width * 0.12f, 2.5f, 14f);
                float h0 = track.Height(z0 - 6f), h1 = track.Height(z1 + 6f);
                int n = Mathf.Clamp(Mathf.RoundToInt(width / 3.2f), 4, 40);
                foreach (int k in new[] { n / 4, n / 2, n - n / 4 })
                {
                    float t = (float)k / n;
                    float zz = Mathf.Lerp(z0, z1, t);
                    float y = Mathf.Lerp(h0, h1, t) + 1.4f + lift * Mathf.Sin(t * Mathf.PI);
                    Add(new Vector3(track.CenterX(zz), y, zz), false);   // 2026-10-24: çukur üstünde coin
                }
            }

            // Elmas kıtlığı (kullanıcı 2026-10-24: "haritalarda çok elmas kazanılıyor, elmas satamayız"): pistte yalnız
            // bu iki zor ulaşılan elmas kalır; köprü ve çukur elmasları coin oldu.
            // 4) Pistin %45 ve %82'si civarında, bir tepenin üstünde ve rotanın yanında (sırayla sağ, sol): yan yamaçlı
            // pistte yamacın üstünde (tırmanmak hız yer), düz ormanda tepede rotadan 5-6 m açıkta (sapmak yol uzatır).
            int gemSide = 1;
            foreach (float f in new[] { 0.45f, 0.82f })
            {
                float z = track.finishZ * f, best = float.MinValue;
                for (float c = track.finishZ * f - 60f; c <= track.finishZ * f + 60f; c += 2f)
                {
                    if (NearWater(c, 10f) || NearFork(c)) continue;
                    float hump = track.Height(c) - 0.5f * (track.Height(c - 30f) + track.Height(c + 30f));
                    if (hump > best) { best = hump; z = c; }
                }
                float gx = track.wallHalf > 0f ? track.CenterX(z) + gemSide * (track.WallHalfAt(z) - 0.9f)   // kanyonda duvar dibinde
                         : track.bankScale > 0f ? track.CenterX(z) + gemSide * (track.halfWidth + 4f)
                                                : track.PathX(z) + gemSide * 5.5f;
                foreach (var o in track.obstacles)
                    if (Mathf.Abs(o.z - z) < 3f && Mathf.Abs(o.x - gx) < o.halfWidth + 1.5f) gx -= gemSide * 2.5f;
                Add(new Vector3(gx, track.Height(gx, z) + 1.1f, z), true);
                gemSide = -gemSide;
            }

            return items;
        }

        /// Exact runtime positions; each item is available once until the daily reset.
        public static List<float> CoinZs(TrackProfile track) => ItemZs(track, false);
        public static List<float> GemZs(TrackProfile track) => ItemZs(track, true);

        static List<float> ItemZs(TrackProfile track, bool gems)
        {
            var list = new List<float>();
            foreach (var it in CreateItems(track)) if (it.gem == gems) list.Add(it.pos.z);
            list.Sort();
            return list;
        }

        /// Elmasların pistteki z konumları (ilerleme çubuğu için).
        public List<float> GemPositions()
        {
            var list = new List<float>();
            foreach (var it in items) if (it.gem && !it.gone) list.Add(it.pos.z);
            return list;
        }

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
                it.baseScale = it.tf.localScale;
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
                it.tf.localScale = it.baseScale;
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
                    it.tf.localScale = it.baseScale * Mathf.Max(0f, 1f + 0.4f * t - 1.4f * t * t);
                    if (t >= 1f) it.tf.gameObject.SetActive(false);
                    continue;
                }
                float bob = it.gem ? 0.2f * Mathf.Sin(time * 2.5f + it.phase) : 0f;
                it.tf.position = it.pos + Vector3.up * bob;
                // Kızağın gerisinde kalan (toplanmamış) altın kameranın dibinde dev görünüyordu: geride kaldıkça küçülür.
                float behind = sledZ - it.pos.z;
                if (behind > 0.5f) it.tf.localScale = it.baseScale * Mathf.Clamp01(1f - (behind - 0.5f) / 3f);
                else it.tf.localScale = it.baseScale;
                it.tf.rotation = Quaternion.Euler(0f, time * (it.gem ? 90f : 180f) + it.phase * 40f, 0f);
            }
        }
    }
}
