using System.Text;
using UnityEditor;
using UnityEngine;

namespace SledSurfers.EditorTools
{
    /// Oyunu açmadan fizik ayarını doğrulamak için otomatik oyuncuyla simülasyon.
    /// Menü: Sled Surfers > Fizik Testleri  (sonuç Console'a yazılır)
    public static class SimTests
    {
        const float Dt = 1f / 240f;

        /// Komut satırı sayıları sistem dilinden bağımsız okunur (Türkçe Windows'ta "7.5" = 75 olmasın).
        static float ParseF(string v) => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture);

        enum GlassPlayer { Fixed, Skilled }

        // Seviye sırası: sapan, kızak, kanat, bardak, gelir, roket
        [MenuItem("Sled Surfers/Fizik Testleri")]
        public static void RunAll()
        {
            var sb = new StringBuilder("[SIM] Fizik testleri (sapan/kızak/kanat/bardak/gelir/roket)\n");
            sb.AppendLine("Parkur 1 — Çayır (800 m) · kademe sınırı 4/4/0/1/4/4");
            Run(sb, 0, "Başlangıç, bardak sabit", new[] { 0, 0, 0, 0, 0 }, 1f, GlassPlayer.Fixed);
            Run(sb, 0, "Başlangıç, 20° sağa nişan", new[] { 0, 0, 0, 0, 0 }, 1f, GlassPlayer.Skilled, false, 0.35f);
            Run(sb, 0, "Başlangıç", new[] { 0, 0, 0, 0, 0 }, 1f, GlassPlayer.Skilled);
            Run(sb, 0, "1/1/0/0/0/0", new[] { 2, 2, 0, 0, 0 }, 1f, GlassPlayer.Skilled);
            Run(sb, 0, "2/1/0/0/0/1", new[] { 4, 2, 0, 0, 2 }, 1f, GlassPlayer.Skilled);
            Run(sb, 0, "2/2/0/1/0/1", new[] { 4, 4, 4, 0, 2 }, 1f, GlassPlayer.Skilled);
            Run(sb, 0, "3/2/0/1/0/2", new[] { 6, 4, 4, 0, 4 }, 1f, GlassPlayer.Skilled);
            Run(sb, 0, "3/3/0/1/0/2", new[] { 6, 6, 4, 0, 4 }, 1f, GlassPlayer.Skilled);
            Run(sb, 0, "4/3/0/1/0/3", new[] { 8, 6, 4, 0, 6 }, 1f, GlassPlayer.Skilled);
            Run(sb, 0, "4/4/0/1/0/0 (roketsiz)", new[] { 8, 8, 4, 0, 0 }, 1f, GlassPlayer.Skilled);
            Run(sb, 0, "4/4/0/1/0/2", new[] { 8, 8, 4, 0, 4 }, 1f, GlassPlayer.Skilled);
            Run(sb, 0, "4/4/0/1/0/3", new[] { 8, 8, 4, 0, 6 }, 1f, GlassPlayer.Skilled);
            Run(sb, 0, "3/4/0/1/0/4", new[] { 6, 8, 4, 0, 8 }, 1f, GlassPlayer.Skilled);
            Run(sb, 0, "4/3/0/1/0/4", new[] { 8, 6, 4, 0, 8 }, 1f, GlassPlayer.Skilled);
            Run(sb, 0, "Kademe maks", new[] { 8, 8, 4, 8, 8 }, 1f, GlassPlayer.Skilled);
            Run(sb, 0, "Kademe maks, bardak sabit", new[] { 8, 8, 4, 8, 8 }, 1f, GlassPlayer.Fixed);

            sb.AppendLine("Parkur 2 — Orman (1500 m) · kademe sınırı 8/8/4/3/8/8");
            Run(sb, 1, "Çayır kademesi maks", new[] { 8, 8, 4, 8, 8 }, 1f, GlassPlayer.Skilled);
            Run(sb, 1, "8/8/2/3/8/8", new[] { 16, 16, 12, 16, 16 }, 1f, GlassPlayer.Skilled);
            Run(sb, 1, "8/8/3/3/8/8", new[] { 16, 16, 12, 16, 16 }, 1f, GlassPlayer.Skilled);
            Run(sb, 1, "8/8/4/3/8/6", new[] { 16, 16, 12, 16, 12 }, 1f, GlassPlayer.Skilled);
            Run(sb, 1, "8/7/4/3/8/8", new[] { 16, 16, 12, 16, 16 }, 1f, GlassPlayer.Skilled);
            Run(sb, 1, "8/6/4/3/8/8", new[] { 16, 16, 12, 16, 16 }, 1f, GlassPlayer.Skilled);
            Run(sb, 1, "7/8/4/3/8/8", new[] { 14, 16, 12, 16, 16 }, 1f, GlassPlayer.Skilled);
            Run(sb, 1, "6/8/4/3/8/8", new[] { 12, 16, 12, 16, 16 }, 1f, GlassPlayer.Skilled);
            Run(sb, 1, "8/8/4/2/8/8", new[] { 16, 16, 8, 16, 16 }, 1f, GlassPlayer.Skilled);
            Run(sb, 1, "Kademe maks", new[] { 16, 16, 12, 16, 16 }, 1f, GlassPlayer.Skilled);

            sb.AppendLine("Parkur 3 — Kanyon (2500 m) · kademe sınırı 12/12/8/4/12/12");
            Run(sb, 2, "Orman kademesi maks", new[] { 16, 16, 12, 16, 16 }, 1f, GlassPlayer.Skilled);
            Run(sb, 2, "12/12/6/4/12/12", new[] { 20, 20, 16, 20, 20 }, 1f, GlassPlayer.Skilled);
            Run(sb, 2, "12/12/7/4/12/12", new[] { 20, 20, 16, 20, 20 }, 1f, GlassPlayer.Skilled);
            Run(sb, 2, "12/12/8/3/12/12", new[] { 20, 20, 12, 20, 20 }, 1f, GlassPlayer.Skilled);
            Run(sb, 2, "12/12/8/4/12/10", new[] { 20, 20, 16, 20, 20 }, 1f, GlassPlayer.Skilled);
            Run(sb, 2, "10/12/8/4/12/12", new[] { 20, 20, 16, 20, 20 }, 1f, GlassPlayer.Skilled);
            Run(sb, 2, "12/11/8/4/12/12", new[] { 20, 20, 16, 20, 20 }, 1f, GlassPlayer.Skilled);
            Run(sb, 2, "Kademe maks", new[] { 20, 20, 16, 20, 20 }, 1f, GlassPlayer.Skilled);
            Debug.Log(sb.ToString());
        }

