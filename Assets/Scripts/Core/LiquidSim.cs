using UnityEngine;

namespace SledSurfers
{
    /// Bardaktaki sıvının basitleştirilmiş fiziği.
    ///
    /// 1) Kol bir süspansiyon gibidir: bardak ele yay + sönümle bağlıdır, ani darbeleri yumuşatır.
    /// 2) Sıvı yüzeyi "etkin yerçekimine" (g − a) dik durmak ister. Yüzey eğimi, silindirik
    ///    kabın ilk çalkalanma modu gibi salınır: ω² = 1.84·g/R·tanh(1.84·h/R).
    /// 3) Yüzeyin en yüksek noktası bardak ağzını aşarsa sıvı savaktan akar gibi dökülür.
    public class LiquidSim
    {
        const float ArmFrequency = 9f;   // rad/s
        const float ArmDamping = 0.55f;
        const float ArmReach = 0.18f;    // m
        const float MaxAccel = 300f;     // sayısal kararlılık sınırı
        // Kızak ve kol esner: iniş darbesi süte bir fizik adımında değil ~50 ms'de ulaşır (toplam sarsıntı aynı).
        const float SuspensionTime = 0.05f;

        public float fill;               // ortalama sıvı yüksekliği (m)
        public Vector2 slope;            // yüzey eğimi: x = sağ, y = ileri (bardak ekseninde)
        public bool lidClosed;           // kapalı kapta sıvı dökülmez, yüzey kapağı aşamaz

        Vector2 slopeVel;
        Vector3 armOffset, armVel, smoothAccel;
        float initialFill;
        SledConfig cfg;

        public Vector3 ArmOffset => armOffset;
        public float FillRatio => initialFill > 0f ? fill / initialFill : 0f;

        public void Reset(SledConfig config)
        {
            cfg = config;
            initialFill = fill = config.initialFill;
            slope = slopeVel = Vector2.zero;
            lidClosed = true;
            armOffset = armVel = smoothAccel = Vector3.zero;
        }

