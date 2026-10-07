using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Parkurlar sırayla açılır: biri %100 bitince sonraki açılır.
    public static class TrackLibrary
    {
        public const int Count = 4;

        public static readonly string[] Names = { "Çayır", "Orman", "Kanyon", "Karlı Dağ" };
        /// Atış kazancı çarpanı: zor pistler metre başına daha çok öder (son seviyeler pahalı, koşular kısa kalır).
        public static float[] PayFactors = { 0.86f, 1.5f, 4.1f, 5.8f };

        public static readonly float[] Lengths = { 3000f, 5000f, 4500f, 4200f };

        public static TrackProfile Get(int index)
        {
            switch (index)
            {
                case 1: return Forest();
                case 2: return Canyon();
                case 3: return Snow();
                default: return Meadow();
            }
        }

        /// Parkur kontrol noktalarını parça parça kurar (z ileri, y yükseklik).
        public const float ValleyHalfWidth = 6.5f;

        class Path
        {
            public readonly List<Vector2> points = new List<Vector2>();
            public readonly List<Vector3> water = new List<Vector3>();
            /// Keskin kalması gereken bölgeler (rampa dudağı, dere): geniş yumuşatma uygulanmaz.
            public readonly List<Vector2> sharp = new List<Vector2>();
            /// Parça birleşim köşeleri (z): oval yuvarlanır.
            public readonly List<float> corners = new List<float>();
            public readonly List<SurfaceZone> surfaces = new List<SurfaceZone>();
            public readonly List<Flock> flocks = new List<Flock>();
            /// Vadinin düz gitmesi gereken bölgeler (rampa kalkışı ve iniş alanı, atlayışlar).
            public readonly List<Vector2> straight = new List<Vector2>();
            /// Roket tepelerinin zirve z'leri (otomatik pilot roketini bunlara saklar, ilerleme çubuğu işaretler).
            public readonly List<float> rocketHills = new List<float>();
            /// Havada alınan roket hakkının rampası (dudak z'si) ve dudaktan ne kadar ileride durduğu; yoksa -1.
            public float pickupLip = -1f, pickupAhead;
            /// Profilin üstüne eklenecek tümsekler.
            public readonly List<Bump> bumps = new List<Bump>();
            public float lastLip;
            public float z, y;

            public Path(float startZ)
            {
                z = startZ;
                points.Add(new Vector2(z, y));
            }

            public Path To(float dz, float dy)
            {
                corners.Add(z);
                z += dz;
                y += dy;
                points.Add(new Vector2(z, y));
                return this;
            }

            public Path Flat(float length) => To(length, 0f);

            /// Ortalama eğimi drop olan inişte arka arkaya tepeler. Tepeler dışbükeydir:
            /// hızlı gelen kızak tepe üstünde havalanır, yavaş gelen yuvarlanarak geçer.
            /// shape > 1: iniş tepeden hemen sonra dik başlar, aşağıda yumuşar (kayak atlama iniş yamacı gibi içbükey):
            /// tepeden uçan hızlı kızak uçuş yoluna yakın eğimde iner.
            public Path Hills(float length, int count, float amplitude, float drop, float shape = 1f)
            {
                float z0 = z, y0 = y;
                corners.Add(z);
                int samples = Mathf.CeilToInt(length / 2.5f);
                for (int i = 1; i <= samples; i++)
                {
                    float t = (float)i / samples;
                    float wave = 0.5f * (1f - Mathf.Cos(t * count * Mathf.PI * 2f));
                    float f = shape == 1f ? t : 1f - Mathf.Pow(1f - t, shape);
                    points.Add(new Vector2(z0 + t * length, y0 - drop * f - amplitude * wave));
                }
                z = z0 + length;
                y = y0 - drop;
                return this;
            }

            /// Çukur (dere, vadi, kanyon): inLen içinde drop kadar iner, width boyunca düz gider (dibi su),
            /// outLen içinde rise kadar çıkar.
            public Path Gap(float inLen, float drop, float width, float outLen, float rise)
            {
                float zStart = z;
                To(inLen, drop);
                float z0 = z;
                Flat(width);
                water.Add(new Vector3(z0 - 2f, z + 2f, y + 0.9f));
                To(outLen, rise);
                sharp.Add(new Vector2(zStart - 1f, z + 3f));
                straight.Add(new Vector2(zStart - 20f, z + 60f));
                return this;
            }

            /// Su birikintisi: length boyunca (dy kadar eğimle) kızağı frenleyen yüzey.
            public Path Puddle(float length, float dy = 0f)
            {
                // Yuvarlağa yakın: tabanı boydan boya geçen yassı şerit olmasın.
                surfaces.Add(SurfaceZone.Blob(Surface.Puddle, z + length * 0.5f, 0f, Mathf.Max(length * 0.5f, 3.2f), ValleyHalfWidth * 0.7f, z * 0.71f, false));
                return To(length, dy);
            }

            /// Buz: length boyunca (dy kadar eğimle) sürtünmesi çok düşük yüzey.
            public Path Ice(float length, float dy = 0f)
            {
                surfaces.Add(SurfaceZone.Blob(Surface.Ice, z + length * 0.5f, 0f, Mathf.Max(length * 0.5f, 3.2f), ValleyHalfWidth * 0.75f, z * 0.53f, false));
                return To(length, dy);
            }

            /// Yolun bir şeridine (x0..x1) yüzey koyar; pist ilerlemez (ardından gelen parçanın üstüne serilir).
            /// offset: şeridin şu anki noktadan ne kadar ileride başladığı.
            /// Düzensiz kenarlı leke (su, çamur, buz); pist ilerlemez. offset: şu anki noktadan ileride başlangıcı,
            /// length: z boyunca uzunluğu, x0..x1: yanal kapladığı aralık (vadi ya da onPath ise patika ortasına göre).
            public Path Lane(Surface type, float offset, float length, float x0, float x1, bool onPath = false)
            {
                surfaces.Add(SurfaceZone.Blob(type, z + offset + length * 0.5f, (x0 + x1) * 0.5f, length * 0.5f, (x1 - x0) * 0.5f,
                                              (z + offset) * 0.61f + x0, onPath));
                return this;
            }

            /// Dik tepe: rise kadar dik çıkar, kısa düzlükten sonra fall kadar iner.
            /// Yalnızca hızlı gelen (ya da roket ateşleyen) kızak aşabilir.
            public Path Wall(float upLength, float rise, float downLength, float fall) =>
                To(upLength, rise).Flat(3f).To(downLength, -fall);

            /// Pist nesnesine su, buz ve dere bilgisini aktarır.
            /// meander: vadinin sağa sola kıvrılma genliği (m) ve dalga boyu; pathAmp/pathWave: patikanın vadi içindeki S'i.
            public TrackProfile Build(string name, float length, float meander = 6f, float meanderWave = 170f,
                                      float pathAmp = 2.4f, float pathWave = 60f, int seed = 1)
            {
                var t = new TrackProfile(name, points.ToArray(), length, ValleyHalfWidth, corners, sharp);
                t.SetValley(meander, meanderWave, pathAmp, pathWave, seed, straight);
                t.water.AddRange(water);
                t.surfaces.AddRange(surfaces);
                t.flocks.AddRange(flocks);
                t.rocketHills.AddRange(rocketHills);
                t.bumps.AddRange(bumps);
                t.IndexBumps();
                if (pickupLip > 0f)
                {
                    float pz = pickupLip + pickupAhead;
                    t.hasRocketPickup = true;
                    t.rocketPickup = new Vector3(t.PathX(pz), t.Height(t.PathX(pickupLip), pickupLip) + 1.3f, pz);
                }
                return t;
            }

            /// Kısa rampa: rise kadar yükselir, sonra fall kadar keskin düşer.
            public Path Ramp(float length, float rise, float fall)
            {
                float start = z;
                To(length, rise);
                lastLip = z;
                sharp.Add(new Vector2(start - 2f, z + 6f));   // rampa ve dudak olduğu gibi kalır: kalkış açısı ve dönüşü değişmesin
                straight.Add(new Vector2(start - 10f, z + 90f));   // hızlı kızak dudaktan ~80 m uçar
                return To(3f, -fall);
            }
        }

        /// Parkur 1 — Çayır (3 km). Kanatsız bitirilebilir.
        /// Başta düzlük (sapan belirler), sonra basamaklar: her basamak bir iniş ve bir tepe. Basamağın net yükseklik
        /// kaybı en iyi kızağın sürtünme + hava direnci kaybını ancak karşılar; tepeler pist boyunca yükselir, böylece
        /// her yükseltme birkaç tepe daha aşmayı sağlar. Her 6 basamakta bir zorlu yükselti (uzun iniş, yüksek tepe).
        /// Basamaklar karışık: buzlu patika, patikayı kesen su/çamur (yanından çimenden dolaşılır), yayılmış buz, engel.
        public static TrackProfile Meadow()
        {
            var p = new Path(-30f)
                .Flat(50f)
                .Hills(30f, 1, 0.3f, 0f)           // 20-50: düzlük
                .Puddle(5f)                        // 50: ilk su birikintisi
                .Flat(10f);
            var obstacles = Staircase(p, Lengths[0], 21, 7f, 16f, 12f, 18f, MeadowNet, 0, null, 7,
                                      new[] { 0.72f }, new[] { MeadowRocket }, MeadowGate, bumpy: MeadowBumpy);
            var t = p.Build(Names[0], Lengths[0], 7f, 260f, 2f, 110f, 3);
            ScatterRocks(t, 21, 26f);
            AddObstacles(t, obstacles);
            return t;
        }

        // Vadi atlayışları sırayla (genişlik, y > 0: hedef kanat alanı → derinlik süzülme oranından;
        // y < 0: karşı kıyı derinliği, simle ayarlanmış). Küçük kanat 20 m/s'de az taşır: hız da belirleyicidir.
        // Kanat alanları kızak seviyesine göre: 13→0.7, 14→1.1, 15→1.6, 16→2.0, 17→2.4 … 20→3.6.
        public static Vector2[] ForestGaps =
            { new Vector2(8f, -8f), new Vector2(15f, -7f), new Vector2(36f, -15f), new Vector2(46f, -17f),
              new Vector2(54f, -9f) };   // simle ayarlı: 10-12 / 13 / 14 / 16 kızak geçer
        public static Vector2[] CanyonGaps =
            { new Vector2(70f, 1.6f), new Vector2(95f, 2.4f), new Vector2(120f, 2.8f), new Vector2(140f, 3.2f) };

        // Roket tepelerinin normal tepeden fazla yüksekliği (m), parkur başına.
        public static float MeadowRocket = 8f, ForestRocket = 0f, CanyonRocket = 20f, MeadowGate = 0f, MeadowBumpy = 1f, MeadowNet = 19f, ForestNet = 18f, ForestGate = 0f, CanyonNet = 18f, GapRun = 16f, GapMargin = 6f,
                            CanyonGate = 0f, CanyonGateLength = 1000f,
                            SnowRocket = 32f, SnowGapMargin = 6f, SnowNet = 20f, SnowGate = 0f, SnowGateLength = 1000f;
        public static Vector2[] SnowGaps = { new Vector2(70f, -11f), new Vector2(100f, -7f), new Vector2(130f, 3.6f) };   // 2. vadi simle: Kızak 20 ister
        const float Gate = 3.5f;    // kapı basamağında tırmanılan fazla yükseklik
        const float Net = 9.4f;   // basamak başına net iniş: hız korunur; iyi kızak daha yüksek denge hızına ulaşır

        /// Parkur boyunca basamaklar üretir. Tepe yüksekliği riseStart'tan riseEnd'e, zorlu yükseltilerde
        /// bigStart'tan bigEnd'e çıkar. Dönüş: engel konumları (z, patikaya göre x; x &lt; 0 kaya, x &gt; 0 kasa).
        static List<Vector2> Staircase(Path p, float length, int seed, float riseStart, float riseEnd, float bigStart, float bigEnd,
                                       float net = Net, int gapEvery = 0, Vector2[] gaps = null,
                                       int dereEvery = 0, float[] rocketAt = null, float[] rocketExtra = null,
                                       float gate = Gate, float gateLength = 600f, bool snow = false, float margin = -1f,
                                       float bumpy = 0f, float sideChoice = 0.35f, bool kickers = true)
        {
            // Engebe ayrı bir rastgele diziden üretilir: engebesiz pistlerin düzeni değişmez.
            var brnd = new System.Random(seed * 7 + 3);
            float B(float a, float b) => a + (b - a) * (float)brnd.NextDouble();
            int nextGap = 0;
            var rnd = new System.Random(seed);
            float R(float a, float b) => a + (b - a) * (float)rnd.NextDouble();
            var obstacles = new List<Vector2>();
            int i = 0, nextRocket = 0;
            // Havada alınan roket hakkı: %70'ten sonraki ilk rampaya (atlama, dere ya da vadi).
            void Pickup(float ahead)
            {
                if (p.pickupLip > 0f || p.lastLip < length * 0.7f) return;
                p.pickupLip = p.lastLip;
                p.pickupAhead = ahead;
            }
            float carry = 0f;   // roket tepesinin fazla yüksekliği: sonraki iniş bunu geri verir
            while (p.z < length - 160f)
            {
                float t = Mathf.Clamp01(p.z / length);
                // Roket tepesi: normal tepeden rocketExtra kadar yüksek, dik (%32) yokuş. Roketsiz kızak ancak
                // çok hızlı gelirse aşar; zirveden sonra fazladan uzun iniş enerjiyi geri verir.
                bool gapStep = gaps != null && nextGap < gaps.Length && gapEvery > 0 && i % gapEvery == gapEvery - 1
                               && p.z < length - 700f;
                bool rocketStep = rocketAt != null && nextRocket < rocketAt.Length && t >= rocketAt[nextRocket] && !gapStep
                                  && (dereEvery == 0 || i % dereEvery != dereEvery - 1);
                bool big = i % 6 == 5;
                float down = big ? R(160f, 180f) : R(100f, 130f);
                float upDraw = big ? R(70f, 90f) : R(40f, 55f);
                // Tepe yüksekliği pistin hedef kızağının hız enerjisine göre ölçeklidir (riseStart..riseEnd): her seviyede
                // kızak tepeden önce belirgin yavaşlar, vadide hızlanır; yol hiçbir seviyede düz hissettirmez.
                float rise = big ? Mathf.Lerp(bigStart, bigEnd, t) : Mathf.Lerp(riseStart, riseEnd, t) + R(-0.3f, 0.3f);
                // Yokuş eğimi %22-30 (büyük tepede %20-26): gözle seçilen belirgin tepe; hızlı kızak zirveden havalanır.
                float up = big ? rise / Mathf.Lerp(0.2f, 0.26f, (upDraw - 70f) / 20f)
                               : rise / Mathf.Lerp(0.22f, 0.3f, (upDraw - 40f) / 15f);
                // Son basamak bitişi aşacaksa konmaz: bitiş çizgisi her zaman son tepeden sonraki inişte kalır.
                if (p.z + down + up > length - 150f) break;
                int kind = big ? (rnd.Next(2) == 0 ? 1 : 4) : rnd.Next(6);
                // Atlama rampası (inişin ortasında): bardaktaki süt sarsılır. Su/çamur ile üst üste binmez.
                bool jump = !big && p.z > 120f && rnd.NextDouble() < 0.45;
                if (rocketStep || !kickers) jump = false;   // kickers: büyük vadili pistlerde uçuş vadilerde
                if (jump && (kind == 2 || kind == 3)) kind = 0;
                float side = rnd.Next(2) == 0 ? -1f : 1f;
                switch (kind)
                {
                    case 1: p.Lane(Surface.Ice, 6f, down - 12f, -1.7f, 1.7f, true); break;                              // buzlu patika
                    case 2: p.Lane(snow ? Surface.Mud : Surface.Puddle, down * R(0.45f, 0.6f), R(8f, 12f), side < 0 ? -4.2f : -1.7f, side < 0 ? 1.7f : 4.2f, true); break;
                    case 3: p.Lane(Surface.Mud, down * R(0.45f, 0.6f), R(7f, 10f), side < 0 ? -4.4f : -1.7f, side < 0 ? 1.7f : 4.4f, true); break;
                    case 4:   // yayılmış buz lekeleri
                        p.Lane(Surface.Ice, R(4f, 10f), R(12f, 18f), -6f, R(-1.5f, 0f));
                        p.Lane(Surface.Ice, R(18f, 26f), R(10f, 16f), R(0f, 1.5f), 5.8f);
                        break;
                }
                // Karlı pist: her basamağın inişinde ek buz ve çamur lekeleri (buz hız kazandırır, çamur frenler;
                // rota seçimi önemli). Lekeler patikaya göre değil vadiye göre serilir.
                if (snow)
                {
                    int patches = 1 + rnd.Next(3);
                    for (int k = 0; k < patches; k++)
                    {
                        bool ice = rnd.NextDouble() < 0.55;
                        float x0 = R(-5.8f, 2.5f), w = ice ? R(2.5f, 5f) : R(1.8f, 3.2f);
                        p.Lane(ice ? Surface.Ice : Surface.Mud, R(8f, down - 25f), ice ? R(12f, 26f) : R(6f, 10f), x0, Mathf.Min(x0 + w, 5.8f));
                    }
                }
                // Engeller yokuşta: hız orada düşük, kaçmak mümkün.
                // Atlanan basamakta (rampa, dere, vadi) engel yok: hızlı kızak yokuşun dibine kadar uçabilir.
                bool dereStep = dereEvery > 0 && i % dereEvery == dereEvery - 1 && p.z < length - 700f;
                if (kind == 0 || kind == 1)
                {
                    var o = new Vector2(p.z + down + up * R(0.3f, 0.6f), side * R(2.2f, 2.8f));
                    if (!jump && !gapStep && !dereStep && !rocketStep) obstacles.Add(o);
                }
                // Son 600 m: kapı basamakları — inilenden fazla tırmanılır; ancak pist boyunca hız biriktiren
                // (en iyi kızak) aşar.
                // Vadi atlayışı (kanat ister): rampa, altında su olan geniş çukur, alçak karşı kıyı.
                if (gapStep)
                {
                    // gaps[k]: x = genişlik, y > 0: geçmesi gereken kanat alanı (karşı kıyı, o kanadın süzülme
                    // yolunun GapMargin üstünde), y < 0: kanatsız atlayış, karşı kıyı -y kadar alçakta.
                    var gap = gaps[nextGap++];
                    float width = gap.x;
                    float dropHere = gap.y < 0f ? -gap.y : width / (0.9f * SledPhysics.MaxGlideRatio(gap.y)) - (margin < 0f ? GapMargin : margin);
                    // Uzun, dik yaklaşma inişi + alçak rampa: kızak atlayışa hızlı girer.
                    p.Hills(down, 1, 0.4f, net + GapRun).Ramp(14f, 3f, 1.5f);
                    Pickup(5f);
                    // Vadinin ortasında, süzülme yolunda sağa sola salınan kuş sürüsü
                    p.flocks.Add(new Flock { z = p.z + 3f + width * 0.5f, x = 0f, y = p.y - 3f - width * 0.06f,
                                             sway = R(3f, 5f), period = R(2.5f, 4f), radius = 2.2f });
                    // İniş kıyısı dudağın gapDrop altında: kanatsız kızak balistik menzille, kanatlı süzülerek geçer.
                    // Eğimli, uzun iniş bölgesi: süzülüşte hava direncine kaybedilen hız burada geri kazanılır.
                    p.Gap(3f, -(dropHere + 6f), width, 3f, 6f).To(60f, -16f);
                    i++;
                    continue;
                }
                float stepNet = p.z > length - gateLength ? net - gate : net;
                float drop = rise + stepNet + carry;
                carry = 0f;
                // Dere: rampadan atlanır, karşı kıyı biraz alçakta, ardından kısa eğimli iniş.
                if (dereEvery > 0 && i % dereEvery == dereEvery - 1 && p.z < length - 700f)
                {
                    float width = Mathf.Lerp(6f, 10f, t);
                    // Dere inişin ortasında: rampa +1.5, dere -3.5 (karşı kıyı alçak), 16 m'lik iniş yamacı -5 (~17°).
                    // Hızlı kızak yamacı aşarsa da inişin devamına düşer (yokuşa çakılmaz). Kalan iniş toplamı tamamlar.
                    float d1 = down * 0.4f;
                    p.Hills(d1, 1, 0.4f, drop * 0.4f).Ramp(12f, 2.5f, 1f);
                    Pickup(4f);
                    p.Gap(3f, -4f, width, 2f, 0.5f).To(16f, -5f);
                    p.Hills(Mathf.Max(20f, down - d1 - 36f - width), 1, 0.4f, Mathf.Max(1f, drop * 0.6f - 7f));
                    p.To(up, rise).Flat(6f);
                    i++;
                    continue;
                }
                if (jump)
                {
                    // Rampa inişin başlarında: hızlı kızak da inişin içinde (yokuşa değil) iner.
                    float d1 = down * R(0.35f, 0.5f) * 0.6f, kick = Mathf.Lerp(1.6f, 2.8f, t);
                    // Rampa net +(kick - 0.8) yükseltir; ardından gelen iniş bunu da geri verir.
                    // Rampadan önceki kısım da engebeli olabilir (rampaya 10 m kala biter).
                    if (bumpy > 0f && p.z > 300f) AddBumps(p, p.z + down * 0.05f, p.z + d1 - 10f, t, false, bumpy, brnd, B);
                    p.Hills(d1, 1, 0.4f, drop * 0.45f).Ramp(10f, kick, 0.8f);
                    Pickup(4f);
                    p.Hills(down - d1 - 13f, 1, 0.4f, drop * 0.55f + kick - 0.8f, 1.7f);   // dudaktan sonra dik iniş yamacı
                }
                else
                {
                    float s0 = p.z;
                    // Dalgalı iniş: ~38 m'de bir dalga (engebeli pistte).
                    int waves = bumpy > 0f ? Mathf.Max(1, Mathf.RoundToInt(down / 38f)) : 1;
                    p.Hills(down, waves, bumpy > 0f ? 0.4f + 0.9f * bumpy : 0.4f, drop, 1.7f);
                    // İlk 300 m tümseksiz: kapak açılınca sapanın verdiği en yüksek hızla tümseğe girilmesin.
                    // Su/çamur inişin ortasında: engebe ondan önceki kısma.
                    bool wet = kind == 2 || kind == 3;
                    if (bumpy > 0f && !rocketStep && s0 > 300f)
                        AddBumps(p, s0 + down * (wet ? 0.06f : 0.12f), wet ? s0 + down * 0.42f : s0 + down - 12f, t, !wet, bumpy, brnd, B);
                }
                if (rocketStep)
                {
                    float extra = rocketExtra[nextRocket++], high = rise + extra;
                    p.To(high / 0.32f, high).Flat(8f);
                    p.rocketHills.Add(p.z - 4f);
                    carry = extra;
                }
                else
                {
                    float c0 = p.z;
                    p.To(up, rise).Flat(6f);
                    // Taraf seçimi: tepenin bir yanı daha yüksek ama temiz, öbür yanı alçak ama yokuşunda su/çamur var.
                    // Hızlı gelen yüksek yanı, yavaş gelen alçak yanı seçer (oyuncu becerisi).
                    // Son 700 m'de yok: finale doğru tepeler temiz (seviye kapısı yalnızca enerji).
                    if (sideChoice > 0f && !dereStep && c0 < length - 700f && brnd.NextDouble() < sideChoice)
                    {
                        float hs = brnd.Next(2) == 0 ? -1f : 1f, len = up * 1.4f + 10f;
                        p.bumps.Add(new Bump { z0 = c0 + up + 3f - len * 0.5f, length = len, height = Mathf.Clamp(rise * 0.12f, 1.2f, 2.2f),
                                               xc = hs * 3.3f, halfWidth = 3f });
                        var wet = snow || brnd.NextDouble() < 0.5 ? Surface.Mud : Surface.Puddle;
                        p.surfaces.Add(SurfaceZone.Blob(wet, c0 + up * B(0.45f, 0.65f), -hs * 3.2f, B(3.5f, 5f), B(2.2f, 2.8f), c0 * 0.37f, false));
                    }
                }
                i++;
            }
            // Son tepeden bitişe uzun hafif iniş (bitişten 40 m önce düzlüğe iner).
            float rest = Mathf.Max(150f, length - p.z - 40f);
            p.To(rest, -Mathf.Max(12f, rest * 0.1f)).Flat(120f);   // en az %10: sürtünme artınca da bitişe ulaşılır
            return obstacles;
        }

        /// İnişin [a, b] aralığına engebe ekler: tümsek dizisi (bazen vadinin tek yanında), zorlu kambur ya da
        /// tabanın bir yanını kaldıran uzun kabarma. Tümsekler uzun ve yüksek: gözle net görünür, eğrilikleri
        /// (sertlikleri) ise kızağı ancak hızlıyken sektirecek kadardır.
        static void AddBumps(Path p, float a, float b, float t, bool allowHump, float bumpy, System.Random brnd,
                             System.Func<float, float, float> B)
        {
            float room = b - a;
            if (room < 16f) return;
            double r = brnd.NextDouble();
            if (allowHump && r < 0.3 && room > 42f)
            {
                // Zorlu kambur: uzun ve belirgin; yavaş gelen zor aşar, çok hızlı gelen (~17 m/s üstü) havalanır.
                float len = B(30f, 40f);
                p.bumps.Add(new Bump { z0 = a + B(0f, room - len), length = len, height = Mathf.Lerp(1.6f, 2.6f, t) * bumpy, halfWidth = 40f });
                return;
            }
            // Tümsek dizileri kaldırıldı: büyük tepelerin dik inişinde kızak bir tümsekten uçup ötekinin yüzüne
            // çakılıyordu (süt boşalıyordu). Küçük engebe yerine az ama belirgin engebe.
            if (r >= 0.6) return;
            // Yan kabarma: tabanın bir yanı uzun bir tümsekle yükselir, zemin yana eğilir.
            float sl = Mathf.Min(B(30f, 45f), room);
            p.bumps.Add(new Bump { z0 = a + B(0f, room - sl), length = sl, height = B(1.2f, 1.8f) * bumpy,
                                   xc = (brnd.Next(2) == 0 ? -1f : 1f) * B(2f, 3.5f), halfWidth = B(2f, 3f) });
        }

        /// Vadinin çimenli kenarlarına ve yamaçlarına kaya, taş, kütük serpiştirir: hepsi gerçek engeldir
        /// (sahnedeki her kaya çarpılabilir). Patikadan, çukurlardan ve rampalardan uzak tutulur.
        static void ScatterRocks(TrackProfile t, int seed, float spacing)
        {
            var rnd = new System.Random(seed * 31 + 7);
            float R(float a, float b) => a + (b - a) * (float)rnd.NextDouble();
            // Tepe ve rampa dudakları: hızlı kızak buradan havalanır, vadi kıvrılırken patikanın dışına iner;
            // havada direksiyon zayıf olduğundan iniş bölgesine kaya konmaz.
            var crests = new List<float>();
            for (float z = 0f; z < t.finishZ; z += 1f)
                if (t.Slope(z) >= 0f && t.Slope(z + 1f) < 0f) crests.Add(z);
            for (float z = 60f; z < t.finishZ - 40f; z += R(spacing * 0.6f, spacing * 1.4f))
            {
                bool nearWater = false, landing = false;
                foreach (var w in t.water) if (z > w.x - 14f && z < w.y + 14f) nearWater = true;
                foreach (var c in crests) if (z > c - 5f && z < c + 80f) landing = true;
                if (nearWater || landing || Mathf.Abs(t.Slope(z)) > 0.3f || Mathf.Abs(t.Slope(z + 4f) - t.Slope(z - 4f)) > 0.25f) continue;
                float side = rnd.Next(2) == 0 ? -1f : 1f;
                // Yalnızca yamaçlarda: vadi tabanı, atlayıştan patika dışına inen kızağın da güvenli alanıdır.
                float x = t.CenterX(z) + side * R(t.halfWidth + 1f, t.halfWidth + TrackProfile.BankLimit - 1.5f);
                if (Mathf.Abs(x - t.PathX(z)) < TrackProfile.PathHalfWidth + 1.2f) continue;
                t.AddRock(z, x - t.PathX(z));
            }
        }

        static void AddObstacles(TrackProfile t, List<Vector2> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (i % 2 == 0) t.AddRock(list[i].x, list[i].y);
                else t.AddCrate(list[i].x, list[i].y);
            }
            t.SortObstacles();
        }

        /// Parkur 2 — Orman (5 km). Basamaklar + her 5 basamakta bir vadi atlayışı: çukurlar giderek genişler,
        /// ilk vadiler küçük kanatla, sonuncular kızağın 4. seviyesinin sonundaki kanatla geçilir.
        public static TrackProfile Forest()
        {
            var p = new Path(-30f).Flat(50f).Hills(30f, 1, 0.3f, 0f).Flat(10f);
            var obstacles = Staircase(p, Lengths[1], 33, 10f, 18f, 12f, 18f, ForestNet, 4, ForestGaps, 0,
                                      new[] { 0.62f }, new[] { ForestRocket }, ForestGate);
            var t = p.Build(Names[1], Lengths[1], 8f, 260f, 2f, 110f, 5);
            ScatterRocks(t, 33, 24f);
            AddObstacles(t, obstacles);
            return t;
        }

        /// Parkur 4 — Karlı Dağ (4,2 km). Final: her yer kar; basamaklarda buz ve çamur lekeleri, rampalar,
        /// büyük vadiler, en güçlü roketi isteyen bir roket tepesi ve uzun kapı bölgesi. Her şeyin son seviyesini ister.
        public static TrackProfile Snow()
        {
            var p = new Path(-30f).Flat(50f).Hills(30f, 1, 0.3f, 0f).Flat(10f);
            var obstacles = Staircase(p, Lengths[3], 59, 12f, 22f, 14f, 20f, SnowNet, 5, SnowGaps, 0,
                                      new[] { 0.5f }, new[] { SnowRocket }, SnowGate, SnowGateLength, true, SnowGapMargin, kickers: false);
            var t = p.Build(Names[3], Lengths[3], 9f, 240f, 2.4f, 100f, 9);
            ScatterRocks(t, 59, 22f);
            AddObstacles(t, obstacles);
            return t;
        }

        /// Parkur 3 — Kanyon (4,5 km). Daha yüksek tepeler ve her 4 basamakta bir geniş kanyon: büyük kanat ister.
        public static TrackProfile Canyon()
        {
            var p = new Path(-30f).Flat(50f).Hills(30f, 1, 0.3f, 0f).Flat(10f);
            var obstacles = Staircase(p, Lengths[2], 47, 12f, 22f, 14f, 20f, CanyonNet, 4, CanyonGaps, 0,
                                      new[] { 0.5f }, new[] { CanyonRocket }, CanyonGate, CanyonGateLength, kickers: false);
            var t = p.Build(Names[2], Lengths[2], 9f, 280f, 2f, 120f, 7);
            ScatterRocks(t, 47, 22f);
            AddObstacles(t, obstacles);
            return t;
        }
    }
}
