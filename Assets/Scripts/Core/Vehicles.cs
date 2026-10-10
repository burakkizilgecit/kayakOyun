namespace SledSurfers
{
    /// Pistin aracı (kullanıcı kararı 2026-10-09: son üç haritada aracın şekli tamamen değişir).
    /// Kızak: sörf tahtalı kızak (Çayır–Karlı Dağ). Kum sörfü: raysız geniş tahta (Çöl). Bobsled: kapalı obsidyen
    /// kapsül (Volkan). Hover: yerden süzülen, altı ışıklı yastıklı platform (Mars).
    public enum VehicleKind { Sled, Sandboard, Bobsled, Hover }

    public static class Vehicles
    {
        /// Araç kartının adı (Kızak kartı pistin aracını taşır).
        public static string CardName(VehicleKind v)
        {
            switch (v)
            {
                case VehicleKind.Sandboard: return "Sandboard";
                case VehicleKind.Bobsled: return "Bobsled";
                case VehicleKind.Hover: return "Hover";
                default: return "Sled";
            }
        }

        /// Aracın aşama adları (0..6): kart altında ve aşama kutlamasında görünür.
        public static string StageName(VehicleKind v, int stage)
        {
            string[] names;
            switch (v)
            {
                case VehicleKind.Sandboard:
                    names = new[] { "Wooden Board", "Foot Straps", "Sunset Paint", "Big Fins", "Small Wings", "Wind Wings", "Golden Dune" };
                    break;
                case VehicleKind.Bobsled:
                    names = new[] { "Obsidian Pod", "Lava Stripes", "Windshield", "Tail Fin", "Small Wings", "Magma Wings", "Golden Pod" };
                    break;
                case VehicleKind.Hover:
                    names = new[] { "Hover Platform", "Antenna", "Side Thrusters", "Blue Hull", "Small Wings", "Light Ring", "Golden Hover" };
                    break;
                default:
                    names = new[] { "Surf Sled", "Grip Rope", "Tiny Wings", "Small Wings", "Wide Wings", "Big Wings", "Golden Wings" };
                    break;
            }
            return names[UnityEngine.Mathf.Clamp(stage, 0, names.Length - 1)];
        }
    }
}
