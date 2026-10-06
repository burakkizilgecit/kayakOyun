using UnityEngine;

namespace SledSurfers
{
    /// Zemin için döşenebilir (tekrar eden) gri tonlu dokular. Malzeme rengiyle çarpılır: 1 = rengin kendisi,
    /// daha koyu tonlar desen verir. Düz renkli yüzeylere çim, toprak ve taş dokusu katar.
    public static class TextureKit
    {
        static Texture2D grass, dirt, rock;

        public static Texture2D Grass() => grass != null ? grass : grass = Make("Grass", 128, (x, y) =>
        {
            // Büyük lekeler + çim telleri (dikey kısa çizgiler gibi ince benekler)
            float patches = Noise(x, y, 8, 3) * 0.12f;
            float blades = Mathf.Clamp01((Noise(x, y, 64, 7) - 0.6f) * 4f) * 0.12f;
            float fine = Noise(x, y, 32, 5) * 0.08f;
            return 1f - patches - blades - fine;
        });

        public static Texture2D Dirt() => dirt != null ? dirt : dirt = Make("Dirt", 128, (x, y) =>
        {
            // Toprak: yumuşak lekeler, ince taneler ve seyrek çakıl benekleri
            float patches = Noise(x, y, 4, 11) * 0.07f;
            float grain = Noise(x, y, 64, 13) * 0.07f;
            float pebble = Mathf.Clamp01((Noise(x, y, 24, 17) - 0.84f) * 8f) * 0.07f;
            return 1f - patches - grain - pebble;
        });

        public static Texture2D Rock() => rock != null ? rock : rock = Make("Rock", 128, (x, y) =>
        {
            // Kaya: yatay katmanlar ve çatlak benekleri
            float layers = Mathf.Abs(Mathf.Sin((y / 128f + Noise(x, y, 4, 19) * 0.15f) * Mathf.PI * 6f)) * 0.1f;
            float grain = Noise(x, y, 32, 23) * 0.12f;
            return 1f - layers - grain;
        });

        static Texture2D Make(string name, int size, System.Func<int, int, float> f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
                { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4, name = name, filterMode = FilterMode.Trilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float v = Mathf.Clamp01(f(x, y));
                    px[y * size + x] = new Color(v, v, v, 1f);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// Döşenebilir değer gürültüsü: cells x cells ızgara (128 piksellik dokuya), 0..1.
        static float Noise(int x, int y, int cells, int seed)
        {
            float fx = x * cells / 128f, fy = y * cells / 128f;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Hash(x0 % cells, y0 % cells, seed), b = Hash((x0 + 1) % cells, y0 % cells, seed);
            float c = Hash(x0 % cells, (y0 + 1) % cells, seed), d = Hash((x0 + 1) % cells, (y0 + 1) % cells, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static float Hash(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }
    }
}
