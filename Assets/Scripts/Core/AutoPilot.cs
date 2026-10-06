using UnityEngine;

namespace SledSurfers
{
    /// Testler için otomatik oyuncu: engellerden kaçar, vadide süzülür, bardağı hissettiği ivmeye göre eğer.
    /// Hem başsız fizik testleri (SimTests) hem oyun içi ekran görüntüsü testi (AutoShot) kullanır.
    public class AutoPilot
    {
        Vector3 felt;
        public float glassPitch, glassRoll;   // derece

        public void Reset()
        {
            felt = Vector3.zero;
            glassPitch = glassRoll = 0f;
        }

        /// İnsan tepkisi gibi gecikmeli: hissedilen ivmeye göre bardağı eğer.
        public void StepGlass(SledPhysics sled, float dt)
        {
            felt = Vector3.Lerp(felt, sled.acceleration, dt / 0.15f);
            Vector3 local = Quaternion.Inverse(sled.Orientation) * (new Vector3(0f, -SledPhysics.Gravity, 0f) - felt);
            if (local.magnitude < 0.4f * SledPhysics.Gravity)
            {
                // Ağırlıksızken hissedilecek yön yok: bardağı yavaşça düze getir.
                glassPitch = Mathf.MoveTowards(glassPitch, 0f, 90f * dt);
                glassRoll = Mathf.MoveTowards(glassRoll, 0f, 90f * dt);
            }
            else
            {
                Vector3 up = -local.normalized;
                glassPitch = Mathf.Clamp(Mathf.Atan2(up.z, up.y) * Mathf.Rad2Deg, -40f, 40f);
                glassRoll = Mathf.Clamp(Mathf.Atan2(up.x, up.y) * Mathf.Rad2Deg, -40f, 40f);
            }
        }

        /// Roketi ne zaman ateşlemeli: önündeki yokuşu hızıyla çıkamayacağı anlaşılınca
        /// (kinetik enerji tepeye yetmiyor), ya da kanatla vadinin üstünde alçalırken.
        public static bool ShouldFire(TrackProfile track, SledPhysics sled, SledConfig cfg)
        {
            if (!sled.CanFireRocket) return false;
            float z = sled.position.z;
            if (sled.grounded)
            {
                float px = sled.position.x;
                float h = track.Height(px, z), peak = h, peakDist = 0f;
                for (float d = 2f; d <= 90f; d += 2f)
                {
                    float y = track.Height(track.PathX(z + d), z + d);
                    if (y > peak) { peak = y; peakDist = d; }
                }
                float climb = peak - h;
                // Tepeye kadar sürtünme kaybı da hesaba katılır (yükseklik cinsinden μ·yol).
                float need = climb + cfg.groundFriction * peakDist + 0.5f;
                float reach = sled.Speed * sled.Speed / (2f * SledPhysics.Gravity);
                return climb > 1f && reach < need;
            }
            float height = sled.position.y - track.Height(sled.position.x, z);
            // Süzülürken yalnızca önündeki roket tepelerine yetecek kadar hak kalıyorsa.
            int hillsAhead = 0;
            foreach (float hz in track.rocketHills) if (hz > z) hillsAhead++;
            return cfg.wingArea > 0f && height > 10f && sled.velocity.y < -2f && sled.rocketCharges > hillsAhead;
        }

        static SledPhysics cacheSled;
        static float cacheZ, cacheOffset;

