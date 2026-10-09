using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Parkurlar sırayla açılır: biri %100 bitince sonraki açılır.
    public static class TrackLibrary
    {
        public const int Count = 7;

        public static readonly string[] Names = { "Çayır", "Orman", "Kanyon", "Karlı Dağ", "Çöl Kumulları", "Volkan", "Mars" };
        /// Atış kazancı çarpanı: zor pistler metre başına daha çok öder (son seviyeler pahalı, koşular kısa kalır).
        public static float[] PayFactors = { 4f, 3.2f, 2.5f, 2.2f, 2.2f, 2.2f, 2f };

        // Pist başına geliştirme (kullanıcı kararı 2026-10-09): yeni pistte kartlar sıfırdan başlar. Zor pistler 6 aşama
        // (24 kademe) ister. Kademe gücü [PowerStart, PowerEnd] güç endeksine eşlenir (Upgrades.Power); fiyatlar
        // pistin ölçeğiyle büyür (CostScale). Değerler ortalama oyuncu simiyle ayarlanır.
        public static readonly int[] UpgradeStages = { 5, 5, 6, 6, 6, 6, 6 };
        /// Bardak ağız boşluğu çarpanı: Mars'ın düşük yerçekiminde süt yavaş ama kolay çalkalanır, bardak daha az doldurulur.
        public static readonly float[] MilkHeadroomScale = { 1f, 1f, 1f, 1f, 1f, 1f, 1.8f };
        public static float[] PowerStart = { 0f, 5f, 7f, 8f, 9f, 10f, 10f };
        public static float[] PowerEnd = { 15f, 18f, 21f, 23f, 24f, 25f, 25f };
        public static float[] CostScale = { 1f, 3f, 3.5f, 6.5f, 18f, 15f, 26f };
        /// Pistin aracı: son üç haritada aracın şekli tamamen değişir.
        public static readonly VehicleKind[] Vehicles =
            { VehicleKind.Sled, VehicleKind.Sled, VehicleKind.Sled, VehicleKind.Sled, VehicleKind.Sandboard, VehicleKind.Bobsled, VehicleKind.Hover };

        // Gerçek uzunluklar (m). Ekranda ×DisplayScale gösterilir (Sled Surfers gibi: Çayır "3600 m").
        public static readonly float[] Lengths = { 1000f, 1200f, 1400f, 1600f, 1800f, 2000f, 2200f };

        /// Ekrandaki mesafe ölçeği (kullanıcı kararı, videodaki gibi): gösterilen metre = gerçek × 3.6. Böylece km/s
        /// cinsinden hız, ekranda saniyede geçen metreye eşit olur; koşular 20 sn - 2 dk.
        public const float DisplayScale = 3.6f;

        /// Ekranda gösterilecek mesafe (tamsayı metre).
        public static int Show(float realMeters) => Mathf.RoundToInt(realMeters * DisplayScale);

        /// Pisti kurar ve yükseltme bağlamını o piste geçirir (yükseltmeler pist başına: Upgrades.Track).
        public static TrackProfile Get(int index)
        {
            Upgrades.Track = Mathf.Clamp(index, 0, Count - 1);
            switch (index)
            {
                case 1: return Forest();
                case 2: return Canyon();
                case 4: return Desert();
                case 5: return Volcano();
                case 6: return Mars();
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
            /// Köprülerin dere açıklıkları (z0, z1); tabla Build'de kurulur.
            public readonly List<Vector2> bridges = new List<Vector2>();
            /// Çatallar: (z0, z1, iyi şerit yönü).
            public readonly List<Vector3> forks = new List<Vector3>();
            public float lastLip;
            /// true iken Lane yüzey eklemez (çatallı yamaç).
            public bool mute;
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

            /// İnce köprü: pisti enine kesen depth derinliğinde dere (dik kıyılar, dibi su), üstünde span boyunca
            /// patika hizasında tahta tabla (yamaç eğimi grade ile iner). Yaklaşma ve köprü boyunca vadi düz gider.
            public Path Bridge(float span, float depth, float grade)
            {
                float z0 = z, y0 = y, drop = grade * span;
                To(1.5f, -depth);
                float zb = z;
                Flat(span - 3f);
                water.Add(new Vector3(zb - 1f, z + 1f, y + 0.9f));
                To(1.5f, depth - drop);
                bridges.Add(new Vector2(z0, z));
                sharp.Add(new Vector2(z0 - 3f, z + 3f));
                straight.Add(new Vector2(z0 - 40f, z + 6f));   // 40 m düz yaklaşma: kızağı hizalamaya zaman
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
                if (mute) return this;
                surfaces.Add(SurfaceZone.Blob(type, z + offset + length * 0.5f, (x0 + x1) * 0.5f, length * 0.5f, (x1 - x0) * 0.5f,
                                              (z + offset) * 0.61f + x0, onPath));
                return this;
            }

            /// Çatal: offset ileride başlayan length boyunca patika ikiye ayrılır (pist ilerlemez). variant:
            /// 0 bir şeritte su (çamur), öteki temiz; 1 bir şeritte çamur, ötekinde buz (hızlı yol); 2 altınlı şeritte
            /// küçük su birikintisi (altın mı mesafe mi); 3 altınlı şeritte tümsekler (altın mı süt mü).
            /// good: altınların dizildiği şerit (-1/+1).
            public Path Fork(float offset, float length, int variant, int good, bool wetIsMud, bool allowIce)
            {
                float z0 = z + offset, off = TrackProfile.ForkOffset, mid = offset + length * 0.5f;
                int bad = -good;
                forks.Add(new Vector3(z0, z0 + length, good));
                straight.Add(new Vector2(z0, z0 + length));   // çatal boyunca vadi düz: iki yol net görünsün, kıvrımda sırta sürtünmesin
                Path LaneAt(Surface type, int side, float lo, float len) =>
                    Lane(type, lo, len, side * off - 1.6f, side * off + 1.6f);
                switch (variant)
                {
                    case 0: LaneAt(wetIsMud ? Surface.Mud : Surface.Puddle, bad, mid - 5f, 10f); break;
                    case 1:
                        LaneAt(Surface.Mud, bad, mid - 4f, 8f);
                        if (allowIce) LaneAt(Surface.Ice, good, offset + 10f, length - 20f);
                        break;
                    case 2: LaneAt(wetIsMud ? Surface.Mud : Surface.Puddle, good, mid - 3f, 6f); break;
                    case 3:
                        for (int k = 0; k < 3; k++)
                            bumps.Add(new Bump { z0 = z0 + TrackProfile.ForkBlend + 2f + k * 5f, length = 4f, height = 0.45f,
                                                 xc = good * off, halfWidth = 1.1f });
                        break;
                }
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
                var cliffs = bridges.ConvertAll(b => new Vector2(b.x - 3f, b.y + 3f));
                var t = new TrackProfile(name, points.ToArray(), length, ValleyHalfWidth, corners, sharp, rawZones: cliffs);
                t.SetValley(meander, meanderWave, pathAmp, pathWave, seed, straight);
                foreach (var f in forks) t.forks.Add(new TrackProfile.Fork { z0 = f.x, z1 = f.y, good = (int)f.z });
                t.water.AddRange(water);
                t.surfaces.AddRange(surfaces);
                t.flocks.AddRange(flocks);
                t.rocketHills.AddRange(rocketHills);
                t.bumps.AddRange(bumps);
                t.IndexBumps();
                // Tabla kıyılara 3.5 m uzanır (profil yumuşatması ±3 m) ve uçları profile tam oturur.
                foreach (var b in bridges)
                {
                    float z0 = b.x - 3.5f, z1 = b.y + 3.5f;
                    t.bridges.Add(new TrackProfile.Bridge { z0 = z0, z1 = z1, r0 = b.x, r1 = b.y, y0 = t.Height(z0), y1 = t.Height(z1),
                                                            x = t.PathX((b.x + b.y) * 0.5f) });
                }
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

        // ------------------------------------------------------------------ yeni haritalar (2026-10-09)
        // Kullanıcı kararı: üç yeni harita ve her birinde tamamen farklı araç. Yüzeyler aynı fizikle temaya göre görünür:
        // buz = hızlı yol (tuz düzlüğü / obsidyen / buz), çamur = yavaşlatan (bataklık kumu / kül / kızıl toz).

        /// Parkur 5 — Çöl Kumulları: kum sörfü. Vaha birikintileri, tuz düzlüğü şeritleri, köprüler vaha kanalı üstünde.
        public static TrackProfile Desert()
        {
            var gp = GlideStart(4, false);
            var gobs = Glide(gp, 4, 71, 0.35f, false, true);
            var gt = gp.Build(Names[4], Lengths[4], 9f, 220f, 2.4f, 85f, 11);
            gt.fallText = "Kanala düştün!";
            ScatterRocks(gt, 71, 24f);
            AddObstacles(gt, gobs);
            return gt;
        }

        /// Parkur 6 — Volkan: obsidyen bobsled. Obsidyen cam (hızlı) ve kül (yavaş) lekeleri, köprüler lav nehri üstünde.
        public static TrackProfile Volcano()
        {
            var gp = GlideStart(5, false);
            var gobs = Glide(gp, 5, 83, 0.3f, true, true);
            var gt = gp.Build(Names[5], Lengths[5], 5f, 320f, 2f, 110f, 13);
            gt.fallText = "Lava düştün!";
            gt.ravineIsWater = false;
            ScatterRocks(gt, 83, 20f);
            AddObstacles(gt, gobs);
            return gt;
        }

        /// Parkur 7 — Mars: hover kızak, düşük yerçekimi (0.38 g): uzun sıçramalar, süt yavaş ama kolay çalkalanır;
        /// dönüş geniş (yanal tutuş yerçekimiyle azalır), bu yüzden vadi az kıvrılır. Köprüler kraterler üstünde.
        public static TrackProfile Mars()
        {
            var gp = GlideStart(6, false);
            var gobs = Glide(gp, 6, 97, 0.35f, true, true);
            var gt = gp.Build(Names[6], Lengths[6], 3f, 420f, 1.4f, 150f, 17);
            gt.gravity = 0.38f * SledPhysics.EarthGravity;
            gt.fallText = "Kratere düştün!";
            gt.ravineIsWater = false;
            ScatterRocks(gt, 97, 22f);
            AddObstacles(gt, gobs);
            return gt;
        }

        /// Parkur 1 — Çayır (gerçek 1 km, ekranda 3600 m). Kanatsız bitirilebilir.
        /// Başta düzlük (sapan belirler), sonra basamaklar: her basamak bir iniş ve bir tepe. Basamağın net yükseklik
        /// kaybı en iyi kızağın sürtünme + hava direnci kaybını ancak karşılar; tepeler pist boyunca yükselir, böylece
        /// her yükseltme birkaç tepe daha aşmayı sağlar. Her 6 basamakta bir zorlu yükselti (uzun iniş, yüksek tepe).
        /// Basamaklar karışık: buzlu patika, patikayı kesen su/çamur (yanından çimenden dolaşılır), yayılmış buz, engel.
        public static TrackProfile Meadow()
        {
            if (UseGlide)
            {
                var gp = GlideStart(0, true);
                var gobs = Glide(gp, 0, 21, 0.35f, false, false);   // Çayır: buz yok, kanal da buzsuz
                // Kullanıcı isteği (2026-10-09): yol çok düzdü; vadi belirgin kıvrılır, patika içinde S çizer.
                var gt = gp.Build(Names[0], Lengths[0], 11f, 170f, 2.6f, 72f, 3);
                ScatterRocks(gt, 21, 26f);
                AddObstacles(gt, gobs);
                return gt;
            }
            // Sapan düzlüğü, sonra herkesi hızlandıran başlangıç inişi (ilk atış da kapağa ulaşsın) ve ilk su birikintisi.
            var p = Start(MeadowGrade, true);
            // Roket tepesi %45'ten, vadi (kanatla süzülerek geçilir) %62'den sonraki ilk basamakta.
            var obstacles = Staircase(p, Lengths[0], 21, MeadowRise.x, MeadowRise.y, MeadowBig.x, MeadowBig.y, MeadowNet, 0, MeadowGaps, 7,
                                      new[] { MeadowRocketAt }, new[] { MeadowRocket }, MeadowGate, bumpy: MeadowBumpy,
                                      rollers: MeadowRollers, upSlope: MeadowUpSlope, kickApproach: 0.3f, allowIce: false,
                                      gapAt: new[] { MeadowGapAt }, birds: false, grade: MeadowGrade, sideChoice: 0f,
                                      downScale: MeadowDownScale, jumpChance: MeadowJumps);
            var t = p.Build(Names[0], Lengths[0], 7f, 260f, 2f, 110f, 3);
            ScatterRocks(t, 21, 26f);
            AddObstacles(t, obstacles);
            return t;
        }


        // ------------------------------------------------------------------ süzülme pistleri (2026-10-08)
        // Kullanıcı kararı (Sled Surfers videoları incelendi): pist çoğunlukla yumuşak iniş; kızak sapanın verdiği
        // hızla başlar ve düzgünce yavaşlar, durduran tırmanış yok. Aralarda hızı geri veren iniş kanalları ("ikinci
        // nefes"). Mesafeyi sapan, kızak (sürtünme), roket ve rota (buz hızlı, su/çamur yavaş) belirler.

        /// Pist başına süzülme ayarları: yavaşlama yamacı eğimi (pist başında → sonunda; sona doğru düzleşir, her kızak
        /// seviyesi bir iki yamaç daha aşar), yamaç uzunluğu, kanal eğimi ve uzunluğu.
        public static float[] SlowGrade = { 0.143f, 0.095f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f };
        public static float[] SlowGradeEnd = { 0.138f, 0.092f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f };
        public static Vector2[] SlowLength = { new Vector2(60f, 80f), new Vector2(60f, 80f), new Vector2(60f, 80f), new Vector2(60f, 80f), new Vector2(60f, 80f), new Vector2(60f, 80f), new Vector2(60f, 80f) };
        public static float[] BoostGrade = { 0.42f, 0.36f, 0.36f, 0.34f, 0.34f, 0.34f, 0.32f };
        public static float[] BoostLength = { 25f, 25f, 25f, 25f, 25f, 25f, 25f };
        /// İnce köprü olasılığı (yamaç başına) ve iki köprü arası en az mesafe; köprü uzunluğu pist boyunca uzar.
        public static float[] BridgeChance = { 0.5f, 0.45f, 0.35f, 0.5f, 0.4f, 0.45f, 0.4f };
        public const float BridgeSpacing = 120f, BridgeDepth = 5f;
        public static Vector2 BridgeSpan = new Vector2(14f, 22f);
        /// Çatal olasılığı (yüzeysiz yamaç başına).
        public static float[] ForkChance = { 0.9f, 0.8f, 0.45f, 0.5f, 0.45f, 0.5f, 0.3f };
        public const float ForkLength = 44f;

        static List<Vector2> Glide(Path p, int track, int seed, float kickChance, bool snow, bool allowIce)
        {
            float length = Lengths[track], slowGrade, boostGrade = BoostGrade[track], boostLen = BoostLength[track];
            var rnd = new System.Random(seed);
            var brnd = new System.Random(seed ^ 0xb21d);   // köprüler ayrı: pistin geri kalanının dizilişi değişmesin
            var frnd = new System.Random(seed ^ 0xf04c);   // çatallar da ayrı
            float R(float a, float b) => a + (b - a) * (float)rnd.NextDouble();
            var obstacles = new List<Vector2>();
            void Pickup(float ahead)
            {
                if (p.pickupLip > 0f || p.lastLip < length * 0.7f) return;
                p.pickupLip = p.lastLip;
                p.pickupAhead = ahead;
            }
            int i = 0;
            float lastBridge = -BridgeSpacing;
            while (true)
            {
                float t = Mathf.Clamp01(p.z / length);
                slowGrade = Mathf.Lerp(SlowGrade[track], SlowGradeEnd[track], t);
                float L = Mathf.Lerp(SlowLength[track].x, SlowLength[track].y, t) * R(0.85f, 1.15f);
                // Yamaç-kanal döngüsü bitiş çizgisini geçene kadar sürer: sonda kanalsız uzun yamaç (duvar) kalmasın,
                // zorluk yalnızca eğimin pist boyunca azalmasından gelsin (her seviye biraz daha ileri).
                if (p.z > length + 20f) break;
                // Yavaşlama yamacı: hafif dalgalı. Bazılarının başında (kanal çıkışı, hız en yüksekken) atlama rampası:
                // havada kalış = coin. Yamaç ortasındaki rampa yavaşlamış kızağı hep aynı yerde durduruyordu.
                bool kick = i > 0 && rnd.NextDouble() < kickChance;
                float kickH = kick ? R(1.6f, 2.4f) : 0f;
                // İnce köprü (kanal çıkışında, rampa yerine): kızak hızlıyken düz ve tam ortadan geçmeli.
                bool bridge = i > 0 && p.z > length * 0.12f && p.z < length - 60f && p.z - lastBridge > BridgeSpacing
                              && brnd.NextDouble() < BridgeChance[track];
                if (bridge)
                {
                    kick = false;
                    kickH = 0f;
                    float span = Mathf.Lerp(BridgeSpan.x, BridgeSpan.y, t);
                    // Kanal çıkışından 20 m sonra: kızak biraz yavaşlamış, köprüye hizalanacak zaman var.
                    p.Hills(20f, 1, 0f, slowGrade * 20f);
                    p.Bridge(span, BridgeDepth, slowGrade);
                    lastBridge = p.z;
                    L = Mathf.Max(25f, L - 20f - span);   // köprü yamacın parçası: döngünün enerji dengesi değişmesin
                }
                else if (kick)
                {
                    // Kanaldan doğrudan rampaya geçiş 2.5 g'lik sıkışma yapıp sütü bir anda boşaltıyordu: önce 10 m yamaç.
                    p.Hills(10f, 1, 0f, slowGrade * 10f);
                    p.Ramp(8f, kickH, 0.8f);
                    Pickup(4f);
                    L = Mathf.Max(25f, L - 10f);
                }
                float side = rnd.Next(2) == 0 ? -1f : 1f;
                // Yamaçtaki yüzey (yamaç başına göre): buz şeridi (hızlı yol), patikayı kesen su/çamur (yanından dolaş)
                int kind = rnd.Next(5);
                // Yüzeysiz yamaçta çatal: patika ikiye ayrılır, seçilen şerit mesafeyi (ya da altını, sütü) etkiler.
                // Rampa ya da köprüden hemen sonra değil: inişte şerit değiştirmek için sert direksiyon sütü döküyordu.
                bool fork = kind != 1 && !kick && !bridge && L >= 62f && p.z > 60f && p.z + L < length
                            && frnd.NextDouble() < ForkChance[track];
                if (fork)
                {
                    float fo = 12f + (float)frnd.NextDouble() * (L - ForkLength - 16f);   // kanal çıkışından sonra (kızak çok hızlı)
                    int variant = frnd.Next(4), good = frnd.Next(2) == 0 ? -1 : 1;
                    p.Fork(fo, ForkLength, variant, good, snow, allowIce);
                }
                // Çatallı yamaçta kendi su/çamur şeridi konmaz (rastgele sayılar yine çekilir: pistin geri kalanı aynı kalsın).
                p.mute = fork;
                switch (kind)
                {
                    case 1:
                        if (allowIce) p.Lane(Surface.Ice, L * R(0.15f, 0.35f), L * R(0.25f, 0.4f), side < 0 ? -3.2f : 0.4f, side < 0 ? -0.4f : 3.2f, true);
                        break;
                    // Su/çamur yamacın başında (kanaldan çıkan hızlı kızak): hız kaybettirir ama duvar olmaz. Yamaç sonunda
                    // yavaşlamış her kızağı aynı yerde durduruyordu (seviye farkı mesafeye yansımıyordu).
                    case 2: p.Lane(snow ? Surface.Mud : Surface.Puddle, L * R(0.08f, 0.25f), R(8f, 12f), side < 0 ? -4.2f : -1.7f, side < 0 ? 1.7f : 4.2f, true); break;
                    case 3: p.Lane(Surface.Mud, L * R(0.08f, 0.25f), R(7f, 10f), side < 0 ? -4.4f : -1.7f, side < 0 ? 1.7f : 4.4f, true); break;
                }
                if (snow)
                    for (int k = 0; k < 1 + rnd.Next(2); k++)
                    {
                        bool ice = rnd.NextDouble() < 0.55;
                        float x0 = R(-5.8f, 2.5f), w = ice ? R(2.5f, 5f) : R(1.8f, 3.2f);
                        p.Lane(ice ? Surface.Ice : Surface.Mud, R(10f, L - 30f), ice ? R(14f, 28f) : R(6f, 10f), x0, Mathf.Min(x0 + w, 5.8f));
                    }
                p.mute = false;
                // Yamaç (rampadan sonra başlar; su/çamur ve buz konumları buna göre)
                p.Hills(L, Mathf.Max(1, Mathf.RoundToInt(L / 70f)), 0.6f, slowGrade * L + Mathf.Max(0f, kickH - 0.8f), kick ? 1.4f : 1f);
                // Hız toplatan iniş kanalı (Çayır'da buzsuz)
                if (allowIce) p.Lane(Surface.Ice, 4f, boostLen - 8f, -1.8f, 1.8f, true);
                p.Hills(boostLen, 1, 0f, boostGrade * boostLen, 1.2f);
                i++;
            }
            p.Hills(60f, 1, 0.6f, SlowGradeEnd[track] * 60f).Flat(80f);
            return obstacles;
        }

        /// Süzülme pisti başı: sapan düzlüğü, sonra yavaşlama yamacı eğiminde fırlatma koşusu (sapan belirleyici).
        static Path GlideStart(int track, bool puddle)
        {
            float g = SlowGrade[track];
            var p = new Path(-30f).Flat(26f).Hills(50f, 1, 0.3f, g * 50f);
            if (puddle) p.Puddle(5f, -g * 5f);
            return p.Hills(puddle ? 65f : 70f, 1, 0.3f, g * (puddle ? 65f : 70f));
        }

        /// Süzülme modeli açık mı (false: eski basamak/vadi/roket tepesi tasarımı).
        public static bool UseGlide = true;

        /// Pist başı: sapan düzlüğü (fırlatma -6 m'de), sonra fırlatma koşusu: eğimi kızak sürtünmesinin biraz altında
        /// (OpenGrade), her kızak burada yavaş yavaş hız kaybeder; ne kadar ileri gidileceğini sapanın verdiği hız
        /// belirler (kullanıcı: fırlatma hızı belirleyici). Ardından basamaklara 60 m'lik geçiş inişi.
        /// puddle: 50 m'de ilk su birikintisi (Çayır'da öğretici).
        static Path Start(float grade, bool puddle)
        {
            float g = OpenGrade;
            var p = new Path(-30f).Flat(26f).Hills(50f, 1, 0.3f, g * 50f);
            if (puddle) p.Puddle(5f, -g * 5f);
            float rest = OpenLength - 55f;
            p.Hills(rest, Mathf.Max(1, Mathf.RoundToInt(rest / 60f)), 0.5f, g * rest);
            return p.Hills(60f, 1, 0.3f, (grade + StartBoost) * 60f);
        }
        /// Fırlatma koşusu: uzunluk ve eğim (sürtünme 0.25 → 0.17; 0.2 ile çoğu kızak burada yavaşlar).
        public static float OpenLength = 320f, OpenGrade = 0.22f;

        // Vadi atlayışları sırayla (genişlik, y > 0: hedef kanat alanı → derinlik süzülme oranından;
        // y < 0: karşı kıyı derinliği, simle ayarlanmış). Küçük kanat 20 m/s'de az taşır: hız da belirleyicidir.
        // Kanat alanları kızak seviyesine göre: 13→0.7, 14→1.1, 15→1.6, 16→2.0, 17→2.4 … 20→3.6.
        // 2026-10-08 (sürtünmeli fizik, kanat Kızak 5'ten, süzülme süresi sınırlı): vadiler kanat seviyesine göre
        // genişler; hedef sırayla Kızak 9 / 10 / 12 / 14 / 16 (simle ayarlanacak).
        public static Vector2[] ForestGaps =
            { new Vector2(86f, -30f), new Vector2(95f, -30f), new Vector2(105f, -30f), new Vector2(113f, -30f),
              new Vector2(113f, -30f) };   // 30 m derin: süzülme süresi belirleyici (SimTests.GlideTest)
        // Kanyon vadileri (Kızak 16 / 17 / 18 / 19 hedefli, simle ayarlanacak).
        public static Vector2[] CanyonGaps =
            { new Vector2(118f, -30f), new Vector2(133f, -30f), new Vector2(143f, -30f), new Vector2(143f, -30f) };

        // Roket tepelerinin normal tepeden fazla yüksekliği (m), parkur başına.
        public static float MeadowRocket = 12f, ForestRocket = 18f, CanyonRocket = 32f, MeadowGate = 0f, MeadowBumpy = 1f, MeadowNet = 0f, ForestNet = 0f, ForestGate = 0f, CanyonNet = 0f, GapRun = 16f, GapMargin = 6f,
                            CanyonGate = 0f, CanyonGateLength = 1000f,
                            SnowRocket = 36f, SnowGapMargin = 6f, SnowNet = 0f, SnowGate = 0f, SnowGateLength = 1000f;
        // Pist eğimi (metre başına net iniş): hedef kızağın sürtünmesini (+ hava direncini) karşılar. Sapan, roket ve
        // rotanın verdiği fazla enerji kızağı taşır; sürtünmesi yüksek (geri seviye) kızak bu fazlayı daha çabuk tüketir.
        public static float MeadowGrade = 0.34f, ForestGrade = 0.29f, CanyonGrade = 0.3f, SnowGrade = 0.32f;
        public const float StartBoost = 0.1f;   // başlangıç inişi pistten bu kadar dik (ilk atışlar da biraz gitsin)
        // Çayır tepeleri: normal (başta, sonda) ve zorlu tepe yüksekliği; ara tepe = tepenin bu katı; yokuş eğimi çarpanı.
        public static Vector2 MeadowRise = new Vector2(6f, 20f), MeadowBig = new Vector2(10f, 22f);   // sonlara doğru belirgin yükselir
        public static float MeadowDownScale = 1.25f;   // Çayır inişleri daha uzun: dik olmasın (~25°)
        public static float SteepDownScale = 1.35f;    // Orman/Kanyon/Karlı Dağ: pist eğimi yüksek, inişler uzatılır (~28-30°)
        public static float MeadowRollers = 1f, MeadowUpSlope = 0.6f, MeadowGapAt = 0.62f, MeadowRocketAt = 0.45f, MeadowJumps = 0.3f;
        // Çayır vadisi: 52 m, karşı kıyı dudaktan 15 m alçakta. Simle: genişlikte her 6 m bir kızak seviyesi
        // (40 m → Kızak 6, 46 → 7, 52 → 8). Kızak 8 geçer, 7 ve altı vadiye düşer; kanatsız hiçbir hızda geçemez.
        public static Vector2[] MeadowGaps = { new Vector2(52f, -15f) };
        // Karlı Dağ vadileri: sonuncusu Kızak 20 ister (simle ayarlanacak).
        public static Vector2[] SnowGaps = { new Vector2(137f, -30f), new Vector2(146f, -30f), new Vector2(146f, -30f) };
        const float Gate = 3.5f;    // kapı basamağında tırmanılan fazla yükseklik
        const float Net = 9.4f;   // basamak başına net iniş: hız korunur; iyi kızak daha yüksek denge hızına ulaşır

        /// Parkur boyunca basamaklar üretir. Tepe yüksekliği riseStart'tan riseEnd'e, zorlu yükseltilerde
        /// bigStart'tan bigEnd'e çıkar. Dönüş: engel konumları (z, patikaya göre x; x &lt; 0 kaya, x &gt; 0 kasa).
        static List<Vector2> Staircase(Path p, float length, int seed, float riseStart, float riseEnd, float bigStart, float bigEnd,
                                       float net = Net, int gapEvery = 0, Vector2[] gaps = null,
                                       int dereEvery = 0, float[] rocketAt = null, float[] rocketExtra = null,
                                       float gate = Gate, float gateLength = 600f, bool snow = false, float margin = -1f,
                                       float bumpy = 0f, float sideChoice = 0.35f, bool kickers = true,
                                       float rollers = 0f, float upSlope = 1f, float kickApproach = 0.45f, bool allowIce = true,
                                       float[] gapAt = null, bool birds = true, float grade = 0f, float downScale = 1f,
                                       float jumpChance = 0.45f)
        {
            // grade > 0: basamağın net inişi uzunluğuyla orantılı (sürtünme metre başına enerji yer): net + grade·uzunluk.
            // Yüksek sürtünmede iniş eğimi zaten dik; içbükey iniş daha yumuşak tutulur (başı aşırı dikleşmesin).
            float dShape = grade > 0.15f ? 1.25f : 1.7f;
            // Taraf seçimi su/çamur lekeleri (patika dışında, tepenin bir yanında) kaldırıldı: kullanıcı "anlamsız"
            // buldu; hızlı kızak virajda açılıp bunlara giriyor, tepeyi çıkamıyordu.
            if (grade > 0f) sideChoice = 0f;
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
            bool afterGap = false;   // önceki basamak vadi miydi (vadiden süzülen kızak sonraki rampaya çakılmasın)
            while (p.z < length - 160f)
            {
                float t = Mathf.Clamp01(p.z / length);
                // Roket tepesi: normal tepeden rocketExtra kadar yüksek, dik (%32) yokuş. Roketsiz kızak ancak
                // çok hızlı gelirse aşar; zirveden sonra fazladan uzun iniş enerjiyi geri verir.
                // Vadi: her gapEvery basamakta bir ya da (gapAt) pistin verilen oranlarından sonraki ilk basamakta.
                bool gapStep = gaps != null && nextGap < gaps.Length
                               && (gapAt != null ? nextGap < gapAt.Length && t >= gapAt[nextGap]
                                                 : gapEvery > 0 && i % gapEvery == gapEvery - 1 && p.z < length - 700f);
                bool rocketStep = rocketAt != null && nextRocket < rocketAt.Length && t >= rocketAt[nextRocket] && !gapStep
                                  && (dereEvery == 0 || i % dereEvery != dereEvery - 1);
                bool big = i % 6 == 5;
                float down = (big ? R(160f, 180f) : R(100f, 130f)) * downScale;
                float upDraw = big ? R(70f, 90f) : R(40f, 55f);
                // Tepe yüksekliği pistin hedef kızağının hız enerjisine göre ölçeklidir (riseStart..riseEnd): her seviyede
                // kızak tepeden önce belirgin yavaşlar, vadide hızlanır; yol hiçbir seviyede düz hissettirmez.
                float rise = big ? Mathf.Lerp(bigStart, bigEnd, t) : Mathf.Lerp(riseStart, riseEnd, t) + R(-0.3f, 0.3f);
                // Yokuş eğimi %22-30 (büyük tepede %20-26): gözle seçilen belirgin tepe; hızlı kızak zirveden havalanır.
                float up = (big ? rise / Mathf.Lerp(0.2f, 0.26f, (upDraw - 70f) / 20f)
                                : rise / Mathf.Lerp(0.22f, 0.3f, (upDraw - 40f) / 15f)) / upSlope;
                // Sürtünmeli pistte uzun yatık yokuş çok enerji yer (yükseklik + μ·uzunluk): yokuş en çok 75 m.
                if (grade > 0f) up = Mathf.Min(up, 75f);
                // Son basamak bitişi aşacaksa konmaz: bitiş çizgisi her zaman son tepeden sonraki inişte kalır.
                if (p.z + down + up > length - 150f) break;
                int kind = big ? (rnd.Next(2) == 0 ? 1 : 4) : rnd.Next(6);
                // Atlama rampası (inişin ortasında): bardaktaki süt sarsılır. Su/çamur ile üst üste binmez.
                bool jump = !big && p.z > 120f && rnd.NextDouble() < jumpChance;
                if (rocketStep || !kickers || afterGap) jump = false;   // kickers: büyük vadili pistlerde uçuş vadilerde; vadiden hemen sonra rampa yok
                if (jump && (kind == 2 || kind == 3)) kind = 0;
                float side = rnd.Next(2) == 0 ? -1f : 1f;
                bool dereStep = dereEvery > 0 && i % dereEvery == dereEvery - 1 && p.z < length - 700f;
                // Ara tepe (rollers > 0): normal inişin ilk ~%40'ından sonra gerçek bir yokuş (tepenin rollers katı,
                // %25 eğim), ardından inişin geri kalanı. Basamağın net inişi değişmez; yol daha sık tırmanır.
                bool roller = rollers > 0f && !big && !jump && !rocketStep && !gapStep && !dereStep && p.z > 120f;
                float mRise = roller ? rise * rollers * B(0.85f, 1.15f) : 0f, mUp = mRise / 0.25f, rd1 = down * 0.4f;
                // Islak şerit ikinci inişte (ara tepenin yokuşunda/tepesinde değil)
                float WetAt() => roller ? rd1 + mUp + 6f + (down - rd1) * R(0.2f, 0.35f) : down * R(0.45f, 0.6f);
                switch (kind)
                {
                    case 1:   // buzlu patika (ara tepeli inişte ikinci inişte)
                        if (!allowIce) break;   // buzsuz pist (Çayır): yalnızca su ve çamur
                        if (roller) p.Lane(Surface.Ice, rd1 + mUp + 8f, down - rd1 - 14f, -1.7f, 1.7f, true);
                        else p.Lane(Surface.Ice, 6f, down - 12f, -1.7f, 1.7f, true);
                        break;
                    case 2: p.Lane(snow ? Surface.Mud : Surface.Puddle, WetAt(), R(8f, 12f), side < 0 ? -4.2f : -1.7f, side < 0 ? 1.7f : 4.2f, true); break;
                    case 3: p.Lane(Surface.Mud, WetAt(), R(7f, 10f), side < 0 ? -4.4f : -1.7f, side < 0 ? 1.7f : 4.4f, true); break;
                    case 4:   // yayılmış buz lekeleri
                        if (!allowIce) { R(0f, 1f); R(0f, 1f); R(0f, 1f); R(0f, 1f); R(0f, 1f); R(0f, 1f); break; }   // aynı rastgele dizi
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
                if (kind == 0 || kind == 1)
                {
                    var o = new Vector2(p.z + down + (roller ? mUp + 4f : 0f) + up * R(0.3f, 0.6f), side * (grade > 0f ? R(3f, 3.8f) : R(2.2f, 2.8f)));   // hızlı (sürtünmeli) pistte yarış çizgisinden biraz dışarıda
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
                    // Sürtünmeli pistte vadiye hızlı girilsin: daha çok iniş, alçak dudak (rampa hız kesmesin).
                    if (grade > 0f) p.Hills(down, 1, 0.4f, net + grade * down + GapRun * 1.8f).Ramp(14f, 1.5f, 1f);
                    else p.Hills(down, 1, 0.4f, net + grade * down + GapRun).Ramp(14f, 3f, 1.5f);
                    Pickup(5f);   // havada alınan hak ancak yere inince ateşlenir (SledPhysics): vadiyi roketle geçirmez
                    // Vadinin ortasında, süzülme yolunda sağa sola salınan kuş sürüsü
                    var flock = new Flock { z = p.z + 3f + width * 0.5f, x = 0f, y = p.y - 3f - width * 0.06f,
                                            sway = R(3f, 5f), period = R(2.5f, 4f), radius = 2.2f };
                    if (birds) p.flocks.Add(flock);   // Çayır'da kuş yok
                    // İniş kıyısı dudağın gapDrop altında: kanatsız kızak balistik menzille, kanatlı süzülerek geçer.
                    // Eğimli, uzun iniş bölgesi: süzülüşte hava direncine kaybedilen hız burada geri kazanılır.
                    p.Gap(3f, -(dropHere + 6f), width, 3f, 6f).To(60f, -16f - grade * 60f);
                    i++;
                    afterGap = true;
                    continue;
                }
                float climbLen = rocketStep ? (rise + rocketExtra[nextRocket]) / 0.32f + 8f : up + 6f;
                float stepLen = down + climbLen + (roller ? mUp + 4f : 0f);
                float stepNet = (p.z > length - gateLength ? net - gate : net) + grade * stepLen;
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
                    // kickApproach: düşüşün rampadan önceki payı. Küçükse rampaya yatık girilir, iniş yamacı daha uzun
                    // süre dik kalır (yüksek tepeli Çayır'da hızlı kızak uçuştan sonra düz yere çakılmasın).
                    p.Hills(d1, 1, 0.4f, drop * kickApproach).Ramp(10f, kick, 0.8f);
                    Pickup(4f);
                    p.Hills(down - d1 - 13f, 1, 0.4f, drop * (1f - kickApproach) + kick - 0.8f, dShape);   // dudaktan sonra dik iniş yamacı
                }
                else if (roller)
                {
                    // İniş + ara tepe + iniş: iki iniş toplamı drop + ara tepe yüksekliği (net değişmez).
                    float s0 = p.z, total = drop + mRise, d2 = down - rd1;
                    p.Hills(rd1, 1, 0.4f, total * 0.42f, dShape);
                    p.To(mUp, mRise).Flat(4f);
                    float s1 = p.z;
                    int waves = bumpy > 0f ? Mathf.Max(1, Mathf.RoundToInt(d2 / 38f)) : 1;
                    p.Hills(d2, waves, bumpy > 0f ? 0.4f + (grade > 0f ? 0.3f : 0.6f) * bumpy : 0.4f, total * 0.58f, dShape);
                    bool wet = kind == 2 || kind == 3;
                    if (bumpy > 0f && s0 > 300f && !wet) AddBumps(p, s1 + d2 * 0.15f, s1 + d2 - 12f, t, true, bumpy, brnd, B);
                }
                else
                {
                    float s0 = p.z;
                    // Dalgalı iniş: ~38 m'de bir dalga (engebeli pistte).
                    int waves = bumpy > 0f ? Mathf.Max(1, Mathf.RoundToInt(down / 38f)) : 1;
                    // Dik (sürtünmeli) pistte dalga yarı yükseklikte: 28 m/s'de sert dalga sütü döker.
                    p.Hills(down, waves, bumpy > 0f ? 0.4f + (grade > 0f ? 0.4f : 0.9f) * bumpy : 0.4f, drop, dShape);
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
                afterGap = false;
            }
            // Son tepeden bitişe uzun hafif iniş (bitişten 40 m önce düzlüğe iner).
            float rest = Mathf.Max(150f, length - p.z - 40f);
            p.To(rest, -Mathf.Max(12f, rest * (0.1f + grade))).Flat(120f);   // eğim sürtünmenin üstünde: bitişe ulaşılır
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
            if (UseGlide)
            {
                var gp = GlideStart(1, false);
                var gobs = Glide(gp, 1, 33, 0.3f, false, true);
                var gt = gp.Build(Names[1], Lengths[1], 8f, 220f, 2.3f, 85f, 5);
                ScatterRocks(gt, 33, 24f);
                AddObstacles(gt, gobs);
                return gt;
            }
            var p = Start(ForestGrade, false);
            var obstacles = Staircase(p, Lengths[1], 33, 8f, 18f, 11f, 20f, ForestNet, 4, ForestGaps, 0,
                                      new[] { 0.62f }, new[] { ForestRocket }, ForestGate, grade: ForestGrade,
                                      downScale: SteepDownScale, kickers: false);   // uçuş vadilerde (kanatlı kızak rampaya çakılmasın)
            var t = p.Build(Names[1], Lengths[1], 5f, 320f, 2f, 110f, 5);   // az kıvrım: uzun süzülmelerde kızak vadide kalsın
            ScatterRocks(t, 33, 24f);
            AddObstacles(t, obstacles);
            return t;
        }

        /// Parkur 4 — Karlı Dağ (4,2 km). Final: her yer kar; basamaklarda buz ve çamur lekeleri, rampalar,
        /// büyük vadiler, en güçlü roketi isteyen bir roket tepesi ve uzun kapı bölgesi. Her şeyin son seviyesini ister.
        public static TrackProfile Snow()
        {
            if (UseGlide)
            {
                var gp = GlideStart(3, false);
                var gobs = Glide(gp, 3, 59, 0.3f, true, true);
                var gt = gp.Build(Names[3], Lengths[3], 5f, 320f, 2.4f, 100f, 9);
                ScatterRocks(gt, 59, 22f);
                AddObstacles(gt, gobs);
                return gt;
            }
            var p = Start(SnowGrade, false);
            var obstacles = Staircase(p, Lengths[3], 59, 9f, 13f, 12f, 14f, SnowNet, 5, SnowGaps, 0,
                                      new[] { 0.35f }, new[] { SnowRocket }, SnowGate, SnowGateLength, true, SnowGapMargin, kickers: false,
                                      grade: SnowGrade, downScale: SteepDownScale);
            var t = p.Build(Names[3], Lengths[3], 5f, 320f, 2.4f, 100f, 9);   // az kıvrım: uzun süzülmelerde kızak vadide kalsın
            ScatterRocks(t, 59, 22f);
            AddObstacles(t, obstacles);
            return t;
        }

        /// Parkur 3 — Kanyon (4,5 km). Daha yüksek tepeler ve her 4 basamakta bir geniş kanyon: büyük kanat ister.
        public static TrackProfile Canyon()
        {
            if (UseGlide)
            {
                var gp = GlideStart(2, false);
                var gobs = Glide(gp, 2, 47, 0.3f, false, true);
                var gt = gp.Build(Names[2], Lengths[2], 7f, 240f, 2.2f, 90f, 7);
                ScatterRocks(gt, 47, 22f);
                AddObstacles(gt, gobs);
                return gt;
            }
            var p = Start(CanyonGrade, false);
            var obstacles = Staircase(p, Lengths[2], 47, 9f, 17f, 12f, 18f, CanyonNet, 4, CanyonGaps, 0,
                                      new[] { 0.5f }, new[] { CanyonRocket }, CanyonGate, CanyonGateLength, kickers: false,
                                      grade: CanyonGrade, downScale: SteepDownScale);
            var t = p.Build(Names[2], Lengths[2], 5f, 340f, 2f, 120f, 7);   // az kıvrım: uzun süzülmelerde kızak vadide kalsın
            ScatterRocks(t, 47, 22f);
            AddObstacles(t, obstacles);
            return t;
        }
    }
}
