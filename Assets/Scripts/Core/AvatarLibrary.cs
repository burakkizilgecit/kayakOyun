namespace SledSurfers
{
    public enum Perk { None, Slosh, Sling, Magnet, Friction, Lid, Chest, Rocket, Armor, Income, Glide }

    /// Seçilebilir karakterler (Quaternius CC0 modelleri) ve küçük avantajları. Fiyatlar pembe elmas.
    public static class AvatarLibrary
    {
        public struct Info
        {
            public string name, model, perkTitle, perkText;
            public Perk perk;
            public int price;
        }

        public static readonly Info[] All =
        {
            new Info { name = "Can", model = "Casual_Male", perkTitle = "Başlangıç", perkText = "Sapanın yeni kahramanı", perk = Perk.None, price = 0 },
            new Info { name = "Elif", model = "Casual_Female", perkTitle = "Başlangıç", perkText = "Sapanın yeni kahramanı", perk = Perk.None, price = 0 },
            new Info { name = "Kaptan Mira", model = "Pirate_Female", perkTitle = "Denizci dengesi", perkText = "Süt %10 daha az çalkalanır", perk = Perk.Slosh, price = 10 },
            new Info { name = "Bjorn", model = "Viking_Male", perkTitle = "Güçlü kollar", perkText = "Sapan çıkışı %4 daha hızlı", perk = Perk.Sling, price = 15 },
            new Info { name = "Tex", model = "Cowboy_Male", perkTitle = "Kement", perkText = "Coin toplama alanı %40 daha geniş", perk = Perk.Magnet, price = 15 },
            new Info { name = "Kage", model = "Ninja_Female", perkTitle = "Hafif ayak", perkText = "Kızak sürtünmesi %5 daha az", perk = Perk.Friction, price = 20 },
            new Info { name = "Şef Ayşe", model = "Chef_Female", perkTitle = "Usta eller", perkText = "Kapak 100 m yerine 130 m'de açılır", perk = Perk.Lid, price = 20 },
            new Info { name = "Zombi Zeki", model = "Zombie_Male", perkTitle = "Sandık avcısı", perkText = "Sandık ödülleri %25 daha büyük", perk = Perk.Chest, price = 20 },
            new Info { name = "Çavuş Defne", model = "Soldier_Female", perkTitle = "Yakıt ustası", perkText = "Roket itişi %10 daha güçlü", perk = Perk.Rocket, price = 25 },
            new Info { name = "Altın Şövalye", model = "Knight_Golden_Male", perkTitle = "Zırh", perkText = "Sert inişlere %15 daha dayanıklı", perk = Perk.Armor, price = 25 },
            new Info { name = "Bay Kemal", model = "Suit_Male", perkTitle = "Yatırımcı", perkText = "Coin kazancı %10 daha fazla", perk = Perk.Income, price = 30 },
            new Info { name = "Büyücü Merlin", model = "Wizard", perkTitle = "Büyülü kanat", perkText = "Süzülürken kaldırma %8 daha fazla", perk = Perk.Glide, price = 35 },
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
                case Perk.Lid: c.lidDistance = 130f; break;
                case Perk.Rocket: c.rocketThrust *= 1.1f; break;
                case Perk.Armor: c.crashImpact *= 1.15f; break;
                case Perk.Glide: c.wingArea *= 1.08f; break;
            }
        }
    }
}
