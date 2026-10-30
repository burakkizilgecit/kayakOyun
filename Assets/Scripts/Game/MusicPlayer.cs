using System.Collections.Generic;
using UnityEngine;

namespace SlingRiders
{
    /// Müzik (kullanıcı 2026-10-30, ElevenLabs ile üretildi, Resources/Audio): ana ekranda temanın menü parçası, fırlatma
    /// ekranında temanın fırlatma parçası. Döngü iki kaynakla: parça bitmeden CrossFade saniye önce baştan başlayan ikinci
    /// kopyaya yumuşak geçiş (üretilen parçaların başı ve sonu dikişsiz olmayabilir). Parça değişince eskisi kısılır,
    /// yenisi kaldığı yerden açılır. Dosyası olmayan parça yerine Fallback çalar.
    public class MusicPlayer
    {
        const float Volume = 0.32f, CrossFade = 2.5f, FadeRate = 0.6f, SwitchRate = 1.6f;   // ses payı / sn
        public const string Fallback = "menu_music";

        readonly AudioSource[] src = new AudioSource[2];
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> resume = new Dictionary<string, float>();
        AudioClip clip;
        string playing;
        int cur;
        float level;
        bool enabled = true;

        public MusicPlayer(Transform parent)
        {
            var go = new GameObject("Music");
            go.transform.SetParent(parent, false);
            for (int i = 0; i < 2; i++)
            {
                src[i] = go.AddComponent<AudioSource>();
                src[i].playOnAwake = false;
                src[i].loop = false;
                src[i].spatialBlend = 0f;
                src[i].volume = 0f;
            }
        }

        public void SetEnabled(bool on) => enabled = on;

        AudioClip Get(string name)
        {
            if (!clips.TryGetValue(name, out var c))
            {
                c = Resources.Load<AudioClip>("Audio/" + name);
                clips[name] = c;
            }
            return c;
        }

        /// name: istenen parça (Resources/Audio altında); target: 0 (sus) .. 1 (tam ses).
        public void Tick(string name, float target, float dt)
        {
            if (Get(name) == null) name = Fallback;
            if (!enabled) target = 0f;
            bool switching = playing != null && name != playing;
            level = Mathf.MoveTowards(level, switching ? 0f : target, (switching ? SwitchRate : FadeRate) * dt);
            if (switching && level <= 0f)
            {
                // Eski parça sustu: kaldığı yer saklanır, yeni parça yüklenir.
                if (src[cur].isPlaying || src[cur].time > 0f) resume[playing] = src[cur].time;
                src[0].Stop();
                src[1].Stop();
                playing = null;
            }
            if (playing == null)
            {
                if (target <= 0f || !enabled) return;
                clip = Get(name);
                if (clip == null) return;
                playing = name;
                cur = 0;
                src[0].clip = src[1].clip = clip;
                src[0].time = resume.TryGetValue(name, out float t) && t < clip.length - CrossFade - 0.5f ? t : 0f;
                src[0].Play();
            }

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
                // Tamamen susunca durur; yeniden açılınca kaldığı yerden devam eder.
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
            float fade = b.isPlaying ? Mathf.Clamp01(left / CrossFade) : 1f;
            a.volume = Volume * level * Mathf.Sqrt(fade);
            b.volume = Volume * level * Mathf.Sqrt(1f - fade);
        }

        public bool DebugPlaying => playing != null && (src[0].isPlaying || src[1].isPlaying);
        public float DebugLevel => level;
        public string DebugTrack => playing;
    }
}
