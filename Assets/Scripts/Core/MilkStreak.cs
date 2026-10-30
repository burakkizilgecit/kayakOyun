using UnityEngine;

namespace SlingRiders
{
    /// Süt becerisi (kullanıcı 2026-10-28: "süt mekaniğini daha eğlenceli ve rekabetçi yapalım"). Kapak açıldıktan sonra:
    /// - Sağlam el: süt sakin (Danger < CalmDanger) ve dökülmeden gidilen her StepMeters metrede çarpan StepBonus artar
    ///   (en çok MaxMult; ilk denemede x1.5 herkesin ulaştığı bir tavan olunca beceri farkı yoktu). Çarpan, o an
    ///   gidilen metreye ek mesafe ödülü kazandırır (bonusMeters). Dökülünce (%0.6 ve üstü) çarpan x1.e döner.
    /// - Kıl payı: süt ağza dayanıp (Danger ≥ ArmDanger) dökülmeden geri çekilirse ödül.
    public class MilkStreak
    {
        public const float StepMeters = 80f, StepBonus = 0.05f, MaxMult = 1.3f;
        public const float CalmDanger = 0.5f;              // kademe yalnız süt sakinken (ağza uzak) ilerler
        public const float BreakSpill = 0.006f;           // çarpanı bozan döküm (bardağın oranı)
        public const float ArmDanger = 0.9f, SafeDanger = 0.45f, CloseCallSpill = 0.004f, CloseCallCooldown = 1.5f;

        public enum Event { None, LevelUp, Break, CloseCall }

        public float mult = 1f;
        public float bonusMeters;   // çarpanın kazandırdığı ek metre (ödülde mesafeye eklenir)
        public int closeCalls, breaks;
        public float bestMult = 1f;
        float meters, refFill = 1f, armFill, cooldown;
        bool armed;

        public float Progress => mult >= MaxMult ? 1f : meters / StepMeters;   // sonraki kademeye ilerleme (gösterge)

        public void Reset()
        {
            mult = bestMult = 1f;
            bonusMeters = meters = cooldown = 0f;
            refFill = 1f;
            closeCalls = breaks = 0;
            armed = false;
        }

        /// dz: bu adımda gidilen yeni metre (rekor mesafedeki artış); fill: bardak oranı; danger: LiquidSim.Danger;
        /// active: kapak açık ve koşu sürüyor.
        public Event Step(float dz, float fill, float danger, bool active, float dt)
        {
            cooldown -= dt;
            if (!active)
            {
                refFill = fill;
                return Event.None;
            }
            if (refFill - fill > BreakSpill)
            {
                bool had = mult > 1f;
                mult = 1f;
                meters = 0f;
                refFill = fill;
                armed = false;
                breaks++;
                return had ? Event.Break : Event.None;
            }
            // Küçük damlalar çarpanı bozmaz ama birikirse bozar: referans yalnız bozulmada yenilenir.

            var e = Event.None;
            if (danger >= ArmDanger && !armed)
            {
                armed = true;
                armFill = fill;
            }
            else if (armed && danger < SafeDanger)
            {
                armed = false;
                if (armFill - fill < CloseCallSpill && cooldown <= 0f)
                {
                    closeCalls++;
                    cooldown = CloseCallCooldown;
                    e = Event.CloseCall;
                }
            }

            bonusMeters += Mathf.Max(dz, 0f) * (mult - 1f);
            if (danger < CalmDanger) meters += Mathf.Max(dz, 0f);   // çalkalanırken kademe ilerlemez (beceri farkı)
            if (mult < MaxMult - 0.001f && meters >= StepMeters)
            {
                meters -= StepMeters;
                mult = Mathf.Min(MaxMult, mult + StepBonus);
                bestMult = Mathf.Max(bestMult, mult);
                if (e == Event.None) e = Event.LevelUp;
            }
            if (mult >= MaxMult - 0.001f) meters = 0f;
            return e;
        }

        /// Bitişte süt notu: "NOT A DROP!" (≥%98), "STEADY HAND" (≥%85), yoksa boş.
        public static string Grade(float fill) => fill >= 0.98f ? "NOT A DROP!" : fill >= 0.85f ? "STEADY HAND" : null;
    }
}
