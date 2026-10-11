using UnityEngine;

namespace SledSurfers
{
    /// Aşamalı öğretim ipuçları (yol haritası aşama 5): her biri ilgili an ilk geldiğinde birkaç kez gösterilir.
    public enum Hint { Steer, Milk, Rocket, Nose, Bridge, Launch, Fork }

    public class SaveData
    {
        /// Para parkur başına (kullanıcı kararı 2026-10-10: yeni haritaya geçince para sıfırlanır; elmas ortak kalır).
        public readonly int[] trackMoney = new int[TrackLibrary.Count];
        public int money
        {
            get => trackMoney[Mathf.Clamp(track, 0, TrackLibrary.Count - 1)];
            set => trackMoney[Mathf.Clamp(track, 0, TrackLibrary.Count - 1)] = value;
        }
        /// Pistte toplanmış altın ve elmaslar (parkur başına '1'/'0' dizisi; Collectibles sırasıyla): bir sonraki denemede
        /// pistte olmazlar (kullanıcı kararı 2026-10-10). Gün değişince pist yeniden dolar (takenDay).
        public readonly string[] taken = new string[TrackLibrary.Count];
        public int takenDay;

        /// Gün değiştiyse toplananlar sıfırlanır (pistler yeniden dolar). true: yenilendi.
        public bool RollTaken()
        {
            int today = DailyQuests.Today;
            if (takenDay == today) return false;
            takenDay = today;
            for (int t = 0; t < taken.Length; t++) taken[t] = "";
            return true;
        }
        public int gems;              // pembe elmas
        public long chestReadyTicks;  // ücretsiz sandığın açılacağı an (UTC)

        /// Fırlatma hakkı: en fazla MaxLaunches; her LaunchRegenMinutes'te bir dolar (oyun kapalıyken de), sınırı aşmaz.
        /// Hak fırlatma anında harcanır. Bitince ya beklenir ya da (ödüllü) reklamla +1 alınır.
        public const int MaxLaunches = 8;
        public const double LaunchRegenMinutes = 25;
        public int launches = MaxLaunches;
        public long launchTicks;      // dolum sayacının başladığı an (UTC); haklar doluyken her an güncellenir
        public int hand = 1;   // 1 = sağ el, -1 = sol el
        public bool sound = true;
        public int steerSens = 1;      // direksiyon hassasiyeti: 0 düşük, 1 orta, 2 yüksek
        public int tutorialCounts;     // aşamalı öğretim: her ipucunun kaç kez gösterildiği (4 bit × ipucu)

        public int TutorialCount(Hint h) => (tutorialCounts >> (4 * (int)h)) & 0xF;
        public void BumpTutorial(Hint h)
        {
            int c = TutorialCount(h);
            if (c < 15) tutorialCounts += 1 << (4 * (int)h);
        }
        /// Günlük görevler: listenin günü ve parkuru (gün içinde parkur değişse de liste sabit kalır), ilerleme, alınanlar.
        public int questDay, questTrack;
        public readonly int[] questProgress = new int[DailyQuests.PerDay];
        public int questClaimed;   // bit maskesi

        /// Gün değiştiyse yeni listeye geçer (ilerleme ve alınanlar sıfırlanır). true: yenilendi.
        public bool RollQuests()
        {
            int today = DailyQuests.Today;
            if (questDay == today) return false;
            questDay = today;
            questTrack = unlocked;
            System.Array.Clear(questProgress, 0, questProgress.Length);
            questClaimed = 0;
            return true;
        }

        /// Günlük giriş ödülü (kullanıcı isteği 2026-10-09): 7 günlük seri; bir gün atlanırsa seri baştan başlar.
        public int loginDay, loginStreak;   // son ödül alınan gün (DailyQuests.Today) ve seride kaçıncı gün (1..7)

        /// Bugün alınmamış ödül varsa serideki günü (1..7) döndürür, yoksa 0.
        public int PendingLoginDay()
        {
            int today = DailyQuests.Today;
            if (loginDay == today) return 0;
            bool yesterday = loginDay == DailyQuests.Yesterday;
            return yesterday ? loginStreak % 7 + 1 : 1;
        }

        public void ClaimLogin(int day)
        {
            loginDay = DailyQuests.Today;
            loginStreak = day;
        }

        /// Mağazada reklamla ücretsiz elmas: günde en çok FreeGemsPerDay (kullanıcı kararı 2026-10-11).
        public const int FreeGemsPerDay = 2;
        public int adGemDay, adGemCount;
        public int FreeGemsLeft => adGemDay == DailyQuests.Today ? Mathf.Max(0, FreeGemsPerDay - adGemCount) : FreeGemsPerDay;
        public void UseFreeGem()
        {
            if (adGemDay != DailyQuests.Today) { adGemDay = DailyQuests.Today; adGemCount = 0; }
            adGemCount++;
        }

