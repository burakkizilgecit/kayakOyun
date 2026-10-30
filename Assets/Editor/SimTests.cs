using System.Text;
using UnityEditor;
using UnityEngine;

namespace SlingRiders.EditorTools
{
    /// Oyunu açmadan fizik ayarını doğrulamak için otomatik oyuncuyla simülasyon.
    /// Menü: Sling Riders > Fizik Testleri  (sonuç Console'a yazılır)
    public static class SimTests
    {
        const float Dt = 1f / 240f;

        /// Komut satırı sayıları sistem dilinden bağımsız okunur (Türkçe Windows'ta "7.5" = 75 olmasın).
        static float ParseF(string v) => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture);

        enum GlassPlayer { Fixed, Skilled }

        // Seviye sırası: sapan, kızak, kanat, bardak, gelir, roket
        [MenuItem("Sling Riders/Fizik Testleri")]
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

        [MenuItem("Sling Riders/Fizik Teşhis")]
        public static void Diagnose()
        {
            var sb = new StringBuilder("[SIM] Teşhis\n");
            int track = 0;
            var levels = new[] { 8, 8, 4, 8, 8 };
            float[] range = null;
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("track=")) track = int.Parse(arg.Substring(6));
                if (arg == "noboost") TrackLibrary.BoostsOn = false;
                if (arg == "chaseramps") AutoPilot.ChaseRamps = true;
                if (arg.StartsWith("boostkick=")) SledPhysics.BoostKick = ParseF(arg.Substring(10));
                if (arg.StartsWith("boostgap=")) TrackLibrary.BoostSpacing = ParseF(arg.Substring(9));
                if (arg == "nosidebridge") TrackLibrary.SideBridgesOn = false;
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

        static bool MilkSkillPay = true;   // "nomilkskill": süt becerisi ödülü olmadan (öncesiyle karşılaştırma)

