using UnityEngine;

namespace SledSurfers
{
    public class SaveData
    {
        public int money;
        public int gems;              // pembe elmas
        public long chestReadyTicks;  // ücretsiz sandığın açılacağı an (UTC)
        public int hand = 1;   // 1 = sağ el, -1 = sol el
        public bool sound = true;
        public int track;      // seçili parkur
        public int avatar;     // seçili karakter (AvatarLibrary)
        public int avatarsOwned = 0b11;   // sahip olunan karakterler (bit maskesi; ilk ikisi ücretsiz)
        public int unlocked;   // açılmış en ileri parkur
        public readonly int[] levels = new int[Upgrades.Count];
        public readonly int[] bests = new int[TrackLibrary.Count];

        /// Seçili parkurun rekoru (m).
        public int best
        {
            get => bests[track];
            set => bests[track] = value;
        }

        public bool Owns(int avatarIndex) => (avatarsOwned & (1 << avatarIndex)) != 0;
        public Perk Perk => AvatarLibrary.All[avatar].perk;

        public bool Finished(int i) => bests[i] >= TrackLibrary.Lengths[i];

        public static SaveData Load()
        {
            var s = new SaveData
            {
                money = PlayerPrefs.GetInt("money", 0),
                gems = PlayerPrefs.GetInt("gems", 0),
                chestReadyTicks = long.TryParse(PlayerPrefs.GetString("chest", "0"), out var ticks) ? ticks : 0,
                hand = PlayerPrefs.GetInt("hand", 1),
                sound = PlayerPrefs.GetInt("sound", 1) != 0,
                unlocked = Mathf.Clamp(PlayerPrefs.GetInt("unlocked", 0), 0, TrackLibrary.Count - 1),
                avatarsOwned = PlayerPrefs.GetInt("avatars", 0b11) | 0b11,
            };
            s.track = Mathf.Clamp(PlayerPrefs.GetInt("track", 0), 0, s.unlocked);
            s.avatar = Mathf.Clamp(PlayerPrefs.GetInt("avatar", 0), 0, AvatarLibrary.Count - 1);
            if (!s.Owns(s.avatar)) s.avatar = 0;
            for (int i = 0; i < Upgrades.Count; i++)
                s.levels[i] = Mathf.Clamp(PlayerPrefs.GetInt("lvl" + i, 0), 0, Upgrades.MaxLevel(i));
            for (int i = 0; i < TrackLibrary.Count; i++)
                s.bests[i] = PlayerPrefs.GetInt("best_t" + i, 0);
            // Bir parkur bitmeden sonraki açılmaz (eski kayıtlar ve değişen pistler için de geçerli).
            while (s.unlocked > 0 && !s.Finished(s.unlocked - 1)) s.unlocked--;
            s.track = Mathf.Min(s.track, s.unlocked);
            for (int i = 0; i < Upgrades.Count; i++)
                s.levels[i] = Mathf.Min(s.levels[i], Upgrades.Cap(i, s.unlocked));
            return s;
        }

        public void Save()
        {
            PlayerPrefs.SetInt("money", money);
            PlayerPrefs.SetInt("gems", gems);
            PlayerPrefs.SetString("chest", chestReadyTicks.ToString());
            PlayerPrefs.SetInt("hand", hand);
            PlayerPrefs.SetInt("sound", sound ? 1 : 0);
            PlayerPrefs.SetInt("track", track);
            PlayerPrefs.SetInt("avatar", avatar);
            PlayerPrefs.SetInt("avatars", avatarsOwned);
            PlayerPrefs.SetInt("unlocked", unlocked);
            for (int i = 0; i < Upgrades.Count; i++) PlayerPrefs.SetInt("lvl" + i, levels[i]);
            for (int i = 0; i < TrackLibrary.Count; i++) PlayerPrefs.SetInt("best_t" + i, bests[i]);
            PlayerPrefs.Save();
        }
    }
}
