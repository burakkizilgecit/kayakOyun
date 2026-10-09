using System;
using UnityEngine;

namespace SledSurfers
{
    public enum QuestKind
    {
        Distance,       // tek atışta X m
        TotalDistance,  // gün boyu toplam X m
        Coins,          // pistte X coin topla (toplam)
        Gems,           // X pembe elmas topla
        AirTime,        // toplam X sn havada kal
        LongAir,        // tek uçuşta X sn
        Perfect,        // X mükemmel atış
        Bridges,        // X köprüyü geç
        ForkGood,       // çatalda X kez altınlı şerit
        Milk,           // bir atışı sütün en az %X'iyle bitir
        Rocket,         // roketi X kez ateşle
        Ice,            // buzda toplam X m kay
        DryRun,         // su/çamura hiç girmeden X m git (tek atış)
        Finish,         // parkuru X kez bitir
        Count
    }

    /// Bir koşunun görevlere yansıyan özeti (GameController doldurur).
    public struct RunReport
    {
        public float distance, fill, airTime, longestAir, iceMeters, wetMeters;
        public int coins, gems, bridges, forksGood, rockets;
        public bool perfect, finished;
    }

    /// Günlük görevler (kullanıcı isteği 2026-10-09): her gün 3 görev, oyuncunun bulunduğu parkura göre çeşitlenir
    /// (buzlu pistte buz görevi, köprülü pistte köprü görevi; hedefler parkur uzunluğuna göre). Görev listesi gün ve
    /// parkurdan türetilir (aynı gün aynı parkurda hep aynı liste); ilerleme ve alınan ödüller kayıtta tutulur.
    public static class DailyQuests
    {
        public const int PerDay = 3;

        public struct Quest
        {
            public QuestKind kind;
            public int target;
            public int coins, gems;   // ödül
            public string text;
        }

        /// Gün anahtarı (yerel takvim günü; 00:00'da yenilenir).
        public static int Today => DateTime.Now.Year * 1000 + DateTime.Now.DayOfYear;

        public static int Yesterday
        {
            get { var d = DateTime.Now.AddDays(-1); return d.Year * 1000 + d.DayOfYear; }
        }

        /// Günlük giriş ödülü (gün 1..7): coin parkura göre ölçeklenir; 3. ve 7. gün elmas, 7. gün büyük ödül.
        public static void LoginReward(int day, int track, out int coins, out int gems)
        {
            float pay = Mathf.Max(1f, TrackLibrary.PayFactors[track] * TrackLibrary.CostScale[track]);
            int[] baseCoins = { 100, 150, 200, 250, 300, 400, 800 };
            coins = Mathf.RoundToInt(baseCoins[Mathf.Clamp(day - 1, 0, 6)] * pay / 4f / 10f) * 10;
            coins = Mathf.Max(coins, baseCoins[Mathf.Clamp(day - 1, 0, 6)]);
            gems = day == 7 ? 15 : day == 3 ? 3 : day == 5 ? 5 : 0;
        }

        public static TimeSpan UntilReset => DateTime.Today.AddDays(1) - DateTime.Now;

        /// Bu gün ve parkur için görev listesi. track: oyuncunun açtığı en ileri parkur (görevler oraya göre ölçeklenir).
        public static Quest[] Generate(int day, int track, TrackProfile profile)
        {
            var rnd = new System.Random(day * 31 + track * 7 + 3);
            float L = TrackLibrary.Lengths[track];
            float pay = TrackLibrary.PayFactors[track] * TrackLibrary.CostScale[track];
            bool hasIce = false, hasWet = false;
            foreach (var s in profile.surfaces)
            {
                if (s.type == Surface.Ice) hasIce = true;
                else hasWet = true;
            }
            // Parkura uyan görev havuzu (ağırlıklarla): her gün farklı üçlü, aynı türden iki görev yok.
            var pool = new System.Collections.Generic.List<QuestKind>
            {
                QuestKind.Distance, QuestKind.TotalDistance, QuestKind.Coins, QuestKind.AirTime, QuestKind.Perfect, QuestKind.Milk,
                QuestKind.LongAir, QuestKind.Gems,
            };
            if (profile.bridges.Count > 0) pool.Add(QuestKind.Bridges);
            if (profile.forks.Count > 0) pool.Add(QuestKind.ForkGood);
            if (hasIce) pool.Add(QuestKind.Ice);
            if (hasWet) pool.Add(QuestKind.DryRun);
            if (track > 0) pool.Add(QuestKind.Rocket);
            pool.Add(QuestKind.Finish);

            var list = new Quest[PerDay];
            for (int i = 0; i < PerDay; i++)
            {
                int k = rnd.Next(pool.Count);
                var kind = pool[k];
                pool.RemoveAt(k);
                // Üçüncü görev daha zor (ödülü elmas da içerir).
                float hard = i == 2 ? 1.5f : 1f;
                list[i] = Make(kind, track, L, hard, rnd);
                int baseCoins = Mathf.RoundToInt(Mathf.Max(50f, 60f * pay) * hard / 10f) * 10;
                list[i].coins = baseCoins;
                list[i].gems = i == 2 ? 3 + track : i == 1 ? 1 : 0;
            }
            return list;
        }

