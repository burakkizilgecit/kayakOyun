using UnityEngine;

namespace SledSurfers
{
    /// Sonuç ekranındaki öneri: koşunun ölçümlerinden (RunStats) oyuncunun bir sonraki atışta yapabileceği tek şey.
    /// Yol haritası aşama 3: başarısızlığın nedeni görünsün, oyuncu "daha iyi oynayabilirim" hissetsin.
    public static class RunTips
    {
        public static string Tip(RunStats s, RunEnd end, string reason, SledConfig cfg, float distance)
        {
            if (end == RunEnd.Finished) return "";
            if (reason == SledPhysics.FellInCreek)
                return "Köprüye yaklaşırken kızağı tablanın ortasına hizala ve düz git.";
            if (reason == SledPhysics.WentOffTrack)
                return "Yamacın tepesini aşınca pistten çıkarsın: yana bu kadar sert tırmanma.";
            if (end == RunEnd.Crashed)
            {
                if (reason.Contains("sert") || reason.Contains("Takla"))
                    return "Havadayken burnu yere paralel tut: parmağını yukarı-aşağı sürükle.";
                return "Engeli erken gör ve yanından dolaş.";
            }
            if (end == RunEnd.Spilled) return MilkTip(TopCause(s));

            // Kızak durdu: en büyük kayıp
            float minLoss = Mathf.Max(8f, 0.04f * distance);
            int worst = -1;
            for (int i = 0; i < (int)LossSurface.Ice; i++)
                if (s.lostMeters[i] >= minLoss && (worst < 0 || s.lostMeters[i] > s.lostMeters[worst])) worst = i;
            if (worst >= 0)
            {
                string m = TrackLibrary.Show(s.lostMeters[worst]) + " m";
                switch ((LossSurface)worst)
                {
                    case LossSurface.Mud: return "Çamurda ~" + m + " kaybettin: yanından dolaş.";
                    case LossSurface.Puddle: return "Suda ~" + m + " kaybettin: birikintinin yanından geç.";
                    default: return "Çimende ~" + m + " kaybettin: toprak patikada kal.";
                }
            }
            if (s.MilkLostTotal > 0.25f)
                return "Sütün %" + Mathf.RoundToInt(s.MilkLostTotal * 100f) + "'i döküldü, kazancın azaldı. " + MilkTip(TopCause(s));
            if (cfg.rocketCharges > 0 && s.rocketsFired == 0)
                return "Roketini kullanmadın: kızak yavaşlamadan ateşle.";
            if (s.rocketsFired > 0 && s.rocketSpeed >= 0f && s.rocketSpeed < 5f)
                return "Roketi kızak hızlıyken ateşlemek çoğu zaman daha uzağa taşır.";
            return "";
        }

        static SpillCause TopCause(RunStats s)
        {
            int top = 0;
            for (int i = 1; i < (int)SpillCause.Count; i++) if (s.milkLost[i] > s.milkLost[top]) top = i;
            return (SpillCause)top;
        }

        static string MilkTip(SpillCause c)
        {
            switch (c)
            {
                case SpillCause.Landing: return "İnişte süt sıçrıyor: rampadan inmeden telefonu hafifçe geriye eğ.";
                case SpillCause.Surface: return "Su ve çamur kızağı birden frenler: girmeden telefonu geriye eğ ya da yanından dolaş.";
                case SpillCause.Turn: return "Sert dönüşte sütü dönüşün içine doğru eğ.";
                case SpillCause.Accel: return "Roket ve iniş kanalı sütü sarsar: telefonu geriye eğerek karşıla.";
                default: return "Telefonu eğerek bardağı düz tut; daha iyi bardak sütü daha iyi korur.";
            }
        }
    }
}
