using System.Collections.Generic;
using UnityEngine;

namespace SledSurfers
{
    /// Kalıcı parçaların görünüşü (Upgrades.PartNames): kızağın arkasına aero kuyruk, pervane, turbo pervane (halkalı),
    /// çift turbo pervane ve jet motoru. Her araçta (kızak, kum sörfü, bobsled, hover) aynı yerde, arka uçta durur.
    /// Pervaneler hızla döner; jet motorunda hız arttıkça parlayan alev.
    public class PartAddons
    {
        const float BackZ = -0.72f, BaseY = 0.26f;   // kök 1.3 ölçekli: arka uç ~-0.95 m

        readonly Transform root, tail, prop, turbo, twin, jet;
        readonly List<Transform> rotors = new List<Transform>();
        readonly List<Transform> flames = new List<Transform>();
        float spin;

        public PartAddons(Transform parent)
        {
            root = new GameObject("Part Addons").transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * 1.3f;   // koşu kamerasından seçilsin
            var white = Mats.Solid(new Color(0.96f, 0.96f, 0.98f), 0.45f);
            var red = Mats.Solid(new Color(0.92f, 0.24f, 0.18f), 0.4f);
            var dark = Mats.Solid(new Color(0.2f, 0.22f, 0.27f), 0.6f);
            var orange = Mats.Solid(new Color(1f, 0.55f, 0.12f), 0.45f);
            var blade = Mats.Solid(new Color(0.85f, 0.87f, 0.9f), 0.7f);
            var flame = Mats.Glow(new Color(1f, 0.6f, 0.15f), new Color(1f, 0.45f, 0.05f) * 2f);

            // Aero kuyruk: dikey kanatçık + iki yatay dengeleyici (videodaki "aero tail")
            tail = Group("Aero Tail");
            Mats.Prim(PrimitiveType.Cube, "Fin", tail, new Vector3(0f, 0.28f, -0.05f), new Vector3(0.05f, 0.42f, 0.36f), white)
                .localRotation = Quaternion.Euler(-18f, 0f, 0f);
            Mats.Prim(PrimitiveType.Cube, "Fin Tip", tail, new Vector3(0f, 0.48f, -0.13f), new Vector3(0.06f, 0.08f, 0.26f), red)
                .localRotation = Quaternion.Euler(-18f, 0f, 0f);
            for (int s = -1; s <= 1; s += 2)
                Mats.Prim(PrimitiveType.Cube, "Stabilizer", tail, new Vector3(0.24f * s, 0.07f, -0.02f), new Vector3(0.42f, 0.04f, 0.24f), white)
                    .localRotation = Quaternion.Euler(0f, -12f * s, 6f * s);

            // Pervane: arkada direk üstünde iki kanatlı açık pervane
            prop = Group("Propeller");
            Mats.Prim(PrimitiveType.Cylinder, "Mast", prop, new Vector3(0f, 0.16f, 0.05f), new Vector3(0.06f, 0.16f, 0.06f), dark);
            rotors.Add(Rotor(prop, new Vector3(0f, 0.36f, -0.02f), 0.34f, 2, blade, red));

            // Turbo pervane: turuncu halka içinde dört kanatlı pervane
            turbo = Group("Turbo Propeller");
            Mats.Prim(PrimitiveType.Cylinder, "Mast", turbo, new Vector3(0f, 0.14f, 0.05f), new Vector3(0.07f, 0.14f, 0.07f), dark);
            Duct(turbo, new Vector3(0f, 0.38f, -0.02f), 0.3f, orange);
            rotors.Add(Rotor(turbo, new Vector3(0f, 0.38f, -0.02f), 0.26f, 4, blade, red));

            // Çift turbo pervane: yan yana iki halkalı pervane, aralarında kiriş
            twin = Group("Twin Turbo");
            Mats.Prim(PrimitiveType.Cube, "Beam", twin, new Vector3(0f, 0.3f, 0.02f), new Vector3(0.9f, 0.06f, 0.1f), dark);
            for (int s = -1; s <= 1; s += 2)
            {
                Mats.Prim(PrimitiveType.Cylinder, "Mast", twin, new Vector3(0.18f * s, 0.14f, 0.04f), new Vector3(0.06f, 0.14f, 0.06f), dark);
                Duct(twin, new Vector3(0.42f * s, 0.36f, -0.02f), 0.24f, orange);
                rotors.Add(Rotor(twin, new Vector3(0.42f * s, 0.36f, -0.02f), 0.21f, 4, blade, red));
            }

            // Jet motoru: iki koyu nozul, ucunda alev (hızla büyür)
            jet = Group("Jet");
            for (int s = -1; s <= 1; s += 2)
            {
                var body = Mats.Prim(PrimitiveType.Cylinder, "Nozzle", jet, new Vector3(0.3f * s, 0.2f, -0.05f), new Vector3(0.2f, 0.22f, 0.2f), dark);
                body.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var ring = Mats.Prim(PrimitiveType.Cylinder, "Ring", jet, new Vector3(0.3f * s, 0.2f, -0.27f), new Vector3(0.24f, 0.03f, 0.24f), orange);
                ring.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var f = Mats.Prim(PrimitiveType.Sphere, "Flame", jet, new Vector3(0.3f * s, 0.2f, -0.42f), new Vector3(0.14f, 0.14f, 0.3f), flame);
                flames.Add(f);
            }
            Show(0);
        }

        Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(0f, BaseY, BackZ);
            return t;
        }

        /// Arkaya bakan (z ekseni etrafında dönen) pervane: göbek ve blades adet kanat.
        static Transform Rotor(Transform parent, Vector3 pos, float radius, int blades, Material bladeMat, Material hubMat)
        {
            var r = new GameObject("Rotor").transform;
            r.SetParent(parent, false);
            r.localPosition = pos;
            var hub = Mats.Prim(PrimitiveType.Sphere, "Hub", r, Vector3.zero, new Vector3(0.09f, 0.09f, 0.12f), hubMat);
            hub.localRotation = Quaternion.identity;
            for (int i = 0; i < blades; i++)
            {
                var b = Mats.Prim(PrimitiveType.Cube, "Blade", r, Vector3.zero, new Vector3(0.07f, radius, 0.015f), bladeMat);
                b.localRotation = Quaternion.Euler(0f, 0f, i * 360f / blades) * Quaternion.Euler(0f, 18f, 0f);
                b.localPosition = b.localRotation * new Vector3(0f, radius * 0.5f, 0f);
            }
            return r;
        }

        /// Halka (8 parçalı çember): turbo pervanenin kanalı.
        static void Duct(Transform parent, Vector3 pos, float radius, Material mat)
        {
            const int n = 10;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var seg = Mats.Prim(PrimitiveType.Cube, "Duct", parent, pos + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius,
                                    new Vector3(2f * radius * Mathf.Tan(Mathf.PI / n) + 0.02f, 0.05f, 0.14f), mat);
                seg.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
            }
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
            tail.localPosition = new Vector3(0f, BaseY, parts >= 3 ? BackZ + 0.32f : BackZ);
        }

        /// speed: m/s. Pervaneler hızla döner (dururken yavaş), jet alevi hızla büyür.
        public void Tick(float speed, float dt)
        {
            spin += (360f + speed * 140f) * dt;
            foreach (var r in rotors) r.localRotation = Quaternion.Euler(0f, 0f, spin);
            float k = Mathf.Clamp01(speed / 20f);
            foreach (var f in flames) f.localScale = new Vector3(0.14f, 0.14f, 0.18f + 0.3f * k + 0.06f * Random.value);
        }
    }
}