        public int track;      // seçili parkur
        public int avatar;     // seçili karakter (AvatarLibrary)
        public int avatarsOwned = 0b1;   // sahip olunan karakterler (bit maskesi; ilki — Panda — ücretsiz)
        public int unlocked;   // açılmış en ileri parkur
        /// Pist başına yükseltme kademeleri (kullanıcı kararı 2026-10-09): yeni pistte kartlar sıfırdan başlar.
        public readonly int[][] trackLevels = NewTrackLevels();
        /// Geçerli pistin kademeleri (çağıranlar her zaman o anki pistinkini görür).
        public int[] levels => trackLevels[Mathf.Clamp(track, 0, TrackLibrary.Count - 1)];

        static int[][] NewTrackLevels()
        {
            var a = new int[TrackLibrary.Count][];
            for (int t = 0; t < a.Length; t++) a[t] = new int[Upgrades.Count];
            return a;
        }
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

        /// Çayır ilk kez bitene kadar fırlatma hakkı düşmez (kullanıcı kararı 2026-10-09): yeni oyuncu öğretici pisti
        /// kesintisiz oynar; hak sistemi Çayır bitince başlar (hak sayacı o ana kadar dolu kalır).
        public bool LaunchesFree => !Finished(0);

        static long RegenPeriod => System.TimeSpan.FromMinutes(LaunchRegenMinutes).Ticks;

        /// Geçen süreye göre hakları doldurur. Dönüş: hak sayısı değişti mi.
        public bool RegenLaunches(long now)
        {
            if (launches >= MaxLaunches)
            {
                launches = MaxLaunches;
                launchTicks = now;   // doluyken sayaç işlemez: ilk harcamadan itibaren 25 dk
                return false;
            }
            if (launchTicks <= 0 || launchTicks > now) launchTicks = now;
            long gained = (now - launchTicks) / RegenPeriod;
            if (gained <= 0) return false;
            launches = (int)System.Math.Min(MaxLaunches, launches + gained);
            launchTicks = launches >= MaxLaunches ? now : launchTicks + gained * RegenPeriod;
            return true;
        }

        /// Bir sonraki hakka kalan saniye (haklar doluysa 0).
        public int SecondsToNextLaunch(long now) =>
            launches >= MaxLaunches ? 0 : Mathf.Max(0, Mathf.CeilToInt((float)((launchTicks + RegenPeriod - now) / 1e7)));

        /// Kayıt anahtarlarının öneki. Otomatik test oyunları (AutoShot) "autotest." önekli ayrı anahtarlara yazar:
        /// gerçek oyuncu kaydına hiç dokunmazlar.
        public static string Prefix = "";
        static string K(string name) => Prefix + name;

        static System.Collections.Generic.IEnumerable<string> Keys()
        {
            for (int t = 0; t < TrackLibrary.Count; t++) { PlayerPrefs.DeleteKey(K("money_t" + t)); PlayerPrefs.DeleteKey(K("taken_t" + t)); }
            foreach (var k in new[] { "adGemDay", "adGemCount", "takenDay", "lday", "lstreak", "qday", "qtrack", "qp0", "qp1", "qp2", "qclaim", "money", "gems", "chest", "launches", "launchTicks", "hand", "sound", "track", "avatar", "avatars", "avatar2", "avatars2", "unlocked",
                                      "sens", "tut" })
                yield return k;
            for (int t = 0; t < TrackLibrary.Count; t++)
                for (int i = 0; i < Upgrades.Count; i++) yield return "lvl" + t + "_" + i;
            for (int i = 0; i < TrackLibrary.Count; i++) yield return "best_t" + i;
        }

        /// Geçerli önekle yazılmış bütün kayıt anahtarlarını siler (testler için; PlayerPrefs.DeleteAll yerine).
        public static void ClearAll()
        {
            foreach (var k in Keys()) PlayerPrefs.DeleteKey(K(k));
            PlayerPrefs.Save();
        }

