using System;
using UnityEngine;

namespace SlingRiders
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

    /// Günlük görevler (kullanıcı isteği 2026-10-09; 2026-10-23'ten beri 5): her gün PerDay görev, oyuncunun bulunduğu parkura göre çeşitlenir
    /// (buzlu pistte buz görevi, köprülü pistte köprü görevi; hedefler parkur uzunluğuna göre). Görev listesi gün ve
    /// parkurdan türetilir (aynı gün aynı parkurda hep aynı liste); ilerleme ve alınan ödüller kayıtta tutulur.
    public static class DailyQuests
    {
        public const int PerDay = 5;   // kullanıcı 2026-10-23: "günlük en az 5 görev"

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
            // 2026-10-24 (kullanıcı: "günlük ödülle elmas olmamalı"): elmas yok; 7. gün büyük coin ödülü.
            gems = 0;
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
                // İlk ikisi kolay, üçüncüsü orta, son ikisi zor; elmas yalnız orta ve son görevde (elmas kıtlığı bilinçli).
                float hard = i >= 3 ? 1.5f : i == 2 ? 1.2f : 1f;
                list[i] = Make(kind, track, L, hard, rnd);
                int baseCoins = Mathf.RoundToInt(Mathf.Max(50f, 60f * pay) * hard / 10f) * 10;
                list[i].coins = baseCoins;
                list[i].gems = 0;   // 2026-10-24: görevlerden elmas çıkmaz (elmas satılır)
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
                    q.text = "Go " + q.target + " m in one run"; break;
                case QuestKind.TotalDistance:
                    q.target = Round(TrackLibrary.Show(L * R(2f, 3f) * hard), 500);
                    q.text = "Slide " + q.target + " m in total today"; break;
                case QuestKind.Coins:
                    q.target = Mathf.RoundToInt(R(4f, 8f) * hard);   // altınlar tek tek ve dağınık
                    q.text = "Collect " + q.target + " coins on the track"; break;
                case QuestKind.Gems:
                    q.target = Mathf.RoundToInt(R(2f, 4f) * hard);
                    q.text = "Collect " + q.target + " pink gems"; break;
                case QuestKind.AirTime:
                    q.target = Round(R(20f, 35f) * hard, 5);
                    q.text = "Stay airborne for " + q.target + " s in total"; break;
                case QuestKind.LongAir:
                    q.target = Mathf.RoundToInt(R(3f, 5f) * hard);
                    q.text = "Stay airborne for " + q.target + " s in one flight"; break;
                case QuestKind.Perfect:
                    q.target = Mathf.RoundToInt(R(2f, 4f) * hard);
                    q.text = "Make " + q.target + " perfect launches"; break;
                case QuestKind.Bridges:
                    q.target = Mathf.RoundToInt(R(3f, 5f) * hard);
                    q.text = "Cross " + q.target + " narrow bridges"; break;
                case QuestKind.ForkGood:
                    q.target = Mathf.RoundToInt(R(2f, 4f) * hard);
                    q.text = "Take the coin lane at a fork " + q.target + " times"; break;
                case QuestKind.Milk:
                    q.target = Mathf.Min(95, Round(R(70f, 80f) + 8f * (hard - 1f), 5));
                    q.text = "Finish a run with at least " + q.target + "% milk"; break;
                case QuestKind.Rocket:
                    q.target = Mathf.RoundToInt(R(3f, 5f) * hard);
                    q.text = "Fire the rocket " + q.target + " times"; break;
                case QuestKind.Ice:
                    q.target = Round(TrackLibrary.Show(R(120f, 200f) * hard), 50);
                    q.text = "Slide " + q.target + " m on " + IceName(track); break;
                case QuestKind.DryRun:
                    q.target = Round(TrackLibrary.Show(L * R(0.25f, 0.4f) * hard), 50);
                    q.text = "Go " + q.target + " m in one run without touching " + WetName(track); break;
                default:
                    q.target = hard > 1f ? 2 : 1;
                    q.text = "Finish a track " + (q.target > 1 ? q.target + " times" : "once"); break;
            }
            return q;
        }

        static string IceName(int track) => track == 4 ? "salt flats" : track == 5 ? "obsidian" : "ice";
        static string WetName(int track) => track == 4 ? "quicksand" : track == 5 ? "ash" : track == 6 ? "red dust" : "water or mud";

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
