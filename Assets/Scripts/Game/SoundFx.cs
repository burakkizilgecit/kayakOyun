using UnityEngine;

namespace SlingRiders
{
    /// Ses dosyası kullanmadan, kodla üretilen oyun sesleri.
    /// Sürekli sesler (lastik gıcırtısı, kayma, rüzgâr, süt çalkalanması/akışı) her kare ses seviyesiyle sürülür;
    /// anlık sesler bir kaynak havuzundan çalınır (2026-10-28): tek kaynağın perdesi değişince o an çalan başka sesin
    /// perdesi de kayıyordu (ör. art arda coin sesi iniş gümlemesini tizleştiriyordu).
    public class SoundFx
    {
        const int Rate = 44100;
        const int PoolSize = 10;

        readonly AudioSource creak, slide, wind, rocket, slosh, pour;
        readonly AudioSource[] pool = new AudioSource[PoolSize];
        int next;
        readonly AudioClip twang, thud, pop, coin, deny, pickup, gem, boost, closeCall, streakBreak, fanfare, record;
        readonly AudioClip[] splashes = new AudioClip[3];
        readonly AudioClip[] streakNotes = new AudioClip[6];
        readonly System.Random rng = new System.Random(3);

        public SoundFx(Transform listener)
        {
            var go = new GameObject("Sound");
            go.transform.SetParent(listener, false);
            creak = Loop(go, MakeCreak());
            slide = Loop(go, MakeSlide());
            wind = Loop(go, MakeWind());
            rocket = Loop(go, MakeRocket());
            slosh = Loop(go, MakeSlosh());
            pour = Loop(go, MakePour());
            for (int i = 0; i < PoolSize; i++)
            {
                pool[i] = go.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
            }
            twang = MakeTwang();
            thud = MakeThud();
            for (int i = 0; i < splashes.Length; i++) splashes[i] = MakeSplash(i);
            pop = MakePop();
            coin = MakeCoin();
            deny = MakeDeny();
            pickup = MakePickup();
            gem = MakeGem();
            boost = MakeBoost();
            closeCall = MakeCloseCall();
            streakBreak = MakeStreakBreak();
            // Sağlam el kademeleri: do-re-mi-sol-la (pentatonik), her kademe bir nota yukarı
            float[] notes = { 784f, 880f, 988f, 1175f, 1319f, 1568f };
            for (int i = 0; i < streakNotes.Length; i++) streakNotes[i] = MakeBell("streak" + i, notes[i], 0.5f);
            fanfare = MakeArpeggio("fanfare", new[] { 523f, 659f, 784f, 1047f }, 0.11f, 1.1f);
            record = MakeArpeggio("record", new[] { 784f, 988f, 1175f }, 0.09f, 0.7f);
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

        /// Bardak: danger (0..1) süt ağza yaklaştıkça çalkalanma sesi yükselir (dökülmeden önce kulakla uyarı);
        /// spillRate (bardak oranı/s) dökülürken akan süt sesi.
        public void Milk(float danger, float spillRate, bool running, float dt)
        {
            float s = running ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.95f, danger)) : 0f;
            Fade(slosh, s * 0.55f, dt, 8f);
            slosh.pitch = 0.85f + 0.4f * s;
            float p = running ? Mathf.Clamp01(spillRate * 6f) : 0f;
            Fade(pour, p * 0.7f, dt, p > pour.volume ? 25f : 6f);
            pour.pitch = 0.9f + 0.3f * p;
        }

        public void Twang(float strength) => Play(twang, 0.4f + 0.6f * strength, 0.9f + strength * 0.25f);
        public void Thud(float impact) => Play(thud, Mathf.Clamp01(0.25f + impact / 10f), Vary(0.06f));
        /// Süt sıçraması: üç çeşitten biri, miktarla yükselir.
        public void Splash(float amount) => Play(splashes[rng.Next(splashes.Length)], Mathf.Clamp01(0.35f + amount), Vary(0.08f));

        // Arayüz sesleri
        public void Click() => Play(pop, 0.5f, 1f);
        public void Coin() => Play(coin, 0.6f, 1f);
        public void Pop() => Play(pop, 1f, 1f);
        public void Deny() => Play(deny, 0.5f, 1f);

