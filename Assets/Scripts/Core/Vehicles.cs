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
                case VehicleKind.Sandboard: return "Kum Sörfü";
                case VehicleKind.Bobsled: return "Bobsled";
                case VehicleKind.Hover: return "Hover";
                default: return "Kızak";
            }
        }

        /// Aracın aşama adları (0..6): kart altında ve aşama kutlamasında görünür.
        public static string StageName(VehicleKind v, int stage)
        {
            string[] names;
            switch (v)
            {
                case VehicleKind.Sandboard:
                    names = new[] { "Ahşap Tahta", "Ayak Kayışı", "Gün Batımı Boyası", "Büyük Yüzgeçler", "Küçük Kanatlar", "Rüzgâr Kanatları", "Altın Kumul" };
                    break;
                case VehicleKind.Bobsled:
                    names = new[] { "Obsidyen Kapsül", "Lav Şeritleri", "Ön Siperlik", "Kuyruk Yüzgeci", "Küçük Kanatlar", "Magma Kanatları", "Altın Kapsül" };
                    break;
                case VehicleKind.Hover:
                    names = new[] { "Hover Platform", "Anten", "Yan İticiler", "Mavi Gövde", "Küçük Kanatlar", "Işık Halkası", "Altın Hover" };
                    break;
                default:
                    names = new[] { "Sörf Kızağı", "Tutma İpi", "Gümüş Kayaklar", "Arka Tampon Kanadı", "Kanatlı", "Büyük Kanatlar", "Altın Kanatlar" };
                    break;
            }
            return names[UnityEngine.Mathf.Clamp(stage, 0, names.Length - 1)];
        }
    }
}
