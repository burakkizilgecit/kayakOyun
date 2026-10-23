using UnityEngine;

namespace SledSurfers
{
    /// Sütün dökülme nedeni (sonuç ekranı ve simler aynı sınıflandırmayı kullanır).
    public enum SpillCause { Air, Landing, Surface, Ice, Turn, Accel, Bump, Count }

    /// Kayıp yaratan yüzey (enerji muhasebesi).
    public enum LossSurface { Mud, Puddle, Grass, Ice, Count }

    /// Bir koşunun ölçümleri: hangi yüzeyde ne kadar yol ve enerji gitti, süt neden döküldü, roket ne zaman
    /// ateşlendi. Fizik (SledPhysics) ve sıvı adımı doldurur; sonuç ekranı ve simler okur.
    public class RunStats
    {
        /// Yüzeyde yerde geçilen yol (m).
        public readonly float[] meters = new float[(int)LossSurface.Count];
        /// Yüzeyin patikaya göre fazladan yaktığı enerji (J/kg); buzda eksi (kazanç).
        public readonly float[] energy = new float[(int)LossSurface.Count];
        /// Aynı enerjinin o noktadaki yavaşlamayla kabaca kaç metre daha götüreceği (yaklaşık, gerçek m).
        public readonly float[] lostMeters = new float[(int)LossSurface.Count];
        /// Dökülen süt oranı (0..1), nedene göre.
        public readonly float[] milkLost = new float[(int)SpillCause.Count];

        public float rocketSpeed = -1f, rocketZ = -1f;   // ilk ateşlemede hız (m/s) ve konum (m); -1: kullanılmadı
        public int rocketsFired;
        public float launchSpeed = -1f;                  // sapandan çıkış hızı (m/s)
        public bool perfectLaunch;                       // mükemmel bırakış (lastik %6 güçlü)
        public bool fellInRavine;                        // köprüden dereye (lav, kratere...) düştü
        public float airTime, longestAir;
        public int bridgesCrossed;
        public int boosts;   // geçilen hızlandırıcı
        public bool collapsed;   // kaya köprü altında çöktü
        public int forksPassed, forksGood;   // çatallar: geçilen ve altınlı şeritten geçilen

        public void Reset()
        {
            System.Array.Clear(meters, 0, meters.Length);
            System.Array.Clear(energy, 0, energy.Length);
            System.Array.Clear(lostMeters, 0, lostMeters.Length);
            System.Array.Clear(milkLost, 0, milkLost.Length);
            rocketSpeed = rocketZ = launchSpeed = -1f;
            rocketsFired = 0;
            perfectLaunch = false;
            fellInRavine = false;
            airTime = longestAir = 0f;
            bridgesCrossed = 0;
            boosts = 0;
            collapsed = false;
            forksPassed = forksGood = 0;
        }

        public float MilkLostTotal
        {
            get { float s = 0f; foreach (float m in milkLost) s += m; return s; }
        }

        /// Yerde bir fizik adımı: ds yol, extra fazladan yavaşlatan ivme (m/s²), netDecel o noktadaki normal yavaşlama.
        public void AddGround(LossSurface s, float ds, float extra, float netDecel)
        {
            int i = (int)s;
            meters[i] += ds;
            energy[i] += extra * ds;
            lostMeters[i] += extra * ds / Mathf.Max(netDecel, 0.04f * SledPhysics.Gravity);
        }

        /// Sütün o anda dökülme nedeni: havada, inişten hemen sonra, su/çamur, buz, viraj/yamaç, hızlanma/fren, zemin.
        public static SpillCause Classify(SledPhysics sled, TrackProfile track)
        {
            if (!sled.grounded) return SpillCause.Air;
            if (sled.sinceLanding < 0.6f) return SpillCause.Landing;
            var surf = track.SurfaceAt(sled.position.z, sled.position.x);
            if (surf == Surface.Puddle || surf == Surface.Mud) return SpillCause.Surface;
            if (surf == Surface.Ice) return SpillCause.Ice;
            Vector3 local = Quaternion.Inverse(sled.Orientation) * sled.acceleration;
            if (Mathf.Abs(local.x) > 4f) return SpillCause.Turn;
            if (Mathf.Abs(local.z) > 3f) return SpillCause.Accel;
            return SpillCause.Bump;
        }

        public static string CauseName(SpillCause c)
        {
            switch (c)
            {
                case SpillCause.Air: return "havada";
                case SpillCause.Landing: return "landing impact";
                case SpillCause.Surface: return "water/mud";
                case SpillCause.Ice: return "buz";
                case SpillCause.Turn: return "turn/slope";
                case SpillCause.Accel: return "speeding/braking";
                default: return "bumpy ground";
            }
        }
    }
}