        /// Coin toplama: art arda toplandıkça perde yükselir.
        public void Pickup(int streak) => Play(pickup, 0.45f, (1f + Mathf.Min(streak, 12) * 0.045f) * Vary(0.02f));
        public void Gem() => Play(gem, 0.8f, 1f);
        public void Boost() => Play(boost, 0.7f, Vary(0.05f));
        /// Sağlam el çarpanı bir kademe arttı (1..6).
        public void StreakUp(int level) => Play(streakNotes[Mathf.Clamp(level - 1, 0, streakNotes.Length - 1)], 0.5f, 1f);
        public void StreakBreak() => Play(streakBreak, 0.6f, 1f);
        public void CloseCall() => Play(closeCall, 0.75f, 1f);
        public void Fanfare() => Play(fanfare, 0.7f, 1f);
        public void Record() => Play(record, 0.65f, 1f);
        /// Bitişte bardakta hiç süt eksilmedi: parlak elmas parıltısı, biraz tiz.
        public void NotADrop() => Play(gem, 0.8f, 1.25f);

        /// Ses açıkken genel ses düzeyi (otomatik test oyunları kısık sesle oynar).
        public static float MaxVolume = 1f;

        public static void SetEnabled(bool on) => AudioListener.volume = on ? MaxVolume : 0f;

        void Play(AudioClip clip, float volume, float pitch)
        {
            // Boş kaynak yoksa en eski sırayla yeniden kullanılır.
            AudioSource s = null;
            for (int i = 0; i < PoolSize && s == null; i++)
            {
                var c = pool[(next + i) % PoolSize];
                if (!c.isPlaying) s = c;
            }
            if (s == null) s = pool[next];
            next = (next + 1) % PoolSize;
            s.pitch = pitch;
            s.PlayOneShot(clip, volume);
        }

        float Vary(float amount) => 1f + amount * Noise();

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

