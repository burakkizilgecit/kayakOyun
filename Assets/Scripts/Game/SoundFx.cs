using UnityEngine;

namespace SledSurfers
{
    /// Ses dosyası kullanmadan, kodla üretilen oyun sesleri.
    /// Sürekli sesler (lastik gıcırtısı, kayma, rüzgâr) her kare ses seviyesiyle sürülür;
    /// anlık sesler (şaklama, gümleme, su sıçraması) PlayOneShot ile çalınır.
    public class SoundFx
    {
        const int Rate = 44100;

        readonly AudioSource creak, slide, wind, rocket, shots, ui;
        readonly AudioClip twang, thud, splash, pop, coin, deny, pickup, gem;
        readonly System.Random rng = new System.Random(3);

        public SoundFx(Transform listener)
        {
            var go = new GameObject("Sound");
            go.transform.SetParent(listener, false);
            creak = Loop(go, MakeCreak());
            slide = Loop(go, MakeSlide());
            wind = Loop(go, MakeWind());
            rocket = Loop(go, MakeRocket());
            shots = go.AddComponent<AudioSource>();
            shots.playOnAwake = false;
            ui = go.AddComponent<AudioSource>();
            ui.playOnAwake = false;
            twang = MakeTwang();
            thud = MakeThud();
            splash = MakeSplash();
            pop = MakePop();
            coin = MakeCoin();
            deny = MakeDeny();
            pickup = MakePickup();
            gem = MakeGem();
        }

        /// tension: lastiğin gerilme hızı (0..1), speed: m/s, airborne: havada mı
        public void Drive(float tension, float speed, bool grounded, bool running, bool rocketOn, float dt)
        {
            Fade(rocket, rocketOn ? 0.75f : 0f, dt, rocketOn ? 30f : 8f);
            Fade(creak, tension * 0.9f, dt, 18f);
            creak.pitch = 0.8f + tension * 0.6f;

            float slideVol = running && grounded ? Mathf.Clamp01(speed / 22f) * 0.55f : 0f;
            Fade(slide, slideVol, dt, 10f);
            slide.pitch = 0.75f + Mathf.Clamp01(speed / 30f) * 0.6f;

            float windVol = running ? Mathf.Clamp01((speed - 6f) / 24f) * (grounded ? 0.25f : 0.6f) : 0f;
            Fade(wind, windVol, dt, 4f);
            wind.pitch = 0.8f + Mathf.Clamp01(speed / 30f) * 0.5f;
        }

        public void Twang(float strength)
        {
            shots.pitch = 0.9f + strength * 0.25f;
            shots.PlayOneShot(twang, 0.4f + 0.6f * strength);
        }

        public void Thud(float impact) => shots.PlayOneShot(thud, Mathf.Clamp01(0.25f + impact / 10f));
        public void Splash(float amount) => shots.PlayOneShot(splash, Mathf.Clamp01(0.3f + amount));

        // Arayüz sesleri
        public void Click() { ui.pitch = 1f; ui.PlayOneShot(pop, 0.5f); }
        public void Coin() { ui.pitch = 1f; ui.PlayOneShot(coin, 0.6f); }
        public void Pop() => shots.PlayOneShot(pop, 1f);
        public void Deny() { ui.pitch = 1f; ui.PlayOneShot(deny, 0.5f); }

        /// Coin toplama: art arda toplandıkça perde yükselir.
        public void Pickup(int streak)
        {
            ui.pitch = 1f + Mathf.Min(streak, 12) * 0.045f;
            ui.PlayOneShot(pickup, 0.45f);
        }

        public void Gem()
        {
            ui.pitch = 1f;
            ui.PlayOneShot(gem, 0.8f);
        }

        public static void SetEnabled(bool on) => AudioListener.volume = on ? 1f : 0f;

        static void Fade(AudioSource s, float target, float dt, float rate) =>
            s.volume = Mathf.Lerp(s.volume, target, 1f - Mathf.Exp(-rate * dt));

        static AudioSource Loop(GameObject go, AudioClip clip)
        {
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = 0f;
            s.playOnAwake = false;
            s.Play();
            return s;
        }

        float Noise() => (float)rng.NextDouble() * 2f - 1f;

        static AudioClip Clip(string name, float[] data)
        {
            float peak = 0.0001f;
            foreach (float v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < data.Length; i++) data[i] /= peak;
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Lastik gerilirken: düşük, pürüzlü titreşim + sürtünme hışırtısı
        AudioClip MakeCreak()
        {
            var d = new float[Rate / 2];
            float phase = 0f, lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                phase += (70f + 25f * Mathf.Sin(t * 23f) + 10f * Noise()) / Rate;
                float saw = (phase % 1f) * 2f - 1f;
                lp += (Noise() - lp) * 0.15f;
                d[i] = saw * 0.5f + lp * 0.8f;
            }
            return Clip("creak", d);
        }