        public static SaveData Load()
        {
            var s = new SaveData
            {
                gems = PlayerPrefs.GetInt(K("gems"), 0),
                chestReadyTicks = long.TryParse(PlayerPrefs.GetString(K("chest"), "0"), out var ticks) ? ticks : 0,
                hand = PlayerPrefs.GetInt(K("hand"), 1),
                sound = PlayerPrefs.GetInt(K("sound"), 1) != 0,
                steerSens = Mathf.Clamp(PlayerPrefs.GetInt(K("sens"), 1), 0, 2),
                tutorialCounts = PlayerPrefs.GetInt(K("tut"), 0),
                unlocked = Mathf.Clamp(PlayerPrefs.GetInt(K("unlocked"), 0), 0, TrackLibrary.Count - 1),
                avatarsOwned = PlayerPrefs.GetInt(K("avatars2"), 0b1) | 0b1,   // 2: karakter listesi yenilendi (eski dizinler geçersiz)
                launches = Mathf.Clamp(PlayerPrefs.GetInt(K("launches"), MaxLaunches), 0, MaxLaunches),
                launchTicks = long.TryParse(PlayerPrefs.GetString(K("launchTicks"), "0"), out var lt) ? lt : 0,
            };
            s.RegenLaunches(System.DateTime.UtcNow.Ticks);
            s.track = Mathf.Clamp(PlayerPrefs.GetInt(K("track"), 0), 0, s.unlocked);
            s.avatar = Mathf.Clamp(PlayerPrefs.GetInt(K("avatar2"), 0), 0, AvatarLibrary.Count - 1);
            if (!s.Owns(s.avatar)) s.avatar = 0;
            // Pist başına kademeler ("lvl{pist}_{kart}"). Eski tek dizi ("lvl{kart}") yeni sistemde anlam taşımadığı için okunmaz.
            for (int t = 0; t < TrackLibrary.Count; t++)
                for (int i = 0; i < Upgrades.Count; i++)
                    s.trackLevels[t][i] = Mathf.Clamp(PlayerPrefs.GetInt(K("lvl" + t + "_" + i), 0), 0, Upgrades.MaxFor(t));
            for (int i = 0; i < TrackLibrary.Count; i++)
                s.bests[i] = PlayerPrefs.GetInt(K("best_t" + i), 0);
            // Bir parkur bitmeden sonraki açılmaz (eski kayıtlar ve değişen pistler için de geçerli).
            while (s.unlocked > 0 && !s.Finished(s.unlocked - 1)) s.unlocked--;
            s.track = Mathf.Min(s.track, s.unlocked);
            for (int t = 0; t < TrackLibrary.Count; t++)
            {
                s.trackMoney[t] = PlayerPrefs.GetInt(K("money_t" + t), 0);
                s.taken[t] = PlayerPrefs.GetString(K("taken_t" + t), "");
            }
            // Eski tek cüzdan: bulunulan parkura aktarılır (bir kez).
            if (PlayerPrefs.HasKey(K("money")))
            {
                s.trackMoney[s.track] += PlayerPrefs.GetInt(K("money"), 0);
                PlayerPrefs.DeleteKey(K("money"));
            }
            s.takenDay = PlayerPrefs.GetInt(K("takenDay"), 0);
            s.adGemDay = PlayerPrefs.GetInt(K("adGemDay"), 0);
            s.adGemCount = PlayerPrefs.GetInt(K("adGemCount"), 0);
            s.questDay = PlayerPrefs.GetInt(K("qday"), 0);
            s.questTrack = Mathf.Clamp(PlayerPrefs.GetInt(K("qtrack"), 0), 0, TrackLibrary.Count - 1);
            for (int i = 0; i < DailyQuests.PerDay; i++) s.questProgress[i] = PlayerPrefs.GetInt(K("qp" + i), 0);
            s.questClaimed = PlayerPrefs.GetInt(K("qclaim"), 0);
            s.loginDay = PlayerPrefs.GetInt(K("lday"), 0);
            s.loginStreak = PlayerPrefs.GetInt(K("lstreak"), 0);
            return s;
        }

        public void Save()
        {
            for (int t = 0; t < TrackLibrary.Count; t++)
            {
                PlayerPrefs.SetInt(K("money_t" + t), trackMoney[t]);
                PlayerPrefs.SetString(K("taken_t" + t), taken[t] ?? "");
            }
            PlayerPrefs.SetInt(K("takenDay"), takenDay);
            PlayerPrefs.SetInt(K("adGemDay"), adGemDay);
            PlayerPrefs.SetInt(K("adGemCount"), adGemCount);
            PlayerPrefs.SetInt(K("gems"), gems);
            PlayerPrefs.SetString(K("chest"), chestReadyTicks.ToString());
            PlayerPrefs.SetInt(K("launches"), launches);
            PlayerPrefs.SetString(K("launchTicks"), launchTicks.ToString());
            PlayerPrefs.SetInt(K("hand"), hand);
            PlayerPrefs.SetInt(K("sound"), sound ? 1 : 0);
            PlayerPrefs.SetInt(K("sens"), steerSens);
            PlayerPrefs.SetInt(K("tut"), tutorialCounts);
            PlayerPrefs.SetInt(K("track"), track);
            PlayerPrefs.SetInt(K("avatar2"), avatar);
            PlayerPrefs.SetInt(K("avatars2"), avatarsOwned);
            PlayerPrefs.SetInt(K("unlocked"), unlocked);
            for (int t = 0; t < TrackLibrary.Count; t++)
                for (int i = 0; i < Upgrades.Count; i++) PlayerPrefs.SetInt(K("lvl" + t + "_" + i), trackLevels[t][i]);
            for (int i = 0; i < TrackLibrary.Count; i++) PlayerPrefs.SetInt(K("best_t" + i), bests[i]);
            PlayerPrefs.SetInt(K("qday"), questDay);
            PlayerPrefs.SetInt(K("qtrack"), questTrack);
            for (int i = 0; i < DailyQuests.PerDay; i++) PlayerPrefs.SetInt(K("qp" + i), questProgress[i]);
            PlayerPrefs.SetInt(K("qclaim"), questClaimed);
            PlayerPrefs.SetInt(K("lday"), loginDay);
            PlayerPrefs.SetInt(K("lstreak"), loginStreak);
            PlayerPrefs.Save();
        }
    }
}