        /// Döngülü seslerin başı ve sonu yumuşakça birleşir (tık sesi olmasın).
        static void LoopFade(float[] d, float seconds = 0.02f)
        {
            int n = Mathf.Min(d.Length / 4, (int)(Rate * seconds));
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                d[i] *= k;
                d[d.Length - 1 - i] *= k;
            }
        }

        /// Damla: perdesi hızla yükselen kısa sinüs (gerçek damlanın "blop" sesi gibi).
        void AddDrop(float[] d, int start, float f0, float f1, float len, float gain)
        {
            float phase = 0f;
            int n = (int)(len * Rate);
            for (int i = 0; i < n && start + i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float k = t / len;
                float f = Mathf.Lerp(f0, f1, k * k);
                phase += f / Rate * Mathf.PI * 2f;
                float env = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-t * 3f / len);
                d[start + i] += Mathf.Sin(phase) * env * gain;
            }
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

        // Bardakta çalkalanma: yavaş dalgalanan boğuk sıvı sesi + ara sıra kabarcık. Süre 2 s, dalga 4 Hz (döngüde düz).
        AudioClip MakeSlosh()
        {
            var d = new float[Rate * 2];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                lp += (Noise() - lp) * 0.06f;
                lp2 += (lp - lp2) * 0.1f;
                float wave = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 4f);
                d[i] = (lp - lp2 * 0.6f) * wave * wave * 1.6f;
            }
            for (int k = 0; k < 14; k++)
            {
                float at = (float)rng.NextDouble() * 1.9f;
                AddDrop(d, (int)(at * Rate), 180f + 160f * (float)rng.NextDouble(), 420f + 300f * (float)rng.NextDouble(), 0.05f, 0.35f);
            }
            LoopFade(d);
            return Clip("slosh", d);
        }

        // Süt akışı (dökülürken): sık damla yağmuru + akan su hışırtısı.
        AudioClip MakePour()
        {
            var d = new float[Rate];
            float hp = 0f, prev = 0f, lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float n = Noise();
                hp = 0.9f * (hp + n - prev);
                prev = n;
                lp += (hp - lp) * 0.35f;
                d[i] = lp * 0.35f;
            }
            for (int k = 0; k < 40; k++)
            {
                float at = (float)rng.NextDouble() * 0.96f;
                AddDrop(d, (int)(at * Rate), 500f + 600f * (float)rng.NextDouble(), 1200f + 900f * (float)rng.NextDouble(), 0.035f, 0.5f);
            }
            LoopFade(d);
            return Clip("pour", d);
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

        // Süt sıçraması: ıslak "şlap" (kısa boğuk gürültü patlaması) + etrafa saçılan damlalar. Çeşide göre damla sayısı/perdesi.
        AudioClip MakeSplash(int variant)
        {
            var d = new float[(int)(Rate * 0.5f)];
            float lp = 0f, hp = 0f, prev = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float n = Noise();
                lp += (n - lp) * 0.25f;
                hp = 0.8f * (hp + lp - prev);
                prev = lp;
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 22f);
                d[i] = hp * env * 1.3f + n * 0.25f * Mathf.Exp(-t * 40f);
            }
            int drops = 5 + variant * 2;
            for (int k = 0; k < drops; k++)
            {
                float at = 0.02f + (float)rng.NextDouble() * (0.18f + 0.06f * variant);
                float f0 = 450f + 500f * (float)rng.NextDouble() + variant * 80f;
                AddDrop(d, (int)(at * Rate), f0, f0 * (2.2f + (float)rng.NextDouble()), 0.03f + 0.03f * (float)rng.NextDouble(),
                        0.7f * Mathf.Exp(-at * 4f));
            }
            return Clip("splash" + variant, d);
        }

        // Hızlandırıcı: yükselen hava süpürmesi ("vuuş")
        AudioClip MakeBoost()
        {
            var d = new float[(int)(Rate * 0.6f)];
            float lp = 0f, band = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / d.Length;
                float cut = 0.03f + 0.25f * t;   // süzgeç açılır: perde yükselir
                lp += (Noise() - lp) * cut;
                band += (lp - band) * cut * 0.5f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.6f));
                d[i] = (lp - band) * env;
            }
            return Clip("boost", d);
        }

        // Kıl payı: kısa nefes (süpürme) + yükselen iki nota "fiyuu-tiing"
        AudioClip MakeCloseCall()
        {
            var d = new float[(int)(Rate * 0.7f)];
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                lp += (Noise() - lp) * 0.12f;
                d[i] = lp * Mathf.Exp(-t * 10f) * 0.5f;
                for (int k = 0; k < 2; k++)
                {
                    float local = t - 0.08f - k * 0.09f;
                    if (local < 0f) continue;
                    float f = k == 0 ? 1175f : 1568f;
                    d[i] += (Mathf.Sin(local * f * Mathf.PI * 2f) + 0.3f * Mathf.Sin(local * f * 3f * Mathf.PI * 2f)) * Mathf.Exp(-local * 6f) * 0.6f;
                }
            }
            return Clip("closecall", d);
        }

        // Çarpan bozuldu: perdesi düşen, hafif titreyen "bluup"
        AudioClip MakeStreakBreak()
        {
            var d = new float[(int)(Rate * 0.45f)];
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float f = 620f * Mathf.Exp(-t * 3.2f) * (1f + 0.04f * Mathf.Sin(t * 60f));
                phase += f / Rate * Mathf.PI * 2f;
                d[i] = (Mathf.Sin(phase) + 0.25f * Mathf.Sin(phase * 2f)) * Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 5f);
            }
            return Clip("streakbreak", d);
        }

        // Çan: temel + kesirli üst tınılar, yumuşak sönüş
        AudioClip MakeBell(string name, float f, float seconds)
        {
            var d = new float[(int)(Rate * seconds)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float w = t * f * Mathf.PI * 2f;
                d[i] = (Mathf.Sin(w) + 0.4f * Mathf.Sin(w * 2.01f) * Mathf.Exp(-t * 6f) + 0.2f * Mathf.Sin(w * 3.98f) * Mathf.Exp(-t * 12f))
                       * Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t * 7f);
            }
            return Clip(name, d);
        }

        // Arpej + son nota uzun (bitiş fanfarı / rekor)
        AudioClip MakeArpeggio(string name, float[] notes, float step, float seconds)
        {
            var d = new float[(int)(Rate * seconds)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float v = 0f;
                for (int k = 0; k < notes.Length; k++)
                {
                    float local = t - k * step;
                    if (local < 0f) continue;
                    bool last = k == notes.Length - 1;
                    float w = local * notes[k] * Mathf.PI * 2f;
                    // Üçgene yakın tını (tek harmonikler azalan): sıcak, oyuncak boru sesi
                    float tone = Mathf.Sin(w) + 0.33f * Mathf.Sin(3f * w) * 0.33f + 0.2f * Mathf.Sin(2f * w);
                    v += tone * Mathf.Min(1f, local / 0.006f) * Mathf.Exp(-local * (last ? 2.5f : 9f));
                }
                d[i] = v;
            }
            return Clip(name, d);
        }
    }
}