        /// Oyuncu benzetimi: her atıştan sonra kazancı hesaplar (gerçek yerleşimden sonlu altın stoku,
        /// tahmini toplama şansı), parası yettikçe kademe sınırı içindeki en ucuz yükseltmeyi alır.
        /// Her parkurun kaç atışta bittiğini ve mesafenin atış atış nasıl arttığını yazar.
        [MenuItem("Sling Riders/İlerleme Testi")]
        public static void Progression()
        {
            // Fiyat tablosu denemesi: base=a,b,c,d,e growth=... later=... (sapan/kızak/bardak/gelir/roket)
            // player=usta|ortalama|egmeyen  gems=1 (coin yetmezse elmasla da alır)
            var kind = PlayerKind.Usta;
            bool gems = false;
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg == "noboost") TrackLibrary.BoostsOn = false;
                if (arg == "chaseramps") AutoPilot.ChaseRamps = true;
                if (arg.StartsWith("boostkick=")) SledPhysics.BoostKick = ParseF(arg.Substring(10));
                if (arg.StartsWith("boostgap=")) TrackLibrary.BoostSpacing = ParseF(arg.Substring(9));
                if (arg == "nosidebridge") TrackLibrary.SideBridgesOn = false;
                if (arg == "nomilkskill") MilkSkillPay = false;   // süt becerisi ödülü öncesiyle karşılaştırma
                if (arg.StartsWith("base=")) Upgrades.baseCosts = System.Array.ConvertAll(arg.Substring(5).Split(','), int.Parse);
                if (arg.StartsWith("growth=")) Upgrades.growth = System.Array.ConvertAll(arg.Substring(7).Split(','), ParseF);
                if (arg.StartsWith("scale=")) TrackLibrary.CostScale = System.Array.ConvertAll(arg.Substring(6).Split(','), ParseF);
                if (arg.StartsWith("pstart=")) TrackLibrary.PowerStart = System.Array.ConvertAll(arg.Substring(7).Split(','), ParseF);
                if (arg.StartsWith("pend=")) TrackLibrary.PowerEnd = System.Array.ConvertAll(arg.Substring(5).Split(','), ParseF);
                if (arg.StartsWith("pay=")) TrackLibrary.PayFactors = System.Array.ConvertAll(arg.Substring(4).Split(','), ParseF);
                if (arg.StartsWith("player=")) kind = SimPlayer.Parse(arg.Substring(7));
                if (arg.StartsWith("gems=")) gems = arg.Substring(5) == "1";
                if (arg.StartsWith("tracks="))   // yalnız bu parkurlar: ilk:son (ör. tracks=1:1 yalnız Orman; para sıfırdan başlar)
                {
                    var v = arg.Substring(7).Split(':');
                    ecoFirst = int.Parse(v[0]);
                    ecoLast = v.Length > 1 ? int.Parse(v[1]) : ecoFirst;
                }
            }
            var sb = new StringBuilder("[SIM] İlerleme (" + SimPlayer.Name(kind) + (gems ? ", elmasla alım" : "") +
                                       ") (atış: mesafe · süt · seviyeler sapan/kızak/bardak/gelir/roket · para · elmas)\n");
            sb.AppendLine("Aynı gün, sonlu pist envanteri. Toplama rota yerine olasılıkla modellenir; görev/giriş ödülleri ve günlük yenilenme dahil değildir.");
            var eco = RunEconomy(kind, gems, sb);
            sb.AppendLine(eco.Summary());
            Debug.Log(sb.ToString());
        }

        /// Ekonomi simi sonucu: pist başına atış, ilerlemeyen atış, en uzun duraklama, yeni piste girişte ulaşılan oran.
        class EconomyResult
        {
            public string label;
            public int[] runs = new int[TrackLibrary.Count], stalled = new int[TrackLibrary.Count], longest = new int[TrackLibrary.Count];
            public float[] arrival = new float[TrackLibrary.Count], milkLoss = new float[TrackLibrary.Count];
            public int gemsEarned, gemsSpent, gemBuys;
            public bool[] finished = new bool[TrackLibrary.Count];
            public string[] endLevels = new string[TrackLibrary.Count];
            public readonly float[] surfaceLoss = new float[(int)LossSurface.Count];
            public readonly float[] milkByCause = new float[(int)SpillCause.Count];
            public int Total { get { int s = 0; foreach (int r in runs) s += r; return s; } }
            public int Stalled { get { int s = 0; foreach (int r in stalled) s += r; return s; } }

            public string Summary()
            {
                var sb = new StringBuilder();
                for (int t = 0; t < TrackLibrary.Count; t++)
                    sb.AppendLine(string.Format("  {0,-13} {1,3} atış{2}  ilerlemeyen {3,3}  en uzun duraklama {4,2}  ilk atış %{5,3:F0}  süt kaybı %{6,4:F1}  bitişte {7}",
                        TrackLibrary.Names[t], runs[t], finished[t] ? " " : "!", stalled[t], longest[t], arrival[t] * 100f, milkLoss[t] * 100f, endLevels[t]));
                sb.AppendLine(string.Format("  Toplam {0} atış · ilerlemeyen %{1:F0} · elmas kazanılan {2}, harcanan {3} ({4} kademe)",
                    Total, 100f * Stalled / Mathf.Max(Total, 1), gemsEarned, gemsSpent, gemBuys));
                return sb.ToString();
            }
        }

        /// One simulated day: runtime pickup positions, finite stock, approximate route/pickup probability.
        /// Login rewards, quests and next-day respawns require a session policy and are excluded explicitly.
        static int ecoFirst = 0, ecoLast = TrackLibrary.Count - 1;

        sealed class PickupStock
        {
            readonly System.Collections.Generic.IList<float> positions;
            readonly bool[] taken, seen;
            public int Taken { get; private set; }

            public PickupStock(System.Collections.Generic.IList<float> positions)
            {
                this.positions = positions;
                taken = new bool[positions.Count];
                seen = new bool[positions.Count];
            }

            public int Collect(float distance, System.Random random, float firstChance, float repeatChance)
            {
                int count = 0;
                for (int i = 0; i < positions.Count; i++)
                {
                    if (taken[i] || positions[i] > distance) continue;
                    float chance = seen[i] ? repeatChance : firstChance;
                    seen[i] = true;
                    if (random.NextDouble() >= chance) continue;
                    taken[i] = true;
                    count++;
                }
                Taken += count;
                return count;
            }
        }

        static EconomyResult RunEconomy(PlayerKind kind, bool useGems, StringBuilder log)
        {
            var res = new EconomyResult { label = SimPlayer.Name(kind) + (useGems ? " + elmas" : "") };
            int[] levels;
            int money = 0, gems = 0, seed = 0;
            var rnd = new System.Random(17 + (int)kind);
            float pickChance = kind == PlayerKind.Usta ? 1f : 0.75f;
            for (int t = ecoFirst; t <= Mathf.Min(ecoLast, TrackLibrary.Count - 1); t++)
            {
                var track = TrackLibrary.Get(t);   // yükseltme bağlamı bu pist
                levels = new int[Upgrades.Count];   // yükseltmeler pist başına: yeni pistte sıfırdan
                money = 0;                          // para da parkur başına (elmas ortak)
                var coins = new PickupStock(Collectibles.CoinZs(track));
                var gemStock = new PickupStock(Collectibles.GemZs(track));
                log?.AppendLine("Parkur " + (t + 1) + " — " + track.name + " (" + Upgrades.Max + " kademe)");
                int runs = 0, streak = 0;
                float best = 0f, milk = 0f;
                bool finished = false;
                while (!finished && runs < 250)
                {
                    runs++;
                    var r = Simulate(track, Upgrades.BuildConfig(levels), kind, ++seed);
                    finished = r.end == RunEnd.Finished;
                    if (runs == 1) res.arrival[t] = r.distance / track.finishZ;
                    if (r.distance > best * 1.01f) { best = r.distance; streak = 0; }
                    else { res.stalled[t]++; streak++; res.longest[t] = Mathf.Max(res.longest[t], streak); }
                    milk += 1f - r.fill;
                    for (int i = 0; i < res.surfaceLoss.Length; i++) res.surfaceLoss[i] += r.stats.lostMeters[i];
                    for (int i = 0; i < res.milkByCause.Length; i++) res.milkByCause[i] += r.stats.milkLost[i];
                    // Only uncollected items can pay again on a later attempt. The same runtime
                    // layout includes forks, surface rewards and jump pickups as well as ordinary coins.
                    int coinCount = coins.Collect(r.distance, rnd, 0.5f * pickChance, 0.15f * pickChance);
                    float milkFactor = 0.4f + 0.6f * Mathf.Clamp01(r.fill);
                    // Süt becerisi oyundakiyle aynı: ek metre + kıl payları (GameController.EndRun, CloseCallMeters 15)
                    int earned = Mathf.RoundToInt((Mathf.FloorToInt(r.distance) * milkFactor + (finished ? 1000 * (t + 1) : 0)
                                                   + (MilkSkillPay ? (r.bonusMeters + r.closeCalls * 15f) * milkFactor : 0f))
                                                  * Upgrades.IncomeMultiplier(levels[(int)UpgradeType.Income]) * TrackLibrary.PayFactors[t])
                                 + coinCount * Collectibles.CoinValue;
                    money += earned;
                    int gotGems = gemStock.Collect(r.distance, rnd, pickChance, pickChance);
                    gems += gotGems;
                    res.gemsEarned += gotGems;
                    // En ucuz alınabilir yükseltmeyi tekrar tekrar al; coin yetmezse (istenirse) elmasla.
                    while (true)
                    {
                        int pick = -1, bestCost = int.MaxValue;
                        for (int u = 0; u < Upgrades.Count; u++)
                        {
                            if (levels[u] >= Upgrades.MaxLevel(u)) continue;
                            int c = Upgrades.Cost(u, levels[u]);
                            if (c < bestCost) { pick = u; bestCost = c; }
                        }
                        if (pick < 0) break;
                        if (bestCost <= money) { money -= bestCost; levels[pick]++; continue; }
                        int gc = Upgrades.GemCost(pick, levels[pick]);
                        if (useGems && gc <= gems) { gems -= gc; res.gemsSpent += gc; res.gemBuys++; levels[pick]++; continue; }
                        break;
                    }
                    log?.AppendFormat("  {0,3}: {1,5:F0} m %{2,3:F0} {3,-14} {4}  para {5}  elmas {6}\n", runs, r.distance, r.fill * 100f,
                                      r.reason, string.Join("/", levels), money, gems);
                }
                res.runs[t] = runs;
                res.finished[t] = finished;
                res.endLevels[t] = string.Join("/", levels) + " (en çok " + Upgrades.Max + ")";
                res.milkLoss[t] = milk / Mathf.Max(runs, 1);
                log?.AppendLine(string.Format("  => {0} atış{1}", runs, finished ? "" : " (bitmedi!)"));
            }
            return res;
        }

        /// Hızlı merdiven: bir parkurda birkaç seviye setinin mesafesi (track=, sets=a/b/c/d/e;...).
        /// rh=çayırRoket,ormanRoket,kanyonRoket,kanyonNet,gapRun,gapMargin  ·  gaps=g:genişlik:derinlik,...
        /// (o parkurun vadi listesinde g. vadiyi ayarlar; derinlik eksi değil, metre) · sweep=g:d1,d2,... (g. vadinin
        /// derinliğini sırayla dener).
        [MenuItem("Sling Riders/Merdiven Testi")]
        public static void Ladder()
        {
            int track = 0;
            string sets = "0/0/0/0/0;2/2/0/0/2;4/4/2/0/4;6/6/4/0/6;8/8/8/0/8;8/7/8/0/8;7/8/8/0/8;8/8/8/0/7";
            string sweep = null, mnets = null, gsweep = null, wsweep = null, gsSlow = null;
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("track=")) track = int.Parse(arg.Substring(6));
                if (arg == "noboost") TrackLibrary.BoostsOn = false;
                if (arg == "chaseramps") AutoPilot.ChaseRamps = true;
                if (arg.StartsWith("boostkick=")) SledPhysics.BoostKick = ParseF(arg.Substring(10));
                if (arg.StartsWith("boostgap=")) TrackLibrary.BoostSpacing = ParseF(arg.Substring(9));
                if (arg == "nosidebridge") TrackLibrary.SideBridgesOn = false;
                if (arg.StartsWith("sets=")) sets = arg.Substring(5);
                if (arg.StartsWith("player=")) ladderKind = SimPlayer.Parse(arg.Substring(7));
                if (arg.StartsWith("seeds=")) ladderSeeds = int.Parse(arg.Substring(6));
                if (arg == "forks=0") System.Array.Clear(TrackLibrary.ForkChance, 0, TrackLibrary.ForkChance.Length);
                if (arg.StartsWith("wall=")) TrackLibrary.WallRise[track] = ParseF(arg.Substring(5));
                if (arg.StartsWith("drag="))   // kızak kartının hava direnci çarpanı: baş,son
                {
                    var v = System.Array.ConvertAll(arg.Substring(5).Split(','), ParseF);
                    TrackLibrary.DragStart[track] = v[0];
                    TrackLibrary.DragEnd[track] = v[1];
                }
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
                    var gg = g.Split(':');   // başEğim:sonEğim (eksi eğim yokuş yukarı)
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
        [MenuItem("Sling Riders/Süzülme Testi")]
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

        static PlayerKind ladderKind = PlayerKind.Usta;
        static int ladderSeeds = 1;

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
                if (ladderSeeds > 1 || ladderKind != PlayerKind.Usta)
                {
                    // Gürültülü oyuncu: tohum ortalaması, en kötü-en iyi ve bitirme oranı
                    float sum = 0f, lo = float.MaxValue, hi = 0f, milk = 0f;
                    int fin = 0;
                    var reasons = new System.Collections.Generic.Dictionary<string, int>();
                    for (int sd = 1; sd <= ladderSeeds; sd++)
                    {
                        var rr = Simulate(t, cfg, ladderKind, sd * 97 + track);
                        sum += rr.distance; lo = Mathf.Min(lo, rr.distance); hi = Mathf.Max(hi, rr.distance); milk += 1f - rr.fill;
                        if (rr.end == RunEnd.Finished) fin++;
                        reasons[rr.reason] = (reasons.TryGetValue(rr.reason, out int c) ? c : 0) + 1;
                    }
                    var rs = new System.Collections.Generic.List<string>();
                    foreach (var kv in reasons) rs.Add(kv.Key + " " + kv.Value);
                    sb.AppendLine(string.Format("  {0,-16} {1,6:F0} m ({2:F0}–{3:F0})  biten {4}/{5}  süt kaybı %{6:F0}  {7}", set, sum / ladderSeeds, lo, hi,
                                                fin, ladderSeeds, 100f * milk / ladderSeeds, string.Join(", ", rs)));
                    continue;
                }
                var r = Simulate(t, cfg);
                sb.AppendLine(string.Format("  {0,-16} {1,6:F0} m  {2,4:F0} s  ort. {3,3:F0} km/sa  süt %{4,3:F0}  {5}", set, r.distance, r.time,
                                            r.distance / Mathf.Max(r.time, 0.1f) * 3.6f, r.fill * 100f, r.reason));
            }
        }

        /// Yamaç fiziği: kızak yamaca doğru fırlatılır, direksiyon yok. Yamaca tırmanıp yavaşlamalı, geri kayıp
        /// vadide salınmalı; mekanik enerji (½v² + g·y) hiç artmamalı; gövde yamacın eğimiyle yana yatmalı.
        [MenuItem("Sling Riders/Yamaç Testi")]
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
        static float traceA = -1f, traceB = -2f;

        /// Pist profilleri (görselleştirme için): her pist için 1 m aralıkla yükseklik ve eğim, ayrıca köprü, dere, çatal,
        /// yüzey ve engel listeleri. Tools/out/profiles/track_N.csv
        public static void Profiles()
        {
            string dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", "SledRunner", "Tools", "out", "profiles");
            foreach (var arg in System.Environment.GetCommandLineArgs())
                if (arg.StartsWith("out=")) dir = arg.Substring(4);
            System.IO.Directory.CreateDirectory(dir);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            for (int t = 0; t < TrackLibrary.Count; t++)
            {
                var tr = TrackLibrary.Get(t);
                var sb = new StringBuilder("kind,a,b,c,d" + System.Environment.NewLine);
                for (float z = -30f; z <= tr.finishZ + 40f; z += 1f)
                    sb.AppendLine(string.Format(inv, "p,{0:F1},{1:F3},{2:F4},{3:F3}", z, tr.Height(z), tr.Slope(z), tr.Curvature(z)));
                foreach (var b in tr.bridges) sb.AppendLine(string.Format(inv, "bridge,{0:F1},{1:F1},{2},{3:F2}", b.r0, b.r1, b.shape, b.bend));
                foreach (var w in tr.water) if (!tr.BridgeAt((w.x + w.y) * 0.5f, out _)) sb.AppendLine(string.Format(inv, "gap,{0:F1},{1:F1},0,0", w.x, w.y));
                foreach (var f in tr.forks) sb.AppendLine(string.Format(inv, "fork,{0:F1},{1:F1},{2},0", f.z0, f.z1, f.good));
                foreach (var sz in tr.surfaces) sb.AppendLine(string.Format(inv, "surf,{0:F1},{1:F1},{2},0", sz.z0, sz.z1, (int)sz.type));
                foreach (var o in tr.obstacles) sb.AppendLine(string.Format(inv, "obst,{0:F1},{1:F2},{2},0", o.z, o.x - tr.PathX(o.z), o.variant));
                foreach (var h in tr.rocketHills) sb.AppendLine(string.Format(inv, "rocket,{0:F1},0,0,0", h));
                foreach (var u in tr.updrafts) sb.AppendLine(string.Format(inv, "updraft,{0:F1},{1:F1},{2:F1},0", u.x, u.y, u.z));
                foreach (var f in tr.flocks) sb.AppendLine(string.Format(inv, "flock,{0:F1},{1:F1},{2:F1},0", f.z, f.y - tr.Height(f.z), f.sway));
                sb.AppendLine(string.Format(inv, "finish,{0:F1},0,0,0", tr.finishZ));
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "track_" + t + ".csv"), sb.ToString());
            }
            Debug.Log("[SIM] Profiller yazıldı: " + dir);
        }

        static int spillAverage;

        [MenuItem("Sling Riders/Döküm Analizi")]
        public static void Spill()
        {
            int track = 0;
            string sets = "4/4/0/0/4;6/6/2/0/6;8/8/4/0/8;8/8/8/0/8";
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("track=")) track = int.Parse(arg.Substring(6));
                if (arg == "noboost") TrackLibrary.BoostsOn = false;
                if (arg == "chaseramps") AutoPilot.ChaseRamps = true;
                if (arg.StartsWith("boostkick=")) SledPhysics.BoostKick = ParseF(arg.Substring(10));
                if (arg.StartsWith("boostgap=")) TrackLibrary.BoostSpacing = ParseF(arg.Substring(9));
                if (arg == "nosidebridge") TrackLibrary.SideBridgesOn = false;
                if (arg.StartsWith("sets=")) sets = arg.Substring(5);
                if (arg.StartsWith("bumpy=")) TrackLibrary.MeadowBumpy = ParseF(arg.Substring(6));
                if (arg.StartsWith("average=")) spillAverage = int.Parse(arg.Substring(8));   // ortalama oyuncu tohum sayısı
                if (arg.StartsWith("trace="))
                {
                    var r = arg.Substring(6).Split('-');
                    traceA = ParseF(r[0]); traceB = ParseF(r[1]);
                }
            }
            var t = TrackLibrary.Get(track);
            var sb = new StringBuilder("[SIM] Döküm analizi " + TrackLibrary.Names[track] + "\n");
            sb.AppendLine("  hızlandırıcılar z: " + string.Join(", ", t.boosts.ConvertAll(b => b.z0.ToString("F0"))));
            foreach (var set in sets.Split(';'))
                for (int skilled = 0; skilled < 2 + spillAverage; skilled++)
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
                    // skilled >= 2: ortalama oyuncu (gecikmeli/titrek direksiyon, bardağı yarım düzeltir), farklı tohumlar
                    var avg = skilled >= 2 ? new SimPlayer(PlayerKind.Ortalama, skilled * 13) : null;
                    Vector2 avgGlass = Vector2.zero;
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
                        if (avg != null)
                        {
                            avgGlass = avg.Glass(sled, cfg, Dt);
                            if (avg.Fire(t, sled, cfg)) sled.FireRocket();
                            sled.Step(avg.Steer(t, sled, Dt), avg.Pitch(t, sled, cfg), Dt);
                        }
                        else
                        {
                            if (AutoPilot.ShouldFire(t, sled, cfg)) sled.FireRocket();
                            sled.Step(AutoPilot.Steer(t, sled), AutoPilot.Pitch(t, sled, cfg), Dt);
                        }
                        if (sled.landings != landings) { landings = sled.landings; sinceLanding = 0f; }
                        sinceLanding += Dt;
                        float before = liquid.FillRatio;
                        liquid.lidClosed = sled.position.z < cfg.lidDistance;
                        var rot = avg != null ? SimPlayer.GlassRotation(sled, avgGlass)
                                : skilled == 1 ? sled.Orientation * Quaternion.Euler(pilot.glassPitch, 0f, -pilot.glassRoll)
                                               : sled.Orientation * Quaternion.Euler(reflex.pitch, 0f, -reflex.roll);
                        liquid.Step(sled.acceleration, rot, Dt);
                        float lost = before - liquid.FillRatio;
                        float tz = sled.position.z;
                        if (tz >= traceA && tz <= traceB && Mathf.Repeat(time, 0.1f) < Dt)
                        {
                            var e = sled.Orientation.eulerAngles;
                            sb.AppendLine(string.Format("      iz z {0:0.0} x {1:0.0} yer {2:0.0} {3} v {4:0.0} vy {5:0.0} ivme {6} yön {7:0} {8:0} {9:0} süt %{10:0.0}",
                                tz, sled.position.x, sled.position.y - t.Height(sled.position.x, tz), sled.grounded ? "Y" : "H", sled.Speed,
                                sled.velocity.y, sled.acceleration.ToString("0.0"), e.x, e.y, e.z, liquid.FillRatio * 100f));
                        }
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
                    sb.AppendLine(string.Format("  {0,-14} {1,-7} {2,6:F0} m  süt %{3,3:F0}  {4}", set, skilled >= 2 ? "ort" + (skilled - 1) : skilled == 1 ? "usta" : "refleks",
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
            public float distance, fill, time, topSpeed;
            public float bonusMeters, bestMult;   // süt becerisi (MilkStreak)
            public int closeCalls, breaks;
            public RunEnd end;
            public string reason;
            public RunStats stats;
        }

        /// Bounded regression entry point for Editor or batch mode. No PlayerPrefs, scene loading or assets written.
        [MenuItem("Sling Riders/Quick Regression")]
        public static void SmokeRegression()
        {
            int oldTrack = Upgrades.Track, oldParts = Upgrades.PartTier;
            float oldGravity = SledPhysics.Gravity;
            int checks = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var log = new StringBuilder("[REGRESSION] Bounded smoke: 12 simulated seconds at start/mid/max, every track\n");
            void Check(bool ok, string message)
            {
                if (!ok) throw new System.InvalidOperationException("[REGRESSION] " + message);
                checks++;
            }
            void Budget()
            {
                if (watch.Elapsed.TotalSeconds > 90)
                    throw new System.TimeoutException("[REGRESSION] 90 second budget exceeded; inspect the last reported track.");
            }
            bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
            try
            {
                Upgrades.PartTier = -1;
                var flat = new TrackProfile("Regression flat", new[] { new Vector2(-20f, 0f), new Vector2(1000f, 0f) }, 900f, 10f);
                var cfg = new SledConfig();

                // Exercise actual landing physics, then verify tip selection does not depend on display language.
                var hard = new SledPhysics(flat, cfg);
                hard.PlaceForAim(0f);
                hard.position = new Vector3(0f, 0.001f, 30f);
                hard.velocity = new Vector3(0f, -cfg.crashImpact - 5f, 10f);
                hard.grounded = false;
                hard.Step(0f, 0f, Dt);
                Check(hard.end == RunEnd.Crashed && hard.crashCause == CrashCause.HardLanding, "hard landing must retain its typed cause");
                string hardTip = RunTips.Tip(hard.stats, hard.end, hard.endReason, cfg, 30f, hard.crashCause);
                Check(hardTip.Contains("nose") && hardTip == RunTips.Tip(hard.stats, hard.end, "Başka bir dil", cfg, 30f, hard.crashCause), "landing tip must be independent of translated text");

                var flipCfg = new SledConfig { crashImpact = 50f };
                var flip = new SledPhysics(flat, flipCfg);
                flip.PlaceForAim(0f);
                flip.position = new Vector3(0f, 0.001f, 30f);
                flip.velocity = new Vector3(0f, -5f, 10f);
                flip.pitch = 1.2f;
                flip.grounded = false;
                flip.Step(0f, 0f, Dt);
                Check(flip.crashCause == CrashCause.Flip, "misaligned landing must be classified as a flip");
                flip.ForceEnd(RunEnd.Stopped, "later reason");
                Check(flip.crashCause == CrashCause.Flip && flip.end == RunEnd.Crashed, "first end reason must be preserved");
                flip.pickupAwaitingLanding = true;
                flip.PlaceForAim(1f);
                Check(flip.crashCause == CrashCause.None && !flip.pickupAwaitingLanding, "new run must reset crash and pickup lock");

                var offTrack = new SledPhysics(flat, cfg);
                offTrack.PlaceForAim(0f);
                offTrack.position = new Vector3(flat.halfWidth + TrackProfile.BankRim + 1f, 0f, 30f);
                offTrack.Step(0f, 0f, Dt);
                Check(offTrack.crashCause == CrashCause.OffTrack, "leaving the course must report the boundary");
                string boundaryTip = RunTips.Tip(offTrack.stats, offTrack.end, "", cfg, 30f, offTrack.crashCause);
                Check(boundaryTip.Contains("boundary") && !boundaryTip.Contains("rim"), "boundary tip must also apply to open forest");

                float TurnOn(Surface surface)
                {
                    flat.surfaces.Clear();
                    if (surface != Surface.Ground)
                        flat.surfaces.Add(SurfaceZone.Blob(surface, 50f, 0f, 30f, 10f, 1f, false));
                    var sled = new SledPhysics(flat, cfg);
                    sled.PlaceForAim(0f);
                    sled.position = new Vector3(0f, 0f, 50f);
                    sled.velocity = new Vector3(0f, 0f, 20f);
                    sled.Step(1f, 0f, Dt);
                    return Mathf.Atan2(sled.velocity.x, sled.velocity.z);
                }
                float groundTurn = TurnOn(Surface.Ground);
                float iceTurn = TurnOn(Surface.Ice);
                float mudTurn = TurnOn(Surface.Mud);
                Check(iceTurn > 0f && iceTurn < groundTurn * 0.9f, "ice must retain steering with a wider turn than ground");
                Check(mudTurn > iceTurn && mudTurn < groundTurn, "mud steering must be heavier without becoming less controllable than ice");
                flat.surfaces.Clear();
                flat.surfaces.Add(SurfaceZone.Blob(Surface.Ice, 50f, 0f, 30f, 10f, 1f, false));
                flat.bridges.Add(new TrackProfile.Bridge { z0 = 40f, z1 = 60f, r0 = 42f, r1 = 58f, x = 0f });
                Check(Mathf.Approximately(flat.LateralGrip(50f, 0f), 1f), "bridge must preserve full lateral control");

                var stockProbe = new PickupStock(new[] { 10f, 20f, 30f });
                var random = new System.Random(719);
                Check(stockProbe.Collect(20f, random, 0f, 0f) == 0, "missed pickups must remain available");
                Check(stockProbe.Collect(20f, random, 0f, 1f) == 2, "retry can collect previously missed pickups");
                Check(stockProbe.Collect(20f, random, 1f, 1f) == 0, "collected pickups must not pay twice");
                Check(stockProbe.Collect(30f, random, 1f, 1f) == 1 && stockProbe.Taken == 3, "unreached pickups remain until reached");

                for (int t = 0; t < TrackLibrary.Count; t++)
                {
                    Budget();
                    var track = TrackLibrary.Get(t);
                    var levels = new int[Upgrades.Count];
                    SledConfig previous = null;
                    for (int level = 0; level <= Upgrades.Max; level++)
                    {
                        levels[(int)UpgradeType.Glass] = level;
                        var current = Upgrades.BuildConfig(levels);
                        if (previous != null)
                        {
                            Check(current.sloshDamping + 1e-6f >= previous.sloshDamping, track.name + " cup damping regresses at " + level);
                            Check(current.milkHeadroom + 1e-6f >= previous.milkHeadroom, track.name + " cup headroom regresses at " + level);
                            Check(current.glassReflex > previous.glassReflex, track.name + " cup reflex must improve at " + level);
                        }
                        Check(current.milkHeadroom > 0f && current.milkHeadroom < current.glassHeight, track.name + " invalid cup fill height");
                        previous = current;
                    }
                    var coinZs = Collectibles.CoinZs(track);
                    var stock = new PickupStock(coinZs);
                    int paid = 0;
                    for (int attempt = 0; attempt < 250; attempt++) paid += stock.Collect(track.finishZ, random, 0.5f, 0.15f);
                    Check(paid <= coinZs.Count, track.name + " paid more coins than its finite stock");
                    paid += stock.Collect(float.MaxValue, random, 1f, 1f);
                    Check(paid == coinZs.Count && stock.Collect(float.MaxValue, random, 1f, 1f) == 0, track.name + " inventory exhaustion mismatch");

                    foreach (int level in new[] { 0, Upgrades.Max / 2, Upgrades.Max })
                    {
                        Budget();
                        for (int i = 0; i < levels.Length; i++) levels[i] = level;
                        var result = Simulate(track, Upgrades.BuildConfig(levels), PlayerKind.Usta, t * 31 + level,
                            RocketPolicy.Varsayilan, 12f);
                        Check(Finite(result.distance) && Finite(result.topSpeed) && Finite(result.fill), track.name + " non-finite simulation at " + level);
                        Check(result.distance > 0f && result.fill >= 0f && result.fill <= 1.001f, track.name + " invalid launch/liquid at " + level);
                        log.AppendFormat("  {0}, level {1}: {2:F1} m, milk {3:F2}, {4}\n", track.name, level, result.distance, result.fill, result.end);
                    }
                    Debug.Log("[REGRESSION] Checked " + track.name + " (finite stock " + coinZs.Count + " coins)");
                }
                Budget();
                log.AppendFormat("PASS: {0} checks; {1:F1} seconds. Full course completion and human skill balance are separate tests.", checks, watch.Elapsed.TotalSeconds);
                Debug.Log(log.ToString());
            }
            finally
            {
                Upgrades.Track = oldTrack;
                Upgrades.PartTier = oldParts;
                SledPhysics.Gravity = oldGravity;
            }
        }

        /// Başlangıç raporu (yol haritası aşama 0): oyuncu tipine göre ekonomi, süt kaybı matrisi, roket zamanlaması ve
        /// usta-ortalama mesafe farkı. parts=eco,milk,rocket,gap (varsayılan hepsi); out=<dosya.md> raporu dosyaya da yazar.
        public static void Baseline()
        {
            string parts = "eco,milk,rocket,gap", outPath = null;
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("parts=")) parts = arg.Substring(6);
                if (arg.StartsWith("out=")) outPath = arg.Substring(4);
            }
            var md = new StringBuilder("# Başlangıç raporu\n\n");
            md.AppendLine("Sim tarihi: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ". Oyuncu tipleri: usta = otomatik pilot; ortalama = 0.25 s gecikmeli, hafif titrek direksiyon, bardağın eksik dengesinin yarısını gecikmeli düzeltir, roketi hız yarıya inince ateşler, atışların %40'ında mükemmel bırakış, elmasların %75'ini toplar; eğmeyen = ortalama gibi sürer, telefonu hiç eğmez.\n");
            md.AppendLine("Ekonomi aynı günü ve sonlu gerçek yerleşimi kullanır. Toplama olasılığı tahminidir; görev/giriş ödülleri, hak bekleme süreleri ve ertesi gün yenilenme bu rapora dahil değildir.\n");
            // Relative levels cover every track and stay valid if stage counts change.
            int[][] mid = new int[TrackLibrary.Count][];
            for (int t = 0; t < mid.Length; t++)
            {
                int max = Upgrades.MaxFor(t);
                mid[t] = new[] { Mathf.RoundToInt(max * 0.6f), Mathf.RoundToInt(max * 0.6f), Mathf.RoundToInt(max * 0.4f),
                                Mathf.RoundToInt(max * 0.5f), Mathf.RoundToInt(max * 0.5f) };
            }

            if (parts.Contains("eco"))
            {
                md.AppendLine("## 1. Ekonomi (pist başına atış)\n");
                md.Append("| Oyuncu |");
                foreach (string name in TrackLibrary.Names) md.Append(" " + name + " |");
                md.AppendLine(" Toplam | İlerlemeyen atış | En uzun duraklama | Yeni pistlerde ilk atış | Elmasla alınan kademe |");
                md.Append("|---|");
                for (int i = 0; i < TrackLibrary.Count + 5; i++) md.Append("---|");
                md.AppendLine();
                var cases = new (PlayerKind, bool)[] { (PlayerKind.Usta, false), (PlayerKind.Usta, true), (PlayerKind.Ortalama, true), (PlayerKind.Egmeyen, true) };
                EconomyResult avg = null;
                foreach (var (kind, gems) in cases)
                {
                    var e = RunEconomy(kind, gems, null);
                    if (kind == PlayerKind.Ortalama) avg = e;
                    int longest = 0;
                    foreach (int l in e.longest) longest = Mathf.Max(longest, l);
                    md.Append("| " + e.label + " |");
                    for (int t = 0; t < TrackLibrary.Count; t++) md.Append(" " + R(e, t) + " |");
                    md.AppendFormat(" {0} | %{1:F0} | {2} | ", e.Total, 100f * e.Stalled / Mathf.Max(e.Total, 1), longest);
                    for (int t = 1; t < TrackLibrary.Count; t++)
                        md.Append((t > 1 ? " / " : "") + "%" + (e.arrival[t] * 100f).ToString("F0"));
                    md.AppendLine(" | " + e.gemBuys + " |");
                    Debug.Log("[SIM] Başlangıç ekonomi " + e.label + "\n" + e.Summary());
                }
                if (avg != null)
                {
                    float runs = Mathf.Max(avg.Total, 1);
                    md.AppendLine("\nOrtalama oyuncuda atış başına yüzey kaybı (yaklaşık, gerçek m): çamur " + (avg.surfaceLoss[0] / runs).ToString("F1")
                                  + " · su " + (avg.surfaceLoss[1] / runs).ToString("F1") + " · çimen " + (avg.surfaceLoss[2] / runs).ToString("F1")
                                  + " · buz (kazanç) " + (-avg.surfaceLoss[3] / runs).ToString("F1") + "\n");
                }
            }

            if (parts.Contains("milk"))
            {
                md.AppendLine("## 2. Süt kaybı (koşu başına ortalama, %)\n");
                md.AppendLine("Seviyeler pistin ortasındaki bir oyuncununki, yalnız bardak değişir. Gürültülü oyuncular 5 tohumun ortalaması.\n");
                md.AppendLine("| Pist | Oyuncu | Bardak %0 | %20 | %40 | %60 | %80 | %100 | Başlıca neden |");
                md.AppendLine("|---|---|---|---|---|---|---|---|---|");
                for (int t = 0; t < TrackLibrary.Count; t++)
                {
                    var track = TrackLibrary.Get(t);
                    foreach (PlayerKind kind in new[] { PlayerKind.Usta, PlayerKind.Ortalama, PlayerKind.Egmeyen })
                    {
                        var row = new StringBuilder("| " + track.name + " | " + SimPlayer.Name(kind) + " |");
                        var causes = new float[(int)SpillCause.Count];
                        for (int sample = 0; sample <= 5; sample++)
                        {
                            int g = Mathf.RoundToInt(Upgrades.Max * sample / 5f);
                            var lv = (int[])mid[t].Clone();
                            lv[(int)UpgradeType.Glass] = g;
                            int seeds = kind == PlayerKind.Usta ? 1 : 5;
                            float loss = 0f;
                            for (int sd = 1; sd <= seeds; sd++)
                            {
                                var r = Simulate(track, Upgrades.BuildConfig(lv), kind, sd * 31 + g);
                                loss += 1f - r.fill;
                                for (int i = 0; i < causes.Length; i++) causes[i] += r.stats.milkLost[i];
                            }
                            row.Append(" " + (100f * loss / seeds).ToString("F1") + " |");
                        }
                        int top = 0;
                        for (int i = 1; i < causes.Length; i++) if (causes[i] > causes[top]) top = i;
                        row.Append(" " + (causes[top] > 0.001f ? RunStats.CauseName((SpillCause)top) : "—") + " |");
                        md.AppendLine(row.ToString());
                    }
                }
                md.AppendLine();
            }

            if (parts.Contains("rocket"))
            {
                md.AppendLine("## 3. Roket zamanlaması (usta direksiyon, mesafe m)\n");
                md.AppendLine("| Pist | Seviyeler | Geç (durmak üzere) | Yarı hızda | Erken (çıkışta) | Roketsiz |");
                md.AppendLine("|---|---|---|---|---|---|");
                for (int t = 0; t < TrackLibrary.Count; t++)
                {
                    var track = TrackLibrary.Get(t);
                    var lv = mid[t];
                    var cfg = Upgrades.BuildConfig(lv);
                    var noRocket = (int[])lv.Clone();
                    noRocket[(int)UpgradeType.Rocket] = 0;
                    md.AppendLine(string.Format("| {0} | {1} | {2:F0} | {3:F0} | {4:F0} | {5:F0} |", track.name, string.Join("/", lv),
                        Simulate(track, cfg, PlayerKind.Usta, 1, RocketPolicy.Gec).distance,
                        Simulate(track, cfg, PlayerKind.Usta, 1, RocketPolicy.Yari).distance,
                        Simulate(track, cfg, PlayerKind.Usta, 1, RocketPolicy.Erken).distance,
                        Simulate(track, Upgrades.BuildConfig(noRocket), PlayerKind.Usta, 1).distance));
                }
                md.AppendLine();
            }

            if (parts.Contains("gap"))
            {
                md.AppendLine("## 4. Beceri farkı (aynı seviyeler, mesafe m; gürültülü oyuncular 5 tohum ortalaması ve en kötü-en iyi)\n");
                md.AppendLine("| Pist | Seviyeler | Usta | Ortalama | Eğmeyen | Ortalama köprüden düşme |");
                md.AppendLine("|---|---|---|---|---|---|");
                for (int t = 0; t < TrackLibrary.Count; t++)
                {
                    var track = TrackLibrary.Get(t);
                    var cfg = Upgrades.BuildConfig(mid[t]);
                    float usta = Simulate(track, cfg, PlayerKind.Usta, 1).distance;
                    string Avg(PlayerKind k, out int falls)
                    {
                        float sum = 0f, lo = float.MaxValue, hi = 0f;
                        falls = 0;
                        for (int sd = 1; sd <= 5; sd++)
                        {
                            var r = Simulate(track, cfg, k, sd * 101 + t);
                            sum += r.distance; lo = Mathf.Min(lo, r.distance); hi = Mathf.Max(hi, r.distance);
                            if (r.stats.fellInRavine) falls++;
                        }
                        return (sum / 5f).ToString("F0") + " (" + lo.ToString("F0") + "–" + hi.ToString("F0") + ")";
                    }
                    string avgS = Avg(PlayerKind.Ortalama, out int f1);
                    string noTilt = Avg(PlayerKind.Egmeyen, out _);
                    md.AppendLine(string.Format("| {0} | {1} | {2:F0} | {3} | {4} | {5}/5 |", track.name, string.Join("/", mid[t]), usta, avgS, noTilt, f1));
                }
                md.AppendLine();
            }

            Debug.Log("[SIM] Başlangıç raporu\n" + md);
            if (!string.IsNullOrEmpty(outPath)) System.IO.File.WriteAllText(outPath, md.ToString(), new UTF8Encoding(false));
        }

        /// Köprü düşmeleri: ortalama oyuncu (10 tohum) her köprüye hangi hız, sapma ve açıyla giriyor, kaçı düşüyor.
        /// track=  levels=a,b,c,d,e
        /// Çatalların geometrisi: şerit konumları, patikada mı, sırt yüksekliği.
        static int ProbeTrack;
        public static void ForkProbe()
        {
            foreach (var arg in System.Environment.GetCommandLineArgs())
                if (arg.StartsWith("track=")) ProbeTrack = int.Parse(arg.Substring(6));
            var sb = new System.Text.StringBuilder("[SIM] Çatallar" + System.Environment.NewLine);
            for (int t = 0; t < TrackLibrary.Count; t++)
            {
                var track = TrackLibrary.Get(t);
                sb.AppendLine("Parkur " + track.name + "  çatal " + track.forks.Count);
                foreach (var f in track.forks)
                {
                    sb.AppendLine(string.Format("  {0:0}-{1:0} iyi {2}", f.z0, f.z1, f.good));
                    if (t != ProbeTrack) continue;
                    foreach (var bm in track.bumps)
                        if (bm.z0 > f.z0 - 30f && bm.z0 < f.z1 + 10f)
                            sb.AppendLine(string.Format("   tümsek {0:0} uzunluk {1:0.0} yük {2:0.00} xc {3:0.0} yarı {4:0.0}", bm.z0, bm.length, bm.height, bm.xc, bm.halfWidth));
                    for (float z = f.z0 - 12f; z <= f.z1 + 4f; z += 4f)
                    {
                        float c = track.CenterX(z), w = track.ForkWeight(z);
                        sb.AppendLine(string.Format("   z {0:0} c {1:0.0} path {2:0.0} w {3:0.00} sol {4} sağ {5} orta {6}  sırt {7:0.00} yüz {8}/{9}",
                            z, c, track.PathX(z), w, track.OnPath(z, c - 3.1f), track.OnPath(z, c + 3.1f), track.OnPath(z, c),
                            track.BumpHeight(c, z), track.SurfaceAt(z, c - 3.1f), track.SurfaceAt(z, c + 3.1f))
                            + string.Format("  y {0:0.0} eğim {1:0.00}", track.Height(z), track.Slope(z)));
                    }
                }
            }
            Debug.Log(sb.ToString());
        }

        /// Titreme ölçümü (kullanıcı 2026-10-25): her parkurda ortalama oyuncu orta ve üst seviyede; 60 Hz karede örneklenir.
        /// Kısa sıçrama: 0.12 s'den kısa havada kalış (zemine değip kalkma). Burun titremesi: kare başına burun açısının ikinci
        /// farkının ortalaması (derece). Yanal titreme: yanal konumun ikinci farkı (cm).
        /// Süt becerisi dağılımı (2026-10-28): oyuncu türüne göre sağlam el çarpanı, ek kazanç (mesafe ödülüne oranı),
        /// kıl payı ve bozulma sayısı. Her pistte orta seviye, 6 tohum.
        public static void MilkSkill()
        {
            var sb = new StringBuilder("[SIM] Süt becerisi (orta seviye, 6 deneme)\n");
            foreach (var kind in new[] { PlayerKind.Usta, PlayerKind.Ortalama, PlayerKind.Egmeyen })
            {
                sb.AppendLine("  " + kind);
                for (int t = 0; t < TrackLibrary.Count; t++)
                {
                    var track = TrackLibrary.Get(t);
                    var cfg = Upgrades.BuildConfig(new[] { 12, 12, 12, 12, 12 });
                    float bonusPct = 0f, mult = 0f, calls = 0f, breaks = 0f, fill = 0f;
                    int maxed = 0;
                    const int N = 6;
                    for (int s = 0; s < N; s++)
                    {
                        var r = Simulate(track, cfg, kind, 100 + s);
                        bonusPct += (r.bonusMeters + r.closeCalls * 15f) / Mathf.Max(1f, r.distance);
                        mult += r.bestMult;
                        calls += r.closeCalls;
                        breaks += r.breaks;
                        fill += r.fill;
                        if (r.bestMult >= MilkStreak.MaxMult - 0.001f) maxed++;
                    }
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "    {0,-18} ek kazanç %{1,5:F1}  en iyi çarpan x{2:F2}  x1.5'e ulaşan {3}/{4}  kıl payı {5:F1}  bozulma {6:F1}  süt %{7:F0}",
                        track.name, 100f * bonusPct / N, mult / N, maxed, N, calls / N, breaks / N, 100f * fill / N));
                }
            }
            Debug.Log(sb.ToString());
        }

        /// Kalkışlar (2026-10-30, kullanıcı videosu: 100 km/s'de rampada takla atıyor ama havalanmıyor): her kalkışta hız,
        /// dikey hız, en yüksek açıklık ve havada kalma; tümsek/rampa listesi.
        public static void Takeoffs()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            int ti = 0;
            int[] lv = { 14, 14, 10, 14, 10 };
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("track=")) ti = int.Parse(arg.Substring(6));
                if (arg.StartsWith("lv=")) lv = System.Array.ConvertAll(arg.Substring(3).Split(','), int.Parse);
            }
            if (ti < 0)
            {
                var all = new StringBuilder("[SIM] Rampalar (ilk 3)\n");
                for (int k = 0; k < TrackLibrary.Count; k++)
                {
                    var tk = TrackLibrary.Get(k);
                    all.Append("  " + k + " " + tk.name + ":");
                    int c = 0;
                    foreach (var b in tk.bumps) if (b.ramp && c++ < 3) all.Append(string.Format(inv, " z{0:F0}({1})", b.z0, TrackLibrary.Show(b.z0)));
                    foreach (var b in tk.bumps) if (!b.ramp && b.height > 0.6f && c++ < 6) all.Append(string.Format(inv, " tümsek z{0:F0}({1}) h{2:F1} L{3:F0}", b.z0, TrackLibrary.Show(b.z0), b.height, b.length));
                    all.AppendLine();
                }
                Debug.Log(all.ToString());
                return;
            }
            var track = TrackLibrary.Get(ti);
            var cfg = Upgrades.BuildConfig(lv);
            var sb = new StringBuilder("[SIM] Kalkışlar " + track.name + " lv " + string.Join(",", lv) + " kanat " + cfg.wingArea + "\n");
            foreach (var b in track.bumps)
                if (b.z0 < 900f) sb.AppendLine(string.Format(inv, "  {0} z0 {1:F0} uzunluk {2:F1} yükseklik {3:F2} yarıgenişlik {4:F1}",
                                                             b.ramp ? "RAMPA " : "tümsek", b.z0, b.length, b.height, b.halfWidth));
            var sled = new SledPhysics(track, cfg);
            sled.PlaceForAim(1f);
            sled.Launch();
            var player = new SimPlayer(PlayerKind.Usta, 1, RocketPolicy.Varsayilan);
            bool was = true; float t = 0f, airT = 0f, maxClear = 0f, z0 = 0f, v0 = 0f, vy0 = 0f;
            while (sled.end == RunEnd.None && t < 120f && sled.position.z < 900f)
            {
                player.Glass(sled, cfg, Dt);
                sled.Step(player.Steer(track, sled, Dt), player.Pitch(track, sled, cfg), Dt);
                t += Dt;
                if (was && !sled.grounded && !sled.launching) { airT = 0f; maxClear = 0f; z0 = sled.position.z; v0 = sled.Speed; vy0 = sled.velocity.y; }
                if (!sled.grounded) { airT += Dt; maxClear = Mathf.Max(maxClear, sled.position.y - track.Height(sled.position.x, sled.position.z)); }
                if (!was && sled.grounded && airT > 0.08f && v0 > 15f)
                    sb.AppendLine(string.Format(inv, "  kalkış z {0,6:F1}  {1,4:F0} km/s  vy {2,5:F2}  açıklık {3,5:F2} m  hava {4,4:F2} s  iniş z {5,6:F1}",
                                                z0, v0 * 3.6f, vy0, maxClear, airT, sled.position.z));
                was = sled.grounded;
            }
            Debug.Log(sb.ToString());
        }

        /// Süzülmede burun kaldırma (2026-10-30): havada 2 m'den yüksekteyken parmak sürekli geri (burun yukarı) tutulursa.
        /// Tam girdide stall olup ters inmeli; hafif girdide düşmemeli.
        public static void StallTest()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder("[SIM] Burun kaldırma (Whispering Woods, lv 20)\n");
            var track = TrackLibrary.Get(1);
            var cfg = Upgrades.BuildConfig(new[] { 20, 20, 12, 20, 14 });
            foreach (float hold in new[] { 0f, 0.15f, 0.35f, 0.6f, 1f })
            {
                var sled = new SledPhysics(track, cfg);
                sled.PlaceForAim(1f);
                sled.Launch();
                var player = new SimPlayer(PlayerKind.Usta, 1, RocketPolicy.Varsayilan);
                float t = 0f, maxEx = 0f;
                int stallFrames = 0;
                while (sled.end == RunEnd.None && t < 120f)
                {
                    player.Glass(sled, cfg, Dt);
                    float pin = player.Pitch(track, sled, cfg);
                    bool high = !sled.grounded && !sled.launching && sled.position.y - track.Height(sled.position.x, sled.position.z) > 2f;
                    if (high && sled.position.z > 100f) pin = hold > 0f ? hold : pin;
                    sled.Step(player.Steer(track, sled, Dt), pin, Dt);
                    if (!sled.grounded)
                    {
                        float ex = sled.pitch - Mathf.Atan2(sled.velocity.y, Mathf.Max(sled.velocity.z, 0.01f));
                        maxEx = Mathf.Max(maxEx, ex);
                        if (sled.Unstable) stallFrames++;
                    }
                    t += Dt;
                }
                sb.AppendLine(string.Format(inv, "  girdi {0:F2}: {1} '{2}' mesafe {3:F0}  en büyük hücum {4:F0}°  stall {5:F1} s",
                                            hold, sled.end, sled.endReason, sled.maxDistance, maxEx * Mathf.Rad2Deg, stallFrames * Dt));
            }
            Debug.Log(sb.ToString());
        }

        public static void Jitter()
        {
            var sb = new StringBuilder("[SIM] Titreme (ortalama oyuncu, 60 Hz kare)\n");
            for (int track = 0; track < TrackLibrary.Count; track++)
                foreach (int lvl in new[] { 12, 25 })
                {
                    var t = TrackLibrary.Get(track);
                    int stages = Upgrades.StagesFor(track) * Upgrades.StageSize;
                    int L = Mathf.Min(lvl, stages);
                    var cfg = Upgrades.BuildConfig(new[] { L, L, L / 2, L, L });
                    var sled = new SledPhysics(t, cfg);
                    sled.PlaceForAim(1f);
                    sled.Launch();
                    var player = new SimPlayer(PlayerKind.Ortalama, 7);
                    float time = 0f, airRun = 0f, frameT = 0f;
                    int hops = 0, frames = 0;
                    bool wasGround = true;
                    double pitchJ = 0, latJ = 0; float p0 = 0, p1 = 0, x0 = 0, x1 = 0; int k = 0;
                    var hopZ = new System.Collections.Generic.List<float>();
                    while (sled.end == RunEnd.None && time < 300f)
                    {
                        sled.Step(player.Steer(t, sled, Dt), player.Pitch(t, sled, cfg), Dt);
                        time += Dt;
                        if (!sled.grounded) airRun += Dt;
                        else
                        {
                            if (!wasGround && airRun > 0f && airRun < 0.12f && !sled.launching) { hops++; if (hopZ.Count < 6) hopZ.Add(sled.position.z); }
                            airRun = 0f;
                        }
                        wasGround = sled.grounded;
                        frameT += Dt;
                        if (frameT >= 1f / 60f && !sled.launching)
                        {
                            frameT -= 1f / 60f;
                            float p = sled.pitch * Mathf.Rad2Deg, x = sled.position.x;
                            if (k >= 2) { pitchJ += Mathf.Abs(p - 2f * p1 + p0); latJ += Mathf.Abs(x - 2f * x1 + x0) * 100f; frames++; }
                            p0 = p1; p1 = p; x0 = x1; x1 = x; k++;
                        }
                    }
                    float km = Mathf.Max(0.05f, sled.maxDistance / 1000f);
                    sb.AppendLine(string.Format("  {0,-17} sv{1,2}: {2,5:F0} m  kısa sıçrama {3,4} ({4,5:F1}/km)  burun {5,5:F2}°  yanal {6,5:F2} cm   ilk: {7}",
                        t.name, lvl, sled.maxDistance, hops, hops / km, frames > 0 ? pitchJ / frames : 0, frames > 0 ? latJ / frames : 0,
                        string.Join(",", hopZ.ConvertAll(z => z.ToString("F0")))));
                }
            Debug.Log(sb.ToString());
        }

        /// Parkur hissi (kullanıcı 2026-10-19: zevksiz, heyecansız, yavaş parkur değişmeli): her pistte, oyuncunun o pisti
        /// bitirdiği seviyenin %60'ı ve %100'ü ile ortalama oyuncu koşusu (3 tohum). Ortalama/en yüksek hız, havada geçen
        /// süre, 0.5 s'den uzun uçuş sayısı, sürünme (< 25 km/sa) oranı, koşu süresi ve km başına olay (köprü, atlayış,
        /// çatal, rampa dudağı) yoğunluğu.
        public static void Feel()
        {
            int[][] finish =
            {
                new[] { 22, 21, 13, 20, 17 }, new[] { 22, 21, 13, 21, 17 }, new[] { 17, 16, 8, 15, 11 }, new[] { 21, 20, 12, 19, 16 },
                new[] { 20, 18, 10, 18, 14 }, new[] { 21, 20, 12, 20, 16 }, new[] { 22, 21, 13, 20, 17 },
            };
            var sb = new StringBuilder("[SIM] Parkur hissi (ortalama oyuncu, 3 tohum)\n");
            sb.AppendLine("  pist               seviye  mesafe  süre   ort km/sa  en yüksek  havada%  uçuş  sürünme%  olay/km");
            for (int t = 0; t < TrackLibrary.Count; t++)
            {
                var tr = TrackLibrary.Get(t);
                Upgrades.Track = t;
                Upgrades.PartTier = t;
                // Olaylar: köprü, köprüsüz atlayış, çatal, rampa dudağı (eğim birden düşer)
                var events = new System.Collections.Generic.List<float>();
                foreach (var b in tr.bridges) events.Add(b.r0);
                foreach (var w in tr.water) if (!tr.BridgeAt((w.x + w.y) * 0.5f, out _)) events.Add(w.x);
                foreach (var f in tr.forks) events.Add(f.z0);
                for (float z = 0f; z < tr.finishZ; z += 1f)
                    if (tr.Slope(z - 1f) > 0.08f && tr.Slope(z + 2f) < -0.02f) { events.Add(z); z += 20f; }
                foreach (float frac in new[] { 0.6f, 1f })
                {
                    var lv = System.Array.ConvertAll(finish[t], v => Mathf.RoundToInt(v * frac));
                    float dist = 0f, time = 0f, top = 0f, air = 0f, crawl = 0f;
                    int flights = 0;
                    for (int seed = 1; seed <= 3; seed++)
                    {
                        var cfg = Upgrades.BuildConfig(lv);
                        var sled = new SledPhysics(tr, cfg);
                        var liquid = new LiquidSim();
                        liquid.Reset(cfg);
                        sled.PlaceForAim(1f);
                        sled.Launch();
                        var player = new SimPlayer(PlayerKind.Ortalama, seed * 13);
                        float clock = 0f, airRun = 0f;
                        while (sled.end == RunEnd.None && clock < 600f)
                        {
                            Vector2 glass = player.Glass(sled, cfg, Dt);
                            if (player.Fire(tr, sled, cfg)) sled.FireRocket();
                            sled.Step(player.Steer(tr, sled, Dt), player.Pitch(tr, sled, cfg), Dt);
                            liquid.lidClosed = sled.position.z < cfg.lidDistance;
                            liquid.Step(sled.acceleration, SimPlayer.GlassRotation(sled, glass), Dt);
                            if (liquid.FillRatio < 0.12f) sled.ForceEnd(RunEnd.Spilled, "Bardak boşaldı!");
                            clock += Dt;
                            if (!sled.grounded) { air += Dt; airRun += Dt; }
                            else { if (airRun > 0.5f) flights++; airRun = 0f; }
                            if (sled.Speed < 25f / 3.6f && sled.position.z > 20f) crawl += Dt;
                            top = Mathf.Max(top, sled.Speed);
                        }
                        dist += sled.maxDistance;
                        time += clock;
                    }
                    int ev = events.FindAll(z => z < dist / 3f).Count;
                    sb.AppendLine(string.Format("  {0,-18} {1,4:P0}  {2,6:F0} m {3,5:F0} s  {4,7:F0}  {5,9:F0}  {6,7:F0}  {7,4:F1}  {8,8:F0}  {9,7:F1}",
                        TrackLibrary.Names[t], frac, dist / 3f, time / 3f, dist / time * 3.6f, top * 3.6f, 100f * air / time, flights / 3f,
                        100f * crawl / time, ev / Mathf.Max(dist / 3f / 1000f, 0.05f)));
                }
            }
            Debug.Log(sb.ToString());
        }

        public static void BridgeFalls()
        {
            int track = 2;
            int[] lv = { 18, 17, 8, 15, 14 };
            var kind = PlayerKind.Ortalama;
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("track=")) track = int.Parse(arg.Substring(6));
                if (arg == "noboost") TrackLibrary.BoostsOn = false;
                if (arg == "chaseramps") AutoPilot.ChaseRamps = true;
                if (arg.StartsWith("boostkick=")) SledPhysics.BoostKick = ParseF(arg.Substring(10));
                if (arg.StartsWith("boostgap=")) TrackLibrary.BoostSpacing = ParseF(arg.Substring(9));
                if (arg == "nosidebridge") TrackLibrary.SideBridgesOn = false;
                if (arg.StartsWith("levels=")) lv = System.Array.ConvertAll(arg.Substring(7).Split(','), int.Parse);
                if (arg.StartsWith("player=")) kind = SimPlayer.Parse(arg.Substring(7));
            }
            TrackLibrary.ApproachLog = "";
            var t = TrackLibrary.Get(track);
            Debug.Log("[YAKLAŞIM] " + TrackLibrary.ApproachLog);
            var cfg = Upgrades.BuildConfig(lv);
            var sb = new StringBuilder("[SIM] Köprü düşmeleri " + t.name + " (" + SimPlayer.Name(kind) + ")\n");
            sb.AppendLine("  elmaslar z: " + string.Join(", ", Collectibles.GemZs(t).ConvertAll(z => z.ToString("F0"))));
            foreach (var b in t.bridges) sb.AppendLine("  köprü " + b.r0.ToString("F0") + "-" + b.r1.ToString("F0") + " x " + b.x.ToString("F1") + " şekil " + b.shape + " kıvrım " + b.bend.ToString("F1"));
            for (int seed = 1; seed <= 10; seed++)
            {
                var sled = new SledPhysics(t, cfg);
                var liquid = new LiquidSim();
                liquid.Reset(cfg);
                sled.PlaceForAim(1f);
                sled.Launch();
                var player = new SimPlayer(kind, seed * 13);
                float time = 0f;
                var line = new StringBuilder("  tohum " + seed + ":");
                int next = 0;
                while (sled.end == RunEnd.None && time < 600f)
                {
                    Vector2 glass = player.Glass(sled, cfg, Dt);
                    if (player.Fire(t, sled, cfg)) sled.FireRocket();
                    sled.Step(player.Steer(t, sled, Dt), player.Pitch(t, sled, cfg), Dt);
                    liquid.lidClosed = sled.position.z < cfg.lidDistance;
                    liquid.Step(sled.acceleration, SimPlayer.GlassRotation(sled, glass), Dt);
                    if (liquid.FillRatio < 0.12f) sled.ForceEnd(RunEnd.Spilled, "Bardak boşaldı!");
                    if (next < t.bridges.Count && sled.position.z >= t.bridges[next].r0)
                    {
                        var b = t.bridges[next];
                        line.AppendFormat("  [{0:F0}: sapma {1:+0.00;-0.00} m, açı {2:+0;-0}°, hız {3:F1}]", b.r0, sled.position.x - b.x,
                                          sled.heading * Mathf.Rad2Deg, sled.Speed);
                        next++;
                    }
                    time += Dt;
                }
                line.AppendFormat("  → {0:F0} m {1}  (x {2:F2} y {3:F2} zemin {4:F2} arazi {5:F2} tabla {6} vy {7:F1})", sled.maxDistance, sled.endReason,
                                  sled.position.x, sled.position.y, t.Height(sled.position.x, sled.position.z),
                                  t.TerrainHeight(sled.position.x, sled.position.z), t.OnDeck(sled.position.x, sled.position.z), sled.velocity.y);
                sb.AppendLine(line.ToString());
            }
            Debug.Log(sb.ToString());
        }

        static string R(EconomyResult e, int t) => e.runs[t] + (e.finished[t] ? "" : "+");

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

        static SimResult Simulate(TrackProfile track, SledConfig cfg, PlayerKind kind = PlayerKind.Usta, int seed = 0,
                                  RocketPolicy rocket = RocketPolicy.Varsayilan, float maxSeconds = 600f)
        {
            var sled = new SledPhysics(track, cfg);
            var liquid = new LiquidSim();
            liquid.Reset(cfg);
            sled.PlaceForAim(1f);
            sled.Launch();
            var player = new SimPlayer(kind, seed, rocket);
            if (player.PerfectLaunch()) { sled.launchBoost = SledPhysics.PerfectLaunchBoost; sled.stats.perfectLaunch = true; }
            float t = 0f, dist = 0f;
            var streak = new MilkStreak();
            while (sled.end == RunEnd.None && t < maxSeconds)
            {
                Vector2 glass = player.Glass(sled, cfg, Dt);
                if (player.Fire(track, sled, cfg)) sled.FireRocket();
                sled.Step(player.Steer(track, sled, Dt), player.Pitch(track, sled, cfg), Dt);
                liquid.lidClosed = sled.position.z < cfg.lidDistance;
                float before = liquid.FillRatio;
                liquid.Step(sled.acceleration, SimPlayer.GlassRotation(sled, glass), Dt);
                float lost = before - liquid.FillRatio;
                if (lost > 0f) sled.stats.milkLost[(int)RunStats.Classify(sled, track)] += lost;
                streak.Step(sled.maxDistance - dist, liquid.FillRatio, liquid.Danger, !liquid.lidClosed, Dt);
                dist = sled.maxDistance;
                if (liquid.FillRatio < 0.12f) sled.ForceEnd(RunEnd.Spilled, "Bardak boşaldı!");
                t += Dt;
            }
            return new SimResult { distance = sled.maxDistance, fill = liquid.FillRatio, end = sled.end, reason = sled.endReason, time = t,
                                   stats = sled.stats, topSpeed = sled.topSpeed, bonusMeters = streak.bonusMeters,
                                   bestMult = streak.bestMult, closeCalls = streak.closeCalls, breaks = streak.breaks };
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
