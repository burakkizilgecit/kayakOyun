using UnityEngine;

namespace SlingRiders
{
    /// Yükseltme önizlemesi (yol haritası aşama 3): kartın bir sonraki kademesinin bu pistte yaklaşık kaç metre
    /// kazandıracağı. Ana ekrandayken arka planda, kare başına süreyle sınırlı adımla otomatik pilot koşuları simüle
    /// edilir: önce mevcut seviyeler, sonra her kart +1 kademe. Sonuç gerçek metre (ekranda TrackLibrary.Show).
    public class GainPredictor
    {
        const float Step = 1f / 240f;
        const float MaxRunTime = 300f;
        const double BudgetMs = 4.0;

        TrackProfile track;
        readonly int[] levels = new int[Upgrades.Count];
        Perk perk;
        bool hasRequest;

        float baseDistance = -1f;
        readonly float[] distances = new float[Upgrades.Count];
        int job = Upgrades.Count;   // -1: mevcut seviyeler, 0..Count-1: kart +1, Count: bitti
        SledPhysics sled;
        SledConfig cfg;
        float clock;

        public bool Ready => hasRequest && job >= Upgrades.Count;

        /// i. kartın tahmini kazancı (gerçek m). Hesaplanmadıysa, kart maksimumdaysa ya da mesafeyi etkilemiyorsa -1.
        public float Gain(int i)
        {
            if (!Ready || distances[i] < 0f) return -1f;
            return Mathf.Max(0f, distances[i] - baseDistance);
        }

        /// Pist ya da seviyeler değiştiyse yeniden hesaplamayı başlatır (aynıysa bir şey yapmaz).
        public void Request(TrackProfile t, int[] lv, Perk p)
        {
            bool same = hasRequest && t == track && p == perk;
            for (int i = 0; i < levels.Length && same; i++) same = levels[i] == lv[i];
            if (same) return;
            track = t;
            perk = p;
            lv.CopyTo(levels, 0);
            hasRequest = true;
            baseDistance = -1f;
            for (int i = 0; i < distances.Length; i++) distances[i] = -1f;
            job = -1;
            sled = null;
        }

        public void Tick()
        {
            if (!hasRequest || job >= Upgrades.Count) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (job < Upgrades.Count && sw.Elapsed.TotalMilliseconds < BudgetMs)
            {
                if (sled == null && !StartJob()) continue;
                for (int k = 0; k < 120 && sled.end == RunEnd.None && clock < MaxRunTime; k++)
                {
                    if (AutoPilot.ShouldFire(track, sled, cfg)) sled.FireRocket();
                    sled.Step(AutoPilot.Steer(track, sled), AutoPilot.Pitch(track, sled, cfg), Step);
                    clock += Step;
                }
                if (sled.end != RunEnd.None || clock >= MaxRunTime)
                {
                    float d = Mathf.Min(sled.maxDistance, track.finishZ);
                    if (job < 0) baseDistance = d;
                    else distances[job] = d;
                    sled = null;
                    job++;
                }
            }
        }

        /// Sıradaki koşuyu kurar; kart mesafeyi etkilemiyorsa ya da maksimumdaysa atlar (false).
        bool StartJob()
        {
            var lv = (int[])levels.Clone();
            if (job >= 0)
            {
                // Bardak ve gelir mesafeyi değiştirmez; maksimumdaki kartın sonraki kademesi yok.
                if (job == (int)UpgradeType.Glass || job == (int)UpgradeType.Income || lv[job] >= Upgrades.MaxLevel(job))
                {
                    job++;
                    return false;
                }
                lv[job]++;
            }
            cfg = Upgrades.BuildConfig(lv, perk);
            sled = new SledPhysics(track, cfg);
            sled.PlaceForAim(1f);
            sled.Launch();
            clock = 0f;
            return true;
        }
    }
}
