using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Parkurlar sırayla açılır: biri %100 bitince sonraki açılır.
    public static class TrackLibrary
    {
        public const int Count = 7;

        public static readonly string[] Names = { "Clover Valley", "Whispering Woods", "Sunset Canyon", "Frostpeak Summit", "Golden Dunes", "Ember Peak", "Red Planet Rush" };
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
        public static float[] CostScale = { 0.8f, 0.78f, 2.6f, 1.45f, 1.7f, 1.2f, 2.15f };   // 2026-10-18: sim artık pistteki gerçek (sınırlı) altını sayıyor; geç pistler hedef atış sayısına geri çekildi   // 2026-10-10: referans fiyat eğrisi (5×aşama, 1.4→1.24) ile
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
                straight.Add(new Vector2(z0 - 75f, z + 6f));   // 75 m düz yaklaşma (ortalama oyuncu 40 m'de hizalanamıyordu)
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
                foreach (var b in bridges) t.centerZones.Add(new Vector2(b.x - 60f, b.y + 5f));
                t.water.AddRange(water);
                t.surfaces.AddRange(surfaces);
                t.flocks.AddRange(flocks);
                t.rocketHills.AddRange(rocketHills);
                t.bumps.AddRange(bumps);
                t.IndexBumps();
                // Tabla kıyılara 3.5 m uzanır (profil yumuşatması ±3 m) ve uçları profile tam oturur.
                // Kıvrım (kullanıcı 2026-10-18): her pistin ilk köprüsü düz (öğretir), sonrakilerin bir kısmı yay, S ya da çapraz.
                int track = System.Array.IndexOf(Names, name);
                var crnd = new System.Random(seed * 131 + 9);
                for (int k = 0; k < bridges.Count; k++)
                {
                    var b = bridges[k];
                    float z0 = b.x - 3.5f, z1 = b.y + 3.5f;
                    var br = new TrackProfile.Bridge { z0 = z0, z1 = z1, r0 = b.x, r1 = b.y, y0 = t.Height(z0), y1 = t.Height(z1),
                                                       x = t.PathX((b.x + b.y) * 0.5f) };
                    double roll = crnd.NextDouble();
                    float amp = (float)crnd.NextDouble();
                    // İki ve daha çok köprülü pistte en az biri kıvrımlı: rastlantı hepsini düz bırakmasın (son köprü).
                    bool anyCurved = t.bridges.Exists(x => x.shape != 0);
                    if (k > 0 && track >= 0 && (roll < BridgeCurveChance[track] || (k == bridges.Count - 1 && !anyCurved)))
                    {
                        br.shape = 1 + crnd.Next(3);
                        float scale = Mathf.Clamp01((b.y - b.x) / 16f);
                        br.bend = (br.shape == 2 ? Mathf.Lerp(0.35f, 0.6f, amp) : Mathf.Lerp(0.5f, 0.9f, amp)) * scale * (crnd.Next(2) == 0 ? -1f : 1f);
                        // Kıvrımlı tabla biraz geniş (simde ortalama oyuncu 0.8 m çapraz köprüden 10'da 6 düşüyordu).
                        br.hw = CurvedBridgeHalfWidth;
                    }
                    t.bridges.Add(br);
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

            // Kar dudağına giriş kademeli dikleşir; doğrusal rampanın başlangıcındaki ani sıkışmayı önler.
            // Arkadaki kar yamacı iniş alanıdır: uçmak için roket ya da bir dereyi aşmak gerekmez.
            public Path SnowTakeoff(float length, float entrySlope, float exitSlope, float fall)
            {
                float z0 = z, y0 = y;
                int samples = Mathf.CeilToInt(length / 2f);
                for (int i = 1; i <= samples; i++)
                {
                    float s = length * i / samples;
                    points.Add(new Vector2(z0 + s, y0 + entrySlope * s
                        + (exitSlope - entrySlope) * s * s / (2f * length)));
                }
                z += length;
                y += (entrySlope + exitSlope) * length * 0.5f;
                lastLip = z;
                sharp.Add(new Vector2(z0 - 2f, z + 6f));
                straight.Add(new Vector2(z0 - 12f, z + 100f));
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
            gt.fallText = "Fell in the canal!";
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
            gt.fallText = "Fell in the lava!";
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
            gt.fallText = "Fell in the crater!";
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
        public static float[] SlowGrade = { 0.178f, 0.18f, 0.18f, 0.1f, 0.1f, 0.08f, 0.08f };   // 2026-10-10: uzun yokuşlarla yeniden tarandı (Mars: sona doğru belirgin azalır; pervane telafisiyle sonda hafif yokuş yukarı)
        public static float[] SlowGradeEnd = { 0.173f, 0.16f, 0.162f, 0.1f, 0.1f, 0.08f, 0.045f };
        public static Vector2[] SlowLength = { new Vector2(60f, 80f), new Vector2(60f, 80f), new Vector2(60f, 80f), new Vector2(60f, 80f), new Vector2(60f, 80f), new Vector2(60f, 80f), new Vector2(60f, 80f) };
        public static float[] BoostGrade = { 0.42f, 0.36f, 0.36f, 0.34f, 0.34f, 0.34f, 0.32f };
        public static float[] BoostLength = { 25f, 25f, 25f, 25f, 25f, 25f, 25f };
        /// Kullanıcı isteği (2026-10-09, oyun videoları): uzun ama düşük eğimli yokuşlar. Yamaç ve kanal CycleScale kat
        /// uzar; kanal BoostSoft kadar yatıklaşır ve aynı düşüşü korumak için o oranda daha da uzar (pistin toplam
        /// düşüşü, yani ekonomi, aşağı yukarı aynı kalır; hız daha az dalgalanır).
        public static float BoostSoft = 0.7f;
        /// Parkur başına uzatma: Çayır'da az (kullanıcı 2026-10-10: ilk parkurda uzun düzlükler boş duruyordu), sonraki
        /// parkurlarda giderek uzun yokuşlar. WaveLength: yamaçtaki dalgaların aralığı (m), WaveAmp: yüksekliği.
        public static float[] CycleScale = { 1.1f, 1.4f, 1.8f, 1.8f, 1.8f, 1.8f, 1.8f };
        // Çayır: belirgin inişli çıkışlı uzun dalgalar (kullanıcı 2026-10-10: çok düzlük vardı). 70 m'de bir, 2.4 m:
        // eğim neredeyse düz (%8) ile dik (%30) arasında gidip gelir; tepeler yumuşak (81 km/sa'nın altında havalanmaz).
        // Orman (kullanıcı 2026-10-11: dümdüzdü; rastgele değil, enerji kuralına göre): tepede havalanma koşulu
        // v²·A·(2π/λ)² > g. 90 m'de 2.2 m'lik dalga 25 m/s'de (90 km/sa) yerde tutar (0.67 g) ama eğimi +%5 yokuş yukarı ile
        // -%26 iniş arasında değiştirir; dalga indiği kadar çıkar, yamacın net düşüşü (= sürtünme kaybı) aynı kalır.
        public static float[] WaveLength = { 70f, 90f, 70f, 80f, 70f, 70f, 70f };
        // Kar: geniş tepeler; net yamaç düşüşü değişmeden 0.6 m yerine 2 m kabartı.
        public static float[] WaveAmp = { 2.4f, 2.2f, 0.6f, 2f, 0.6f, 0.6f, 0.6f };
        /// Krater duvarının yüksekliği (m; 0 = yok) ve pistteki yeri (uzunluk oranı).
        public static float[] WallRise = { 0f, 0f, 0f, 0f, 0f, 0f, 16f };
        public static float WallAt = 0.72f;
        /// Kızak kartının hava direncine etkisi (ilk kademe → son kademe çarpanı). Mars'ta sürtünme küçük, belirleyici olan
        /// hava direnci: hover gövdesi yükseldikçe aerodinamikleşir, böylece yükseltme mesafeye yansır.
        public static float[] DragStart = { 1f, 1f, 1f, 1f, 1f, 1f, 1.8f };
        public static float[] DragEnd = { 1f, 1f, 1f, 1f, 1f, 1f, 0.7f };
        /// İnce köprü olasılığı (yamaç başına) ve iki köprü arası en az mesafe; köprü uzunluğu pist boyunca uzar.
        public static float[] BridgeChance = { 0.5f, 0.45f, 0.35f, 0.5f, 0.25f, 0.22f, 0.22f };   // son üç pist uzun: köprü sayısı 3-4 (7 köprüde hızlı kızak hep birinde düşüyordu)
        public const float BridgeSpacing = 120f, BridgeDepth = 5f, BridgeLead = 40f;
        /// Köprü öncesi yükselti (m) ve ardından köprüye inen yokuşun eğimi (0 = yok, düz yavaşlama yamacı).
        public static float[] BridgeRise = { 0.5f, 0.5f, 0.4f, 0.4f, 0.4f, 0.4f, 0.3f };
        public static float[] BridgeDive = { 0.42f, 0.36f, 0.32f, 0.3f, 0.3f, 0.28f, 0.22f };
        /// Dalgalı yaklaşımda dalga yüksekliği (m).
        public static string ApproachLog = "";   // test: hangi köprüye hangi yaklaşım tipi (pist:tip@z)
        /// Köprünün kıvrımlı olma olasılığı (ilk köprü hariç); geç pistlerde daha sık.
        public const float CurvedBridgeHalfWidth = 1.05f;
        public static float[] BridgeCurveChance = { 0.5f, 0.6f, 0.6f, 0.6f, 0.65f, 0.7f, 0.7f };
        public static float[] BridgeWave = { 1f, 0.9f, 0.5f, 0.5f, 0.5f, 0.45f, 0.4f };
        public static Vector2 BridgeSpan = new Vector2(14f, 22f);
        /// Çatal olasılığı (yüzeysiz yamaç başına).
        public static float[] ForkChance = { 0.9f, 0f, 0.45f, 0.5f, 0.45f, 0.5f, 0.3f };   // yolsuz haritada (Orman) çatal yok
        public const float ForkLength = 44f;

        /// Pervane itişinin eğim karşılığı (o piste gelen oyuncunun parçasıyla; fizikteki tavanla aynı: ≈ %70 μg).
        static float PropGrade(int track)
        {
            float g = track == 6 ? 0.38f : 1f;
            return Mathf.Min(Upgrades.PropThrust(track) / (SledPhysics.EarthGravity * g), 0.06f);
        }

        static List<Vector2> Glide(Path p, int track, int seed, float kickChance, bool snow, bool allowIce)
        {
            float length = Lengths[track], slowGrade, boostGrade = BoostGrade[track] * BoostSoft,
                  boostLen = BoostLength[track] * CycleScale[track];   // yatık kanalda hava direnci az: düşüş de %30 az
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
            bool wallBuilt = false;
            float lastBridge = -BridgeSpacing;
            while (true)
            {
                float t = Mathf.Clamp01(p.z / length);
                // Kalıcı pervane (bu piste gelen oyuncuda takılı) yamaçta eğim gibi iter: pist o kadar yatık kurulur ki
                // ekonomi pervanesiz ayarla aynı kalsın; pervane eski pistlere dönünce gerçek avantaj olur.
                slowGrade = Mathf.Lerp(SlowGrade[track], SlowGradeEnd[track], t) - PropGrade(track);
                // Sık uçuşların hava/iniş kaybını sonraki kar yamaçlarının uzun inişi karşılar.
                // Başlangıç döngüsü aynı kalır: ilk seviyenin menzili sapan yükseltmesini atlamaz.
                if (track == 3 && i > 0) slowGrade += 0.02f;
                float L = Mathf.Lerp(SlowLength[track].x, SlowLength[track].y, t) * CycleScale[track] * R(0.85f, 1.15f);
                // Yamaç-kanal döngüsü bitiş çizgisini geçene kadar sürer: sonda kanalsız uzun yamaç (duvar) kalmasın,
                // zorluk yalnızca eğimin pist boyunca azalmasından gelsin (her seviye biraz daha ileri).
                if (p.z > length + 20f) break;
                // Krater duvarı (Mars, kullanıcı kararı 2026-10-10): pistin sonlarına doğru dik bir tırmanış; hız tek
                // başına yetmez, roketle aşılır (roket kademesi belirleyici). İlerleme çubuğunda roket tepesi olarak işaretli.
                if (WallRise[track] > 0f && !wallBuilt && p.z > length * WallAt)
                {
                    wallBuilt = true;
                    float rise = WallRise[track], w0 = p.z;
                    p.Flat(12f).To(rise / 0.3f, rise).Flat(8f);
                    p.rocketHills.Add(p.z - 4f);
                    p.To(rise / 0.22f, -rise);
                    p.straight.Add(new Vector2(w0 - 30f, p.z + 40f));
                }
                // Yavaşlama yamacı: hafif dalgalı. Bazılarının başında (kanal çıkışı, hız en yüksekken) atlama rampası:
                // havada kalış = coin. Yamaç ortasındaki rampa yavaşlamış kızağı hep aynı yerde durduruyordu.
                bool kick = i > 0 && rnd.NextDouble() < kickChance;
                // Kar parkurunda kalkışlar rastlantıya veya köprü seçimine bağlı kalmaz.
                if (track == 3) kick = i > 0 && (i % 2 == 1);
                float kickH = kick ? R(1.6f, 2.4f) : 0f;
                float snowDropCorrection = 0f;
                // İnce köprü (kanal çıkışında, rampa yerine): kızak hızlıyken düz ve tam ortadan geçmeli.
                bool bridge = i > 0 && p.z > length * 0.12f && p.z < length - 60f && p.z - lastBridge > BridgeSpacing
                              && brnd.NextDouble() < BridgeChance[track];
                // Bitiş yaklaşımında uzun köprü girişi açıklığı çizginin dibine taşımamalı.
                if (track == 3 && (kick || p.z > length - 180f)) bridge = false;
                if (bridge)
                {
                    kick = false;
                    kickH = 0f;
                    float span = Mathf.Lerp(BridgeSpan.x, BridgeSpan.y, t);
                    // Kanal çıkışından 20 m sonra: kızak biraz yavaşlamış, köprüye hizalanacak zaman var.
                    // Uzun kanaldan çıkan kızak hızlı: köprüye 40 m'lik yamaçla yaklaşır (yavaşlasın, hizalansın).
                    // Kanaldan yamaca ani geçişte hızlı kızak havalanıp köprüye havadan iniyordu: önce ara eğimli 20 m.
                    // Köprü yaklaşımı her köprüde farklı (kullanıcı 2026-10-11: uzun düz yaklaşımlar sıkıcıydı):
                    // 0 yükselti + kademeli dikleşen iniş (hız kazandırır), 1 yumuşak dalgalar, 2 kısa düz yamaç.
                    // Son 10 m her tipte düz eğimli: kızak tablaya yerden girer.
                    // Hızlı parkurlarda (Kanyon ve sonrası) yükselti + iniş tipi yok: kızak köprüye fazla hızlı ve hizasız giriyordu.
                    int approach = track <= 1 ? brnd.Next(3) : 1 + brnd.Next(2);
                    ApproachLog += track + ":" + approach + "@" + Mathf.RoundToInt(p.z) + " ";
                    // Kısa yaklaşım yavaş parkurlarda 22 m; hızlı parkurlarda 32 m (22 m'de kızak hizalanamayıp düşüyordu).
                    float lead = approach == 2 ? (track <= 1 ? 22f : 32f) : BridgeLead;
                    float settle = track <= 1 ? 10f : 20f;   // köprüden önceki düz eğimli son kısım
                    if (track == 3) { lead = 60f; settle = 40f; } // süzülme/roket sonrası dar tablaya yerden ve hizalı giriş
                    p.Hills(14f, 1, 0f, (boostGrade + slowGrade) * 0.5f * 14f);
                    if (approach == 0)
                    {
                        float seg = (lead - 13f) / 3f, dive = BridgeDive[track];
                        p.Hills(13f, 1, 0f, -BridgeRise[track]);
                        p.Hills(seg, 1, 0f, dive * 0.35f * seg);
                        p.Hills(seg, 1, 0f, dive * 0.7f * seg);
                        p.Hills(seg, 1, 0f, dive * seg);
                    }
                    else if (approach == 1)
                    {
                        p.Hills(lead - settle, track <= 1 ? 2 : 1, BridgeWave[track], slowGrade * (lead - settle));
                        p.Hills(settle, 1, 0f, slowGrade * settle);
                    }
                    else p.Hills(lead, 1, 0f, slowGrade * lead);
                    p.Bridge(span, BridgeDepth, slowGrade);
                    lastBridge = p.z;
                    L = Mathf.Max(25f, L - 14f - lead - span);   // köprü yamacın parçası: döngünün enerji dengesi değişmesin
                }
                else if (kick)
                {
                    // Kanaldan doğrudan rampaya geçiş 2.5 g'lik sıkışma yapıp sütü bir anda boşaltıyordu: önce 10 m yamaç.
                    p.Hills(10f, 1, 0f, slowGrade * 10f);
                    if (track == 3)
                    {
                        float y0 = p.y;
                        p.SnowTakeoff(18f, -slowGrade, 0.26f, 1.2f);
                        kickH = p.y - y0 + 0.8f;
                        // Daha uzun giriş kalan yamaçtan alınır; döngünün mesafesi ve net düşüşü korunur.
                        L -= 10f;
                        snowDropCorrection = slowGrade * 10f;
                    }
                    else p.Ramp(8f, kickH, 0.8f);
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
                // Süzülme inişinde zorunlu şerit değişimi olmasın; buz/çamur diğer yamaçlarda kalır.
                p.mute = fork || (track == 3 && kick);
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
                int waves = Mathf.Max(1, Mathf.RoundToInt(L / WaveLength[track]));
                float amplitude = WaveAmp[track];
                if (track == 3)
                {
                    // Köprüden kalan kısa parçada tam boy tepe sıkışmasın: eğrilik dalga boyuyla korunur.
                    float spanScale = Mathf.Min(1f, L / waves / WaveLength[track]);
                    amplitude *= spanScale * spanScale;
                }
                p.Hills(L, waves, amplitude, slowGrade * L + Mathf.Max(0f, kickH - 0.8f) + snowDropCorrection, kick ? 1.4f : 1f);
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
            if (track == 3)
            {
                // İlk uçuş düşük seviyede de erişilebilir (dudak 44 m). Başlangıcın 116 m / -12 m
                // birleşimi korunur; uzun, karlı iniş çanağı kanadın açılmasına yer bırakır.
                var snow = new Path(-30f).Flat(26f).Hills(30f, 1, 0.6f, g * 30f);
                snow.SnowTakeoff(18f, -g, 0.22f, 1.2f);
                return snow.Hills(69f, 1, 2.6f, snow.y + g * 120f, 1.4f);
            }
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

        /// Orman zemini: patikanın iki yanına (ve seyrek olarak kenarına) ağaç ve kayalar. Patikanın kendisi, köprü
        /// yaklaşımları, çatallar ve rampa inişleri boş kalır.
        static void ScatterForest(TrackProfile t, int seed, Vector2[] clear = null)
        {
            var rnd = new System.Random(seed * 17 + 3);
            float R(float a, float b) => a + (b - a) * (float)rnd.NextDouble();
            var lips = new List<float>();   // rampa dudakları: eğim birden düşer
            for (float z = 0f; z < t.finishZ; z += 1f)
                if (t.Slope(z - 1f) > 0.08f && t.Slope(z + 2f) < -0.02f) lips.Add(z);
            float reach = t.halfWidth + TrackProfile.BankRim - 1.5f;
            for (float z = 110f; z < t.finishZ - 30f; z += R(3.5f, 7f))   // ilk 165 m boş: sapan hızıyla engele girilmesin
            {
                bool skip = false;
                foreach (var w in t.water) if (z > w.x - 80f && z < w.y + 20f) skip = true;
                foreach (var f in t.forks) if (z > f.z0 - 10f && z < f.z1 + 10f) skip = true;
                foreach (float lip in lips) if (z > lip - 12f && z < lip + 110f) skip = true;
                bool inClear = false;
                if (clear != null) foreach (var c in clear) if (z > c.x && z < c.y) inClear = true;
                if (skip || Mathf.Abs(t.Slope(z)) > 0.3f) continue;
                float side = rnd.Next(2) == 0 ? -1f : 1f;
                // Patika ortasına göre yanal konum: çoğu patikanın dışında, bazıları tam kenarında (dar geçit hissi)
                // Güzergâhın iki yanında ±5 m'lik koridor açık kalır (oradaki engeller aşağıda, seyrek ve sırayla gelir).
                // Koridorun hemen kenarındaki (yakın) engeller de yalnızca kızağın yavaş olduğu çıkış kısmında; inişte uzakta.
                float off = side * (rnd.NextDouble() < 0.25 && t.Slope(z) > -0.07f ? R(TrackProfile.PathHalfWidth + 3.3f, TrackProfile.PathHalfWidth + 4.5f)
                                                            : R(TrackProfile.PathHalfWidth + (t.Slope(z) > -0.07f ? 4.5f : 6.5f), reach));
                float x = t.PathX(z) + off;
                if (Mathf.Abs(x - t.CenterX(z)) > reach || inClear && Mathf.Abs(off) < 9f) continue;   // uçuş bölgesinde yalnız uzak ağaçlar
                // Güzergâha 7 m'den yakın yerde yalnız ince gövdeli ağaç; geniş kayalar daha dışarıda.
                if (Mathf.Abs(off) < 7f || rnd.NextDouble() < 0.6) t.AddTree(z, off);
                else t.AddRock(z, off);
            }
            // Güzergâhın üstünde de engeller (kullanıcı 2026-10-11: ağaçlar hep kenardaydı, yol dümdüzdü): 24-38 m'de bir,
            // sırayla sağa-sola kayık; arada kızağın geçeceği boşluk hep kalır (oyuncu aralarından kıvrılır).
            float turn = 1f;
            for (float z = 150f; z < t.finishZ - 40f; z += R(45f, 70f))
            {
                // Enerji kuralı: ağaç inişin dibine (kızağın en hızlı olduğu yer) konmaz; dalganın çıkış kısmına, eğimin
                // yatıklaştığı yere kaydırılır: kızak orada yavaşlar, kaçınmaya zamanı olur.
                for (int k = 0; k < 20 && t.Slope(z) < -0.07f; k++) z += 3f;
                bool skip = t.Slope(z) < -0.07f;
                foreach (var w in t.water) if (z > w.x - 85f && z < w.y + 25f) skip = true;
                foreach (var f in t.forks) if (z > f.z0 - 15f && z < f.z1 + 15f) skip = true;
                foreach (float lip in lips) if (z > lip - 15f && z < lip + 110f) skip = true;
                if (clear != null) foreach (var c in clear) if (z > c.x && z < c.y) skip = true;
                if (skip || Mathf.Abs(t.Slope(z)) > 0.3f) continue;
                float off = turn * R(1.9f, 2.6f);   // güzergâhın yarısını kapatır: öbür yandan ~1 m kayarak geçilir
                turn = -turn;
                t.AddTree(z, off);   // güzergâhta yalnız ince gövdeli ağaç (geniş kaya yolu kapatıyordu)
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

        /// Whispering Woods profili (kullanıcının çizimi, 2026-10-18): genel eğimin üstüne/altına göreli şekil (z, m).
        /// Baştaki çanak 2. çizimden, gerisi 1. çizimin sırası iki kez farklı boyda: büyük çukur, sahanlık, sivri zirve,
        /// dipte rampa (kızak en hızlı yerde kalkar: süzülme), basamaklar. Rampa iniş alanı aşağı eğimli: yükselen zemine
        /// inişte çarpma hızı kaza sınırını aşıyordu (profil hesabı: 12.6-13.7 m/s, şimdi ≤ 9).
        static readonly float[] WoodsZ =
        {
            0, 25, 80, 95, 140, 160, 195, 225, 250, 305, 320, 365, 400, 425, 433, 490, 505, 513, 516, 575, 620, 650, 675,
            710, 735, 741, 765, 790, 835, 860, 868, 915, 928, 936, 939, 1000, 1040, 1065, 1085, 1120, 1145, 1200,
        };
        static readonly float[] WoodsR =
        {
            0, 0, -9, -9.5f, 2, 3, 0, 3.5f, 3.5f, -9, -9.2f, 4, 5, 11, 11, -7, -9, -5.6f, -8.4f, -14, -4, -3.5f, -4.5f,
            0, 0.5f, -2.5f, -9, -11, -5, -1, -1, -10, -12, -8.6f, -11.4f, -16, -9, -9, -11, -8, -8.5f, -12,
        };
        /// Dipteki rampalar ve uçurum kenarı (z0: giriş, z1: dudak): yumuşatma bunları silmesin, vadi düz gitsin.
        /// Rampa mutlak olarak yükselir (8 m'de ~2 m, diğer parkurların rampası gibi): genel eğime yatırılınca çizimdeki
        /// küçük çentik düzleşiyor, kızak burnunu kaldırmadan geçiyordu.
        static readonly Vector2[] WoodsKickers = { new Vector2(505f, 513f), new Vector2(725f, 735f), new Vector2(928f, 936f) };
        /// Hızlı kızağın havalandığı tepeler ve rampalar: kalkıştan inişe kadar engel yok (havada kaçınılamaz).
        /// Simde son seviye rampalardan ~80 m, uçurum kenarından ~52 m süzülüyor; iniş noktasından sonra 20 m pay.
        static readonly Vector2[] WoodsClear =
        {
            new Vector2(130f, 295f), new Vector2(345f, 500f), new Vector2(500f, 610f), new Vector2(655f, 700f), new Vector2(715f, 800f), new Vector2(845f, 905f), new Vector2(918f, 1030f),
        };

        /// Whispering Woods dereleri (kullanıcı 2026-10-18): pisti enine kesen dere, üstünde tek ince köprü. Köprü vadinin
        /// ortasında değil: oyuncu geçmek için sola, sağa ya da ortaya kaymak zorunda. (orta z, açıklık, köprünün yeri)
        /// Yokuş çıkışlarında (içbükey, kızak yere basar ve yavaşlar): tepe/sahanlık kenarının arkasında hızlı kızak
        /// hafifçe havalanıp dereye havadan giriyordu; rampa inişine de denk gelmez.
        static readonly Vector3[] WoodsCreeks = { new Vector3(342f, 14f, 0f), new Vector3(692f, 14f, -3.5f), new Vector3(1022f, 14f, 3.5f) };
        /// Dere köprüsünün yarı genişliği (Kanyon'daki ince köprülerin 0.8'i yerine).
        public const float CreekBridgeHalfWidth = 1.3f;

        static Path WoodsDrawn()
        {
            var p = Drawn(1, WoodsZ, WoodsR, WoodsCreeks);
            foreach (var kk in WoodsKickers)
            {
                p.sharp.Add(new Vector2(kk.x - 4f, kk.y + 10f));
                p.straight.Add(new Vector2(kk.x - 15f, kk.y + 90f));
            }
            return p;
        }

        /// Çizimden pist: (zs, rs) göreli şekli genel eğime (SlowGrade → SlowGradeEnd, pervane telafili) yatırır.
        /// Şekil monoton kübik (PCHIP) eğriyle çizilir: tepe ve çukurlar çizimdeki yerinde kalır, aralarda taşma olmaz.
        /// creeks: (orta z, açıklık, köprünün vadi ortasına göre yeri): enine dere, kıyılar dik, üstünde ince köprü.
        static Path Drawn(int track, float[] zs, float[] rs, Vector3[] creeks)
        {
            float length = Lengths[track];
            int n = zs.Length;
            var d = new float[n];
            var h = new float[n - 1];
            var s = new float[n - 1];
            for (int k = 0; k < n - 1; k++) { h[k] = zs[k + 1] - zs[k]; s[k] = (rs[k + 1] - rs[k]) / h[k]; }
            for (int k = 1; k < n - 1; k++)
            {
                if (s[k - 1] * s[k] <= 0f) { d[k] = 0f; continue; }
                float w1 = 2f * h[k] + h[k - 1], w2 = h[k] + 2f * h[k - 1];
                d[k] = (w1 + w2) / (w1 / s[k - 1] + w2 / s[k]);
            }
            d[n - 1] = s[n - 2];
            float Rel(float z)
            {
                if (z <= zs[0]) return rs[0];
                if (z >= zs[n - 1]) return rs[n - 1];
                int k = 0;
                while (zs[k + 1] < z) k++;
                float t = (z - zs[k]) / h[k], t2 = t * t, t3 = t2 * t;
                return (2f * t3 - 3f * t2 + 1f) * rs[k] + (t3 - 2f * t2 + t) * h[k] * d[k]
                       + (-2f * t3 + 3f * t2) * rs[k + 1] + (t3 - t2) * h[k] * d[k + 1];
            }
            var p = new Path(-30f).Flat(26f);
            p.corners.Add(-4f);
            var grid = new List<Vector2>();
            float y0 = p.y, drop = 0f, zPrev = 0f;
            for (float z = 0f; z <= length + 60f; z += 2f)
            {
                float t = Mathf.Clamp01(z / length);
                drop += (Mathf.Lerp(SlowGrade[track], SlowGradeEnd[track], t) - PropGrade(track)) * (z - zPrev);
                zPrev = z;
                grid.Add(new Vector2(z, y0 - drop + Rel(Mathf.Min(z, length))));
            }
            int c = 0;
            for (int g = 0; g < grid.Count; g++)
            {
                if (creeks != null && c < creeks.Length && grid[g].x >= creeks[c].x - creeks[c].y * 0.5f)
                {
                    // Dere (Path.Bridge gibi): kıyıdan 1.5 m'de BridgeDepth iner, düz yatak (su), karşı kıyıya çıkar.
                    int g1 = g;
                    while (g1 < grid.Count - 1 && grid[g1].x < grid[g].x + creeks[c].y) g1++;
                    Vector2 a = grid[g], b = grid[g1];
                    float bed = Mathf.Min(a.y, b.y) - BridgeDepth;
                    p.points.Add(a);
                    p.points.Add(new Vector2(a.x + 1.5f, bed));
                    p.points.Add(new Vector2(b.x - 1.5f, bed));
                    p.points.Add(b);
                    // Su kıyının 1.2 m altında: yatakta (bed + 0.9) arkadan bakan kamerada karşı kıyının arkasında kalıp hiç
                    // görünmüyordu, dere çimenli hendek gibi duruyordu.
                    p.water.Add(new Vector3(a.x + 0.5f, b.x - 0.5f, Mathf.Min(a.y, b.y) - 1.2f));
                    p.bridges.Add(new Vector2(a.x, b.x));
                    p.sharp.Add(new Vector2(a.x - 3f, b.x + 3f));
                    p.straight.Add(new Vector2(a.x - 75f, b.x + 6f));
                    g = g1;
                    c++;
                    continue;
                }
                p.points.Add(grid[g]);
            }
            var last = p.points[p.points.Count - 1];
            p.z = last.x;
            p.y = last.y;
            return p;
        }

        /// Çizimli pistte köprüleri derenin yerine kaydırır (Build tablayı güzergâha koyar).
        static void PlaceCreekBridges(TrackProfile t, Vector3[] creeks, float halfWidth)
        {
            for (int k = 0; k < t.bridges.Count && k < creeks.Length; k++)
            {
                var b = t.bridges[k];
                b.x = t.CenterX((b.r0 + b.r1) * 0.5f) + creeks[k].z;
                b.hw = Mathf.Max(b.hw, halfWidth);
                t.bridges[k] = b;
            }
        }

        /// Parkur 2 — Orman (5 km). Basamaklar + her 5 basamakta bir vadi atlayışı: çukurlar giderek genişler,
        /// ilk vadiler küçük kanatla, sonuncular kızağın 4. seviyesinin sonundaki kanatla geçilir.
        public static TrackProfile Forest()
        {
            if (UseGlide)
            {
                var gp = WoodsDrawn();
                var gobs = new List<Vector2>();
                // Az kıvrım: yan yamaç yok, uzun süzülen kızak vadiden dışarı çıkmasın.
                var gt = gp.Build(Names[1], Lengths[1], 4.5f, 260f, 2.3f, 85f, 5);
                // Kullanıcı kararı (2026-10-10): Orman Çayır'dan farklı hissettirsin: yan yamaç yok (dümdüz açık orman),
                // zeminde rastgele ağaçlar ve kayalar; aralarından geçilir, çarpan düşer.
                gt.bankScale = 0f;
                gt.noPath = true;   // toprak yol yok: çimenli açık orman
                // Rampa önünde (görünmeyen) güzergâh vadinin ortasında ve düz: kızak dudağa açılı gelip süzülürken
                // vadiden yana çıkmasın.
                foreach (var kk in WoodsKickers) gt.centerZones.Add(new Vector2(kk.x - 60f, kk.y + 100f));
                PlaceCreekBridges(gt, WoodsCreeks, CreekBridgeHalfWidth);
                ScatterForest(gt, 33, WoodsClear);
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
        /// Sunset Canyon profili (kullanıcının 2. çizimi, 2026-10-18): basamaklar ve uçurumlar. Fırlatmadan sonra çanak,
        /// yuvarlak büyük tepe, keskin kenar ve aşağıda basamak, çukur, yatık uzun plato, yine keskin kenar, vadi, küçük
        /// tepe, uzun hafif iniş; çizim iki kez (ikincisi daha derin) tekrar eder. Kenardan uçan kızak aşağı eğimli
        /// yamaca iner (iniş yumuşak).
        static readonly float[] CanyonZ =
        {
            0, 25, 60, 75, 150, 180, 216, 224, 250, 262, 320, 365, 410, 440, 500, 508, 540, 595, 630, 700, 760, 775, 830, 860, 868, 895, 905, 960, 1000, 1045, 1080, 1130, 1138, 1175, 1230, 1265, 1340, 1400,
        };
        static readonly float[] CanyonR =
        {
            0, 0, -8, -8.3f, 2, 4, 4, 3.2f, -3, -3.5f, -4, -7, -2, -1.8f, -1, -1.8f, -10, -6, -8.5f, -7, -14, -14.3f, -9.5f, -9, -9.8f, -16, -16.5f, -17, -20, -16, -15.6f, -15, -15.8f, -24, -21, -23.5f, -23, -27,
        };
        /// Uçurum kenarları (yuvarlak tepenin bitişi, dudak): yumuşatma silmesin, iniş alanında vadi düz.
        static readonly Vector2[] CanyonEdges = { new Vector2(216f, 224f), new Vector2(500f, 508f), new Vector2(860f, 868f), new Vector2(1130f, 1138f) };
        /// Dar köprülü dereler: yokuş çıkışlarında (kızak yerde, yavaşlıyor), köprü patikada.
        static readonly Vector3[] CanyonCreeks = { new Vector3(392f, 14f, 0f), new Vector3(800f, 14f, 0f), new Vector3(1024f, 14f, 0f) };
        /// Çatallar yatık platolarda (z başı, tip, altınlı şerit): kenardan inişten ve köprüden uzakta.
        static readonly Vector3[] CanyonForks = { new Vector3(430f, 1f, 1f), new Vector3(1062f, 2f, -1f) };

        public static TrackProfile Canyon()
        {
            if (UseGlide)
            {
                var gp = Drawn(2, CanyonZ, CanyonR, CanyonCreeks);
                foreach (var e in CanyonEdges)
                {
                    gp.sharp.Add(new Vector2(e.x - 6f, e.y + 8f));
                    gp.straight.Add(new Vector2(e.x - 15f, e.y + 90f));
                }
                float end = gp.z;
                foreach (var f in CanyonForks)
                {
                    gp.z = f.x;
                    gp.Fork(0f, ForkLength, (int)f.y, (int)f.z, false, true);
                }
                gp.z = end;
                var gt = gp.Build(Names[2], Lengths[2], 7f, 240f, 2.2f, 90f, 7);
                // Kanyonun köprüsü dar kalır (ormandakinden dar), ama 0.8'de gecikmeli oyuncu yokuş çıkarken yavaşken bile
                // düşüyordu.
                PlaceCreekBridges(gt, CanyonCreeks, 1.05f);
                ScatterRocks(gt, 47, 22f);
                AddObstacles(gt, new List<Vector2>());
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