        // Kızak altı kayma: alçak geçirgen gürültü
        AudioClip MakeSlide()
        {
            var d = new float[Rate];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                lp += (Noise() - lp) * 0.08f;
                lp2 += (lp - lp2) * 0.2f;
                d[i] = lp2 + (rng.NextDouble() < 0.0015 ? Noise() * 0.6f : 0f);
            }
            return Clip("slide", d);
        }

        // Rüzgâr: yavaşça dalgalanan yumuşak gürültü
        AudioClip MakeWind()
        {
            var d = new float[Rate * 2];
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / d.Length;
                lp += (Noise() - lp) * 0.04f;
                d[i] = lp * (0.6f + 0.4f * Mathf.Sin(t * Mathf.PI * 2f));
            }
            return Clip("wind", d);
        }

        // Roket: gürleyen alçak gürültü + cızırtı
        AudioClip MakeRocket()
        {
            var d = new float[Rate];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float n = Noise();
                lp += (n - lp) * 0.05f;
                lp2 += (n - lp2) * 0.5f;
                d[i] = lp * 1.4f + (n - lp2) * 0.25f;
            }
            return Clip("rocket", d);
        }

        // Lastiğin şaklaması: perdesi düşen, tınlayan tel sesi
        AudioClip MakeTwang()
        {
            var d = new float[(int)(Rate * 0.7f)];
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float f = 95f + 70f * Mathf.Exp(-t * 9f);
                phase += f / Rate * Mathf.PI * 2f;
                float tone = Mathf.Sin(phase) + 0.5f * Mathf.Sin(phase * 2f) + 0.25f * Mathf.Sin(phase * 3.01f);
                float attack = t < 0.015f ? Noise() * (1f - t / 0.015f) * 1.5f : 0f;
                d[i] = tone * Mathf.Exp(-t * 6f) + attack;
            }
            return Clip("twang", d);
        }

        // İniş gümlemesi
        AudioClip MakeThud()
        {
            var d = new float[(int)(Rate * 0.35f)];
            float phase = 0f, lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                phase += (45f + 60f * Mathf.Exp(-t * 25f)) / Rate * Mathf.PI * 2f;
                lp += (Noise() - lp) * 0.1f;
                d[i] = (Mathf.Sin(phase) + lp * 0.6f) * Mathf.Exp(-t * 14f);
            }
            return Clip("thud", d);
        }

        // Düğme: kısa, yukarı kayan "pop"
        AudioClip MakePop()
        {
            var d = new float[(int)(Rate * 0.08f)];
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                phase += (500f + 5000f * t) / Rate * Mathf.PI * 2f;
                d[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 55f);
            }
            return Clip("pop", d);
        }

        // Satın alma: iki parlak nota (çın-çın)
        AudioClip MakeCoin()
        {
            var d = new float[(int)(Rate * 0.45f)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float f = t < 0.08f ? 988f : 1319f;
                float local = t < 0.08f ? t : t - 0.08f;
                float tone = Mathf.Sin(t * f * Mathf.PI * 2f) + 0.3f * Mathf.Sin(t * f * 2f * Mathf.PI * 2f);
                d[i] = tone * Mathf.Exp(-local * (t < 0.08f ? 20f : 7f));
            }
            return Clip("coin", d);
        }

        // Coin: kısa, parlak "tın"
        AudioClip MakePickup()
        {
            var d = new float[(int)(Rate * 0.18f)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                d[i] = (Mathf.Sin(t * 1568f * Mathf.PI * 2f) + 0.4f * Mathf.Sin(t * 3136f * Mathf.PI * 2f)) * Mathf.Exp(-t * 22f);
            }
            return Clip("pickup", d);
        }

        // Elmas: yükselen üç notalı parıltı
        AudioClip MakeGem()
        {
            var d = new float[(int)(Rate * 0.6f)];
            float[] notes = { 1047f, 1319f, 1760f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float v = 0f;
                for (int k = 0; k < notes.Length; k++)
                {
                    float local = t - k * 0.07f;
                    if (local < 0f) continue;
                    v += Mathf.Sin(local * notes[k] * Mathf.PI * 2f) * Mathf.Exp(-local * 7f);
                }
                d[i] = v;
            }
            return Clip("gem", d);
        }

        // Para yetmedi: alçak, kısa "bıt"
        AudioClip MakeDeny()
        {
            var d = new float[(int)(Rate * 0.22f)];
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                phase += 150f / Rate;
                float sq = (phase % 1f) < 0.5f ? 1f : -1f;
                d[i] = sq * 0.6f * (t < 0.09f || t > 0.12f ? 1f : 0f) * Mathf.Exp(-t * 8f);
            }
            return Clip("deny", d);
        }

        // Süt sıçraması: kısa, parçalı yüksek gürültü
        AudioClip MakeSplash()
        {
            var d = new float[(int)(Rate * 0.4f)];
            float hp = 0f, prev = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float n = Noise();
                hp = 0.85f * (hp + n - prev);
                prev = n;
                float bursts = 0.6f + 0.4f * Mathf.Sin(t * 90f);
                d[i] = hp * bursts * Mathf.Exp(-t * 9f);
            }
            return Clip("splash", d);
        }
    }
}
