using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Kalıcı parçaların görünüşü (Upgrades.PartNames): kızağın arkasına aero kuyruk, pervane, turbo pervane (halkalı),
    /// çift turbo pervane ve jet motoru. Her araçta (kızak, kum sörfü, bobsled, hover) aynı yerde, arka uçta durur.
    /// Kullanıcı isteği (2026-10-10): küplerden kurulu ilk sürüm kabaydı; parçalar torna yüzeyli gövde, burun konisi,
    /// uca doğru incelen burulmuş kanatlar, kanat profilli halka ve geriye yatık yüzgeçlerle yeniden kuruldu.
    /// Pervaneler hızla döner (hızlanınca dönme bulanıklığı diski belirir); jet motorunda hızla büyüyen alev.
    public class PartAddons
    {
        const float BackZ = -0.72f, BaseY = 0.26f;   // kök 1.3 ölçekli: arka uç ~-0.95 m

        readonly Transform root, tail, prop, turbo, twin, jet;
        readonly List<Transform> rotors = new List<Transform>();
        readonly List<Renderer> blurs = new List<Renderer>();
        readonly List<Transform> flames = new List<Transform>();
        readonly Material blurMat;
        float spin;

        static readonly Quaternion AlongZ = Quaternion.Euler(90f, 0f, 0f);   // torna ekseni (y) → ileri (z)

        public PartAddons(Transform parent)
        {
            root = new GameObject("Part Addons").transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * 1.3f;   // koşu kamerasından seçilsin
            var white = Mats.Solid(new Color(0.97f, 0.97f, 0.99f), 0.6f);
            var red = Mats.Solid(new Color(0.9f, 0.2f, 0.16f), 0.55f);
            var dark = Mats.Solid(new Color(0.16f, 0.18f, 0.23f), 0.7f);
            var steel = Mats.Solid(new Color(0.72f, 0.75f, 0.8f), 0.85f);
            var orange = Mats.Solid(new Color(1f, 0.56f, 0.12f), 0.6f);
            var yellow = Mats.Solid(new Color(1f, 0.84f, 0.2f), 0.5f);
            var flame = Mats.Glow(new Color(1f, 0.6f, 0.15f), new Color(1f, 0.45f, 0.05f) * 2f);
            var core = Mats.Glow(new Color(1f, 0.92f, 0.6f), new Color(1f, 0.85f, 0.4f) * 2.2f);
            blurMat = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 1f, 1f, 0f) };

            // ---- Aero kuyruk: geriye yatık dikey yüzgeç (ucu kırmızı) + iki yatay dengeleyici
            tail = Group("Aero Tail");
            Part(tail, "Fin", Fin(0.3f, 0.15f, 0.3f, 0.15f, 0.03f), white, new Vector3(0f, 0.02f, 0.1f), Quaternion.identity);
            Part(tail, "Fin Tip", Fin(0.15f, 0.11f, 0.07f, 0.035f, 0.034f), red, new Vector3(0f, 0.32f, -0.05f), Quaternion.identity);
            for (int s = -1; s <= 1; s += 2)
            {
                var st = Part(tail, "Stabilizer", Fin(0.2f, 0.1f, 0.24f, 0.08f, 0.024f), white, new Vector3(0.02f * s, 0.08f, 0.06f),
                              Quaternion.Euler(0f, 0f, -84f * s));
                Part(st, "Tip", Fin(0.1f, 0.08f, 0.04f, 0.02f, 0.028f), red, new Vector3(0f, 0.24f, -0.08f), Quaternion.identity);
            }

            // ---- Pervane: damla gövdeli motor, burun konisi, üç kanat
            prop = Group("Propeller");
            // Kullanıcı (2026-10-10): direk üstündeki pervane karakteri arkadan kapatıyordu. Motor kızağın arka ucuna,
            // tahta hizasına alındı (karakterin belinin altında kalır); pervane yere değmeyecek kadar küçük.
            Mats.Prim(PrimitiveType.Cube, "Mount", prop, new Vector3(0f, 0.01f, 0.08f), new Vector3(0.07f, 0.045f, 0.22f), white);
            Engine(prop, new Vector3(0f, 0.02f, -0.1f), 0.07f, 0.3f, white, red);
            rotors.Add(Rotor(prop, new Vector3(0f, 0.02f, -0.27f), 0.19f, 3, 0.07f, steel, yellow, red));

            // ---- Turbo pervane: kanat profilli halka içinde beş kanat, üç taşıyıcı kol
            turbo = Group("Turbo Propeller");
            Mats.Prim(PrimitiveType.Cube, "Mount", turbo, new Vector3(0f, 0.01f, 0.08f), new Vector3(0.07f, 0.045f, 0.22f), white);
            Engine(turbo, new Vector3(0f, 0.03f, -0.1f), 0.06f, 0.28f, white, orange);
            DuctedFan(turbo, new Vector3(0f, 0.03f, -0.22f), 0.165f, orange, steel, dark, yellow, red);

            // ---- Çift turbo pervane: gövdeden iki yana uzanan kanatçıkların ucunda birer halkalı pervane
            twin = Group("Twin Turbo");
            for (int s = -1; s <= 1; s += 2)
            {
                Mats.Prim(PrimitiveType.Cube, "Arm", twin, new Vector3(0.17f * s, 0.01f, 0.04f), new Vector3(0.34f, 0.04f, 0.12f), white);
                Engine(twin, new Vector3(0.3f * s, 0.02f, -0.06f), 0.05f, 0.24f, white, orange);
                DuctedFan(twin, new Vector3(0.3f * s, 0.02f, -0.17f), 0.14f, orange, steel, dark, yellow, red);
            }

            // ---- Jet motoru: iki jet (giriş dudağı, gövde, daralan lüle), ucunda çift katlı alev
            jet = Group("Jet");
            for (int s = -1; s <= 1; s += 2)
            {
                var c = new Vector3(0.3f * s, 0.05f, -0.12f);   // jetler de tahta hizasında, arkada
                Mats.Prim(PrimitiveType.Cube, "Mount", jet, new Vector3(0.17f * s, 0.02f, 0.02f), new Vector3(0.3f, 0.04f, 0.14f), white);
                // Gövde: önde yuvarlak giriş dudağı, ortada geniş, arkada daralan lüle
                Part(jet, "Body", GlassModels.Lathe(new[]
                {
                    P(0.075f, 0.22f), P(0.1f, 0.2f), P(0.115f, 0.12f), P(0.118f, 0f), P(0.11f, -0.12f), P(0.085f, -0.22f),
                }, 20), white, c, AlongZ);
                Part(jet, "Intake Lip", GlassModels.Lathe(new[] { P(0.07f, 0.2f), P(0.078f, 0.235f), P(0.1f, 0.225f), P(0.104f, 0.19f) }, 20), red, c, AlongZ);
                Part(jet, "Intake", GlassModels.Lathe(new[] { P(0f, 0.16f), P(0.072f, 0.16f), P(0.076f, 0.22f) }, 20), dark, c, AlongZ);
                Part(jet, "Intake Cone", GlassModels.Lathe(new[] { P(0f, 0.25f), P(0.03f, 0.19f), P(0.034f, 0.16f) }, 14), steel, c, AlongZ);
                Part(jet, "Stripe", GlassModels.Lathe(new[] { P(0.119f, 0.04f), P(0.121f, 0.02f), P(0.121f, -0.02f), P(0.119f, -0.04f) }, 20), orange, c, AlongZ);
                Part(jet, "Nozzle", GlassModels.Lathe(new[] { P(0.086f, -0.2f), P(0.09f, -0.24f), P(0.072f, -0.3f), P(0.062f, -0.3f), P(0.07f, -0.22f) }, 20), steel, c, AlongZ);
                Part(jet, "Nozzle Inside", GlassModels.Lathe(new[] { P(0f, -0.23f), P(0.07f, -0.23f) }, 16), dark, c, AlongZ);
                var f = Part(jet, "Flame", GlassModels.Lathe(new[] { P(0f, 0f), P(0.05f, -0.03f), P(0.058f, -0.1f), P(0.03f, -0.26f), P(0f, -0.36f) }, 12), flame,
                             c + new Vector3(0f, 0f, -0.28f), AlongZ);
                Part(f, "Core", GlassModels.Lathe(new[] { P(0f, 0f), P(0.026f, -0.03f), P(0.022f, -0.12f), P(0f, -0.2f) }, 10), core, Vector3.zero, Quaternion.identity);
                flames.Add(f);
            }
            Show(0);
        }

        static Vector2 P(float r, float y) => new Vector2(r, y);

        Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(0f, BaseY, BackZ);
            return t;
        }

        static Transform Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = rot;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return t;
        }

        /// Motoru taşıyan ince, geriye yatık direk.
        static void Pylon(Transform parent, Vector3 basePos, float height, Material mat) =>
            Part(parent, "Pylon", Fin(0.16f, 0.1f, height, 0.06f, 0.035f), mat, basePos, Quaternion.identity);

        /// Damla biçimli motor gövdesi (önü yuvarlak, arkası pervaneye doğru daralır) ve renkli bant.
        static void Engine(Transform parent, Vector3 center, float radius, float length, Material body, Material band)
        {
            float h = length * 0.5f;
            Part(parent, "Engine", GlassModels.Lathe(new[]
            {
                P(0f, h), P(radius * 0.55f, h * 0.9f), P(radius * 0.9f, h * 0.62f), P(radius, h * 0.2f), P(radius * 0.96f, -h * 0.4f),
                P(radius * 0.8f, -h), P(0f, -h),
            }, 18), body, center, AlongZ);
            Part(parent, "Engine Band", GlassModels.Lathe(new[]
            {
                P(radius * 1.005f, h * 0.3f), P(radius * 1.03f, h * 0.24f), P(radius * 1.03f, h * 0.06f), P(radius * 1.005f, 0f),
            }, 18), band, center, AlongZ);
        }

        /// Arkaya bakan pervane: burun konisi, kanatlar ve hızlanınca beliren dönme bulanıklığı diski.
        Transform Rotor(Transform parent, Vector3 pos, float radius, int blades, float hubRadius, Material bladeMat, Material tipMat, Material hubMat)
        {
            var r = new GameObject("Rotor").transform;
            r.SetParent(parent, false);
            r.localPosition = pos;
            // Burun konisi (arkaya, -z'ye sivrilir)
            Part(r, "Spinner", GlassModels.Lathe(new[]
            {
                P(hubRadius * 0.82f, 0.03f), P(hubRadius * 0.8f, 0f), P(hubRadius * 0.62f, -0.05f), P(hubRadius * 0.3f, -0.1f), P(0f, -0.125f),
            }, 16), hubMat, Vector3.zero, AlongZ);
            var blade = Blade(hubRadius * 0.5f, radius, radius * 0.3f, 0f, 0.86f);
            var tip = Blade(hubRadius * 0.5f, radius, radius * 0.3f, 0.86f, 1f);
            for (int i = 0; i < blades; i++)
            {
                var q = Quaternion.Euler(0f, 0f, i * 360f / blades);
                Part(r, "Blade", blade, bladeMat, Vector3.zero, q);
                Part(r, "Blade Tip", tip, tipMat, Vector3.zero, q);
            }
            // Dönme bulanıklığı: ince saydam disk (pervanenin hemen önünde, dönmez görünür)
            var blur = Part(parent, "Blur", Disc(hubRadius * 0.8f, radius * 0.98f, 28), blurMat, pos + new Vector3(0f, 0f, -0.01f), Quaternion.identity);
            var br = blur.GetComponent<MeshRenderer>();
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            br.receiveShadows = false;
            blurs.Add(br);
            return r;
        }

        /// Halkalı pervane: kanat profilli halka, üç taşıyıcı kol ve beş kanatlı pervane.
        void DuctedFan(Transform parent, Vector3 pos, float radius, Material ductMat, Material rimMat, Material bladeMat, Material tipMat, Material hubMat)
        {
            float ri = radius, ro = radius + 0.032f;
            // Halka kesiti: önü (giriş) yuvarlak ve kalın, arkası ince (kanat profili gibi)
            Part(parent, "Duct", GlassModels.Lathe(new[]
            {
                P(ri, 0.075f), P(ri + 0.014f, 0.092f), P(ro, 0.07f), P(ro + 0.004f, 0.02f), P(ro - 0.006f, -0.06f), P(ri + 0.006f, -0.09f),
                P(ri, -0.06f), P(ri - 0.004f, 0f), P(ri, 0.075f),
            }, 28), ductMat, pos, AlongZ);
            Part(parent, "Duct Rim", GlassModels.Lathe(new[] { P(ri + 0.002f, 0.082f), P(ri + 0.014f, 0.098f), P(ro - 0.004f, 0.08f) }, 28), rimMat, pos, AlongZ);
            for (int k = 0; k < 3; k++)
            {
                var q = Quaternion.Euler(0f, 0f, 60f + k * 120f);
                Part(parent, "Strut", Fin(0.07f, 0.05f, radius, 0f, 0.016f), rimMat, pos + q * new Vector3(0f, 0f, 0.07f), q);
            }
            rotors.Add(Rotor(parent, pos + new Vector3(0f, 0f, -0.03f), radius - 0.014f, 5, Mathf.Min(0.062f, radius * 0.3f), rimMat, tipMat, hubMat));
        }

        /// Pervane kanadı (+y yönünde, r0 → r1): uca doğru daralır, kökte dik uçta yatık (burulmuş). t0..t1: kanadın bu
        /// kesri (uç parçası ayrı renkte kurulabilsin). İki yüzlü.
        static Mesh Blade(float r0, float r1, float chord, float t0, float t1)
        {
            const int n = 8;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= n; i++)
            {
                float t = Mathf.Lerp(t0, t1, i / (float)n);
                float r = Mathf.Lerp(r0, r1, t);
                // Kord: kökte dar, 1/3'te en geniş, uçta yuvarlanarak kapanır
                float c = chord * (0.45f + 1.1f * t) * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(t, 5f))) * 0.5f;
                float twist = Mathf.Lerp(48f, 16f, t) * Mathf.Deg2Rad;
                var half = new Vector3(Mathf.Cos(twist) * c, 0f, Mathf.Sin(twist) * c);
                verts.Add(new Vector3(0f, r, 0f) - half);
                verts.Add(new Vector3(0f, r, 0f) + half);
            }
            for (int i = 0; i < n; i++)
            {
                int a = i * 2;
                tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
            return Build("Blade", verts, tris, true);
        }

        /// Geriye yatık yüzgeç / kanatçık: tabanı (x-z düzleminde) baseChord uzunluğunda, +y yönünde height kadar
        /// yükselir, uç kordu tipChord, uç sweep kadar geride (-z). thickness: x yönünde kalınlık.
        static Mesh Fin(float baseChord, float tipChord, float height, float sweep, float thickness)
        {
            float h = thickness * 0.5f;
            // Yan görünüş (z, y): taban ön, taban arka, uç arka, uç ön
            var side = new[]
            {
                new Vector2(baseChord * 0.5f, 0f), new Vector2(-baseChord * 0.5f, 0f),
                new Vector2(-sweep - tipChord * 0.5f, height), new Vector2(-sweep + tipChord * 0.5f, height),
            };
            var verts = new List<Vector3>();
            var tris = new List<int>();
            // İki yan yüz (düz gölgeleme için kendi köşeleri)
            for (int s = -1; s <= 1; s += 2)
            {
                int b = verts.Count;
                // Kenarlara doğru incelsin: ön ve arka kenar orta düzleme yaklaşır
                foreach (var p in side) verts.Add(new Vector3(s * h, p.y, p.x));
                if (s > 0) tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                else tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            // Kenar şeritleri (ön, alt, arka, üst)
            for (int i = 0; i < 4; i++)
            {
                var p = side[i];
                var q = side[(i + 1) % 4];
                int b = verts.Count;
                verts.Add(new Vector3(-h, p.y, p.x));
                verts.Add(new Vector3(h, p.y, p.x));
                verts.Add(new Vector3(-h, q.y, q.x));
                verts.Add(new Vector3(h, q.y, q.x));
                tris.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
            }
            return Build("Fin", verts, tris, true);
        }

        /// Düz halka disk (x-y düzleminde, z'ye bakar), iki yüzlü.
        static Mesh Disc(float inner, float outer, int sides)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int k = 0; k <= sides; k++)
            {
                float a = 2f * Mathf.PI * k / sides;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                verts.Add(d * inner);
                verts.Add(d * outer);
            }
            for (int k = 0; k < sides; k++)
            {
                int a = k * 2;
                tris.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }
            return Build("Disc", verts, tris, true);
        }

        /// twoSided: arka yüzler kendi köşeleriyle eklenir (aynı köşeleri paylaşan ön/arka yüzün normalleri birbirini
        /// götürüp yüzeyi simsiyah yapıyordu).
        static Mesh Build(string name, List<Vector3> verts, List<int> tris, bool twoSided = false)
        {
            if (twoSided)
            {
                int n = verts.Count, m = tris.Count;
                for (int i = 0; i < n; i++) verts.Add(verts[i]);
                for (int i = 0; i < m; i += 3) { tris.Add(tris[i] + n); tris.Add(tris[i + 2] + n); tris.Add(tris[i + 1] + n); }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// parts: açılan parça sayısı (1 = yalnız kanat; kanat RiderView'da). Pervaneler birbirinin yerini alır.
        public void Show(int parts)
        {
            tail.gameObject.SetActive(parts >= 2);
            prop.gameObject.SetActive(parts == 3);
            turbo.gameObject.SetActive(parts == 4);
            twin.gameObject.SetActive(parts == 5);
            jet.gameObject.SetActive(parts >= 6);
            // Kuyrukla pervane çakışmasın: pervaneli kademelerde kuyruk biraz öne
            tail.localPosition = new Vector3(0f, BaseY, BackZ);   // pervaneler altta: kuyrukla çakışmaz
        }

        /// speed: m/s. Pervaneler hızla döner (dururken yavaş); hızlanınca kanatların yerini dönme bulanıklığı alır.
        public void Tick(float speed, float dt)
        {
            spin += (300f + speed * 160f) * dt;
            foreach (var r in rotors) r.localRotation = Quaternion.Euler(0f, 0f, spin);
            blurMat.color = new Color(1f, 1f, 1f, 0.2f * Mathf.Clamp01((speed - 3f) / 10f));
            float k = Mathf.Clamp01(speed / 20f);
            foreach (var f in flames) f.localScale = new Vector3(1f, 0.6f + 0.6f * k + 0.12f * Random.value, 1f);
        }
    }
}
