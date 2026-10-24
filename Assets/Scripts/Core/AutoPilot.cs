using UnityEngine;

namespace SledSurfers
{
    /// Testler için otomatik oyuncu: engellerden kaçar, vadide süzülür, bardağı hissettiği ivmeye göre eğer.
    /// Hem başsız fizik testleri (SimTests) hem oyun içi ekran görüntüsü testi (AutoShot) kullanır.
    public class AutoPilot
    {
        Vector3 felt;
        public float glassPitch, glassRoll;   // derece
        float prevPitch, prevRoll;

        public void Reset()
        {
            felt = Vector3.zero;
            glassPitch = glassRoll = 0f;
        }

        /// İnsan tepkisi gibi gecikmeli: hissedilen ivmeye göre bardağı eğer.
        public void StepGlass(SledPhysics sled, float dt)
        {
            felt = Vector3.Lerp(felt, sled.acceleration, dt / 0.15f);
            if (GlassReflex.Level(sled.Orientation, felt, out glassPitch, out glassRoll))
            {
                // İnsan gibi: savrulmanın çoğunu karşılar ama elini en çok ±30° eğer (gecikmeli tam karşılık
                // keskin geçişlerde bardağı ters yöne yatırıp döküyordu).
                glassPitch = Mathf.Clamp(glassPitch * 0.85f, -30f, 30f);
                glassRoll = Mathf.Clamp(glassRoll * 0.85f, -30f, 30f);
            }
            else
            {
                // Ağırlıksızken hissedilecek yön yok: bardağı yavaşça düze getir.
                glassPitch = Mathf.MoveTowards(prevPitch, 0f, 90f * dt);
                glassRoll = Mathf.MoveTowards(prevRoll, 0f, 90f * dt);
            }
            prevPitch = glassPitch;
            prevRoll = glassRoll;
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
                // Süzülme pisti (roket tepesi yok): kızak durmak üzereyken roket bir sonraki kanala taşır.
                if (track.rocketHills.Count == 0 && sled.Speed < 6f && sled.position.z > 60f) return true;
                // Tepeye kadar sürtünme kaybı da hesaba katılır (yükseklik cinsinden μ·yol).
                float need = climb + cfg.groundFriction * peakDist + 0.5f;
                float reach = sled.Speed * sled.Speed / (2f * SledPhysics.Gravity);
                if (climb <= 1f || reach >= need) return false;
                // Önde (ilerleme çubuğunda işaretli) roket tepesi varsa tek roket ona saklanır: yalnızca o tepenin
                // yaklaşmasında ya da kızak gerçekten durmak üzereyken ateşlenir.
                float nextHill = -1f;
                foreach (float hz in track.rocketHills) if (hz > z && (nextHill < 0f || hz < nextHill)) nextHill = hz;
                if (nextHill > 0f && sled.rocketCharges <= 1 && nextHill - z > 150f) return sled.Speed < 4f;
                return true;
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
            // İnce köprüye yaklaşırken ve üstündeyken: tablanın ortasına kısa bakışla hizalan (düz git).
            for (float d = 0f; d <= 30f; d += 3f)
                // Derede birden çok köprü olabilir: kızağın gittiği yere en yakın tablaya bağlı kal (hedef yandaki köprüye
                // atlayınca tablanın üstünde direksiyon kırıp dereye düşüyordu).
                if (track.BridgeAt(sled.position.z + d, sled.position.x + sled.velocity.x * d / Mathf.Max(sled.velocity.z, 1f), out var br))
                {
                    aheadZ = sled.position.z + Mathf.Max(5f, sled.Speed * 0.35f);
                    target = br.XAt(aheadZ);
                    break;
                }
            // Önündeki havada asılı roket hakkına yönel (rampaya onun hizasında gir).
            if (track.hasRocketPickup && !sled.rocketPickupTaken)
            {
                float dz = track.rocketPickup.z - sled.position.z;
                if (dz > 0f && dz < 50f) target = track.rocketPickup.x;
            }
            // Sürpriz engelden kaç: karşıdan gelen kartopu ya da düşüş gölgesi yolun üstündeyse, yer olan yana.
            foreach (var h in sled.hazards)
            {
                if (h.phase != 1 && h.phase != 2) continue;
                float dz = h.pos.z - sled.position.z;
                if (dz < -1f || dz > 45f) continue;
                if (Mathf.Abs(h.pos.x - sled.position.x) > h.radius + 1.3f) continue;
                float c = track.CenterX(h.pos.z);
                float room = track.wallHalf > 0f ? track.WallHalfAt(h.pos.z) - 0.6f : track.halfWidth;
                float right = h.pos.x + h.radius + 1.4f, left = h.pos.x - h.radius - 1.4f;
                bool goRight = right < c + room && (h.pos.x <= sled.position.x || left < c - room);
                target = goRight ? right : left;
                aheadZ = sled.position.z + Mathf.Max(6f, Mathf.Min(dz, sled.Speed * 0.6f));
                break;
            }
            // Test çekimi: önündeki rampaya git (AutoShot "ramp").
            if (ChaseRamps)
                foreach (var bo in track.boosts)
                {
                    float dz = bo.z0 - sled.position.z;
                    if (bo.ramp && bo.z1 > sled.position.z && dz < 60f) { target = bo.x; break; }
                }
            if (ChaseRamps)
                foreach (var m in track.bouncers)
                {
                    float dz = m.x - sled.position.z;
                    if (dz > -1f && dz < 50f) { target = m.y; break; }
                }
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
                    {
                        // Sürünün uzak tarafına değil yakın kenarından kaç; vadi kenarını aşacaksa öbür yandan.
                        float side = xA >= fx ? 1f : -1f;
                        float pass = fx + side * (f.radius + 1.5f);
                        if (Mathf.Abs(pass - track.CenterX(f.z)) > track.halfWidth - 0.8f) side = -side;
                        return side;
                    }
                }
            float desired = Mathf.Clamp(Mathf.Atan2(target - sled.position.x, aheadZ - sled.position.z), -0.7f, 0.7f);
            return Mathf.Clamp((desired - sled.heading) * 4f, -1f, 1f);
        }

