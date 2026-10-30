using UnityEngine;

namespace SlingRiders
{
    /// Ana ekran müziği (kullanıcı 2026-10-30: ElevenLabs ile üretildi, Resources/Audio/menu_music). Döngü iki kaynakla:
    /// parça bitmeden CrossFade saniye önce baştan başlayan ikinci kopyaya yumuşak geçiş (üretilen parçanın başı ve
    /// sonu dikişsiz olmayabilir). Koşuda susar, nişanda kısılır; ana ekran ve sonuçta tam ses.
    public class MusicPlayer
    {
        const float Volume = 0.32f, CrossFade = 2.5f, FadeRate = 0.6f;   // FadeRate: saniyede ses payı

        readonly AudioSource[] src = new AudioSource[2];
        readonly AudioClip clip;
        int cur;
        float level, fade = 1f;
        bool enabled = true;

        public MusicPlayer(Transform parent)
        {
            clip = Resources.Load<AudioClip>("Audio/menu_music");
            var go = new GameObject("Music");
            go.transform.SetParent(parent, false);
            for (int i = 0; i < 2; i++)
            {
                src[i] = go.AddComponent<AudioSource>();
                src[i].playOnAwake = false;
                src[i].loop = false;
                src[i].spatialBlend = 0f;
                src[i].clip = clip;
                src[i].volume = 0f;
            }
        }

        public void SetEnabled(bool on) => enabled = on;

        /// target: 0 (sus) .. 1 (tam ses).
        public void Tick(float target, float dt)
        {
            if (clip == null) return;
            if (!enabled) target = 0f;
            level = Mathf.MoveTowards(level, target, FadeRate * dt);
            var a = src[cur];
            var b = src[1 - cur];
            // Geçiş bitti (ilk kopya sona erdi): ikinci kopya asıl kaynak olur.
            if (b.isPlaying && (!a.isPlaying || clip.length - a.time <= 0.02f))
            {
                a.Stop();
                cur = 1 - cur;
                a = src[cur];
                b = src[1 - cur];
            }
            if (level <= 0f)
            {
                // Tamamen susunca durur; yeniden açılınca baştan değil kaldığı yerden devam eder.
                if (a.isPlaying) a.Pause();
                b.Stop();
                return;
            }
            if (!a.isPlaying)
            {
                if (a.time > 0f && a.time < clip.length - 0.05f) a.UnPause();
                else { a.time = 0f; a.Play(); }
            }
            float left = clip.length - a.time;
            if (left < CrossFade && !b.isPlaying)
            {
                b.time = 0f;
                b.Play();
            }
            fade = b.isPlaying ? Mathf.Clamp01(left / CrossFade) : 1f;
            a.volume = Volume * level * Mathf.Sqrt(fade);
            b.volume = Volume * level * Mathf.Sqrt(1f - fade);
        }

        public bool DebugPlaying => clip != null && (src[0].isPlaying || src[1].isPlaying);
        public float DebugLevel => level;
    }
}
