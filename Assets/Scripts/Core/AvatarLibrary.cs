namespace SledSurfers
{
    public enum Perk { None, Slosh, Sling, Magnet, Friction, Lid, Chest, Rocket, Armor, Income, Glide }

    /// Seçilebilir karakterler (Meshy modelleri) ve küçük avantajları. Fiyatlar pembe elmas.
    public static class AvatarLibrary
    {
        public struct Info
        {
            public string name, model, perkTitle, perkText;
            public Perk perk;
            public int price;
        }

        /// Kullanıcı kararı (2026-10-09): yalnızca Meshy 3B karakterler; oyun Panda Pofuduk ile başlar, fiyatlar artarak
        /// sıralanır (ucuzdan pahalıya). Sıra kayıtla eşleşir (avatar2/avatars2 anahtarları): yeni karakter sona eklenir.
        public static readonly Info[] All =
        {
            new Info { name = "Panda Pofuduk", model = "Meshy/Panda", perkTitle = "Başlangıç", perkText = "Sapanın tombul kahramanı", perk = Perk.None, price = 0 },
            new Info { name = "Penguen Buzlu", model = "Meshy/Penguin", perkTitle = "Buz dengesi", perkText = "Süt %10 daha az çalkalanır", perk = Perk.Slosh, price = 8 },
            new Info { name = "Kovboy Rüzgar", model = "Meshy/Cowboy", perkTitle = "Kement", perkText = "Coin toplama alanı %40 daha geniş", perk = Perk.Magnet, price = 12 },
            new Info { name = "Robot Tıkır", model = "Meshy/Robot", perkTitle = "Yağlı dişliler", perkText = "Kızak sürtünmesi %5 daha az", perk = Perk.Friction, price = 16 },
            new Info { name = "Kaptan Kanca", model = "Meshy/Pirate", perkTitle = "Hazine avcısı", perkText = "Sandık ödülleri %25 daha büyük", perk = Perk.Chest, price = 20 },
            new Info { name = "Şef Ali", model = "Meshy/Chef", perkTitle = "Usta eller", perkText = "Bardağı ölçülü doldurur: süt daha zor taşar", perk = Perk.Lid, price = 25 },
            new Info { name = "Ninja Gölge", model = "Meshy/Ninja", perkTitle = "Hafif ayak", perkText = "Kızak sürtünmesi %5 daha az", perk = Perk.Friction, price = 30 },
            new Info { name = "İtfaiyeci Alev", model = "Meshy/Firefighter", perkTitle = "Sağlam kask", perkText = "Sert inişlere %15 daha dayanıklı", perk = Perk.Armor, price = 36 },
            new Info { name = "Şövalye Arda", model = "Meshy/Knight", perkTitle = "Zırh", perkText = "Sert inişlere %15 daha dayanıklı", perk = Perk.Armor, price = 42 },
            new Info { name = "Viking Ragnar", model = "Meshy/Viking", perkTitle = "Güçlü kollar", perkText = "Sapan çıkışı %4 daha hızlı", perk = Perk.Sling, price = 50 },
            new Info { name = "Kaplan Çizgi", model = "Meshy/Tiger", perkTitle = "Pençe gücü", perkText = "Roket itişi %10 daha güçlü", perk = Perk.Rocket, price = 60 },
            new Info { name = "Büyücü Lila", model = "Meshy/Wizard", perkTitle = "Büyülü kanat", perkText = "Süzülürken kaldırma %8 daha fazla", perk = Perk.Glide, price = 72 },
            new Info { name = "Astronot Yıldız", model = "Meshy/Astronaut", perkTitle = "Uzay yürüyüşü", perkText = "Süzülürken kaldırma %8 daha fazla", perk = Perk.Glide, price = 85 },
        };

        public static int Count => All.Length;

        /// Avantajın fizik değerlerine etkisi (oyun içi avantajlar — coin, sandık, mıknatıs — GameController'da).
        public static void ApplyPerk(Perk perk, SledConfig c)
        {
            switch (perk)
            {
                case Perk.Slosh: c.sloshDamping *= 1.1f; break;
                case Perk.Sling: c.bandStiffness *= 1.0816f; break;     // çıkış hızı ∝ √k → +%4
                case Perk.Friction: c.groundFriction *= 0.95f; break;
                case Perk.Lid: c.milkHeadroom += 0.004f; break;   // bardağı biraz eksik doldurur
                case Perk.Rocket: c.rocketThrust *= 1.1f; break;
                case Perk.Armor: c.crashImpact *= 1.15f; break;
                case Perk.Glide: c.wingArea *= 1.08f; break;
            }
        }
    }
}