        /// atZ'deki en iyi yanal konum: patika üstünde kal, buzu seç, sudan kaç. Adaylar o noktadaki patikaya
        /// göre kaydırılarak ileriye doğru puanlanır (patika kıvrılsa da aday çizgisi onu izler).
        static readonly float[] Margins = { -0.6f, 0.6f };

        public static bool ChaseRamps;

        static float BestLane(TrackProfile track, SledPhysics sled, float atZ)
        {
            float look = Mathf.Max(20f, sled.Speed * 2f);
            // Önde dere köprüsü (çizimli pist, vadide sola/sağa kayık) varsa ona kadar bak: oyuncu tablayı uzaktan görür,
            // yerine erkenden kayar (son anda sert direksiyon sütü döküp gecikmeli oyuncuyu dereye düşürüyordu).
            // Patikadaki eski köprülerde değişmez (orada erken bakış diğer şeritleri bıraktırıp ekonomiyi bozuyordu).
            for (float d = look; d <= 60f; d += 3f)
                if (track.BridgeAt(atZ + d, out var ahead)) { if (ahead.hw > 0f) look = d + 6f; break; }
            float best = track.PathX(atZ), bestScore = float.MinValue;
            for (float off = -track.halfWidth; off <= track.halfWidth; off += 0.5f)
            {
                // Yolsuz haritada (her yer aynı hız) güzergâhın ortasına yakın kalır: yamaca ya da kenardaki engellere sürüklenmesin.
                float score = -Mathf.Abs(off) * 0.02f;
                for (float d = 0f; d < look; d += 2f)
                {
                    float zz = atZ + d, x = track.PathX(zz) + off;
                    // İnce köprü: tablanın dışı dere; tablanın tam ortası en güvenli çizgi.
                    if (track.BridgeAt(zz, x, out var br))
                    {
                        float bo = Mathf.Abs(x - br.XAt(zz));
                        if (bo > br.HalfWidth - 0.15f) { score -= 60f; continue; }
                        score -= bo * 8f;
                        if (br.bonus) score -= 2f;   // ortalama oyuncu çoğunlukla düz köprüyü seçer (S köprü riskli ödül)
                    }
                    if (Mathf.Abs(x - track.CenterX(zz)) > track.halfWidth) { score -= 1f; continue; }
                    // Kanyon duvarı: kızak kenarı + pay
                    if (track.wallHalf > 0f && Mathf.Abs(x - track.CenterX(zz)) > track.WallHalfAt(zz) - 0.9f) { score -= 60f; continue; }
                    var type = track.SurfaceAt(zz, x);
                    score += type == Surface.Ice ? 1f : type == Surface.Puddle ? -8f : type == Surface.Mud ? -9f : track.NearRoute(zz, x) ? 0.3f : 0f;   // su/çamur ~1 m enerji/m
                    // Kenarına sürtmesin: su/çamurun 0.6 m yakını da cezalı (yanal kayma ve dönüş gecikmesi payı)
                    if (type != Surface.Puddle && type != Surface.Mud)
                        foreach (float e in Margins)
                        {
                            var near = track.SurfaceAt(zz, x + e);
                            if (near == Surface.Puddle || near == Surface.Mud) { score -= 3f; break; }
                        }
                    if (!track.OnPath(zz, x) && track.ForkWeight(zz) > 0.3f) score -= 1f;   // çatal sırtına çıkma: erkenden şeridini seç
                    score -= track.BumpHeight(x, zz) * 0.12f;
                    // İnce buz: yavaş kızak (kırılma hızının altında) üstünden geçmez
                    if (track.thinIce.Count > 0 && sled.Speed < 16f && track.ThinIceAt(x, zz) >= 0) score -= 30f;
                    // Hızlandırıcı: tabandaysa hafifçe tercih edilir (yamaçtakine tırmanmak hız ve süt yer; engel cezası üstün).
                    if (track.boosts.Count > 0 && Mathf.Abs(x - track.CenterX(zz)) < track.halfWidth && track.BoostAt(x, zz) >= 0) score += 1.5f;   // tümsek az kaybettirir (inişte geri gelir); çamurdan hafif
                }
                // Engelin içinden geçen çizgi elenir.
                foreach (var o in track.obstacles)
                {
                    if (o.z < atZ - 2f || o.z > atZ + look * 1.5f) continue;
                    // Yakındaki engel ağır, uzaktaki hafif cezalı: sık engelli pistte bütün şeritler "kapalı" görünüp rastgele
                    // seçilmesin; önce en yakın engelden kaçılır, sonrakine yeniden bakılır.
                    if (Mathf.Abs(track.PathX(o.z) + off - o.x) < o.halfWidth + 1.6f)
                        score -= 40f * Mathf.Clamp01(1.15f - (o.z - atZ) / (look * 1.5f));
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
