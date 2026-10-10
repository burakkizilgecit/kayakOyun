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
            new Info { name = "Puffy the Panda", model = "Meshy/Panda", perkTitle = "Starter", perkText = "The chubby hero of the slingshot", perk = Perk.None, price = 0 },
            new Info { name = "Frosty the Penguin", model = "Meshy/Penguin", perkTitle = "Ice balance", perkText = "Milk sloshes 10% less", perk = Perk.Slosh, price = 8 },
            new Info { name = "Cowboy Dusty", model = "Meshy/Cowboy", perkTitle = "Lasso", perkText = "40% wider coin pickup", perk = Perk.Magnet, price = 12 },
            new Info { name = "Clank the Robot", model = "Meshy/Robot", perkTitle = "Oiled gears", perkText = "5% less sled friction", perk = Perk.Friction, price = 16 },
            new Info { name = "Captain Hook", model = "Meshy/Pirate", perkTitle = "Treasure hunter", perkText = "25% bigger chest rewards", perk = Perk.Chest, price = 20 },
            new Info { name = "Chef Al", model = "Meshy/Chef", perkTitle = "Steady hands", perkText = "Fills the cup carefully: milk spills less", perk = Perk.Lid, price = 25 },
            new Info { name = "Shadow Ninja", model = "Meshy/Ninja", perkTitle = "Light feet", perkText = "5% less sled friction", perk = Perk.Friction, price = 30 },
            new Info { name = "Firefighter Blaze", model = "Meshy/Firefighter", perkTitle = "Sturdy helmet", perkText = "15% tougher on hard landings", perk = Perk.Armor, price = 36 },
            new Info { name = "Sir Arden", model = "Meshy/Knight", perkTitle = "Armor", perkText = "15% tougher on hard landings", perk = Perk.Armor, price = 42 },
            new Info { name = "Viking Ragnar", model = "Meshy/Viking", perkTitle = "Strong arms", perkText = "4% faster launch", perk = Perk.Sling, price = 50 },
            new Info { name = "Stripes the Tiger", model = "Meshy/Tiger", perkTitle = "Claw power", perkText = "10% stronger rocket", perk = Perk.Rocket, price = 60 },
            new Info { name = "Lila the Wizard", model = "Meshy/Wizard", perkTitle = "Magic wings", perkText = "8% more lift while gliding", perk = Perk.Glide, price = 72 },
            new Info { name = "Astronaut Star", model = "Meshy/Astronaut", perkTitle = "Spacewalk", perkText = "8% more lift while gliding", perk = Perk.Glide, price = 85 },
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