        public static float Steer(TrackProfile track, SledPhysics sled)
        {
            // İstenen yönü seç, kızağın yönünü ona doğru çevir (direksiyon dönüş hızıdır).
            // Biraz ilerideki en iyi şeride yönel: patika kıvrıldıkça yön de kıvrılır.
            float aheadZ = sled.position.z + Mathf.Max(6f, sled.Speed * 0.6f);
            // Rota puanlaması pahalı: kızak 1 m ilerledikçe yeniden hesaplanır.
            if (cacheSled != sled || Mathf.Abs(aheadZ - cacheZ) > 1f)
            {
                cacheSled = sled;
                cacheZ = aheadZ;
                cacheOffset = BestLane(track, sled, aheadZ) - track.PathX(aheadZ);
            }
            float target = track.PathX(aheadZ) + cacheOffset;
            // Havada: önündeki kuş sürüsünün geleceği yerden kaç.
            if (!sled.grounded)
                foreach (var f in track.flocks)
                {
                    float dz = f.z - sled.position.z;
                    if (dz < 0f || dz > 35f) continue;
                    // Varış anındaki konumu yanal hızla tahmin et; yalnızca çarpacaksa, patikanın olduğu
                    // taraftan kaç (vadinin dışına savrulmasın).
                    float tA = dz / Mathf.Max(sled.velocity.z, 1f);
                    float fx = track.FlockCenter(f, sled.clock + tA).x;
                    float xA = sled.position.x + sled.velocity.x * tA;
                    if (Mathf.Abs(fx - xA) < f.radius + 1.5f)
                        return track.PathX(f.z) >= fx ? 1f : -1f;
                }
            float desired = Mathf.Clamp(Mathf.Atan2(target - sled.position.x, aheadZ - sled.position.z), -0.7f, 0.7f);
            return Mathf.Clamp((desired - sled.heading) * 4f, -1f, 1f);
        }

        /// atZ'deki en iyi yanal konum: patika üstünde kal, buzu seç, sudan kaç. Adaylar o noktadaki patikaya
        /// göre kaydırılarak ileriye doğru puanlanır (patika kıvrılsa da aday çizgisi onu izler).
        static float BestLane(TrackProfile track, SledPhysics sled, float atZ)
        {
            float look = Mathf.Max(20f, sled.Speed * 2f);
            float best = track.PathX(atZ), bestScore = float.MinValue;
            for (float off = -track.halfWidth; off <= track.halfWidth; off += 0.5f)
            {
                float score = -Mathf.Abs(off) * 0.02f;
                for (float d = 0f; d < look; d += 2f)
                {
                    float zz = atZ + d, x = track.PathX(zz) + off;
                    if (Mathf.Abs(x - track.CenterX(zz)) > track.halfWidth) { score -= 1f; continue; }
                    var type = track.SurfaceAt(zz, x);
                    score += type == Surface.Ice ? 1f : type == Surface.Puddle ? -2f : type == Surface.Mud ? -3f : track.OnPath(zz, x) ? 0.3f : 0f;
                }
                // Engelin içinden geçen çizgi elenir.
                foreach (var o in track.obstacles)
                {
                    if (o.z < atZ - 2f || o.z > atZ + look * 1.5f) continue;
                    if (Mathf.Abs(track.PathX(o.z) + off - o.x) < o.halfWidth + 1.6f) score -= 40f;
                }
                if (score > bestScore) { bestScore = score; best = track.PathX(atZ) + off; }
            }
            return best;
        }

        public static float Pitch(TrackProfile track, SledPhysics sled, SledConfig cfg)
        {
            if (sled.grounded) return 0f;
            Vector3 v = sled.velocity;
            float gamma = Mathf.Atan2(v.y, Mathf.Max(v.z, 0.01f));
            float height = sled.position.y - track.Height(sled.position.x, sled.position.z);
            // Sadece altında derinlik varken (vadi) süzül; küçük rampalarda hızı koru.
            // Vadinin üstünde kanatlı kızağın kendi süzülmesine bırak; kısa sıçramalarda burnu zemine hizala.
            if (cfg.wingArea > 0f && height > 6f) return 0f;
            float target = Mathf.Atan(track.SlopeZ(sled.position.x, sled.position.z + 4f));
            return Mathf.Clamp((target - sled.pitch) * 3f, -1f, 1f);
        }
    }
}