        /// anchorAccel: elin (kızağın) dünya ivmesi. glassRotation: bardağın dünya yönelimi.
        public void Step(Vector3 anchorAccel, Quaternion glassRotation, float dt)
        {
            anchorAccel = Vector3.ClampMagnitude(anchorAccel, MaxAccel * 10f);
            // Ağırlıksızlık ham ivmeyle anlaşılır: kızak havalandığı an sıvı asılı kalır (süspansiyon gecikmesi
            // yüzünden havada dökülmesin).
            bool rawWeightless = (new Vector3(0f, -SledPhysics.Gravity, 0f) - anchorAccel).magnitude < 0.35f * SledPhysics.Gravity;
            smoothAccel += (anchorAccel - smoothAccel) * Mathf.Min(1f, dt / SuspensionTime);
            anchorAccel = smoothAccel;

            Vector3 prevVel = armVel;
            Vector3 rel = -ArmFrequency * ArmFrequency * armOffset
                          - 2f * ArmDamping * ArmFrequency * armVel
                          - anchorAccel;
            armVel += rel * dt;
            armOffset += armVel * dt;
            float reach = armOffset.magnitude;
            if (reach > ArmReach)
            {
                Vector3 dir = armOffset / reach;
                armOffset = dir * ArmReach;
                float outward = Vector3.Dot(armVel, dir);
                if (outward > 0f) armVel -= dir * outward;
            }
            Vector3 glassAccel = Vector3.ClampMagnitude(anchorAccel + (armVel - prevVel) / dt, MaxAccel);

            Vector3 gEff = new Vector3(0f, -SledPhysics.Gravity, 0f) - glassAccel;
            Vector3 local = Quaternion.Inverse(glassRotation) * gEff;
            float g = local.magnitude;
            float r = cfg.glassRadius;

            Vector2 target = slope;
            if (local.y < -0.05f * SledPhysics.Gravity)
                target = Vector2.ClampMagnitude(new Vector2(local.x, local.z) / -local.y, 4f);

            float depth = Mathf.Max(fill, 0.005f);
            float w2 = 1.84f * g / r * (float)System.Math.Tanh(1.84f * depth / r);
            float wMax = 1.6f / dt;
            w2 = Mathf.Min(w2, wMax * wMax);
            float w = Mathf.Sqrt(w2);
            // Ağırlıksızken (havada) sıvı bardakta asılı kalır: yüzey hareketi söner, dökülme olmaz.
            // Asıl sınav inişte: sert iniş sıvıyı çalkalar.
            bool weightless = rawWeightless || g < 0.35f * SledPhysics.Gravity;
            Vector2 acc = w2 * (target - slope) - (2f * cfg.sloshDamping * w + (weightless ? 6f : 0.5f)) * slopeVel;
            slopeVel += acc * dt;
            slope += slopeVel * dt;

            float rim = fill + slope.magnitude * r;
            if (lidClosed)
            {
                // Yüzey kapağa dayanır: eğim kapağın izin verdiği kadarla sınırlanır, çarpma enerjisi söner.
                float maxSlope = Mathf.Max(cfg.glassHeight - fill, 0f) / r;
                float mag = slope.magnitude;
                if (mag > maxSlope)
                {
                    Vector2 dir = slope / mag;
                    slope = dir * maxSlope;
                    float outward = Vector2.Dot(slopeVel, dir);
                    if (outward > 0f) slopeVel -= dir * outward * 1.5f;
                }
                return;
            }
            if (rim > cfg.glassHeight && !weightless)
            {
                float e = rim - cfg.glassHeight;
                float q = 0.6f * Mathf.Sqrt(2f * Mathf.Clamp(g, 0.5f, 4f * SledPhysics.Gravity)) * 1.2f * r * e * Mathf.Sqrt(e);
                fill -= q / (Mathf.PI * r * r) * dt;
            }
            // Ağız aşağı bakıyorsa (etkin yerçekimi güçlü biçimde bardağın ağzına doğru) sıvı boşalır.
            // Serbest düşüşte kalan küçük ivmeler (hava direnci) sıvıyı bardaktan çıkaramaz.
            // Yalnızca bardak gerçekten ters dönmüşse: iniş darbesi gibi anlık ivmeler sıvıyı bir anda boşaltmaz.
            bool upsideDown = (glassRotation * Vector3.up).y < 0.2f;
            if (upsideDown && local.y > 0.3f * SledPhysics.Gravity)
                fill -= 0.6f * Mathf.Sqrt(2f * Mathf.Min(local.y, 2f * SledPhysics.Gravity) * Mathf.Max(fill, 0.002f)) * dt;

            fill = Mathf.Max(fill, 0f);
        }
    }

    /// Karakterin refleksi: hissettiği savrulmanın (ivme + yerçekimi) bir kısmını kısa bir gecikmeyle kendiliğinden
    /// dengeler. Oranı bardak yükseltmeleri artırır (SledConfig.glassReflex); oyuncunun eğmesi bunun üstüne eklenir.
    public class GlassReflex
    {
        const float ReactionTime = 0.2f;
        Vector3 felt;
        public float pitch, roll;   // derece

        public void Reset()
        {
            felt = Vector3.zero;
            pitch = roll = 0f;
        }

        public void Step(SledPhysics sled, float strength, float dt)
        {
            felt = Vector3.Lerp(felt, sled.acceleration, dt / ReactionTime);
            if (Level(sled.Orientation, felt, out float p, out float r))
            {
                pitch = strength * p;
                roll = strength * r;
            }
            else
            {
                pitch = Mathf.MoveTowards(pitch, 0f, 90f * dt);
                roll = Mathf.MoveTowards(roll, 0f, 90f * dt);
            }
        }

        /// Bardağı hissedilen etkin yerçekimine hizalamak için gereken eğim (derece). Ağırlıksızken false.
        public static bool Level(Quaternion orientation, Vector3 felt, out float pitch, out float roll)
        {
            Vector3 local = Quaternion.Inverse(orientation) * (new Vector3(0f, -SledPhysics.Gravity, 0f) - felt);
            pitch = roll = 0f;
            if (local.magnitude < 0.4f * SledPhysics.Gravity) return false;
            Vector3 up = -local.normalized;
            pitch = Mathf.Clamp(Mathf.Atan2(up.z, up.y) * Mathf.Rad2Deg, -40f, 40f);
            roll = Mathf.Clamp(Mathf.Atan2(up.x, up.y) * Mathf.Rad2Deg, -40f, 40f);
            return true;
        }
    }
}
