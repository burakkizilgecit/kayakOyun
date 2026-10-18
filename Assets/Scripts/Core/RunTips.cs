using UnityEngine;

namespace SledSurfers
{
    /// Sonuç ekranındaki öneri: koşunun ölçümlerinden (RunStats) oyuncunun bir sonraki atışta yapabileceği tek şey.
    /// Yol haritası aşama 3: başarısızlığın nedeni görünsün, oyuncu "daha iyi oynayabilirim" hissetsin.
    public static class RunTips
    {
        public static string Tip(RunStats s, RunEnd end, string reason, SledConfig cfg, float distance, CrashCause cause = CrashCause.None)
        {
            if (end == RunEnd.Finished) return "";
            if (s.fellInRavine || cause == CrashCause.Ravine)
                return "Line up with the middle of the bridge early and go straight.";
            if (cause == CrashCause.OffTrack)
                return "Stay inside the track boundary: turn back toward the course before reaching the edge.";
            if (end == RunEnd.Crashed)
            {
                if (cause == CrashCause.HardLanding || cause == CrashCause.Flip)
                    return "Keep the nose level in the air: drag your finger up and down.";
                if (cause == CrashCause.Valley)
                    return "Approach the jump straight and keep your speed for the far side.";
                if (cause == CrashCause.Wall)
                    return "Aim for the landing slope and keep the nose aligned with it.";
                if (cause == CrashCause.Birds)
                    return "Watch the flock ahead and adjust your flight before you reach it.";
                return "Spot obstacles early and steer around them.";
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
                    case LossSurface.Mud: return "Lost ~" + m + " in mud: steer around it.";
                    case LossSurface.Puddle: return "Lost ~" + m + " in water: pass beside the puddle.";
                    default: return "Lost ~" + m + " on grass: stay on the dirt path.";
                }
            }
            if (s.MilkLostTotal > 0.25f)
                return Mathf.RoundToInt(s.MilkLostTotal * 100f) + "% of the milk spilled, cutting your earnings. " + MilkTip(TopCause(s));
            if (cfg.rocketCharges > 0 && s.rocketsFired == 0)
                return "You didn't use your rocket: fire it before the sled slows down.";
            if (s.rocketsFired > 0 && s.rocketSpeed >= 0f && s.rocketSpeed < 5f)
                return "Firing the rocket while you're fast usually carries you further.";
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
                case SpillCause.Landing: return "Milk splashes on landing: tilt the phone back slightly before you land.";
                case SpillCause.Surface: return "Water and mud brake hard: tilt back before entering, or go around.";
                case SpillCause.Turn: return "In a sharp turn, tilt the milk into the turn.";
                case SpillCause.Accel: return "The rocket and steep drops shake the milk: tilt back to meet them.";
                default: return "Tilt the phone to keep the cup level; a better cup protects the milk.";
            }
        }
    }
}