        [MenuItem("Sled Surfers/Fizik Teşhis")]
        public static void Diagnose()
        {
            var sb = new StringBuilder("[SIM] Teşhis\n");
            int track = 0;
            var levels = new[] { 8, 8, 4, 8, 8 };
            float[] range = null;
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("track=")) track = int.Parse(arg.Substring(6));
                if (arg.StartsWith("levels=")) levels = System.Array.ConvertAll(arg.Substring(7).Split(','), int.Parse);
                if (arg.StartsWith("range=")) range = System.Array.ConvertAll(arg.Substring(6).Split(','), ParseF);   // profil: z0,z1
            }
            if (range != null)
            {
                // Zemin profili: 2,5 m'de bir yükseklik ve eğim (derece)
                var tp = TrackLibrary.Get(track);
                for (float z = range[0]; z <= range[1]; z += 2.5f)
                    sb.AppendLine(string.Format("    profil z {0,7:F1}  y {1,7:F2}  eğim {2,5:F0}°", z, tp.Height(z), Mathf.Atan(tp.Slope(z)) * Mathf.Rad2Deg));
                foreach (var sz in tp.surfaces)
                    if (sz.cz > range[0] - 20f && sz.cz < range[1] + 20f)
                        sb.AppendLine(string.Format("    profil yüzey {0} z {1:F0} ({2:F0}-{3:F0})  x {4:F1} ±{5:F1}  patikada {6}", sz.type, sz.cz, sz.cz - sz.rz, sz.cz + sz.rz, sz.cx, sz.rx, sz.onPath));
            }
            Run(sb, track, string.Join("/", levels), levels, 1f, GlassPlayer.Skilled, true);
            foreach (var b in TrackLibrary.Get(track).bumps)
                sb.AppendLine(string.Format("    tümsek z {0:F0}-{1:F0}  h {2:F1}  x {3:F1} ±{4:F1}", b.z0, b.z0 + b.length, b.height, b.xc, b.halfWidth));
            Debug.Log(sb.ToString());
        }

        /// Oyuncu benzetimi: her atıştan sonra kazancı hesaplar (oyundaki formülle, coin toplama tahmini
        /// mesafeyle orantılı), parası yettikçe kademe sınırı içindeki en ucuz yükseltmeyi alır.
        /// Her parkurun kaç atışta bittiğini ve mesafenin atış atış nasıl arttığını yazar.
        [MenuItem("Sled Surfers/İlerleme Testi")]
        public static void Progression()
        {
            // Fiyat tablosu denemesi: base=a,b,c,d,e growth=... later=... (sapan/kızak/bardak/gelir/roket)
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("base=")) Upgrades.baseCosts = System.Array.ConvertAll(arg.Substring(5).Split(','), int.Parse);
                if (arg.StartsWith("growth=")) Upgrades.growth = System.Array.ConvertAll(arg.Substring(7).Split(','), ParseF);
                if (arg.StartsWith("later=")) Upgrades.laterGrowth = System.Array.ConvertAll(arg.Substring(6).Split(','), ParseF);
                if (arg.StartsWith("top=")) Upgrades.topGrowth = System.Array.ConvertAll(arg.Substring(4).Split(','), ParseF);
                if (arg.StartsWith("final=")) Upgrades.FinalMultiplier = ParseF(arg.Substring(6));
                if (arg.StartsWith("pay=")) TrackLibrary.PayFactors = System.Array.ConvertAll(arg.Substring(4).Split(','), ParseF);
            }
            var sb = new StringBuilder("[SIM] İlerleme (atış: mesafe · süt · seviyeler sapan/kızak/bardak/gelir/roket · para)\n");
            var levels = new int[Upgrades.Count];
            int money = 0, total = 0;
            for (int t = 0; t < TrackLibrary.Count; t++)
            {
                var track = TrackLibrary.Get(t);
                sb.AppendLine("Parkur " + (t + 1) + " — " + track.name);
                int runs = 0;
                bool finished = false;
                var line = new StringBuilder();
                while (!finished && runs < 250)
                {
                    runs++;
                    var r = Simulate(track, Upgrades.BuildConfig(levels));
                    finished = r.end == RunEnd.Finished;
                    float coins = 0.07f * r.distance;   // pistteki coinlerin yaklaşık yarısı toplanır
                    int earned = Mathf.RoundToInt((r.distance * (0.4f + 0.6f * r.fill) + coins * 2f + (finished ? 1000 * (t + 1) : 0))
                                                  * Upgrades.IncomeMultiplier(levels[(int)UpgradeType.Income]) * TrackLibrary.PayFactors[t]);
                    money += earned;
                    // En ucuz alınabilir yükseltmeyi tekrar tekrar al.
                    while (true)
                    {
                        int best = -1, bestCost = int.MaxValue;
                        for (int u = 0; u < Upgrades.Count; u++)
                        {
                            if (levels[u] >= Upgrades.Cap(u, t)) continue;
                            int c = Upgrades.Cost(u, levels[u]);
                            if (c < bestCost) { best = u; bestCost = c; }
                        }
                        if (best < 0 || bestCost > money) break;
                        money -= bestCost;
                        levels[best]++;
                    }
                    line.AppendFormat("  {0,3}: {1,5:F0} m %{2,3:F0} {3,-14} {4}  para {5}\n", runs, r.distance, r.fill * 100f,
                                      r.reason, string.Join("/", levels), money);
                }
                sb.Append(line);
                sb.AppendLine(string.Format("  => {0} atış{1}", runs, finished ? "" : " (bitmedi!)"));
                total += runs;
            }
            sb.AppendLine("Toplam " + total + " atış");
            Debug.Log(sb.ToString());
        }

        /// Hızlı merdiven: bir parkurda birkaç seviye setinin mesafesi (track=, sets=a/b/c/d/e;...).
        /// rh=çayırRoket,ormanRoket,kanyonRoket,kanyonNet,gapRun,gapMargin  ·  gaps=g:genişlik:derinlik,...
        /// (o parkurun vadi listesinde g. vadiyi ayarlar; derinlik eksi değil, metre) · sweep=g:d1,d2,... (g. vadinin
        /// derinliğini sırayla dener).
        [MenuItem("Sled Surfers/Merdiven Testi")]
        public static void Ladder()
        {
            int track = 0;
            string sets = "0/0/0/0/0;2/2/0/0/2;4/4/2/0/4;6/6/4/0/6;8/8/8/0/8;8/7/8/0/8;7/8/8/0/8;8/8/8/0/7";
            string sweep = null, mnets = null, gsweep = null, wsweep = null, gsSlow = null;
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("track=")) track = int.Parse(arg.Substring(6));
                if (arg.StartsWith("sets=")) sets = arg.Substring(5);
                if (arg.StartsWith("meadow="))   // çayır: net,araTepe,yokuşEğimi,tepeA,tepeB,zorluA,zorluB
                {
                    var v = System.Array.ConvertAll(arg.Substring(7).Split(','), ParseF);
                    TrackLibrary.MeadowNet = v[0];
                    if (v.Length > 1) TrackLibrary.MeadowRollers = v[1];
                    if (v.Length > 2) TrackLibrary.MeadowUpSlope = v[2];
                    if (v.Length > 4) TrackLibrary.MeadowRise = new Vector2(v[3], v[4]);
                    if (v.Length > 6) TrackLibrary.MeadowBig = new Vector2(v[5], v[6]);
                }
                if (arg.StartsWith("mnets=")) mnets = arg.Substring(6);   // çayır net değerlerini sırayla dener
                if (arg.StartsWith("grades="))   // pist eğimleri: çayır,orman,kanyon,karlı
                {
                    var v = System.Array.ConvertAll(arg.Substring(7).Split(','), ParseF);
                    TrackLibrary.MeadowGrade = v[0];
                    if (v.Length > 1) TrackLibrary.ForestGrade = v[1];
                    if (v.Length > 2) TrackLibrary.CanyonGrade = v[2];
                    if (v.Length > 3) TrackLibrary.SnowGrade = v[3];
                }
                if (arg.StartsWith("gsweep=")) gsweep = arg.Substring(7);   // bu pistin eğimini sırayla dener
                if (arg.StartsWith("glide="))   // bu pistin süzülme ayarı: yavaşEğim,yavaşUzA,yavaşUzB,kanalEğim,kanalUz
                {
                    var v = System.Array.ConvertAll(arg.Substring(6).Split(','), ParseF);
                    TrackLibrary.SlowGrade[track] = v[0];
                    if (v.Length > 1) TrackLibrary.SlowGradeEnd[track] = v[1];
                    if (v.Length > 3) TrackLibrary.SlowLength[track] = new Vector2(v[2], v[3]);
                    if (v.Length > 4) TrackLibrary.BoostGrade[track] = v[4];
                    if (v.Length > 5) TrackLibrary.BoostLength[track] = v[5];
                }
                if (arg.StartsWith("gsweepslow="))
                    gsSlow = arg.Substring(11);   // yavaşlama eğimini sırayla dener
                if (arg.StartsWith("open="))   // fırlatma koşusu: uzunluk,eğim
                {
                    var v = System.Array.ConvertAll(arg.Substring(5).Split(','), ParseF);
                    TrackLibrary.OpenLength = v[0];
                    if (v.Length > 1) TrackLibrary.OpenGrade = v[1];
                }
                if (arg.StartsWith("rh="))   // roket tepesi fazlalıkları: çayır,orman,kanyon
                {
                    var v = System.Array.ConvertAll(arg.Substring(3).Split(','), ParseF);
                    TrackLibrary.MeadowRocket = v[0];
                    if (v.Length > 1) TrackLibrary.ForestRocket = v[1];
                    if (v.Length > 2) TrackLibrary.CanyonRocket = v[2];
                    if (v.Length > 3) TrackLibrary.CanyonNet = v[3];
                    if (v.Length > 4) TrackLibrary.GapRun = v[4];
                    if (v.Length > 5) TrackLibrary.GapMargin = v[5];
                    if (v.Length > 6) TrackLibrary.CanyonGate = v[6];
                    if (v.Length > 7) TrackLibrary.CanyonGateLength = v[7];
                    if (v.Length > 8) TrackLibrary.MeadowGate = v[8];
                    if (v.Length > 9) TrackLibrary.MeadowBumpy = v[9];
                    if (v.Length > 10) TrackLibrary.MeadowNet = v[10];
                    if (v.Length > 11) TrackLibrary.ForestNet = v[11];
                }
                if (arg.StartsWith("gaps="))
                    foreach (var g in arg.Substring(5).Split(','))
                    {
                        var f = g.Split(':');
                        Gaps(track)[int.Parse(f[0])] = new Vector2(ParseF(f[1]), -ParseF(f[2]));
                    }
                if (arg.StartsWith("sweep=")) sweep = arg.Substring(6);
                if (arg.StartsWith("wsweep=")) wsweep = arg.Substring(7);   // g:genişlik1,genişlik2 (g. vadinin genişliği)
                if (arg.StartsWith("drag=")) SledConfig.DefaultDragArea = ParseF(arg.Substring(5));
                if (arg.StartsWith("snow="))   // karlı dağ: roket,net,kapı,kapıUzunluğu
                {
                    var v = System.Array.ConvertAll(arg.Substring(5).Split(','), ParseF);
                    TrackLibrary.SnowRocket = v[0];
                    if (v.Length > 1) TrackLibrary.SnowNet = v[1];
                    if (v.Length > 2) TrackLibrary.SnowGate = v[2];
                    if (v.Length > 3) TrackLibrary.SnowGateLength = v[3];
                    if (v.Length > 4) TrackLibrary.SnowGapMargin = v[4];
                }
            }
            var sb = new StringBuilder("[SIM] Merdiven " + TrackLibrary.Names[track] + "\n");
            if (gsSlow != null)
                foreach (var g in gsSlow.Split(','))
                {
                    var gg = g.Split('-');   // başEğim-sonEğim
                    TrackLibrary.SlowGrade[track] = ParseF(gg[0]);
                    if (gg.Length > 1) TrackLibrary.SlowGradeEnd[track] = ParseF(gg[1]);
                    sb.AppendLine(" yavaşlama eğimi " + g);
                    LadderRun(sb, track, sets);
                }
            else if (wsweep != null)
            {
                var f = wsweep.Split(':');
                int gi = int.Parse(f[0]);
                foreach (var w in f[1].Split(','))
                {
                    Gaps(track)[gi] = new Vector2(ParseF(w), Gaps(track)[gi].y);
                    sb.AppendLine(" vadi " + gi + " genişlik " + w);
                    LadderRun(sb, track, sets);
                }
            }
            else if (gsweep != null)
                foreach (var g in gsweep.Split(','))
                {
                    float v = ParseF(g);
                    if (track == 0) TrackLibrary.MeadowGrade = v;
                    else if (track == 1) TrackLibrary.ForestGrade = v;
                    else if (track == 2) TrackLibrary.CanyonGrade = v;
                    else TrackLibrary.SnowGrade = v;
                    sb.AppendLine(" eğim " + g);
                    LadderRun(sb, track, sets);
                }
            else if (mnets != null)
                foreach (var n in mnets.Split(','))
                {
                    TrackLibrary.MeadowNet = ParseF(n);
                    sb.AppendLine(" çayır net " + n);
                    LadderRun(sb, track, sets);
                }
            else if (sweep == null) LadderRun(sb, track, sets);
            else
            {
                var f = sweep.Split(':');
                int gi = int.Parse(f[0]);
                foreach (var d in f[1].Split(','))
                {
                    Gaps(track)[gi] = new Vector2(Gaps(track)[gi].x, -ParseF(d));
                    sb.AppendLine(" vadi " + gi + " derinlik " + d);
                    LadderRun(sb, track, sets);
                }
            }
            Debug.Log(sb.ToString());
        }

        /// Süzülme testi: dudaktan (6° yukarı, hız v) kalkan kızağın 15 m alçaktaki karşı kıyı hizasına inene kadar
        /// aldığı yatay yol, kızak seviyesine ve hıza göre (vadi genişliklerini ayarlamak için).
        [MenuItem("Sled Surfers/Süzülme Testi")]
        public static void GlideTest()
        {
            float depth = 15f;
            foreach (var arg in System.Environment.GetCommandLineArgs())
                if (arg.StartsWith("depth=")) depth = ParseF(arg.Substring(6));
            var sb = new StringBuilder("[SIM] Süzülme menzili (m): dudaktan " + depth + " m alçağa inene kadar" + System.Environment.NewLine);
            var pts = new[] { new Vector2(-50f, 0f), new Vector2(0f, 0f), new Vector2(2f, -60f), new Vector2(600f, -60f) };
            var track = new TrackProfile("Test", pts, 600f, 20f);
            track.water.Add(new Vector3(0f, 600f, -60f));   // dudaktan sonrası vadi: sığ süzülme kuralı
            sb.AppendLine("  kızak   16 m/s   20 m/s   24 m/s   28 m/s");
            for (int k = 4; k <= 20; k++)
            {
                var line = new StringBuilder(string.Format("  {0,4}  ", k));
                foreach (float v0 in new[] { 16f, 20f, 24f, 28f })
                {
                    var cfg = Upgrades.BuildConfig(new[] { 8, k, 8, 8, 0 });
                    var sled = new SledPhysics(track, cfg);
                    sled.position = new Vector3(0f, 0.3f, 0.5f);
                    float a = 6f * Mathf.Deg2Rad;
                    sled.velocity = new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a)) * v0;
                    sled.pitch = a;
                    sled.grounded = false;
                    sled.launching = false;
                    float t = 0f;
                    while (sled.position.y > -depth && t < 20f && sled.end == RunEnd.None)
                    {
                        sled.Step(0f, 0f, Dt);
                        t += Dt;
                    }
                    line.Append(string.Format("{0,7:F0}  ", sled.position.z));
                }
                sb.AppendLine(line.ToString());
            }
            Debug.Log(sb.ToString());
        }

        static Vector2[] Gaps(int track) => track == 3 ? TrackLibrary.SnowGaps : track == 2 ? TrackLibrary.CanyonGaps
                                          : track == 0 ? TrackLibrary.MeadowGaps : TrackLibrary.ForestGaps;

        static void LadderRun(StringBuilder sb, int track, string sets)
        {
            var t = TrackLibrary.Get(track);
            // Yokuş payı: pistin ne kadarı tırmanış (eğim > %2), en yüksek tırmanış ve basamak sayısı
            int upM = 0, all = 0;
            for (float z = 0f; z < t.finishZ; z += 1f, all++) if (t.Slope(z) > 0.02f) upM++;
            sb.AppendLine(string.Format("  yokuş %{0:F0}  ({1} m / {2} m)", 100f * upM / Mathf.Max(all, 1), upM, all));
            sb.AppendLine("  roket tepeleri: " + string.Join(", ", t.rocketHills.ConvertAll(z => z.ToString("F0")))
                          + "  ·  vadi/dere: " + string.Join(", ", t.water.ConvertAll(w => w.x.ToString("F0") + "-" + w.y.ToString("F0")))
                          + "  ·  köprü: " + t.bridges.Count);
            foreach (var set in sets.Split(';'))
            {
                // Sonda "-": aynı seviyeler kanatsız (vadinin kanatsız geçilemediğini sınamak için)
                bool noWing = set.EndsWith("-");
                var lv = System.Array.ConvertAll(set.TrimEnd('-').Split('/'), int.Parse);
                var cfg = Upgrades.BuildConfig(lv);
                if (noWing) { cfg.wingArea = 0f; cfg.wingMass = 0f; }
                var r = Simulate(t, cfg);
                sb.AppendLine(string.Format("  {0,-16} {1,6:F0} m  {2,4:F0} s  ort. {3,3:F0} km/sa  süt %{4,3:F0}  {5}", set, r.distance, r.time,
                                            r.distance / Mathf.Max(r.time, 0.1f) * 3.6f, r.fill * 100f, r.reason));
            }
        }

        /// Yamaç fiziği: kızak yamaca doğru fırlatılır, direksiyon yok. Yamaca tırmanıp yavaşlamalı, geri kayıp
        /// vadide salınmalı; mekanik enerji (½v² + g·y) hiç artmamalı; gövde yamacın eğimiyle yana yatmalı.
        [MenuItem("Sled Surfers/Yamaç Testi")]
        public static void BankTest()
        {
            var sb = new StringBuilder("[SIM] Yamaç testi\n");
            var track = TrackLibrary.Get(0);
            var cfg = Upgrades.BuildConfig(new[] { 8, 8, 4, 0, 0 });
            var sled = new SledPhysics(track, cfg);
            sled.PlaceForAim(1f, SledPhysics.MaxAim);
            sled.Launch();
            float t = 0f, lastE = float.MaxValue, worstRise = 0f, maxSide = 0f, maxRoll = 0f;
            while (sled.end == RunEnd.None && t < 40f)
            {
                sled.Step(0f, 0f, Dt);
                t += Dt;
                if (sled.launching) continue;
                var p = sled.position;
                float e = 0.5f * sled.velocity.sqrMagnitude + SledPhysics.Gravity * p.y;
                if (sled.grounded && sled.RocketFiring == false && lastE < float.MaxValue) worstRise = Mathf.Max(worstRise, e - lastE);
                lastE = e;
                float dx = p.x - track.CenterX(p.z);
                maxSide = Mathf.Max(maxSide, Mathf.Abs(dx));
                float roll = Vector3.SignedAngle(Vector3.up, sled.Orientation * Vector3.up, sled.Orientation * Vector3.forward);
                maxRoll = Mathf.Max(maxRoll, Mathf.Abs(roll));
                if (Mathf.Repeat(t, 0.5f) < Dt)
                {
                    var n = track.Normal(p.x, p.z);
                    float groundRoll = Mathf.Atan2(Vector3.Dot(n, sled.Orientation * Vector3.right), n.y) * Mathf.Rad2Deg;
                    sb.AppendLine(string.Format("  t={0,4:F1}  z {1,6:F1}  vadiye uzaklık {2,5:F1} m  yükseklik(taban üstü) {3,4:F1}  hız {4,4:F1}  yana yatış {5,4:F0}° (zemin {6,4:F0}°)  {7}",
                        t, p.z, dx, p.y - track.Height(p.z), sled.Speed, roll, -groundRoll, sled.grounded ? "yerde" : "havada"));
                }
            }
            sb.AppendLine(string.Format("  Sonuç: {0} m, {1}; en büyük yanal sapma {2:F1} m; en büyük yatış {3:F0}°; enerji artışı (olmamalı) {4:F3} J/kg",
                sled.maxDistance, sled.endReason, maxSide, maxRoll, worstRise));
            Debug.Log(sb.ToString());
        }

        /// Süt nerede dökülüyor: her dökülme anı sebebine göre sınıflanır (iniş darbesi, su/çamur, buz, viraj,
        /// zemin geçişi, havada). Bardağı hiç eğmeyen oyuncu ile usta oyuncu karşılaştırılır.
        /// track=, sets=a/b/c/d/e;... (bardağı hiç eğmeyen = klavyesiz PC oyuncusu, usta = otomatik pilot)
        [MenuItem("Sled Surfers/Döküm Analizi")]
        public static void Spill()
        {
            int track = 0;
            string sets = "4/4/0/0/4;6/6/2/0/6;8/8/4/0/8;8/8/8/0/8";
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("track=")) track = int.Parse(arg.Substring(6));
                if (arg.StartsWith("sets=")) sets = arg.Substring(5);
                if (arg.StartsWith("bumpy=")) TrackLibrary.MeadowBumpy = ParseF(arg.Substring(6));
            }
            var t = TrackLibrary.Get(track);
            var sb = new StringBuilder("[SIM] Döküm analizi " + TrackLibrary.Names[track] + "\n");
            foreach (var set in sets.Split(';'))
                for (int skilled = 0; skilled < 2; skilled++)
                {
                    var lv = System.Array.ConvertAll(set.Split('/'), int.Parse);
                    var cfg = Upgrades.BuildConfig(lv);
                    var sled = new SledPhysics(t, cfg);
                    var liquid = new LiquidSim();
                    liquid.Reset(cfg);
                    sled.PlaceForAim(1f);
                    sled.Launch();
                    var pilot = new AutoPilot();
                    var reflex = new GlassReflex();
                    var loss = new System.Collections.Generic.Dictionary<string, float>();
                    var worst = new System.Collections.Generic.List<(float z, float amount, string why)>();
                    float time = 0f, sinceLanding = 9f;
                    int landings = 0;
                    string lastWhy = null;
                    float eventStart = 0f, eventAmount = 0f;
                    while (sled.end == RunEnd.None && time < 600f)
                    {
                        if (skilled == 1) pilot.StepGlass(sled, Dt);
                        reflex.Step(sled, cfg.glassReflex, Dt);
                        if (AutoPilot.ShouldFire(t, sled, cfg)) sled.FireRocket();
                        sled.Step(AutoPilot.Steer(t, sled), AutoPilot.Pitch(t, sled, cfg), Dt);
                        if (sled.landings != landings) { landings = sled.landings; sinceLanding = 0f; }
                        sinceLanding += Dt;
                        float before = liquid.FillRatio;
                        liquid.lidClosed = sled.position.z < cfg.lidDistance;
                        var rot = skilled == 1 ? sled.Orientation * Quaternion.Euler(pilot.glassPitch, 0f, -pilot.glassRoll)
                                               : sled.Orientation * Quaternion.Euler(reflex.pitch, 0f, -reflex.roll);
                        liquid.Step(sled.acceleration, rot, Dt);
                        float lost = before - liquid.FillRatio;
                        if (lost > 0f)
                        {
                            var surf = t.SurfaceAt(sled.position.z, sled.position.x);
                            Vector3 local = Quaternion.Inverse(sled.Orientation) * sled.acceleration;
                            string why = !sled.grounded ? "havada"
                                       : sinceLanding < 0.6f ? "iniş darbesi"
                                       : surf == Surface.Puddle || surf == Surface.Mud ? "su/çamur freni"
                                       : surf == Surface.Ice ? "buz"
                                       : Mathf.Abs(local.x) > 4f ? "viraj/yamaç"
                                       : Mathf.Abs(local.z) > 3f ? "hızlanma/fren"
                                       : "zemin geçişi";
                            loss[why] = (loss.TryGetValue(why, out var l) ? l : 0f) + lost;
                            if (why != lastWhy || sled.position.z - eventStart > 15f)
                            {
                                if (lastWhy != null && eventAmount > 0.01f) worst.Add((eventStart, eventAmount, lastWhy));
                                lastWhy = why; eventStart = sled.position.z; eventAmount = 0f;
                            }
                            eventAmount += lost;
                        }
                        if (liquid.FillRatio < 0.12f) sled.ForceEnd(RunEnd.Spilled, "Bardak boşaldı!");
                        time += Dt;
                    }
                    if (lastWhy != null && eventAmount > 0.01f) worst.Add((eventStart, eventAmount, lastWhy));
                    sb.AppendLine(string.Format("  {0,-14} {1,-7} {2,6:F0} m  süt %{3,3:F0}  {4}", set, skilled == 1 ? "usta" : "refleks",
                                                sled.maxDistance, liquid.FillRatio * 100f, sled.endReason));
                    foreach (var kv in loss) sb.AppendLine(string.Format("      {0,-16} %{1,4:F0}", kv.Key, kv.Value * 100f));
                    worst.Sort((a, b) => b.amount.CompareTo(a.amount));
                    for (int i = 0; i < Mathf.Min(6, worst.Count); i++)
                        sb.AppendLine(string.Format("      en kötü: {0,6:F0} m  %{1,3:F0}  {2}", worst[i].z, worst[i].amount * 100f, worst[i].why));
                }
            Debug.Log(sb.ToString());
        }

        struct SimResult
        {
            public float distance, fill, time;
            public RunEnd end;
            public string reason;
        }

        /// İnce köprü testi: her pistin köprüleri; en iyi kızak ilk köprüye otomatik pilotla gelir, tablada
        /// direksiyonu sağa kırarsa (kısa / uzun) dereye düşmeli, hiç kırmazsa geçmeli.
        public static void BridgeTest()
        {
            var sb = new StringBuilder("[SIM] Köprü testi" + System.Environment.NewLine);
            for (int ti = 0; ti < TrackLibrary.Count; ti++)
            {
                var track = TrackLibrary.Get(ti);
                sb.AppendLine("  " + track.name + ": " + track.bridges.Count + " köprü  "
                              + string.Join(", ", track.bridges.ConvertAll(b => b.r0.ToString("F0") + "-" + b.r1.ToString("F0") + " (x " + b.x.ToString("F1") + ")")));
                if (track.bridges.Count == 0) continue;
                var br = track.bridges[0];
                foreach (float hold in new[] { 0f, 0.15f, 0.5f })
                {
                    var cfg = Upgrades.BuildConfig(new[] { 20, 20, 12, 20, 20 });
                    var sled = new SledPhysics(track, cfg);
                    sled.PlaceForAim(1f);
                    sled.Launch();
                    float t = 0f, held = 0f, maxOff = 0f;
                    while (sled.end == RunEnd.None && t < 600f && sled.position.z < br.z1 + 20f)
                    {
                        float steer = AutoPilot.Steer(track, sled);
                        if (sled.position.z > br.r0 + 2f && held < hold) { steer = 1f; held += Dt; }
                        sled.Step(steer, AutoPilot.Pitch(track, sled, cfg), Dt);
                        if (sled.position.z > br.r0 && sled.position.z < br.r1) maxOff = Mathf.Max(maxOff, Mathf.Abs(sled.position.x - br.x));
                        t += Dt;
                    }
                    sb.AppendLine(string.Format("    sağa {0:F2} s: z {1,6:F0}  tabladan en çok {2:F2} m  hız {3,4:F1}  {4}", hold, sled.position.z, maxOff,
                                                sled.Speed, sled.end == RunEnd.None ? "geçti" : sled.endReason));
                }
            }
            Debug.Log(sb.ToString());
        }

        static SimResult Simulate(TrackProfile track, SledConfig cfg)
        {
            var sled = new SledPhysics(track, cfg);
            var liquid = new LiquidSim();
            liquid.Reset(cfg);
            sled.PlaceForAim(1f);
            sled.Launch();
            var pilot = new AutoPilot();
            float t = 0f;
            while (sled.end == RunEnd.None && t < 600f)
            {
                pilot.StepGlass(sled, Dt);
                if (AutoPilot.ShouldFire(track, sled, cfg)) sled.FireRocket();
                sled.Step(AutoPilot.Steer(track, sled), AutoPilot.Pitch(track, sled, cfg), Dt);
                liquid.lidClosed = sled.position.z < cfg.lidDistance;
                liquid.Step(sled.acceleration, sled.Orientation * Quaternion.Euler(pilot.glassPitch, 0f, -pilot.glassRoll), Dt);
                if (liquid.FillRatio < 0.12f) sled.ForceEnd(RunEnd.Spilled, "Bardak boşaldı!");
                t += Dt;
            }
            return new SimResult { distance = sled.maxDistance, fill = liquid.FillRatio, end = sled.end, reason = sled.endReason, time = t };
        }

        static void Run(StringBuilder sb, int trackIndex, string label, int[] levels, float pull, GlassPlayer player,
                        bool trace = false, float aim = 0f)
        {
            var track = TrackLibrary.Get(trackIndex);
            var cfg = Upgrades.BuildConfig(levels);
            var sled = new SledPhysics(track, cfg);
            var liquid = new LiquidSim();
            liquid.Reset(cfg);
            sled.PlaceForAim(pull, aim);
            sled.Launch();

            var pilot = new AutoPilot();
            float t = 0f;
            float launchSpeed = -1f, fillAfterLaunch = -1f, airTime = 0f, longestAir = 0f;

            while (sled.end == RunEnd.None && t < 600f)
            {
                if (player == GlassPlayer.Skilled) pilot.StepGlass(sled, Dt);
                float glassPitch = pilot.glassPitch, glassRoll = pilot.glassRoll;

                bool wasGrounded = sled.grounded;
                float fillBefore = liquid.FillRatio;
                if (AutoPilot.ShouldFire(track, sled, cfg) && sled.FireRocket() && trace)
                    sb.AppendLine(string.Format("    {0,6:F1} m  ROKET  hız {1:F1}  {2}", sled.position.z, sled.Speed, sled.grounded ? "yerde" : "havada"));
                float steer = AutoPilot.Steer(track, sled);
                sled.Step(steer, AutoPilot.Pitch(track, sled, cfg), Dt);
                liquid.lidClosed = sled.position.z < cfg.lidDistance;
                liquid.Step(sled.acceleration, sled.Orientation * Quaternion.Euler(glassPitch, 0f, -glassRoll), Dt);
                if (trace && track.hasRocketPickup && Mathf.Abs(sled.position.z - track.rocketPickup.z) < 12f && Mathf.Repeat(t, 0.05f) < Dt)
                    sb.AppendLine(string.Format("    {0,6:F1} m  hak yakını: kızak x {1:F2} y {2:F2}  hak x {3:F2} y {4:F2}", sled.position.z,
                        sled.position.x, sled.position.y + 0.6f, track.rocketPickup.x, track.rocketPickup.y));
                if (trace && !sled.grounded && Mathf.Repeat(t, 0.25f) < Dt)
                    sb.AppendLine(string.Format("    {0,6:F1} m  dümen {1:F2}  vx {2:F1}", sled.position.z, steer, sled.velocity.x));
                if (trace)
                {
                    if (wasGrounded != sled.grounded)
                        sb.AppendLine(string.Format("    {0,6:F1} m  {1}  hız {2:F1}  ivme {3:F0}  pitch {4:F0}°  sıvı %{5:F0}",
                            sled.position.z, sled.grounded ? "İNİŞ  " : "HAVADA", sled.Speed,
                            sled.acceleration.magnitude, sled.pitch * Mathf.Rad2Deg, liquid.FillRatio * 100f));
                    if (Mathf.Repeat(t, 0.25f) < Dt)
                        sb.AppendLine(string.Format("    {0,6:F1} m  t={1:F0}s  {2}  hız {3:F1}  yükseklik {4:F1}  pitch {5:F0}°  yol açısı {6:F0}°  x {7:F1} (vadi {8:F1}, patika {9:F1}) {10} {11}",
                            sled.position.z, t, sled.grounded ? "yerde " : "havada", sled.Speed,
                            sled.position.y - track.Height(sled.position.x, sled.position.z), sled.pitch * Mathf.Rad2Deg,
                            Mathf.Atan2(sled.velocity.y, sled.velocity.z) * Mathf.Rad2Deg,
                            sled.position.x, track.CenterX(sled.position.z), track.PathX(sled.position.z),
                            track.SurfaceAt(sled.position.z, sled.position.x), track.OnPath(sled.position.z, sled.position.x) ? "patikada" : "çimende"));
                    if (fillBefore - liquid.FillRatio > 0.002f && Mathf.Repeat(t, 0.1f) < Dt)
                        sb.AppendLine(string.Format("    {0,6:F1} m  döküm  {1}  sıvı %{2:F0}  eğim {3:F2}  bardak p{4:F0} r{5:F0}  ivme ({6:F1},{7:F1},{8:F1})",
                            sled.position.z, sled.grounded ? "yerde " : "havada", liquid.FillRatio * 100f,
                            liquid.slope.magnitude, glassPitch, glassRoll,
                            sled.acceleration.x, sled.acceleration.y, sled.acceleration.z));
                }
                if (liquid.FillRatio < 0.12f) sled.ForceEnd(RunEnd.Spilled, "Bardak boşaldı!");

                if (!sled.launching && launchSpeed < 0f)
                {
                    launchSpeed = sled.Speed;
                    fillAfterLaunch = liquid.FillRatio;
                    if (aim != 0f)
                        sb.AppendLine(string.Format("    çıkış yönü {0:F0}° (nişan {1:F0}°), x={2:F2} m",
                            sled.heading * Mathf.Rad2Deg, aim * Mathf.Rad2Deg, sled.position.x));
                }
                if (!sled.grounded) { airTime += Dt; longestAir = Mathf.Max(longestAir, airTime); }
                else airTime = 0f;
                t += Dt;
            }

            if (track.hasRocketPickup)
                sb.AppendLine(string.Format("    havada roket hakkı: z {0:F0} x {1:F1} y {2:F1} (zemin {3:F1}) · {4}", track.rocketPickup.z,
                    track.rocketPickup.x, track.rocketPickup.y, track.Height(track.rocketPickup.x, track.rocketPickup.z),
                    sled.rocketPickupTaken ? "alındı" : "alınmadı"));
            sb.AppendLine(string.Format(
                "  {0,-30} {1,6:F0} m  {2,-22} çıkış {3,5:F1} m/s  maks {4,5:F1} m/s  sıvı: çıkışta %{5:F0}, sonda %{6:F0}  en uzun uçuş {7:F1} s",
                label, sled.maxDistance, sled.endReason, launchSpeed, sled.topSpeed,
                fillAfterLaunch * 100f, liquid.FillRatio * 100f, longestAir));
        }
    }
}