        static Quest Make(QuestKind kind, int track, float L, float hard, System.Random rnd)
        {
            float R(float a, float b) => a + (b - a) * (float)rnd.NextDouble();
            int Round(float v, int step) => Mathf.Max(step, Mathf.RoundToInt(v / step) * step);
            var q = new Quest { kind = kind };
            switch (kind)
            {
                case QuestKind.Distance:
                    q.target = Round(TrackLibrary.Show(L * Mathf.Min(0.9f, R(0.35f, 0.55f) * hard)), 50);
                    q.text = "Tek atışta " + q.target + " m git"; break;
                case QuestKind.TotalDistance:
                    q.target = Round(TrackLibrary.Show(L * R(2f, 3f) * hard), 500);
                    q.text = "Bugün toplam " + q.target + " m kay"; break;
                case QuestKind.Coins:
                    q.target = Round(R(40f, 70f) * hard, 10);
                    q.text = "Pistte " + q.target + " coin topla"; break;
                case QuestKind.Gems:
                    q.target = Mathf.RoundToInt(R(2f, 4f) * hard);
                    q.text = q.target + " pembe elmas topla"; break;
                case QuestKind.AirTime:
                    q.target = Round(R(20f, 35f) * hard, 5);
                    q.text = "Toplam " + q.target + " sn havada kal"; break;
                case QuestKind.LongAir:
                    q.target = Mathf.RoundToInt(R(3f, 5f) * hard);
                    q.text = "Tek uçuşta " + q.target + " sn havada kal"; break;
                case QuestKind.Perfect:
                    q.target = Mathf.RoundToInt(R(2f, 4f) * hard);
                    q.text = q.target + " mükemmel atış yap"; break;
                case QuestKind.Bridges:
                    q.target = Mathf.RoundToInt(R(3f, 5f) * hard);
                    q.text = q.target + " ince köprüyü düşmeden geç"; break;
                case QuestKind.ForkGood:
                    q.target = Mathf.RoundToInt(R(2f, 4f) * hard);
                    q.text = "Yol ayrımında " + q.target + " kez altınlı şeridi seç"; break;
                case QuestKind.Milk:
                    q.target = Mathf.Min(95, Round(R(70f, 80f) + 8f * (hard - 1f), 5));
                    q.text = "Bir atışı sütün en az %" + q.target + "'iyle bitir"; break;
                case QuestKind.Rocket:
                    q.target = Mathf.RoundToInt(R(3f, 5f) * hard);
                    q.text = "Roketi " + q.target + " kez ateşle"; break;
                case QuestKind.Ice:
                    q.target = Round(TrackLibrary.Show(R(120f, 200f) * hard), 50);
                    q.text = IceName(track) + " üstünde toplam " + q.target + " m kay"; break;
                case QuestKind.DryRun:
                    q.target = Round(TrackLibrary.Show(L * R(0.25f, 0.4f) * hard), 50);
                    q.text = WetName(track) + " girmeden tek atışta " + q.target + " m git"; break;
                default:
                    q.target = hard > 1f ? 2 : 1;
                    q.text = "Bir parkuru " + q.target + " kez bitir"; break;
            }
            return q;
        }

        static string IceName(int track) => track == 4 ? "Tuz düzlüğü" : track == 5 ? "Obsidyen" : "Buz";
        static string WetName(int track) => track == 4 ? "Bataklık kumuna" : track == 5 ? "Küle" : track == 6 ? "Kızıl toza" : "Suya ve çamura";

        /// Koşunun görev ilerlemesine katkısı. Toplam görevlerde eklenir, tek atış görevlerinde en iyisi tutulur.
        public static int Apply(Quest q, int progress, in RunReport r)
        {
            switch (q.kind)
            {
                case QuestKind.Distance: return Mathf.Max(progress, TrackLibrary.Show(r.distance));
                case QuestKind.TotalDistance: return progress + TrackLibrary.Show(r.distance);
                case QuestKind.Coins: return progress + r.coins;
                case QuestKind.Gems: return progress + r.gems;
                case QuestKind.AirTime: return progress + Mathf.FloorToInt(r.airTime);
                case QuestKind.LongAir: return Mathf.Max(progress, Mathf.FloorToInt(r.longestAir));
                case QuestKind.Perfect: return progress + (r.perfect ? 1 : 0);
                case QuestKind.Bridges: return progress + r.bridges;
                case QuestKind.ForkGood: return progress + r.forksGood;
                case QuestKind.Milk: return Mathf.Max(progress, r.distance > 50f ? Mathf.FloorToInt(r.fill * 100f) : 0);
                case QuestKind.Rocket: return progress + r.rockets;
                case QuestKind.Ice: return progress + TrackLibrary.Show(r.iceMeters);
                case QuestKind.DryRun: return Mathf.Max(progress, r.wetMeters <= 0.5f ? TrackLibrary.Show(r.distance) : 0);
                default: return progress + (r.finished ? 1 : 0);
            }
        }
    }
}
