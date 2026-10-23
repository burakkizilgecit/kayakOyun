using System;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SledSurfers
{
    /// Arayüzün her kare ihtiyaç duyduğu oyun durumu.
    public struct HudInfo
    {
        public int money, best, distance, hand;
        public bool freeLaunches;   // Çayır bitene kadar hak düşmez: sayaç "∞" gösterir
        public int sensitivity;     // direksiyon hassasiyeti (0..2)
        public float milkDanger;    // süt taşmaya ne kadar yakın (0..1)
        public bool perfectWindow;  // sapan tam gerildi: şimdi bırakırsa mükemmel atış
        public float progress, bestProgress, speedKmh, fill, pull, runTime, aimAngle;
        public bool airborne, canGlide, dragging, tiltMode, sound;
        public bool mobileControls, hasWings, rocketNeedsLanding;
        public float wingSecondsLeft;
        public Rect glassRect;   // bardak kamerasının ekran oranı cinsinden alanı (alt-sol orijinli)
        public float lidLeft;    // kapağın açılmasına kalan mesafe (m); 0 = açık
        public int trackIndex;
        public string trackName;
        public bool hasRocket, rocketReady;   // roket takılı mı, şu an ateşlenebilir mi
        public int rocketCharges;
        public float rocketFuel;              // yanan roketin kalan yakıtı (0..1)
        public int gems, runCoins, runGems;    // elmas bakiyesi, bu atışta toplanan coin/elmas
        public int chestSeconds;              // ücretsiz sandığa kalan süre (0 = hazır)
        public int launches, launchSeconds;   // fırlatma hakkı ve bir sonrakine kalan süre (s)
        public bool crashScene;               // karakter düştü: hız göstergesi ve bardak paneli gizlenir
    }

    public struct RunSummary
    {
        public string title;
        public bool finished, record;
        public int distance, earned, finishBonus;
        public float fill, progress, bestProgress;
        public string trackName, unlockedName;   // unlockedName: bu atışla açılan parkur (yoksa null)
        public int unlockedLength;
        public bool lastTrack;                   // son parkur da bitti
        public int coins, gems;                  // pistte toplanan coin ve elmas
        public string tip;                       // bir sonraki atış için öneri (RunTips); boşsa gösterilmez
    }

    /// UI Toolkit ile koddan kurulan oyun arayüzü: Ana ekran (yükseltmeler) → Nişan → Koşu → Sonuç → Ana ekran.
    /// Oyun mantığı içermez; düğmeler olay (event) yayınlar, GameController dinler.
    public class GameUI
    {
        public enum Page { Hub, Map, Avatars, Aim, Run, Result }

        /// Sonuç ekranında reklam izlenip kazanç 3 katına çıkarıldı (GameController 2 kat daha ekler).
        public event Action TripleEarnings;
        /// Ana ekrandaki OYNA düğmesi.
        public event Action Play;
        /// Günlük görevler penceresini aç, günlük giriş ödülünü al; görev ödülü al (dizin).
        public event Action OpenQuests, ClaimLogin;
        public event Action<int> ClaimQuest;
        public event Action OpenHub, OpenMap, NextTrack, Retry, FireRocket, OpenChest, OpenAvatars;
        public event Action PauseRequested, ResumeRequested, RetryRun;
        public event Action<int> BrowseAvatar, AvatarAction;
        public event Action<int> ClaimChest;   // çarpan: 1 ya da 3 (reklamla)
        public event Action<int> Buy, SetHand, SelectTrack;
        public event Action<bool> SetSound;
        public event Action<int> SetSensitivity;
        /// Test düğmeleri (yalnız geliştirme sürümü): geçerli haritanın geliştirmelerini sıfırla, bütün haritaları aç, para ver.
        public event Action TestResetUpgrades, TestUnlockAll, TestMoney;
        public event Action WatchLaunchAd;   // hak bitti penceresinde reklam izlendi: +1 hak

        static readonly Color Ink = new Color32(0x2B, 0x23, 0x40, 0xFF);
        static readonly Color Muted = new Color32(0x7D, 0x72, 0x90, 0xFF);
        static readonly Color Gold = new Color32(0xFF, 0xD8, 0x4A, 0xFF);
        // Kart renkleri (Sapan, Kızak, Kanat, Bardak, Gelir, Roket): başlık/buton, butonun alt gölgesi, ikon zemini
        static readonly Color[] UpgradeColors =
        {
            new Color32(0xF0, 0x5A, 0x4A, 0xFF), new Color32(0x3F, 0xA4, 0xFF, 0xFF),
            new Color32(0x9B, 0x6B, 0xFF, 0xFF), new Color32(0xFF, 0xB8, 0x2E, 0xFF), new Color32(0xFF, 0x6A, 0x3D, 0xFF),
        };
        static readonly Color[] UpgradeDark =
        {
            new Color32(0xC2, 0x3A, 0x2C, 0xFF), new Color32(0x1E, 0x78, 0xD0, 0xFF),
            new Color32(0x6E, 0x44, 0xD6, 0xFF), new Color32(0xD9, 0x8A, 0x0B, 0xFF), new Color32(0xD2, 0x45, 0x1E, 0xFF),
        };
        static readonly Color[] UpgradeTints =
        {
            new Color32(0xFF, 0xEA, 0xE6, 0xFF), new Color32(0xE4, 0xF1, 0xFF, 0xFF),
            new Color32(0xEF, 0xE8, 0xFF, 0xFF), new Color32(0xFF, 0xF4, 0xDA, 0xFF), new Color32(0xFF, 0xEB, 0xE3, 0xFF),
        };
        static readonly Color CardGray = new Color32(0xC9, 0xC2, 0xB8, 0xFF);
        static readonly Color GemPink = new Color32(0xF0, 0x4F, 0xB0, 0xFF);
        static readonly Color GemPinkDark = new Color32(0xB8, 0x2A, 0x82, 0xFF);
        static readonly Color CardGrayDark = new Color32(0xA3, 0x9B, 0x90, 0xFF);
        static readonly System.Globalization.CultureInfo Tr = System.Globalization.CultureInfo.InvariantCulture;   // arayüz İngilizce (2026-10-11)
        static readonly Color[] TrackColors =
        {
            new Color32(0x5C, 0xC4, 0x5A, 0xFF), new Color32(0x2E, 0x8B, 0x57, 0xFF), new Color32(0xE8, 0x74, 0x3B, 0xFF),
            new Color32(0x4F, 0x9F, 0xE0, 0xFF), new Color32(0xE8, 0xB9, 0x3B, 0xFF), new Color32(0xD8, 0x3A, 0x2A, 0xFF),
            new Color32(0xB8, 0x5A, 0x3C, 0xFF),
        };
        static readonly IconKind[] UpgradeIcons =
            { IconKind.Slingshot, IconKind.Runners, IconKind.Glass, IconKind.Income, IconKind.Rocket };

        readonly VisualElement root, safe;
        readonly MenuTrackTheme menuTheme = new MenuTrackTheme();
        readonly Dictionary<Page, VisualElement> pages = new Dictionary<Page, VisualElement>();
        readonly Action click;
        Page page = Page.Hub;
        float time;
        Rect lastSafe;
        int shownMoney = -1, shownBest = -1;
        float coinBump;

        readonly List<Label> coinLabels = new List<Label>();
        readonly List<VisualElement> coinPills = new List<VisualElement>();
        readonly List<Label> bestLabels = new List<Label>();
        readonly List<Label> gemLabels = new List<Label>();
        readonly List<VisualElement> gemPills = new List<VisualElement>();
        int shownGems = -1;
        float gemBump;

        // Koşuda toplananlar
        VisualElement runLoot;
        Label runCoinLabel, runGemLabel;
        VisualElement runGemBox;
        int shownRunCoins = -1, shownRunGems = -1;
        float lootBump;

        // Sandık
        Button chestBtn;
        Label chestLabel;
        VisualElement chestModal, chestCard, chestGemRow, adOverlay;
        VisualElement launchModal, launchCard, bubble;
        Label bubbleText;
        float bubbleAge = -1f;
        Label launchModalTimer, launchModalCount;
        readonly List<Label> launchLabels = new List<Label>(), launchTimers = new List<Label>();
        int shownLaunches = -1, shownLaunchSeconds = -1;
        VisualElement chestIcon;
        Label chestCoins, chestGems, adCount;
        Button chestTriple, resTriple;
        int coinFrom;
        float coinStart = 0.6f;
        int chestCoinValue, chestGemValue, chestMultiplier = 1;
        float chestClock, adClock = -1f;
        Action adDone;

        // Sonuç: toplananlar
        Label resLoot, resTip;

        // Karakterler
        VisualElement hubAvatarPortrait;
        Label avName, avPerkTitle, avPerkText, avActionLabel;
        Button avAction;
        Icon avActionGem;
        readonly AvatarCard[] avCards = new AvatarCard[AvatarLibrary.Count];
        ScrollView avScroll;
        int avBrowse;
        float avBump;

        class AvatarCard
        {
            public Button root;
            public VisualElement portrait, priceRow, badge, lockBadge, chip;
            public Label name, price, state;
        }

        // Karakter nadirliği (fiyata göre): ücretsiz yeşil, 10-15 mavi, 20-25 mor, 30+ altın.
        static readonly Color[] RarityColors =
        {
            new Color32(0x3F, 0xD1, 0x5B, 0xFF), new Color32(0x3F, 0xA4, 0xFF, 0xFF),
            new Color32(0x9B, 0x5C, 0xFF, 0xFF), new Color32(0xFF, 0xB0, 0x20, 0xFF),
        };
        static readonly string[] RarityNames = { "FREE", "RARE", "EPIC", "LEGENDARY" };
        static int Rarity(int price) => price <= 0 ? 0 : price < 25 ? 1 : price < 50 ? 2 : 3;   // 8-20 nadir, 25-42 epik, 50+ efsane

        static IconKind PerkIcon(Perk perk)
        {
            switch (perk)
            {
                case Perk.Slosh: case Perk.Lid: return IconKind.Glass;
                case Perk.Sling: return IconKind.Slingshot;
                case Perk.Magnet: return IconKind.Coin;
                case Perk.Friction: return IconKind.Runners;
                case Perk.Chest: return IconKind.Chest;
                case Perk.Rocket: return IconKind.Rocket;
                case Perk.Armor: return IconKind.Check;
                case Perk.Income: return IconKind.Income;
                case Perk.Glide: return IconKind.Wing;
                default: return IconKind.Play;
            }
        }

        VisualElement avPerkCard, avPerkIconBox, avRarity;
        Label avRarityLabel;
        Icon avPerkIcon;

        // Ana ekran
        Label playHint, hubTrackName, hubTrackSub, hubTrackPct;
        Label homeCoinValue, homeGemValue;
        VisualElement hubRibbon, playPill;
        Button playButton;
        VisualElement homeCanvas, homeChestDot;
        VisualElement homeBannerArt, homePlayArt, homeTrayArt;
        Rect? debugHomeSafeArea;
        Vector2 lastSafeLayout;
        float homeProgress, homeProgressTarget, homeRelease;
        int homeTrack = -1;
        bool homePlayHeld;
        /// Mağaza ve (test) karşılama teklifi.
        public event Action OpenShop, TestOffer;
        VisualElement hubTrackFill, settings, settingsCard;
        VisualElement pauseModal, pauseCard;
        bool paused;
        Button handLeft, handRight, soundOn, soundOff;
        readonly Button[] sensButtons = new Button[3];
        float hintTime;

        // Harita
        readonly MapCard[] mapCards = new MapCard[TrackLibrary.Count];
        ScrollView mapScroll;

        // Yükseltme kartları
        readonly UpgradeCard[] cards = new UpgradeCard[Upgrades.Count];

        // Roket butonu (koşu)
        int hubAvatar;
        Button rocketBtn;
        VisualElement rocketGlow;
        VisualElement gaugeBox;
        SpeedGauge gauge;
        VisualElement rocketFill;
        Label rocketCount, rocketLabel;

        // Nişan
        VisualElement hint, hintHand, power;
        Label hintSub;
        TrackBar powerBar;
        Label powerLabel;

        // Koşu
        ProgressColumn runColumn;
        Label distLabel, speedLabel, airLabel, wingLabel;
        VisualElement tutorial;
        public bool debugHideTutorial;   // otomatik ekran görüntüsü testleri için
        bool wasAirborne;
        float airClock;
        int shownDist = -1, shownSpeed = -1;

        // Kapak açıldı yazısı
        Label toastLabel, stageLabel;
        float stageClock = -1f;
        float toastClock = -1f;

        // Bardak (nişan + koşu)
        VisualElement glassHud, glassFrame, milkPill, milkFill;
        float milkDangerShown;
        Label milkLabel;
        Icon milkIcon, lidIcon;
        int shownMilk = -1;

        // Sonuç
        VisualElement resultCard, resRecord, resRibbon, unlockBox, confetti;
        Label unlockTitle, unlockName, unlockProgression, goLabel;
        Icon goIcon;
        bool goNext;
        readonly List<Confetti> bits = new List<Confetti>();
        Label resTitle, resDist, resMilk, resCoins, resBonus;
        TrackBar resBar;
        RunSummary result;
        float resultClock;

        class UpgradeCard
        {
            public VisualElement root, fill, bar, pipRow, preview, shine, top;
            public readonly VisualElement[] pips = new VisualElement[Upgrades.StageSize];
            public Label badge, sub, cost, level, gain, title;
            public bool canBuy;
            public string gainText = "";
            public Button buy;
            public Icon coin, gem, lockIcon;
            public float bump, shake;
        }

        class MapCard
        {
            public Button root;
            public VisualElement badge, fill, island, lockDisc, pic, bar;
            public Label number, status, wallet, lockName;
            public Icon lockIcon;
        }

        class Confetti
        {
            public VisualElement e;
            public float x, y, speed, sway, phase, spin;
        }

        /// Koşuda sağ kenarda dikey ilerleme çubuğu (referanstaki gibi): tepede bitiş bayrağı, elmasların yerleri,
        /// rekor işareti, alttan dolan çubuk ve ilerleyen topuzun yanında yüzde.
        class ProgressColumn
        {
            const float Top = 70f;   // bayrağın altından itibaren çubuk
            public readonly VisualElement root, track, fill, knob, bestMark, gems;

            public void SetAvatar(StyleBackground portrait)
            {
                knob.style.backgroundImage = portrait;
                knob.style.backgroundColor = Color.white;
            }
            readonly Label percent, bestLabel;

            public ProgressColumn()
            {
                root = new VisualElement();
                root.pickingMode = PickingMode.Ignore;
                root.style.position = Position.Absolute;
                root.style.right = 24;
                root.style.top = 330;
                root.style.width = 190;
                root.style.height = 1000;

                var flag = new Icon(IconKind.Finish, 64, Color.white);
                flag.style.position = Position.Absolute;
                flag.style.right = 6;
                flag.style.top = 0;
                root.Add(flag);

                track = new VisualElement();
                track.AddToClassList("vbar");
                track.style.top = Top;
                root.Add(track);
                fill = new VisualElement();
                fill.AddToClassList("vbar-fill");
                track.Add(fill);

                gems = new VisualElement();
                gems.pickingMode = PickingMode.Ignore;
                gems.style.position = Position.Absolute;
                gems.style.right = 0;
                gems.style.width = 60;
                gems.style.top = Top;
                gems.style.bottom = 0;
                root.Add(gems);

                bestMark = new VisualElement();
                bestMark.AddToClassList("vbar-best");
                bestLabel = new Label("BEST");
                bestLabel.AddToClassList("vbar-label");
                bestLabel.AddToClassList("outlined");
                bestMark.Add(bestLabel);
                root.Add(bestMark);

                knob = new VisualElement();
                knob.AddToClassList("vbar-knob");
                percent = new Label("0%");
                percent.AddToClassList("vbar-label");
                percent.AddToClassList("outlined");
                knob.Add(percent);
                root.Add(knob);
            }

            float Y(float t) => Top + (1000f - Top) * (1f - Mathf.Clamp01(t));

            /// Elmasların pistteki yerleri (0..1).
            public void SetGems(IList<float> fractions)
            {
                gems.Clear();
                foreach (float f in fractions)
                {
                    var g = new Icon(IconKind.Gem, 44);
                    g.style.position = Position.Absolute;
                    g.style.right = 8;
                    g.style.top = Length.Percent((1f - Mathf.Clamp01(f)) * 100f);
                    g.style.marginTop = -22;
                    gems.Add(g);
                }
            }

            public void Set(float progress, float bestProgress)
            {
                progress = Mathf.Clamp01(progress);
                fill.style.height = Length.Percent(progress * 100f);
                knob.style.top = Y(progress) - 22f;
                string text = Mathf.FloorToInt(progress * 100f) + "%";
                if (percent.text != text) percent.text = text;
                bool showBest = bestProgress > 0.01f && Mathf.Abs(bestProgress - progress) > 0.03f;
                bestMark.style.display = showBest ? DisplayStyle.Flex : DisplayStyle.None;
                bestMark.style.top = Y(bestProgress) - 14f;
            }
        }

        class TrackBar
        {
            public readonly VisualElement root, fill, best;

            public TrackBar()
            {
                root = new VisualElement();
                root.style.height = 62;
                root.style.justifyContent = Justify.Center;
                var bar = new VisualElement();
                bar.AddToClassList("bar");
                fill = new VisualElement();
                fill.AddToClassList("bar-fill");
                bar.Add(fill);
                root.Add(bar);
                best = new VisualElement();
                best.AddToClassList("marker");
                root.Add(best);
            }

            public void Set(float progress, float bestProgress)
            {
                fill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
                best.style.display = bestProgress > 0.002f ? DisplayStyle.Flex : DisplayStyle.None;
                best.style.left = Length.Percent(Mathf.Clamp01(bestProgress) * 100f);
            }
        }

        public GameUI(GameObject host, Action clickSound)
        {
            click = clickSound;
            var doc = host.AddComponent<UIDocument>();
            doc.panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
            if (doc.panelSettings == null) Debug.LogError("[UI] Resources/UI/PanelSettings bulunamadı (Sled Surfers > Arayüz Varlıklarını Kur)");

            root = doc.rootVisualElement;
            root.style.position = Position.Absolute;
            root.style.left = root.style.top = root.style.right = root.style.bottom = 0;
            var winterStyles = Resources.Load<StyleSheet>("UI/HomeWinter");
            if (winterStyles != null) root.styleSheets.Add(winterStyles);

            glassHud = BuildGlassHud();
            root.Add(glassHud);

            safe = E("fill");
            safe.name = "home-safe";
            safe.RegisterCallback<GeometryChangedEvent>(_ => ResizeHomeCanvas());
            root.Add(safe);

            BuildHub();
            BuildAim();
            BuildRun();
            BuildResult();
            BuildMap();
            BuildAvatars();
            BuildSettings();
            BuildChest();
            BuildPartUnlock();
            BuildQuests();
            BuildLogin();
            BuildShop();
            BuildOffer();
            BuildNoLaunches();
            BuildPause();
            ConfigureMenuBackgrounds();

            root.Query<VisualElement>().ForEach(e =>
            {
                if (!(e is Button) && !e.ClassListContains("pick")) e.pickingMode = PickingMode.Ignore;
            });
            foreach (var p in pages.Values)
            {
                p.AddToClassList("off");
                p.style.display = DisplayStyle.None;
            }
        }

        // ================================================================ dışarıya açık

        public Page Current => page;

        /// The controller freezes gameplay; the modal only blocks input and offers an explicit resume.
        public void SetPaused(bool value)
        {
            paused = value;
            pauseModal.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            if (value)
            {
                pauseModal.BringToFront();
                pauseCard.RemoveFromClassList("off");
            }
        }

        /// Ekran noktası bir düğmenin ya da modal pencerenin üstünde mi? (Sapan sürüklemesi başlamasın diye.)
        /// Ekran noktası (alt-sol orijinli) roket düğmesinin üstünde mi? Düğme arayüz tıklamasını beklemeden, her yeni
        /// dokunuşla kontrol edilir: arayüz yalnızca ilk parmağı tıklama sayar, direksiyonu tutarken ikinci parmakla
        /// basılan roket ateşlenmiyordu (kullanıcı testi 2026-10-10).
        public bool OnRocketButton(Vector2 screenPos)
        {
            if (paused) return false;
            var panel = root.panel;
            if (panel == null || rocketBtn == null || !rocketBtn.enabledSelf || rocketBtn.resolvedStyle.display == DisplayStyle.None) return false;
            var pos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            var r = rocketBtn.worldBound;
            float pad = r.width * 0.2f;   // parmak için biraz pay
            return pos.x >= r.xMin - pad && pos.x <= r.xMax + pad && pos.y >= r.yMin - pad && pos.y <= r.yMax + pad;
        }

        public bool BlocksPointer(Vector2 screenPos)
        {
            if (paused) return true;
            var panel = root.panel;
            if (panel == null) return false;
            var pos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            for (var e = panel.Pick(pos); e != null; e = e.parent)
                if (e is Button || e.ClassListContains("pick")) return true;
            return false;
        }

        public void ShowHub(SaveData save)
        {
            RefreshHub(save, -1);
            SetPage(Page.Hub);
        }
        public void ShowAim() => SetPage(Page.Aim);

        public void ShowRun()
        {
            runColumn.SetAvatar(Portrait(hubAvatar));   // ilerleme çubuğunda seçili karakterin yüzü
            shownRunCoins = shownRunGems = -1;
            wasAirborne = false;
            toastClock = -1f;
            toastLabel.style.display = DisplayStyle.None;
            airLabel.style.display = DisplayStyle.None;
            SetPage(Page.Run);
        }

        public void ShowResult(RunSummary summary)
        {
            result = summary;
            resultClock = 0f;
            resTitle.text = summary.title.ToUpper(Tr);
            resRecord.style.display = summary.record ? DisplayStyle.Flex : DisplayStyle.None;
            resBonus.style.display = summary.finished ? DisplayStyle.Flex : DisplayStyle.None;
            resBonus.text = "Finish bonus: +" + summary.finishBonus;
            resLoot.text = "Collected: " + summary.coins + " coins" + (summary.gems > 0 ? "  ·  " + summary.gems + " pink gems" : "");
            resLoot.style.display = summary.coins > 0 || summary.gems > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            resTip.text = summary.tip ?? "";
            resTip.style.display = string.IsNullOrEmpty(summary.tip) ? DisplayStyle.None : DisplayStyle.Flex;
            resMilk.text = Mathf.RoundToInt(Mathf.Clamp01(summary.fill) * 100f) + "%";
            resDist.text = "0 m";
            resCoins.text = "+0";
            coinFrom = 0;
            coinStart = 0.6f;
            resTriple.style.display = summary.earned > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            resBar.Set(0f, summary.bestProgress);

            bool finished = summary.finished;
            resRibbon.style.backgroundColor = Col(finished, new Color32(0x3F, 0xD1, 0x5B, 0xFF));
            resRibbon.style.borderBottomColor = Col(finished, new Color32(0x22, 0xA0, 0x3C, 0xFF));
            confetti.style.display = finished ? DisplayStyle.Flex : DisplayStyle.None;

            goNext = summary.unlockedName != null;
            goLabel.text = goNext ? "NEXT TRACK" : "CONTINUE";
            unlockBox.style.display = goNext || summary.lastTrack ? DisplayStyle.Flex : DisplayStyle.None;
            if (goNext)
            {
                unlockTitle.text = "NEW TRACK UNLOCKED!";
                unlockName.text = summary.unlockedName + " · " + TrackLibrary.Show(summary.unlockedLength) + " m";
            }
            else if (summary.lastTrack)
            {
                unlockTitle.text = "ALL TRACKS COMPLETE!";
                unlockName.text = "You're the champion!";
            }
            unlockProgression.style.display = goNext ? DisplayStyle.Flex : DisplayStyle.None;
            resultCard.AddToClassList("off");
            resultCard.schedule.Execute(() => resultCard.RemoveFromClassList("off")).StartingIn(140);
            SetPage(Page.Result);
        }

        /// Parkur yüklenince: elmasların yerleri ilerleme çubuğuna işlenir.
        public void SetRunGems(IList<float> fractions) => runColumn.SetGems(fractions);

        /// Koşuda kapak açılınca kısa bir kutlama yazısı.
        public void LidOpened() => Toast("LID OFF!");

        /// Koşuda ekranın ortasında kısa bir yazı (kapak, roket kilidi...).
        public void Toast(string text)
        {
            toastLabel.text = text;
            toastClock = 0f;
            toastLabel.style.display = DisplayStyle.Flex;
        }

        /// Parası yetmeyen karta basınca kart titrer.
        public void Deny(int i) => cards[i].shake = 1f;

        public void Tick(float dt, in HudInfo h)
        {
            if (paused)
            {
                UpdateSafeArea();
                return;
            }
            time += dt;
            UpdateSafeArea();
            TickOverlays(dt);
            UpdateCoins(dt, h);
            UpdateLaunches(h);

            // Hız çizgileri: 55 km/s üstünde belirir, 100 km/s'de tam; fırlatmanın ilk saniyesinde patlama.
            if (speedLines != null)
            {
                float target = page == Page.Run && !h.crashScene
                    ? Mathf.Max(Mathf.InverseLerp(55f, 100f, h.speedKmh), h.runTime < 1.2f ? 1f - h.runTime / 1.2f : 0f) : 0f;
                speedLines.intensity = Mathf.MoveTowards(speedLines.intensity, target, dt * 2.5f);
                speedLines.Tick(dt);
            }

            bool glass = (page == Page.Aim || page == Page.Run) && !h.crashScene;
            if (gaugeBox != null) gaugeBox.style.display = h.crashScene ? DisplayStyle.None : DisplayStyle.Flex;
            glassHud.style.display = glass ? DisplayStyle.Flex : DisplayStyle.None;
            if (glass) UpdateGlass(dt, h);

            switch (page)
            {
                case Page.Hub: TickHub(dt, h); break;
                case Page.Map: break;
                case Page.Avatars: TickAvatars(dt); break;
                case Page.Aim: TickAim(h); break;
                case Page.Run: TickRun(dt, h); break;
                case Page.Result: TickResult(dt); break;
            }
        }

        // ================================================================ sayfa geçişi

        void SetPage(Page p)
        {
            SetPaused(false);
            page = p;
            foreach (var kv in pages)
            {
                var key = kv.Key;
                var e = kv.Value;
                if (key == p)
                {
                    e.style.display = DisplayStyle.Flex;
                    e.schedule.Execute(() => { if (page == key) e.RemoveFromClassList("off"); }).StartingIn(30);
                }
                else if (!e.ClassListContains("off"))
                {
                    e.AddToClassList("off");
                    e.schedule.Execute(() => { if (page != key) e.style.display = DisplayStyle.None; }).StartingIn(260);
                }
            }
            if (p != Page.Hub && p != Page.Avatars)
            {
                settings.style.display = DisplayStyle.None;
                chestModal.style.display = DisplayStyle.None;
            }
        }

        VisualElement NewPage(Page p)
        {
            var e = E("page");
            e.name = "menu-" + p.ToString().ToLowerInvariant();
            pages[p] = e;
            safe.Add(e);
            return e;
        }

        void UpdateSafeArea()
        {
            var sa = debugHomeSafeArea ?? Screen.safeArea;
            float w = root.layout.width, h = root.layout.height;
            // İlk yerleşimden önce boyut NaN olur; o karede güvenli alan hesaplanmaz.
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0f || h <= 0f || Screen.width <= 0 || Screen.height <= 0) return;
            if (sa == lastSafe && lastSafeLayout == new Vector2(w, h)) return;
            lastSafeLayout = new Vector2(w, h);
            lastSafe = sa;
            float sx = w / Screen.width, sy = h / Screen.height;
            safe.style.left = sa.xMin * sx;
            safe.style.right = (Screen.width - sa.xMax) * sx;
            safe.style.top = (Screen.height - sa.yMax) * sy;
            safe.style.bottom = sa.yMin * sy;
            menuTheme.SetSafeInsets(sa.xMin * sx, (Screen.height - sa.yMax) * sy,
                (Screen.width - sa.xMax) * sx, sa.yMin * sy);
        }

        void UpdateCoins(float dt, in HudInfo h)
        {
            if (h.money != shownMoney)
            {
                if (shownMoney >= 0) coinBump = 1f;
                shownMoney = h.money;
                foreach (var l in coinLabels) l.text = h.money.ToString();
                homeCoinValue.text = ShortWallet(h.money);
                homeCoinValue.tooltip = h.money.ToString();
            }
            if (h.best != shownBest)
            {
                shownBest = h.best;
                foreach (var l in bestLabels) l.text = "Best " + TrackLibrary.Show(h.best) + " m";
            }
            coinBump = Mathf.MoveTowards(coinBump, 0f, dt * 3f);
            float s = 1f + 0.18f * Mathf.Sin(coinBump * Mathf.PI) * coinBump;
            foreach (var p in coinPills) p.style.scale = new Scale(new Vector2(s, s));

            if (h.gems != shownGems)
            {
                if (shownGems >= 0) gemBump = 1f;
                shownGems = h.gems;
                foreach (var l in gemLabels) l.text = h.gems.ToString();
                homeGemValue.text = ShortWallet(h.gems);
                homeGemValue.tooltip = h.gems.ToString();
            }
            gemBump = Mathf.MoveTowards(gemBump, 0f, dt * 3f);
            float g = 1f + 0.2f * Mathf.Sin(gemBump * Mathf.PI) * gemBump;
            foreach (var p in gemPills) p.style.scale = new Scale(new Vector2(g, g));
        }

        // ================================================================ ana ekran

        void BuildHub()
        {
            var pageRoot = NewPage(Page.Hub);
            pageRoot.style.paddingLeft = pageRoot.style.paddingRight = pageRoot.style.paddingTop = pageRoot.style.paddingBottom = 0;
            var p = homeCanvas = E("winter-home");
            p.name = "home-canvas";
            p.style.position = Position.Absolute;
            p.style.left = p.style.top = 0;
            p.style.transformOrigin = new TransformOrigin(0, 0);
            pageRoot.Add(p);

            var top = E("home-topbar");
            var wallet = E("home-wallet");
            var coins = HomeCurrency(CoinPill(), "home-coins-plus");
            coins.AddToClassList("home-first");
            wallet.Add(coins);
            wallet.Add(HomeCurrency(GemPill(), "home-gems-plus"));
            wallet.Add(LaunchPill());
            top.Add(wallet);
            var gear = B(() => ShowSettings(true), "btn", "home-gear");
            gear.name = "home-settings";
            gear.style.backgroundImage = new StyleBackground(VGrad(new Color32(69, 181, 251, 255), new Color32(28, 101, 219, 255)));
            gear.Add(new Icon(IconKind.Gear, 64));
            top.Add(gear);
            p.Add(top);

            var title = B(() => OpenMap?.Invoke(), "hub-title");
            title.name = "home-track";
            homeBannerArt = CreateHomeArt(title, "home-banner-art", "home-art-banner");
            hubRibbon = E("hub-ribbon");
            hubTrackName = L("", "hub-ribbon-text");
            hubRibbon.Add(hubTrackName);
            title.Add(hubRibbon);
            hubTrackSub = L("", "hub-sub");
            title.Add(hubTrackSub);
            var slim = E("hub-bar");
            hubTrackFill = E("hub-bar-fill");
            slim.Add(hubTrackFill);
            slim.Add(E("hub-bar-shine"));
            hubTrackPct = L("0%", "hub-bar-pct");
            slim.Add(hubTrackPct);
            var flag = new Icon(IconKind.Finish, 46);
            flag.AddToClassList("hub-bar-flag");
            slim.Add(flag);
            title.Add(slim);
            // MAP kartı kaldırıldı (kullanıcı 2026-10-23): haritalara parkur adına dokunarak gidilir; altında küçük ipucu.
            var allMaps = L("SHOW ALL MAPS  ›", "heavy");
            allMaps.name = "home-all-maps";
            allMaps.style.fontSize = 30;
            allMaps.style.color = Color.white;
            allMaps.style.unityTextOutlineWidth = 4;
            allMaps.style.unityTextOutlineColor = new Color(0f, 0f, 0f, 0.45f);
            allMaps.style.alignSelf = Align.Center;
            allMaps.style.marginTop = 4;
            // Plakanın hemen altında, ona sabitli (ana ekran öğeleri mutlak konumlu: ayrı öğe en tepeye kayıyordu).
            allMaps.pickingMode = PickingMode.Ignore;
            allMaps.style.position = Position.Absolute;
            allMaps.style.left = allMaps.style.right = 0;
            allMaps.style.bottom = -46;
            allMaps.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.Add(allMaps);
            p.Add(title);

            var mid = E("home-stage");
            var left = E("home-left");
            var avatar = SideCard(() => OpenAvatars?.Invoke(), new Color32(38, 139, 246, 255), new Color32(23, 86, 197, 255));
            avatar.name = "home-hero";
            avatar.AddToClassList("home-hero");
            hubAvatarPortrait = E("avatar-portrait", "home-side-art");
            avatar.Add(hubAvatarPortrait);
            avatar.Add(L("HERO", "side-label"));
            left.Add(avatar);
            chestBtn = SideCard(() => OpenChest?.Invoke(), new Color32(255, 185, 31, 255), new Color32(221, 123, 9, 255));
            chestBtn.name = "home-chest";
            chestBtn.AddToClassList("home-chest");
            chestIcon = WinterHomeArt.Chest(144);
            chestIcon.AddToClassList("home-side-art");
            chestIcon.style.width = StyleKeyword.Null;
            chestIcon.style.height = StyleKeyword.Null;
            chestIcon.style.flexShrink = StyleKeyword.Null;
            chestBtn.Add(chestIcon);
            chestLabel = L("OPEN!", "side-label");
            chestBtn.Add(chestLabel);
            homeChestDot = L("!", "home-attention");
            homeChestDot.style.display = DisplayStyle.None;
            chestBtn.Add(homeChestDot);
            left.Add(chestBtn);
            var quests = SideCard(() => OpenQuests?.Invoke(), new Color32(141, 78, 236, 255), new Color32(86, 43, 186, 255));
            quests.name = "home-quests";
            quests.AddToClassList("home-quests");
            var clipboard = WinterHomeArt.Clipboard(142);
            clipboard.AddToClassList("home-side-art");
            clipboard.style.width = StyleKeyword.Null;
            clipboard.style.height = StyleKeyword.Null;
            clipboard.style.flexShrink = StyleKeyword.Null;
            quests.Add(clipboard);
            quests.Add(L("QUESTS", "side-label"));
            questDot = L("!", "home-attention");
            questDot.style.display = DisplayStyle.None;
            quests.Add(questDot);
            left.Add(quests);
            mid.Add(left);

            var right = E("home-right");
            var map = SideCard(() => OpenMap?.Invoke(), new Color32(29, 189, 213, 255), new Color32(9, 133, 174, 255));
            map.name = "home-map";
            map.AddToClassList("home-map");
            var mapArt = WinterHomeArt.Map(142);
            mapArt.AddToClassList("home-side-art");
            mapArt.style.width = StyleKeyword.Null;
            mapArt.style.height = StyleKeyword.Null;
            mapArt.style.flexShrink = StyleKeyword.Null;
            map.Add(mapArt);
            map.Add(L("MAP", "side-label"));
            map.style.display = DisplayStyle.None;   // 2026-10-23: haritalar parkur adından açılır
            right.Add(map);
            var shop = SideCard(() => OpenShop?.Invoke(), new Color32(242, 68, 167, 255), new Color32(184, 34, 126, 255));
            shop.name = "home-shop";
            shop.AddToClassList("home-shop");
            var storeArt = WinterHomeArt.Shop(144);
            storeArt.AddToClassList("home-side-art");
            storeArt.style.width = StyleKeyword.Null;
            storeArt.style.height = StyleKeyword.Null;
            storeArt.style.flexShrink = StyleKeyword.Null;
            shop.Add(storeArt);
            shop.Add(L("SHOP", "side-label"));
            right.Add(shop);
            var rocket = BuildUpgradeCard((int)UpgradeType.Rocket);
            rocket.root.AddToClassList("home-rocket");
            right.Add(rocket.root);
            cards[(int)UpgradeType.Rocket] = rocket;
            mid.Add(right);
            p.Add(mid);

            stageLabel = L("", "outlined");
            stageLabel.style.position = Position.Absolute;
            stageLabel.style.left = 0;
            stageLabel.style.right = 0;
            stageLabel.style.top = Length.Percent(54);
            stageLabel.style.fontSize = 60;
            stageLabel.style.color = Gold;
            stageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            stageLabel.style.display = DisplayStyle.None;
            p.Add(stageLabel);

            playPill = E("play-wrap", "home-play");
            var play = B(() => Play?.Invoke(), "btn", "play-btn");
            play.name = "home-play";
            homePlayArt = CreateHomeArt(play, "home-play-art", "home-art-play");
            var triangle = new Icon(IconKind.Play, 74, Color.white);
            triangle.AddToClassList("home-play-icon");
            play.Add(triangle);
            playHint = L("PLAY", "play-text");
            play.Add(playHint);
            play.RegisterCallback<PointerDownEvent>(_ => homePlayHeld = true);
            play.RegisterCallback<PointerUpEvent>(_ => { homePlayHeld = false; homeRelease = 1f; });
            play.RegisterCallback<PointerCaptureOutEvent>(_ => homePlayHeld = false);
            play.RegisterCallback<PointerCancelEvent>(_ => homePlayHeld = false);
            playButton = play;
            playPill.Add(play);
            p.Add(playPill);

            var panel = E("upg-panel", "home-upgrades");
            homeTrayArt = CreateHomeArt(panel, "home-upgrades-art", "home-art-tray");
            var head = E("row", "upg-head");
            head.Add(E("upg-line"));
            head.Add(L("UPGRADES", "upg-title"));
            head.Add(E("upg-line"));
            panel.Add(head);
            var row = E("home-upgrade-row");
            panel.Add(row);
            p.Add(panel);
            for (int i = 0; i < (int)UpgradeType.Rocket; i++)
            {
                var c = BuildUpgradeCard(i);
                if (i == 0) c.root.AddToClassList("home-first");
                cards[i] = c;
                row.Add(c.root);
            }
            // Built-in Button themes can override an inherited Font with their FontDefinition.
            // Assign the existing game font directly to home labels so headings stay consistently bold.
            var homeFont = Resources.Load<Font>("UI/Fonts/Baloo2-ExtraBold");
            if (homeFont != null) p.Query<Label>().ForEach(label =>
            {
                label.style.unityFontDefinition = FontDefinition.FromFont(homeFont);
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            });
        }

        VisualElement HomeCurrency(VisualElement pill, string buttonName)
        {
            if (buttonName == "home-coins-plus") homeCoinValue = pill.Q<Label>();
            else homeGemValue = pill.Q<Label>();
            pill.AddToClassList("home-currency");
            pill.AddToClassList("pick");
            // The entire wallet is a touch target; the smaller plus remains keyboard-accessible.
            pill.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target != pill) return;
                click?.Invoke();
                OpenShop?.Invoke();
            });
            var plus = B(() => OpenShop?.Invoke(), "home-plus");
            plus.name = buttonName;
            plus.Add(WinterHomeArt.Plus(42));
            pill.Add(plus);
            return pill;
        }

        static string ShortWallet(int value)
        {
            if (value >= 1000000000) return (value / 1000000000f).ToString("0.#", CultureInfo.InvariantCulture) + "B";
            if (value >= 1000000) return (value / 1000000f).ToString("0.#", CultureInfo.InvariantCulture) + "M";
            if (value >= 10000) return (value / 1000f).ToString("0.#", CultureInfo.InvariantCulture) + "K";
            return value.ToString();
        }

        static VisualElement CreateHomeArt(VisualElement parent, string css, string name)
        {
            var art = E(css);
            art.name = name;
            art.pickingMode = PickingMode.Ignore;
            parent.AddToClassList("home-has-art");
            parent.Add(art);
            return art;
        }

        void ApplyHomeTheme(int track)
        {
            HomeTrackTheme.Apply(track, homeBannerArt, homePlayArt, homeTrayArt, homeCanvas);
            menuTheme.Apply(track);
            var palette = HomeTrackTheme.Get(track);
            // The offer's opaque blue card used to hide most of the surrounding landscape.
            offerCard.style.backgroundImage = new StyleBackground(VGrad(Color.Lerp(palette.panel, palette.edge, 0.38f), palette.panel));
            offerCard.style.borderTopColor = offerCard.style.borderLeftColor = offerCard.style.borderRightColor = Color.Lerp(palette.edge, Color.white, 0.45f);
            offerCard.style.borderBottomColor = Color.Lerp(palette.panel, Color.black, 0.3f);
            shopBody.Query<Label>(className: "shop-section").ForEach(section =>
            {
                Color color = palette.panel;
                color.a = 0.88f;
                section.style.backgroundColor = color;
                section.style.borderBottomColor = Color.Lerp(palette.panel, Color.black, 0.3f);
            });
            bool winter = track == 3;
            // Keep category colors recognizable; only the rim and surrounding surfaces follow the track.
            Color rim = winter ? (Color)new Color32(239, 250, 255, 255) : Color.Lerp(palette.edge, Color.white, 0.72f);
            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                card.root.style.borderTopColor = card.root.style.borderLeftColor = card.root.style.borderRightColor = rim;
                card.sub.style.color = winter ? new StyleColor(StyleKeyword.Null) : new StyleColor(palette.panel);
                var strip = card.top.Q<VisualElement>(className: "pcard-name");
                strip.style.backgroundColor = winter ? new StyleColor(StyleKeyword.Null)
                    : new StyleColor(Color.Lerp(UpgradeDark[i], palette.panel, 0.4f));
            }
            hubTrackName.style.unityTextOutlineColor = winter ? new StyleColor(StyleKeyword.Null) : new StyleColor(palette.panel);
            hubTrackSub.style.color = winter ? new StyleColor(StyleKeyword.Null) : new StyleColor(Color.Lerp(palette.edge, Color.white, 0.7f));
            var bar = hubTrackFill.parent;
            bar.style.backgroundColor = winter ? new StyleColor(StyleKeyword.Null) : new StyleColor(Color.Lerp(palette.panel, Color.black, 0.35f));
            StyleColor barEdge = winter ? new StyleColor(StyleKeyword.Null) : new StyleColor(rim);
            bar.style.borderTopColor = bar.style.borderRightColor = bar.style.borderBottomColor = bar.style.borderLeftColor = barEdge;
        }

        void ConfigureMenuBackgrounds()
        {
            menuTheme.Register(pages[Page.Map], "map", 0.10f);
            menuTheme.Register(pages[Page.Result], "result", 0.42f);
            menuTheme.Register(settings, "settings", 0.40f);
            menuTheme.Register(chestModal, "chest", 0.38f);
            menuTheme.Register(questModal, "quests", 0.38f);
            menuTheme.Register(loginModal, "login", 0.38f);
            menuTheme.Register(partModal, "part", 0.40f);
            menuTheme.Register(shopModal, "shop", 0.06f);
            menuTheme.Register(offerModal, "offer", 0.38f);
            menuTheme.Register(launchModal, "launches", 0.40f);
            menuTheme.Register(pauseModal, "pause", 0.48f);
            menuTheme.Register(adOverlay, "ad", 0.78f);
            // Only decorate the hero menu's header and grid: the middle is the live 3D preview.
            menuTheme.Register(pages[Page.Avatars].Q<VisualElement>(className: "topbar"), "heroes-top", 0.12f);
            menuTheme.Register(avScroll.contentViewport, "heroes-grid", 0.18f);
            loginModal.name = "menu-login";
            offerModal.name = "menu-offer";
            partModal.name = "menu-part";
            launchModal.name = "menu-launches";
            pauseModal.name = "menu-pause";
            adOverlay.name = "menu-ad";
        }

        void ResizeHomeCanvas()
        {
            if (homeCanvas == null) return;
            float width = safe.layout.width, height = safe.layout.height;
            if (float.IsNaN(width) || float.IsNaN(height) || width <= 0 || height <= 0) return;
            const float designWidth = 1080f;
            float designHeight = designWidth * height / width;
            homeCanvas.style.width = designWidth;
            homeCanvas.style.height = designHeight;
            homeCanvas.style.scale = new Scale(new Vector2(width / designWidth, width / designWidth));
            homeCanvas.EnableInClassList("home-compact", designHeight < 2050f);
        }

        // Available only to the isolated screenshot harness, never to production save/UI flows.
        public void DebugSetSafeArea(Rect? value)
        {
            if (!AutoShot.Requested) return;
            debugHomeSafeArea = value;
            lastSafe = default;
            UpdateSafeArea();
        }


        /// Dairesel hız göstergesi: altı açık (270°) gri iz, hızla dolan camgöbeği yay; uçta parlak nokta.
        class SpeedGauge : VisualElement
        {
            float value;

            public SpeedGauge()
            {
                AddToClassList("gauge");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            public void SetValue(float v)
            {
                v = Mathf.Clamp01(v);
                if (Mathf.Abs(v - value) < 0.003f) return;
                value = v;
                MarkDirtyRepaint();
            }

            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                float size = Mathf.Min(r.width, r.height);
                if (size < 4f) return;
                var c = r.center;
                float rad = size * 0.5f - 16f;
                var p = ctx.painter2D;
                p.lineCap = LineCap.Round;
                const float start = 135f, sweep = 270f;
                p.lineWidth = 22f;
                p.strokeColor = new Color(0.1f, 0.13f, 0.2f, 0.45f);
                p.BeginPath();
                p.Arc(c, rad, Angle.Degrees(start), Angle.Degrees(start + sweep));
                p.Stroke();
                if (value > 0.002f)
                {
                    p.strokeColor = Color.Lerp(new Color32(0x3E, 0xE0, 0xF0, 0xFF), new Color32(0xFF, 0x8F, 0x1F, 0xFF), Mathf.InverseLerp(0.75f, 1f, value));
                    p.BeginPath();
                    p.Arc(c, rad, Angle.Degrees(start), Angle.Degrees(start + sweep * value));
                    p.Stroke();
                }
            }
        }

        /// Profesyonel yükseltme kartı: renkli degrade üst bölümde parçanın o anki seviyedeki canlı 3D önizlemesi
        /// (arkasında ışık halesi), sol üstte seviye rozeti, sağ üstte toplam kademe, önizlemenin altında ad şeridi;
        /// altta seviye adı, 4 parçalı kademe göstergesi ve parlayan satın alma butonu.
        UpgradeCard BuildUpgradeCard(int i)
        {
            Color main = UpgradeColors[i], dark = UpgradeDark[i];
            var c = new UpgradeCard { root = E("pcard", "pick") };
            c.root.style.borderBottomColor = dark;
            // Icy white rim and coloured lower extrusion match the home design system.
            Color frame = new Color32(239, 250, 255, 255);
            c.root.style.borderTopColor = c.root.style.borderLeftColor = c.root.style.borderRightColor = frame;

            var top = c.top = E("pcard-top");
            top.style.backgroundImage = new StyleBackground(VGrad(Color.Lerp(main, Color.white, 0.55f), main));
            var glow = E("pcard-glow");
            glow.style.backgroundImage = new StyleBackground(Radial());
            top.Add(glow);
            c.preview = E("pcard-preview");
            top.Add(c.preview);
            var gloss = E("pcard-gloss");
            gloss.pickingMode = PickingMode.Ignore;
            top.Add(gloss);

            var lvl = E("pcard-lvl");
            lvl.style.backgroundColor = dark;
            var lvlCap = L("LV", "pcard-lvl-cap");
            lvl.Add(lvlCap);
            c.level = L("1", "pcard-lvl-num");
            lvl.Add(c.level);
            top.Add(lvl);
            c.badge = L("0/20", "pcard-count");
            top.Add(c.badge);
            // Bir sonraki kademenin bu pistte yaklaşık kazandıracağı mesafe (GainPredictor)
            c.gain = L("", "pcard-gain");
            c.gain.style.display = DisplayStyle.None;
            top.Add(c.gain);

            var ribbon = E("pcard-name");
            var name = c.title = L(Upgrades.Names[i].ToUpperInvariant(), "outlined");
            ribbon.Add(name);
            top.Add(ribbon);
            c.root.Add(top);

            c.sub = L("", "pcard-stage");
            c.root.Add(c.sub);

            c.pipRow = E("pcard-pips");
            for (int k = 0; k < c.pips.Length; k++)
            {
                c.pips[k] = E("pcard-pip");
                c.pipRow.Add(c.pips[k]);
            }
            c.root.Add(c.pipRow);
            // Eski ilerleme çubuğu (gizli; bazı yerler hâlâ başvuruyor)
            c.bar = E("ucard-bar");
            c.fill = E("ucard-fill");
            c.bar.Add(c.fill);
            c.bar.style.display = DisplayStyle.None;
            c.root.Add(c.bar);

            c.buy = B(() => Buy?.Invoke(i), "btn", "pcard-btn");
            c.buy.name = "home-upgrade-" + i;
            c.shine = E("pcard-shine");
            c.shine.pickingMode = PickingMode.Ignore;
            c.buy.Add(c.shine);
            // Üstte "UPGRADE", altında ikon + fiyat (iki satır).
            c.buy.Add(L("UPGRADE", "pcard-btn-cap"));
            var priceRow = E("row");
            priceRow.pickingMode = PickingMode.Ignore;
            priceRow.style.marginTop = -6;
            priceRow.style.height = 48;
            priceRow.style.flexShrink = 0;
            c.coin = new Icon(IconKind.Coin, 38);
            priceRow.Add(c.coin);
            c.gem = new Icon(IconKind.Gem, 36);
            priceRow.Add(c.gem);
            c.lockIcon = new Icon(IconKind.Lock, 30, Color.white);
            priceRow.Add(c.lockIcon);
            c.cost = Sp(L(""), 6);
            c.cost.style.height = 48;
            c.cost.style.unityTextAlign = TextAnchor.MiddleCenter;
            priceRow.Add(c.cost);
            c.buy.Add(priceRow);
            c.root.Add(c.buy);
            return c;
        }

        /// Yan buton: degrade renkli, beyaz çerçeveli, altı koyu gölgeli yuvarlak köşeli kart.
        Button SideCard(Action onClick, Color main, Color dark)
        {
            var b = B(onClick, "btn", "side-card");
            b.style.backgroundImage = new StyleBackground(VGrad(Color.Lerp(main, Color.white, 0.3f), main));
            b.style.backgroundColor = main;
            b.style.borderBottomColor = dark;
            var gl = E("side-glow");
            gl.style.backgroundImage = new StyleBackground(Radial());
            gl.pickingMode = PickingMode.Ignore;
            b.Add(gl);
            return b;
        }

        static readonly Dictionary<int, Texture2D> gradients = new Dictionary<int, Texture2D>();
        static Texture2D radial;

        /// Dikey degrade (üst → alt) dokusu.
        static Texture2D VGrad(Color top, Color bottom)
        {
            int key = ((Color32)top).GetHashCode() * 31 + ((Color32)bottom).GetHashCode();
            if (gradients.TryGetValue(key, out var tex)) return tex;
            tex = new Texture2D(2, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++)
            {
                var col = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, y / 63f));
                tex.SetPixel(0, y, col);
                tex.SetPixel(1, y, col);
            }
            tex.Apply();
            gradients[key] = tex;
            return tex;
        }

        /// Ortası parlak, kenara doğru sönen beyaz hale.
        static Texture2D Radial()
        {
            if (radial != null) return radial;
            const int N = 64;
            radial = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    radial.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * 0.85f));
                }
            radial.Apply();
            return radial;
        }

        /// Kartları kayıtla eşitler; bought >= 0 ise o kart "satın alındı" diye zıplar.
        /// Kartlarda bir sonraki kademenin tahmini mesafe kazancı ("+120 m"); hesaplanmadıysa ya da ihmal edilebilirse gizli.
        public void SetGains(GainPredictor g)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                if (c == null || c.gain == null) continue;
                float m = g.Gain(i);
                string text = m >= 1f ? "+" + TrackLibrary.Show(m) + " m" : "";
                if (text == c.gainText) continue;
                c.gainText = text;
                c.gain.text = text;
                c.gain.style.display = text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void RefreshHub(SaveData save, int bought)
        {
            int tier = save.unlocked;
            for (int i = 0; i < Upgrades.Count; i++)
            {
                var c = cards[i];
                // Yükseltmeler pist başına: kademe sınırı ve kartın adı (Kızak kartı pistin aracı) geçerli pistin.
                int lvl = save.levels[i], max = Upgrades.MaxLevel(i), cap = max;
                c.title.text = Upgrades.Name(i).ToUpperInvariant();
                c.badge.text = lvl + "/" + max;
                c.level.text = Mathf.Min(lvl / Upgrades.StageSize + 1, Upgrades.Stages).ToString();
                var preview = PartPreview.Instance;
                if (preview != null)
                {
                    preview.Refresh(save.levels);
                    if (preview.Get(i) is RenderTexture rt) c.preview.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
                }
                bool staged = true;
                // Aşama: içinde bulunulan (ya da açılmış sonuncusu tamamlanmışsa o) aşama ve 4 boncuktan kaçı dolu.
                int stageNum = Mathf.Max(1, Mathf.Min(lvl / Upgrades.StageSize + 1, Mathf.Max(cap, Upgrades.StageSize) / Upgrades.StageSize));
                int inStage = Mathf.Clamp(lvl - (stageNum - 1) * Upgrades.StageSize, 0, Upgrades.StageSize);
                string look = PartVisuals.StageName(i, Upgrades.Stage(lvl));
                c.sub.text = i == (int)UpgradeType.Income ? look + " · x" + Upgrades.IncomeMultiplier(lvl).ToString("0.##", Tr) : look;
                c.fill.style.width = Length.Percent(cap > 0 ? 100f * lvl / cap : 0f);
                c.bar.style.display = staged ? DisplayStyle.None : DisplayStyle.Flex;
                c.pipRow.style.display = staged ? DisplayStyle.Flex : DisplayStyle.None;
                for (int k = 0; k < c.pips.Length; k++)
                {
                    bool on = cap > 0 && k < inStage;
                    c.pips[k].style.backgroundColor = on ? UpgradeColors[i] : (Color)new Color32(0xE9, 0xE3, 0xDA, 0xFF);
                    c.pips[k].style.borderBottomColor = on ? UpgradeDark[i] : (Color)new Color32(0xD6, 0xCE, 0xC3, 0xFF);
                }
                c.root.style.opacity = cap == 0 ? 0.75f : 1f;

                bool maxed = lvl >= max;
                bool tierLocked = !maxed && lvl >= cap;
                int cost = maxed || tierLocked ? 0 : Upgrades.Cost(i, lvl);
                bool afford = !maxed && !tierLocked && save.money >= cost;
                // Coin yetmiyorsa ama elmas yetiyorsa seviye elmasla alınabilir.
                int gemCost = maxed || tierLocked ? 0 : Upgrades.GemCost(i, lvl);
                bool gemPay = !afford && !maxed && !tierLocked && save.gems >= gemCost;

                c.coin.style.display = !maxed && !tierLocked && !gemPay ? DisplayStyle.Flex : DisplayStyle.None;
                c.gem.style.display = gemPay ? DisplayStyle.Flex : DisplayStyle.None;
                c.lockIcon.style.display = tierLocked ? DisplayStyle.Flex : DisplayStyle.None;
                if (maxed) c.cost.text = "MAX";
                else if (tierLocked) c.cost.text = "MAX";
                else c.cost.text = gemPay ? gemCost.ToString() : cost.ToString();
                c.cost.style.fontSize = c.cost.text.Length <= 4 ? 48 : c.cost.text.Length == 5 ? 44 : 38;

                // Buton: alınabilirse yeşil (parıltılı), elmasla pembe, tamamlandıysa altın, yetmiyorsa gri.
                Color green = new Color32(0x3F, 0xD1, 0x5B, 0xFF), greenDark = new Color32(0x22, 0xA0, 0x3C, 0xFF);
                Color goldC = new Color32(0xFF, 0xC4, 0x2E, 0xFF), goldDark = new Color32(0xD0, 0x8A, 0x0B, 0xFF);
                Color btnTop = afford ? green : gemPay ? GemPink : maxed ? goldC : CardGray;
                Color btnDark = afford ? greenDark : gemPay ? GemPinkDark : maxed ? goldDark : CardGrayDark;
                c.buy.style.backgroundImage = new StyleBackground(VGrad(Color.Lerp(btnTop, Color.white, 0.25f), btnTop));
                c.buy.style.backgroundColor = btnTop;
                c.buy.style.borderBottomColor = btnDark;
                c.canBuy = afford || gemPay;
                if (i == bought) c.bump = 1f;
            }

            hubAvatarPortrait.style.backgroundImage = Portrait(save.avatar);
            hubAvatar = save.avatar;

            int t = save.track;
            float progress = Mathf.Clamp01(save.bests[t] / TrackLibrary.Lengths[t]);
            hubTrackName.text = TrackLibrary.Names[t].ToUpperInvariant();
            hubTrackSub.text = "Track " + (t + 1) + "  ·  Target: " + TrackLibrary.Show(TrackLibrary.Lengths[t]) + " m";
            hubTrackPct.text = Mathf.FloorToInt(progress * 100f) + "%";
            homeProgressTarget = progress;
            if (homeTrack != t)
            {
                ApplyHomeTheme(t);
                homeTrack = t;
                homeProgress = progress;
                hubTrackFill.style.width = Length.Percent(progress * 100f);
            }
            var fillCol = save.Finished(t) ? (Color)new Color32(0x3F, 0xD1, 0x5B, 0xFF) : (Color)new Color32(0xFF, 0xB0, 0x20, 0xFF);
            hubTrackFill.style.backgroundImage = new StyleBackground(VGrad(Color.Lerp(fillCol, Color.white, 0.4f), fillCol));
        }

        /// Bir yükseltmenin aşaması tamamlanınca ana ekranda kutlama.
        public void StageUp(string text)
        {
            stageLabel.text = text;
            stageClock = 0f;
            stageLabel.style.display = DisplayStyle.Flex;
        }

        void TickHub(float dt, in HudInfo h)
        {
            if (stageClock >= 0f)
            {
                stageClock += dt;
                float k = EaseOutBack(Mathf.Clamp01(stageClock / 0.4f));
                stageLabel.style.scale = new Scale(new Vector2(k, k));
                stageLabel.style.opacity = Mathf.Clamp01((2.4f - stageClock) / 0.5f);
                if (stageClock > 2.4f)
                {
                    stageClock = -1f;
                    stageLabel.style.display = DisplayStyle.None;
                }
            }
            homeRelease = Mathf.MoveTowards(homeRelease, 0f, dt * 3.5f);
            float spring = Mathf.Sin((1f - homeRelease) * Mathf.PI * 3f) * homeRelease * 0.04f;
            float s = homePlayHeld ? 0.96f : 1f + 0.01f * Mathf.Sin(time * 2.2f) + spring;
            playButton.style.scale = new Scale(new Vector2(s, s));
            if (Mathf.Abs(homeProgress - homeProgressTarget) > 0.0001f)
            {
                homeProgress = Mathf.MoveTowards(homeProgress, homeProgressTarget, dt * 0.8f);
                hubTrackFill.style.width = Length.Percent(homeProgress * 100f);
            }

            bool chestReady = h.chestSeconds <= 0;
            string chestText = chestReady ? "OPEN!" : (h.chestSeconds / 60) + ":" + (h.chestSeconds % 60).ToString("00");
            if (chestLabel.text != chestText) chestLabel.text = chestText;
            chestLabel.style.color = Color.white;
            homeChestDot.style.display = chestReady ? DisplayStyle.Flex : DisplayStyle.None;
            float wiggle = chestReady ? 3f * Mathf.Sin(time * 5f) * Mathf.Max(0f, Mathf.Sin(time * 0.8f)) : 0f;
            chestIcon.style.rotate = new Rotate(Angle.Degrees(wiggle));

            for (int ci = 0; ci < cards.Length; ci++)
            {
                var c = cards[ci];
                c.bump = Mathf.MoveTowards(c.bump, 0f, dt * 3f);
                c.shake = Mathf.MoveTowards(c.shake, 0f, dt * 2.5f);
                float k = 1f + 0.12f * Mathf.Sin(c.bump * Mathf.PI);
                c.root.style.scale = new Scale(new Vector2(k, k));
                c.root.style.translate = new Translate(Mathf.Sin(time * 60f) * 14f * c.shake, 0f);
                // A short confirmation glint only after a purchase; press states stay under USS control.
                c.shine.style.display = c.bump > 0f ? DisplayStyle.Flex : DisplayStyle.None;
                if (c.bump > 0f) c.shine.style.left = Length.Percent(Mathf.Lerp(-30f, 120f, 1f - c.bump));
            }

            handLeft.EnableInClassList("on", h.hand < 0);
            handRight.EnableInClassList("on", h.hand > 0);
            soundOn.EnableInClassList("on", h.sound);
            soundOff.EnableInClassList("on", !h.sound);
            for (int k = 0; k < sensButtons.Length; k++) sensButtons[k]?.EnableInClassList("on", h.sensitivity == k);
        }

        void BuildSettings()
        {
            settings = E("fill", "dim", "center", "pick");
            settings.name = "home-settings-modal";
            settings.style.display = DisplayStyle.None;

            settingsCard = E("card", "card-cream", "pop", "off");
            settingsCard.style.width = Length.Percent(86);
            settingsCard.style.paddingLeft = settingsCard.style.paddingRight = 44;
            settingsCard.style.paddingBottom = 44;

            var title = L("SETTINGS", "outlined");
            title.style.fontSize = 84;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = 10;
            settingsCard.Add(title);

            settingsCard.Add(SettingLabel("Which hand holds the cup?"));
            var hand = E("seg");
            handLeft = Seg("LEFT", () => SetHand?.Invoke(-1));
            handRight = Seg("RIGHT", () => SetHand?.Invoke(1));
            hand.Add(handLeft);
            hand.Add(handRight);
            settingsCard.Add(hand);

            settingsCard.Add(SettingLabel("Steering sensitivity"));
            var sens = E("seg");
            string[] sensNames = { "LOW", "MEDIUM", "HIGH" };
            for (int k = 0; k < 3; k++)
            {
                int v = k;
                sensButtons[k] = Seg(sensNames[k], () => SetSensitivity?.Invoke(v));
                sens.Add(sensButtons[k]);
            }
            settingsCard.Add(sens);

            settingsCard.Add(SettingLabel("Sound"));
            var sound = E("seg");
            soundOn = Seg("ON", () => SetSound?.Invoke(true));
            soundOff = Seg("OFF", () => SetSound?.Invoke(false));
            sound.Add(soundOn);
            sound.Add(soundOff);
            settingsCard.Add(sound);

            // Test düğmeleri (kullanıcı isteği 2026-10-10): yalnızca geliştirme sürümünde görünür; yayın sürümünde yok.
            if (Debug.isDebugBuild)
            {
                settingsCard.Add(SettingLabel("Test (dev build only)"));
                var reset = B(() => { TestResetUpgrades?.Invoke(); ShowSettings(false); }, "btn", "btn-orange", "btn-mid");
                reset.text = "Reset progress";
                reset.style.alignSelf = Align.Stretch;
                settingsCard.Add(reset);
                var unlock = B(() => { TestUnlockAll?.Invoke(); ShowSettings(false); }, "btn", "btn-white", "btn-mid");
                unlock.text = "Unlock all tracks";
                unlock.style.alignSelf = Align.Stretch;
                unlock.style.marginTop = 14;
                settingsCard.Add(unlock);
                var offerTest = B(() => { ShowSettings(false); TestOffer?.Invoke(); }, "btn", "btn-white", "btn-mid");
                offerTest.text = "Show daily deal";
                offerTest.style.alignSelf = Align.Stretch;
                offerTest.style.marginTop = 14;
                settingsCard.Add(offerTest);
                var money = B(() => { TestMoney?.Invoke(); ShowSettings(false); }, "btn", "btn-white", "btn-mid");
                money.text = "+100k coins, +100 gems";
                money.style.alignSelf = Align.Stretch;
                money.style.marginTop = 14;
                settingsCard.Add(money);
            }

            var ok = B(() => ShowSettings(false), "btn", "btn-green", "btn-mid");
            ok.name = "home-settings-close";
            ok.text = "OK";
            ok.style.marginTop = 46;
            settingsCard.Add(ok);

            settings.Add(settingsCard);
            safe.Add(settings);
        }

        Label SettingLabel(string text)
        {
            var l = L(text, "heavy");
            l.style.fontSize = 40;
            l.style.marginTop = 30;
            l.style.marginBottom = 12;
            return l;
        }

        Button Seg(string text, Action action)
        {
            var b = B(action, "seg-btn");
            b.text = text;
            return b;
        }

        void ShowSettings(bool show)
        {
            if (show)
            {
                settings.style.display = DisplayStyle.Flex;
                settingsCard.AddToClassList("off");
                settingsCard.schedule.Execute(() => settingsCard.RemoveFromClassList("off")).StartingIn(30);
            }
            else
            {
                settingsCard.AddToClassList("off");
                settings.schedule.Execute(() => settings.style.display = DisplayStyle.None).StartingIn(180);
            }
        }

        /// Otomatik ekran görüntüsü testi için.
        public void DebugSettings(bool show) => ShowSettings(show);
        public void DebugTripleChest() => ShowAd(() => SetChestMultiplier(3));

        // ================================================================ sandık

        RayBurst chestRays;
        RewardConfetti chestConfetti;

        /// Test: açık bütün pencereleri (görev, ödül, sandık, fırsat...) kapatır.
        public void DebugScrollMapEnd() => mapScroll.ScrollTo(mapCards[TrackLibrary.Count - 1].root);

        public void DebugCloseModals()
        {
            foreach (var child in safe.Children())
                if (child.ClassListContains("dim")) child.style.display = DisplayStyle.None;
        }

        void BuildChest()
        {
            chestModal = E("fill", "dim", "center", "pick");
            chestModal.name = "home-chest-modal";
            chestModal.style.display = DisplayStyle.None;

            // 2026-10-23 (örnek görsele göre): kart yok; dönen ışın patlaması önünde büyük sandık, düşen konfeti.
            chestCard = E("col", "pop", "off");
            chestCard.style.width = Length.Percent(84);
            chestCard.style.alignItems = Align.Center;

            var stage = E("center");
            stage.style.width = 640;
            stage.style.height = 560;
            chestRays = new RayBurst();
            stage.Add(chestRays);
            var big = new Icon(IconKind.Chest, 400);
            big.name = "chest-big";
            stage.Add(big);
            chestCard.Add(stage);
            chestConfetti = new RewardConfetti();
            chestModal.Add(chestConfetti);

            var coinRow = E("row");
            coinRow.Add(new Icon(IconKind.Coin, 90));
            chestCoins = Sp(L("0", "outlined"), 14);
            chestCoins.style.fontSize = 140;
            coinRow.Add(chestCoins);
            chestCard.Add(coinRow);

            chestGemRow = E("row");
            chestGemRow.Add(new Icon(IconKind.Gem, 70));
            chestGems = Sp(L("+1", "outlined"), 12);
            chestGems.style.fontSize = 110;
            chestGemRow.Add(chestGems);
            chestCard.Add(chestGemRow);

            chestTriple = B(() => ShowAd(() => SetChestMultiplier(3)), "btn", "btn-orange", "btn-big");
            chestTriple.style.alignSelf = Align.Stretch;
            chestTriple.style.marginTop = 30;
            chestTriple.Add(new Icon(IconKind.Video, 70));
            chestTriple.Add(Sp(L("TRIPLE IT"), 16));
            chestCard.Add(chestTriple);

            var go = B(() =>
            {
                ClaimChest?.Invoke(chestMultiplier);
                chestCard.AddToClassList("off");
                chestModal.schedule.Execute(() => chestModal.style.display = DisplayStyle.None).StartingIn(180);
            }, "btn", "btn-green", "btn-mid");
            go.name = "home-chest-claim";
            go.text = "CONTINUE";
            go.style.alignSelf = Align.Stretch;
            go.style.marginTop = 22;
            chestCard.Add(go);

            chestModal.Add(chestCard);
            safe.Add(chestModal);

            // Test reklamı: gerçek reklam ağı bağlanana kadar ödüllü reklamın yerini tutar.
            adOverlay = E("fill", "center", "pick");
            adOverlay.style.backgroundColor = new Color(0.05f, 0.04f, 0.08f, 0.94f);
            adOverlay.style.display = DisplayStyle.None;
            var adTitle = L("AD", "outlined");
            adTitle.style.fontSize = 90;
            adOverlay.Add(adTitle);
            var adSub = L("(test mode: real ads will be connected before release)", "heavy");
            adSub.style.color = new Color(1f, 1f, 1f, 0.6f);
            adSub.style.fontSize = 32;
            adOverlay.Add(adSub);
            adCount = L("3", "outlined");
            adCount.style.fontSize = 140;
            adCount.style.marginTop = 30;
            adOverlay.Add(adCount);
            root.Add(adOverlay);

            // Konuşma baloncuğu (düşüp kalkan karakterin başının üstünde): beyaz, yuvarlak, altta sivri kuyruk.
            bubble = E("bubble");
            bubble.pickingMode = PickingMode.Ignore;
            bubble.style.display = DisplayStyle.None;
            bubbleText = L("", "bubble-text");
            bubble.Add(bubbleText);
            var tail = E("bubble-tail");
            bubble.Add(tail);
            root.Insert(0, bubble);
        }

        /// Sandık ödülünü gösterir; DEVAM ile ClaimChest(çarpan) yayınlanır.
        // ================================================================ mağaza ve karşılama teklifi

        /// Mağaza olayları: reklamla ücretsiz elmas, elmasla coin paketi (dizin), gerçek parayla ürün (kimlik; ödeme
        /// sistemi bağlanana kadar yalnızca "yakında" bilgisi gösterilir).
        public event Action ShopFreeGem;
        public event Action<int> ShopBuyCoins;
        public event Action<string> ShopPurchase;

        VisualElement shopModal, shopBody, offerModal, offerCard;
        Label shopNote, shopGems, shopPreview;
        readonly Dictionary<Button, string> purchaseButtons = new Dictionary<Button, string>();
        readonly Label[] shopCoinAmounts = new Label[6];
        float shopNoteClock = -1f;

        static readonly int[] GemPacks = { 10, 30, 100, 150, 200, 500 };
        static readonly string[] GemPrices = { "$1.99", "$4.99", "$9.99", "$13.99", "$17.99", "$19.99" };
        static readonly int[] LaunchPacks = { 20, 80, 200 };
        static readonly string[] LaunchPrices = { "$0.99", "$2.49", "$4.99" };
        /// Elmasla coin paketleri (ödeme gerektirmez, hemen çalışır): elmas fiyatı; coin miktarı parkura göre (GameController).
        /// Coin paketlerinin miktarları (kullanıcı kararı 2026-10-11: altı sabit paket).
        public static readonly int[] CoinPackCoins = { 7500, 20000, 50000, 100000, 150000, 200000 };

        VisualElement Section(string text)
        {
            var l = L(text, "shop-section");
            return l;
        }

        Button PriceButton(string productId)
        {
            var b = B(() => { if (Store.PurchasesAvailable) ShopPurchase?.Invoke(productId); }, "btn", "btn-green", "shop-price");
            purchaseButtons[b] = productId;
            return b;
        }

        void BuildShop()
        {
            shopModal = E("fill", "pick", "shop-page");
            shopModal.name = "home-shop-modal";
            shopModal.style.display = DisplayStyle.None;
            // The shared track landscape is attached after all menu surfaces have been built.

            var top = E("topbar");
            var wallet = E("row");
            var gemPill = E("pill");
            gemPill.Add(new Icon(IconKind.Gem, 60));
            shopGems = Sp(L("0", "outlined"), 10);
            shopGems.style.fontSize = 46;
            gemPill.Add(shopGems);
            wallet.Add(gemPill);
            top.Add(wallet);
            var title = L("SHOP", "outlined");
            title.style.fontSize = 84;
            top.Add(title);
            var close = B(() => shopModal.style.display = DisplayStyle.None, "btn", "btn-round", "shop-close");
            close.name = "home-shop-close";
            close.text = "X";
            top.Add(close);
            shopModal.Add(top);

            shopPreview = L("STORE PREVIEW · Purchases are unavailable in this build.", "preview-note");
            shopModal.Add(shopPreview);

            shopNote = L("", "shop-note");
            shopNote.style.display = DisplayStyle.None;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            scroll.style.overflow = Overflow.Hidden;
            var inner = scroll.Q(className: "unity-scroll-view__content-and-vertical-scroll-container");
            if (inner != null) { inner.style.flexGrow = 1; inner.style.flexShrink = 1; inner.style.flexDirection = FlexDirection.Row; }
            scroll.contentViewport.style.flexGrow = 1;
            scroll.contentViewport.style.flexShrink = 1;
            scroll.contentViewport.style.overflow = Overflow.Hidden;
            scroll.contentContainer.style.flexShrink = 0;
            scroll.contentContainer.style.paddingBottom = 40;
            shopBody = scroll.contentContainer;

            // Mağazada yalnızca elmas, fırlatma hakkı ve coin satılır (kullanıcı kararı 2026-10-11); paketler günlük fırsatlarda.
            // Elmas: reklamla ücretsiz + paketler
            shopBody.Add(Section("GEMS"));
            var free = E("shop-free");
            free.Add(ShopArt("gem_free", IconKind.Gem, 110));
            var freeText = L("x1", "outlined");
            freeText.style.fontSize = 70;
            freeText.style.flexGrow = 1;
            freeText.style.marginLeft = 10;
            free.Add(freeText);
            // Günde en çok SaveData.FreeGemsPerDay kez; hak bitince düğme söner ve "Tomorrow" yazar.
            var freeBtn = shopFreeBtn = B(() => { if (shopFreeLeft > 0) ShowAd(() => ShopFreeGem?.Invoke()); }, "btn", "btn-blue", "shop-price");
            freeBtn.Add(new Icon(IconKind.Video, 56));
            shopFreeLabel = Sp(L("FREE"), 10);
            freeBtn.Add(shopFreeLabel);
            freeBtn.style.width = 340;
            free.Add(freeBtn);
            shopBody.Add(free);
            shopBody.Add(Grid(GemPacks, GemPrices, IconKind.Gem, "gems_", new[] { -1, -1, 2, -1, -1, 5 }, new[] { "POPULAR", "BEST VALUE" }));

            // Fırlatma hakkı
            shopBody.Add(Section("LAUNCHES"));
            shopBody.Add(Grid(LaunchPacks, LaunchPrices, IconKind.Slingshot, "launches_", null, null));

            // Coin paketleri
            shopBody.Add(Section("COINS"));
            VisualElement coinRow = null;
            for (int k = 0; k < CoinPackCoins.Length; k++)
            {
                if (k % 3 == 0)
                {
                    coinRow = E("row", "shop-grid");
                    shopBody.Add(coinRow);
                }
                var cell = E("card", "col", "shop-cell");
                shopCoinAmounts[k] = L("", "outlined");
                shopCoinAmounts[k].style.fontSize = 46;
                cell.Add(shopCoinAmounts[k]);
                cell.Add(ShopArt("coins_" + (k + 1), IconKind.Coin, 96 + 14 * k));
                // Coin paketleri gerçek parayla satılır (kullanıcı kararı 2026-10-11); fiyat mağazadan.
                string coinId = "coins_" + CoinPackCoins[k];
                cell.Add(PriceButton(coinId));
                coinRow.Add(cell);
            }

            shopModal.Add(scroll);
            shopModal.Add(shopNote);
            safe.Add(shopModal);
        }

        /// Ürün görseli: Resources/UI/Shop/<ad>.png varsa o (180 px kutuda), yoksa çizilen ikon.
        static VisualElement ShopArt(string name, IconKind fallback, float iconSize)
        {
            var tex = Resources.Load<Texture2D>("UI/Shop/" + name);
            if (tex == null) return new Icon(fallback, iconSize);
            var img = new VisualElement();
            img.style.width = 190;
            img.style.height = 160;
            img.style.backgroundImage = new StyleBackground(tex);
            img.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            return img;
        }

        VisualElement Grid(int[] amounts, string[] prices, IconKind icon, string idPrefix, int[] tagAt, string[] tags)
        {
            // Üçerli satırlar (sarmalı tek satır ikinci sıranın yüksekliğini hesaba katmıyordu: bölümler üst üste biniyordu).
            var grid = E("col");
            grid.style.flexShrink = 0;
            grid.style.alignItems = Align.Stretch;
            VisualElement line = null;
            int tagIndex = 0;
            for (int k = 0; k < amounts.Length; k++)
            {
                if (k % 3 == 0)
                {
                    line = E("row", "shop-grid");
                    grid.Add(line);
                }
                string id = idPrefix + amounts[k];
                var cell = E("card", "col", "shop-cell");
                var amount = L(amounts[k].ToString(), "outlined");
                amount.style.fontSize = 60;
                cell.Add(amount);
                cell.Add(ShopArt(id, icon, 84 + Mathf.Min(40, 8 * k)));
                cell.Add(PriceButton(id));
                if (Store.PurchasesAvailable && tagAt != null && System.Array.IndexOf(tagAt, k) >= 0 && tagIndex < tags.Length) cell.Add(L(tags[tagIndex++], "shop-tag"));
                line.Add(cell);
            }
            return grid;
        }

        /// coinAmounts: elmasla coin paketlerinin bu parkurdaki miktarları.
        public void ShowShop(int gems, int[] coinAmounts, int freeGemsLeft)
        {
            RefreshShop(gems, coinAmounts, freeGemsLeft);
            shopModal.style.display = DisplayStyle.Flex;
        }

        Button shopFreeBtn;
        Label shopFreeLabel;
        int shopFreeLeft;

        public void DebugShopScroll() { var sv = shopBody.parent?.parent as ScrollView ?? shopModal.Q<ScrollView>(); if (sv != null) sv.ScrollTo(shopBody[shopBody.childCount - 1]); }

        public void RefreshShop(int gems, int[] coinAmounts, int freeGemsLeft)
        {
            bool available = Store.PurchasesAvailable;
            shopPreview.style.display = available ? DisplayStyle.None : DisplayStyle.Flex;
            foreach (var pair in purchaseButtons)
            {
                pair.Key.text = available ? Store.Price(pair.Value) : "UNAVAILABLE";
                pair.Key.SetEnabled(available);
                pair.Key.EnableInClassList("purchase-unavailable", !available);
            }
            shopFreeLeft = freeGemsLeft;
            shopFreeLabel.text = freeGemsLeft > 0 ? "FREE " + freeGemsLeft + "/" + SaveData.FreeGemsPerDay : "TOMORROW";
            shopFreeBtn.style.opacity = freeGemsLeft > 0 ? 1f : 0.5f;
            shopGems.text = gems.ToString();
            for (int k = 0; k < shopCoinAmounts.Length; k++) shopCoinAmounts[k].text = coinAmounts[k].ToString("N0", Tr);
        }

        /// Mağazanın altında kısa bilgi (ör. ödeme henüz bağlı değil, elmas yetmiyor).
        public void ShopNote(string text)
        {
            shopNote.text = text;
            shopNote.style.display = DisplayStyle.Flex;
            shopNoteClock = 0f;
        }

        // ---------------------------------------------------------------- günlük fırsat penceresi
        // Kullanıcı isteği (2026-10-11, referans görsel): panel, başlık kurdelesi, metinler, kapatma ve satın alma
        // düğmeleri ayrı bileşenler; illüstrasyon ayrı bir görsel katmanı. Fiyat mağazadan (Store.Price), süre oyun
        // verisinden (bitiş zamanı) gelir; pencerede hiçbir fiyat elle yazılmaz.

        VisualElement dealRays, dealArt, dealArtFallback, dealHeadline;
        Label dealTitle, dealInfo, dealCaption, dealPriceLabel, dealOldPrice, dealTimer;
        Button dealClose, dealBuy;
        Store.Deal currentDeal;
        long dealEndTicks;
        /// Günlük fırsat penceresi kapatıldı / satın alma istendi (ürün kimliği ShopPurchase ile gider).
        public event Action DealClosed;

        void BuildOffer()
        {
            offerModal = E("fill", "dim", "center", "pick");
            offerModal.style.display = DisplayStyle.None;

            // 1) Panel
            offerCard = DealPanel();
            // 2) Başlık kurdelesi (iki ucu kuyruklu)
            offerCard.Add(DealRibbon(out dealTitle));
            // 3) Kapatma düğmesi
            dealClose = DealCloseButton(() => { Close(offerModal, offerCard); DealClosed?.Invoke(); });
            dealClose.name = "menu-offer-close";
            offerCard.Add(dealClose);
            // 4) Büyük metin (vurgulu kelime ayrı renkte)
            dealHeadline = E("deal-headline");
            offerCard.Add(dealHeadline);
            // 5) İllüstrasyon katmanı: ışık huzmesi + görsel (Resources/UI/Offers/<art>); görsel yoksa oyun içi çizim
            var artBox = E("deal-art-box");
            dealRays = E("deal-rays");
            dealRays.style.backgroundImage = new StyleBackground(Radial());
            dealRays.pickingMode = PickingMode.Ignore;
            artBox.Add(dealRays);
            dealArt = E("deal-art");
            artBox.Add(dealArt);
            dealArtFallback = E("deal-art-fallback");
            artBox.Add(dealArtFallback);
            offerCard.Add(artBox);
            // 6) Bilgi şeridi
            var infoRow = E("row", "deal-info");
            dealInfo = L("", "deal-info-text");
            infoRow.Add(dealInfo);
            infoRow.Add(new Icon(IconKind.Video, 60));
            offerCard.Add(infoRow);
            // 7) Süre (oyun verisinden) ve indirimsiz fiyat
            var meta = E("row", "deal-meta");
            dealTimer = L("", "deal-timer");
            meta.Add(dealTimer);
            dealOldPrice = L("", "deal-old");
            meta.Add(dealOldPrice);
            offerCard.Add(meta);
            // 8) Satın alma düğmesi: üstte açıklama satırı, altında mağaza fiyatı
            dealBuy = DealBuyButton(out dealCaption, out dealPriceLabel, () =>
            {
                if (Store.PurchasesAvailable) ShopPurchase?.Invoke(currentDeal.productId);
            });
            offerCard.Add(dealBuy);

            offerModal.Add(offerCard);
            safe.Add(offerModal);
        }

        VisualElement DealPanel()
        {
            var panel = E("deal-panel", "col", "pop", "off");
            panel.style.backgroundImage = new StyleBackground(VGrad(new Color32(0x3F, 0xA8, 0xFF, 0xFF), new Color32(0x1E, 0x62, 0xD8, 0xFF)));
            return panel;
        }

        VisualElement DealRibbon(out Label title)
        {
            var wrap = E("deal-ribbon-wrap");
            wrap.pickingMode = PickingMode.Ignore;
            wrap.Add(E("deal-ribbon-tail", "deal-ribbon-tail-l"));
            wrap.Add(E("deal-ribbon-tail", "deal-ribbon-tail-r"));
            var rib = E("deal-ribbon");
            rib.style.backgroundImage = new StyleBackground(VGrad(new Color32(0xFF, 0xC8, 0x4A, 0xFF), new Color32(0xFF, 0x96, 0x1A, 0xFF)));
            title = L("", "deal-ribbon-text");
            rib.Add(title);
            wrap.Add(rib);
            return wrap;
        }

        Button DealCloseButton(Action onClick)
        {
            var b = B(onClick, "btn", "deal-close");
            b.style.backgroundImage = new StyleBackground(VGrad(new Color32(0xFF, 0x5A, 0x4A, 0xFF), new Color32(0xE0, 0x1E, 0x1E, 0xFF)));
            var x = new VisualElement();
            x.pickingMode = PickingMode.Ignore;
            x.style.width = x.style.height = 52;
            x.generateVisualContent += ctx =>
            {
                var pt = ctx.painter2D;
                pt.strokeColor = Color.white;
                pt.lineWidth = 12f;
                pt.lineCap = LineCap.Round;
                pt.BeginPath(); pt.MoveTo(new Vector2(8f, 8f)); pt.LineTo(new Vector2(44f, 44f)); pt.Stroke();
                pt.BeginPath(); pt.MoveTo(new Vector2(44f, 8f)); pt.LineTo(new Vector2(8f, 44f)); pt.Stroke();
            };
            b.Add(x);
            return b;
        }

        Button DealBuyButton(out Label caption, out Label price, Action onClick)
        {
            var b = B(onClick, "btn", "deal-buy");
            b.style.backgroundImage = new StyleBackground(VGrad(new Color32(0x8C, 0xE8, 0x4A, 0xFF), new Color32(0x3F, 0xC0, 0x2E, 0xFF)));
            var gloss = E("deal-buy-gloss");
            gloss.pickingMode = PickingMode.Ignore;
            b.Add(gloss);
            caption = L("", "deal-buy-caption");
            b.Add(caption);
            price = L("", "deal-buy-price");
            b.Add(price);
            return b;
        }

        /// "GET {X3} COINS" → satır satır etiketler; { } arası sarı ve büyük.
        void SetHeadline(string text)
        {
            dealHeadline.Clear();
            foreach (var line in text.Split('\n'))
            {
                var row = E("row");
                row.style.justifyContent = Justify.Center;
                foreach (var part in System.Text.RegularExpressions.Regex.Split(line, @"(\{[^}]*\})"))
                {
                    if (part.Length == 0) continue;
                    bool hi = part[0] == '{';
                    var l = L(hi ? part.Substring(1, part.Length - 2) : part, "deal-headline-text");
                    if (hi) l.AddToClassList("deal-headline-hi");
                    row.Add(l);
                }
                dealHeadline.Add(row);
            }
        }

        /// Oyun içi çizim (görsel dosyası yokken): karakterin portresi, çevresinde tema ikonları.
        void BuildFallbackArt(string art, StyleBackground portrait)
        {
            dealArtFallback.Clear();
            IconKind kind = art == "gems" ? IconKind.Gem : art == "rocket" ? IconKind.Rocket : art == "noads" ? IconKind.Video
                          : art == "starter" ? IconKind.Chest : IconKind.Coin;
            // Arkada dağınık ikonlar (iki yay), önde ikon yığını, ortada karakter
            float[] ax = { -250f, -175f, -70f, 80f, 185f, 255f, -215f, 220f }, ay = { -60f, -150f, -185f, -180f, -140f, -50f, 60f, 70f };
            float[] asz = { 96f, 82f, 70f, 76f, 88f, 100f, 110f, 104f }, arot = { -24f, 14f, -8f, 18f, -16f, 26f, 10f, -12f };
            for (int k = 0; k < ax.Length; k++)
            {
                var ic = new Icon(kind, asz[k]);
                ic.style.position = Position.Absolute;
                ic.style.left = 300f + ax[k] - asz[k] * 0.5f;
                ic.style.top = 210f + ay[k] - asz[k] * 0.5f;
                ic.style.rotate = new Rotate(arot[k]);
                dealArtFallback.Add(ic);
            }
            var hero = E("deal-hero");
            hero.style.backgroundImage = portrait;
            dealArtFallback.Add(hero);
            for (int k = 0; k < 9; k++)
            {
                float sz = 84f + 10f * (k % 3);
                var ic = new Icon(kind, sz);
                ic.style.position = Position.Absolute;
                ic.style.left = 60f + k * 56f - (k % 2) * 10f;
                ic.style.top = 318f + (k % 2 == 0 ? 0f : 26f) + (k % 3) * 6f;
                dealArtFallback.Add(ic);
            }
        }

        /// deal: gösterilecek fırsat; endTicks: bitiş anı (UTC, oyun verisi); heroPortrait: illüstrasyon yoksa kullanılan karakter.
        public void ShowDeal(Store.Deal deal, long endTicks, int heroAvatar)
        {
            currentDeal = deal;
            dealEndTicks = endTicks;
            bool available = Store.PurchasesAvailable;
            dealTitle.text = available ? deal.title : "PACK PREVIEW";
            SetHeadline(deal.headline);
            dealInfo.text = available ? deal.info : "Purchases are unavailable in this build.";
            dealCaption.text = available ? deal.buttonCaption : "PREVIEW ONLY";
            dealPriceLabel.text = available ? Store.Price(deal.productId) : "UNAVAILABLE";
            dealBuy.SetEnabled(available);
            dealBuy.EnableInClassList("purchase-unavailable", !available);
            bool hasOld = available && !string.IsNullOrEmpty(deal.regularId);
            dealOldPrice.text = hasOld ? "Was " + Store.Price(deal.regularId) : "";
            dealOldPrice.style.display = hasOld ? DisplayStyle.Flex : DisplayStyle.None;
            var tex = Resources.Load<Texture2D>("UI/Offers/" + deal.art);
            dealArt.style.backgroundImage = tex != null ? new StyleBackground(tex) : new StyleBackground();
            dealArt.style.display = tex != null ? DisplayStyle.Flex : DisplayStyle.None;
            dealArtFallback.style.display = tex != null ? DisplayStyle.None : DisplayStyle.Flex;
            if (tex == null) BuildFallbackArt(deal.art, Portrait(heroAvatar));
            UpdateDealTimer();
            Open(offerModal, offerCard);
        }

        void UpdateDealTimer()
        {
            if (!Store.PurchasesAvailable)
            {
                dealTimer.text = "";
                dealTimer.style.display = DisplayStyle.None;
                return;
            }
            dealTimer.style.display = DisplayStyle.Flex;
            long left = System.Math.Max(0L, (dealEndTicks - System.DateTime.UtcNow.Ticks) / System.TimeSpan.TicksPerSecond);
            dealTimer.text = "Ends in " + (left / 3600).ToString("00") + ":" + (left / 60 % 60).ToString("00") + ":" + (left % 60).ToString("00");
            if (left <= 0 && offerModal.style.display.value == DisplayStyle.Flex) Close(offerModal, offerCard);
        }

        void TickShop(float dt)
        {
            if (shopNoteClock >= 0f)
            {
                shopNoteClock += dt;
                shopNote.style.opacity = Mathf.Clamp01(2.6f - shopNoteClock);
                if (shopNoteClock > 2.6f) { shopNoteClock = -1f; shopNote.style.display = DisplayStyle.None; }
            }
            if (offerModal.style.display.value == DisplayStyle.Flex)
            {
                UpdateDealTimer();
                dealRays.style.rotate = new Rotate(time * 18f);
                float s2 = Store.PurchasesAvailable ? 1f + 0.03f * Mathf.Sin(time * 5f) : 1f;
                dealBuy.style.scale = new Scale(new Vector2(s2, s2));
            }
        }

        // ================================================================ günlük görevler ve giriş ödülü

        VisualElement questModal, questCard, questDot, loginModal, loginCard;
        readonly VisualElement[] questRows = new VisualElement[DailyQuests.PerDay];
        readonly Label[] questTexts = new Label[DailyQuests.PerDay], questCounts = new Label[DailyQuests.PerDay],
                         questRewards = new Label[DailyQuests.PerDay];
        readonly VisualElement[] questFills = new VisualElement[DailyQuests.PerDay];
        readonly Button[] questClaims = new Button[DailyQuests.PerDay];
        Label questTimer;
        readonly VisualElement[] loginDays = new VisualElement[7];
        readonly Label[] loginAmounts = new Label[7];

        static VisualElement Modal(out VisualElement card, string ribbonText, Color ribbonColor, Color ribbonDark)
        {
            var modal = new VisualElement();
            foreach (var c in new[] { "fill", "dim", "center", "pick" }) modal.AddToClassList(c);
            modal.style.display = DisplayStyle.None;
            card = new VisualElement();
            foreach (var c in new[] { "card", "card-cream", "col", "pop", "off" }) card.AddToClassList(c);
            card.style.width = Length.Percent(90);
            card.style.paddingTop = 0;
            card.style.paddingLeft = card.style.paddingRight = 30;
            card.style.paddingBottom = 34;
            var ribbon = new VisualElement();
            ribbon.AddToClassList("ribbon");
            ribbon.style.marginTop = -50;
            ribbon.style.height = 104;
            ribbon.style.backgroundColor = ribbonColor;
            ribbon.style.borderBottomColor = ribbonDark;
            var title = new Label(ribbonText);
            title.style.fontSize = 52;
            ribbon.Add(title);
            card.Add(ribbon);
            modal.Add(card);
            return modal;
        }

        static void Open(VisualElement modal, VisualElement card)
        {
            modal.style.display = DisplayStyle.Flex;
            card.AddToClassList("off");
            card.schedule.Execute(() => card.RemoveFromClassList("off")).StartingIn(30);
        }

        static void Close(VisualElement modal, VisualElement card)
        {
            card.AddToClassList("off");
            modal.schedule.Execute(() => modal.style.display = DisplayStyle.None).StartingIn(180);
        }

        void BuildQuests()
        {
            // 2026-10-23 (örnek görsele göre; kullanıcı: "daily quest hala çok sade"): pencerenin kendisi iplerle sarkan
            // tahta pano; tepede çivili, damarlı tahta başlık; saatli sayaç hapı; her görev renkli kenarlı beyaz satır,
            // parlak ikon kutusu, içinde sayaç yazan kalın ilerleme çubuğu ve coin/gem ödül çipleri.
            questModal = Modal(out questCard, "DAILY QUESTS", Wood, WoodDark);
            questModal.name = "home-quests-modal";
            questCard.RemoveAt(0);   // standart kurdele yerine tahta başlık
            questCard.style.overflow = Overflow.Visible;
            questCard.style.backgroundColor = (Color)new Color32(0xFF, 0xF1, 0xD6, 0xFF);
            questCard.style.borderTopWidth = questCard.style.borderLeftWidth = questCard.style.borderRightWidth = 18;
            questCard.style.borderBottomWidth = 28;
            questCard.style.borderTopColor = questCard.style.borderLeftColor = questCard.style.borderRightColor = Wood;
            questCard.style.borderBottomColor = WoodDark;
            questCard.style.borderTopLeftRadius = questCard.style.borderTopRightRadius = 44;
            questCard.style.borderBottomLeftRadius = questCard.style.borderBottomRightRadius = 44;
            questCard.style.marginTop = 120;
            // İpler (panonun üstünden ekranın tepesine)
            for (int r = 0; r < 2; r++)
            {
                var rope = E();
                rope.pickingMode = PickingMode.Ignore;
                rope.style.position = Position.Absolute;
                rope.style.width = 14;
                rope.style.top = -420;
                rope.style.height = 440;
                if (r == 0) rope.style.left = Length.Percent(16); else rope.style.right = Length.Percent(16);
                rope.style.backgroundColor = (Color)new Color32(0xD8, 0xAE, 0x6A, 0xFF);
                rope.style.borderLeftWidth = rope.style.borderRightWidth = 3;
                rope.style.borderLeftColor = rope.style.borderRightColor = (Color)new Color32(0x8A, 0x60, 0x2E, 0xFF);
                questCard.Add(rope);
            }
            // Tahta başlık: damar çizgileri, iki uçta çivi
            var plank = E("center");
            plank.pickingMode = PickingMode.Ignore;
            plank.style.alignSelf = Align.Stretch;
            plank.style.marginLeft = plank.style.marginRight = -46;
            plank.style.marginTop = -96;
            plank.style.height = 150;
            plank.style.backgroundColor = (Color)new Color32(0xC2, 0x7A, 0x3C, 0xFF);
            plank.style.borderBottomWidth = 16;
            plank.style.borderBottomColor = WoodDark;
            plank.style.borderTopWidth = 5;
            plank.style.borderTopColor = (Color)new Color32(0xE2, 0xA2, 0x5E, 0xFF);
            plank.style.borderTopLeftRadius = plank.style.borderTopRightRadius = plank.style.borderBottomLeftRadius = plank.style.borderBottomRightRadius = 26;
            float[] grain = { 0.24f, 0.46f, 0.7f };
            for (int g = 0; g < grain.Length; g++)
            {
                var line = E();
                line.pickingMode = PickingMode.Ignore;
                line.style.position = Position.Absolute;
                line.style.top = Length.Percent(grain[g] * 100f);
                line.style.left = Length.Percent(6 + g * 9);
                line.style.right = Length.Percent(4 + (2 - g) * 11);
                line.style.height = 5;
                line.style.borderTopLeftRadius = line.style.borderTopRightRadius = line.style.borderBottomLeftRadius = line.style.borderBottomRightRadius = 3;
                line.style.backgroundColor = new Color(0.43f, 0.24f, 0.09f, 0.35f);
                plank.Add(line);
            }
            for (int n = 0; n < 2; n++)
            {
                var nail = E();
                nail.pickingMode = PickingMode.Ignore;
                nail.style.position = Position.Absolute;
                nail.style.width = nail.style.height = 30;
                nail.style.top = 52;
                if (n == 0) nail.style.left = 26; else nail.style.right = 26;
                nail.style.borderTopLeftRadius = nail.style.borderTopRightRadius = nail.style.borderBottomLeftRadius = nail.style.borderBottomRightRadius = 15;
                nail.style.backgroundColor = (Color)new Color32(0xC9, 0xCC, 0xD6, 0xFF);
                nail.style.borderBottomWidth = 6;
                nail.style.borderBottomColor = (Color)new Color32(0x6E, 0x72, 0x80, 0xFF);
                plank.Add(nail);
            }
            var signText = L("DAILY QUESTS", "heavy");
            signText.style.fontSize = 80;
            signText.style.color = Color.white;
            signText.style.unityTextOutlineWidth = 9;
            signText.style.unityTextOutlineColor = WoodDark;
            plank.Add(signText);
            questCard.Add(plank);

            // Sayaç hapı
            var timerRow = E("row");
            timerRow.style.alignSelf = Align.Center;
            timerRow.style.alignItems = Align.Center;
            timerRow.style.marginTop = 18;
            timerRow.style.paddingLeft = 14; timerRow.style.paddingRight = 28;
            timerRow.style.paddingTop = timerRow.style.paddingBottom = 6;
            timerRow.style.backgroundColor = (Color)new Color32(0x6E, 0x3E, 0x18, 0xFF);
            timerRow.style.borderTopLeftRadius = timerRow.style.borderTopRightRadius = timerRow.style.borderBottomLeftRadius = timerRow.style.borderBottomRightRadius = 34;
            timerRow.Add(new ClockIcon(56));
            questTimer = L("", "heavy");
            questTimer.style.fontSize = 34;
            questTimer.style.color = Color.white;
            questTimer.style.marginLeft = 10;
            timerRow.Add(questTimer);
            questCard.Add(timerRow);

            var coinCol = (Color)new Color32(0xD8, 0x8A, 0x0A, 0xFF);
            for (int i = 0; i < DailyQuests.PerDay; i++)
            {
                int index = i;
                var row = E("row");
                row.style.alignSelf = Align.Stretch;
                row.style.marginTop = 18;
                row.style.paddingTop = row.style.paddingBottom = 16;
                row.style.paddingLeft = 18; row.style.paddingRight = 18;
                row.style.alignItems = Align.Center;
                row.style.backgroundColor = Color.white;
                row.style.borderTopLeftRadius = row.style.borderTopRightRadius = row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 30;
                row.style.borderLeftWidth = 14;
                row.style.borderBottomWidth = 8;
                row.style.borderBottomColor = (Color)new Color32(0xE6, 0xD3, 0xB0, 0xFF);
                questIcons[i] = E("center");
                questIcons[i].style.width = questIcons[i].style.height = 124;
                questIcons[i].style.flexShrink = 0;
                questIcons[i].style.marginRight = 20;
                questIcons[i].style.borderTopLeftRadius = questIcons[i].style.borderTopRightRadius = 32;
                questIcons[i].style.borderBottomLeftRadius = questIcons[i].style.borderBottomRightRadius = 32;
                questIcons[i].style.borderTopWidth = questIcons[i].style.borderLeftWidth = questIcons[i].style.borderRightWidth = 5;
                questIcons[i].style.borderTopColor = questIcons[i].style.borderLeftColor = questIcons[i].style.borderRightColor = Color.white;
                questIcons[i].style.borderBottomWidth = 10;
                row.Add(questIcons[i]);
                var col = E("grow");
                questTexts[i] = L("", "heavy");
                questTexts[i].style.fontSize = 34;
                questTexts[i].style.whiteSpace = WhiteSpace.Normal;
                col.Add(questTexts[i]);
                // Kalın ilerleme çubuğu, sayaç içinde
                var bar = E();
                bar.style.marginTop = 8;
                bar.style.height = 44;
                bar.style.backgroundColor = (Color)new Color32(0xEC, 0xE3, 0xD2, 0xFF);
                bar.style.borderTopWidth = bar.style.borderBottomWidth = bar.style.borderLeftWidth = bar.style.borderRightWidth = 4;
                bar.style.borderTopColor = bar.style.borderBottomColor = bar.style.borderLeftColor = bar.style.borderRightColor = (Color)new Color32(0xC9, 0xB4, 0x90, 0xFF);
                bar.style.borderTopLeftRadius = bar.style.borderTopRightRadius = bar.style.borderBottomLeftRadius = bar.style.borderBottomRightRadius = 22;
                bar.style.overflow = Overflow.Hidden;
                questFills[i] = E();
                questFills[i].style.position = Position.Absolute;
                questFills[i].style.left = questFills[i].style.top = questFills[i].style.bottom = 0;
                questFills[i].style.borderTopLeftRadius = questFills[i].style.borderTopRightRadius = questFills[i].style.borderBottomLeftRadius = questFills[i].style.borderBottomRightRadius = 18;
                var shine = E();
                shine.style.position = Position.Absolute;
                shine.style.left = 10; shine.style.right = 10; shine.style.top = 4;
                shine.style.height = 9;
                shine.style.borderTopLeftRadius = shine.style.borderTopRightRadius = shine.style.borderBottomLeftRadius = shine.style.borderBottomRightRadius = 5;
                shine.style.backgroundColor = new Color(1f, 1f, 1f, 0.4f);
                questFills[i].Add(shine);
                bar.Add(questFills[i]);
                questCounts[i] = L("", "heavy");
                questCounts[i].style.position = Position.Absolute;
                questCounts[i].style.left = questCounts[i].style.right = questCounts[i].style.top = questCounts[i].style.bottom = 0;
                questCounts[i].style.unityTextAlign = TextAnchor.MiddleCenter;
                questCounts[i].style.fontSize = 28;
                questCounts[i].style.color = Color.white;
                questCounts[i].style.unityTextOutlineWidth = 5;
                questCounts[i].style.unityTextOutlineColor = (Color)new Color32(0x4A, 0x2A, 0x12, 0xFF);
                bar.Add(questCounts[i]);
                col.Add(bar);
                // Ödül çipleri
                var chips = E("row");
                chips.style.marginTop = 8;
                var coinChip = RewardChip(IconKind.Coin, coinCol, out questRewards[i]);
                chips.Add(coinChip);
                questGemChips[i] = RewardChip(IconKind.Gem, (Color)new Color32(0xD0, 0x3F, 0x93, 0xFF), out questGemTexts[i]);
                questGemChips[i].style.marginLeft = 10;
                chips.Add(questGemChips[i]);
                col.Add(chips);
                row.Add(col);
                questClaims[i] = B(() => ClaimQuest?.Invoke(index), "btn", "btn-mid");
                questClaims[i].text = "CLAIM";
                questClaims[i].style.width = 200;
                questClaims[i].style.fontSize = 40;
                questClaims[i].style.marginLeft = 16;
                row.Add(questClaims[i]);
                // Alındı damgası
                questStamps[i] = E("center");
                questStamps[i].pickingMode = PickingMode.Ignore;
                questStamps[i].style.position = Position.Absolute;
                questStamps[i].style.right = 30;
                questStamps[i].style.width = questStamps[i].style.height = 120;
                questStamps[i].style.borderTopLeftRadius = questStamps[i].style.borderTopRightRadius = questStamps[i].style.borderBottomLeftRadius = questStamps[i].style.borderBottomRightRadius = 60;
                questStamps[i].style.backgroundColor = (Color)new Color32(0x3F, 0xD1, 0x5B, 0xFF);
                questStamps[i].style.borderBottomWidth = 8;
                questStamps[i].style.borderBottomColor = (Color)new Color32(0x22, 0x9A, 0x3C, 0xFF);
                questStamps[i].style.rotate = new Rotate(-12f);
                questStamps[i].Add(new Icon(IconKind.Check, 74, Color.white));
                row.Add(questStamps[i]);
                questRows[i] = row;
                questCard.Add(row);
            }
            var close = B(() => Close(questModal, questCard), "btn", "btn-white", "btn-mid");
            close.name = "home-quests-close";
            close.text = "CLOSE";
            close.style.alignSelf = Align.Stretch;
            close.style.marginTop = 26;
            questCard.Add(close);
            safe.Add(questModal);
        }

        /// Küçük ödül çipi: krem hap içinde ikon ve "+N".
        VisualElement RewardChip(IconKind kind, Color textColor, out Label text)
        {
            var chip = E("row");
            chip.style.alignItems = Align.Center;
            chip.style.paddingLeft = 6; chip.style.paddingRight = 16;
            chip.style.paddingTop = chip.style.paddingBottom = 2;
            chip.style.backgroundColor = (Color)new Color32(0xFF, 0xF1, 0xC8, 0xFF);
            chip.style.borderTopLeftRadius = chip.style.borderTopRightRadius = chip.style.borderBottomLeftRadius = chip.style.borderBottomRightRadius = 22;
            chip.Add(new Icon(kind, 40));
            text = L("", "heavy");
            text.style.fontSize = 30;
            text.style.marginLeft = 6;
            text.style.color = textColor;
            chip.Add(text);
            return chip;
        }

        readonly VisualElement[] questGemChips = new VisualElement[DailyQuests.PerDay], questStamps = new VisualElement[DailyQuests.PerDay];
        readonly Label[] questGemTexts = new Label[DailyQuests.PerDay];

        static readonly Color Wood = new Color32(0xB0, 0x6A, 0x32, 0xFF), WoodDark = new Color32(0x6E, 0x3E, 0x18, 0xFF);
        readonly VisualElement[] questIcons = new VisualElement[DailyQuests.PerDay];

        /// Görev türünün ikonu ve kutu rengi.
        static (IconKind, Color) QuestLook(QuestKind k)
        {
            switch (k)
            {
                case QuestKind.Distance: case QuestKind.TotalDistance: return (IconKind.Flag, new Color32(0x9B, 0x6B, 0xFF, 0xFF));
                case QuestKind.Milk: return (IconKind.Glass, new Color32(0x8E, 0x7C, 0xFF, 0xFF));
                case QuestKind.Finish: return (IconKind.Finish, new Color32(0x3F, 0xC0, 0x7A, 0xFF));
                case QuestKind.Coins: return (IconKind.Coin, new Color32(0xFF, 0xB0, 0x20, 0xFF));
                case QuestKind.Gems: return (IconKind.Gem, new Color32(0xF0, 0x5F, 0xB0, 0xFF));
                case QuestKind.AirTime: case QuestKind.LongAir: return (IconKind.Wing, new Color32(0x3F, 0xA9, 0xF5, 0xFF));
                case QuestKind.Perfect: return (IconKind.Slingshot, new Color32(0xFF, 0x8A, 0x3D, 0xFF));
                case QuestKind.Rocket: return (IconKind.Rocket, new Color32(0xF2, 0x55, 0x55, 0xFF));
                case QuestKind.Bridges: return (IconKind.Map, new Color32(0x2F, 0xB8, 0xC8, 0xFF));
                case QuestKind.ForkGood: return (IconKind.Swap, new Color32(0x2F, 0xB8, 0xC8, 0xFF));
                default: return (IconKind.Runners, new Color32(0x6F, 0x9B, 0xD8, 0xFF));
            }
        }

        /// Görev listesini günceller (open: pencereyi de açar); alınabilir ödül varsa ana ekrandaki düğmede kırmızı nokta.
        public void ShowQuests(DailyQuests.Quest[] quests, int[] progress, int claimed, bool open)
        {
            bool any = false;
            var left = DailyQuests.UntilReset;
            questTimer.text = "Resets in " + (int)left.TotalHours + " h " + left.Minutes + " min";
            for (int i = 0; i < quests.Length; i++)
            {
                var q = quests[i];
                int p = Mathf.Min(progress[i], q.target);
                bool done = p >= q.target, got = (claimed & (1 << i)) != 0;
                questTexts[i].text = q.text;
                questCounts[i].text = p + "/" + q.target;
                questFills[i].style.width = Length.Percent(100f * p / Mathf.Max(1, q.target));
                questFills[i].style.backgroundColor = done ? (Color)new Color32(0x3F, 0xD1, 0x5B, 0xFF) : (Color)new Color32(0xFF, 0x9A, 0x1F, 0xFF);
                questFills[i].style.display = p > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                var (kindIcon, kindColor) = QuestLook(q.kind);
                if (questIcons[i].userData == null || (QuestKind)questIcons[i].userData != q.kind)
                {
                    questIcons[i].Clear();
                    questIcons[i].Add(new Icon(kindIcon, 78));
                    questIcons[i].style.backgroundColor = kindColor;
                    questIcons[i].style.borderBottomColor = Color.Lerp(kindColor, Color.black, 0.25f);
                    questIcons[i].userData = q.kind;
                    questRows[i].style.borderLeftColor = kindColor;
                    var gloss = E();
                    gloss.pickingMode = PickingMode.Ignore;
                    gloss.style.position = Position.Absolute;
                    gloss.style.left = 10; gloss.style.right = 10; gloss.style.top = 6;
                    gloss.style.height = 30;
                    gloss.style.borderTopLeftRadius = gloss.style.borderTopRightRadius = gloss.style.borderBottomLeftRadius = gloss.style.borderBottomRightRadius = 15;
                    gloss.style.backgroundColor = new Color(1f, 1f, 1f, 0.28f);
                    questIcons[i].Insert(0, gloss);
                }
                bool ready = done && !got;
                questClaims[i].style.backgroundColor = ready ? (Color)new Color32(0xFF, 0xC9, 0x3C, 0xFF) : (Color)new Color32(0xC9, 0xC8, 0xCF, 0xFF);
                questClaims[i].style.borderBottomColor = ready ? (Color)new Color32(0xD2, 0x86, 0x10, 0xFF) : (Color)new Color32(0xA3, 0xA2, 0xAA, 0xFF);
                questClaims[i].style.color = ready ? (Color)new Color32(0x4A, 0x22, 0x0C, 0xFF) : Color.white;
                questRewards[i].text = "+" + q.coins.ToString("N0", Tr);
                questGemTexts[i].text = "+" + q.gems;
                questGemChips[i].style.display = q.gems > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                questStamps[i].style.display = got ? DisplayStyle.Flex : DisplayStyle.None;
                questClaims[i].style.visibility = got ? Visibility.Hidden : Visibility.Visible;
                questClaims[i].SetEnabled(done && !got);
                questClaims[i].text = got ? "CLAIMED" : "CLAIM";
                questClaims[i].style.opacity = 1f;
                questRows[i].style.opacity = got ? 0.75f : 1f;
                if (done && !got) any = true;
            }
            questDot.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            if (open) Open(questModal, questCard);
        }

        // Günlük ödül (kullanıcı 2026-10-19, örnek görsele benzer): ahşap çerçeve, buz kenarlı mavi panel, kırmızı kurdele
        // başlık; 1-6. gün iki sütunlu kahverengi kutular (alınanlarda büyük sarı tik, bugünkü altın çerçeveli ve nabızlı),
        // 7. gün geniş mor kutu (elmaslar), altta büyük sarı CLAIM düğmesi.
        readonly VisualElement[] loginChecks = new VisualElement[7];
        readonly VisualElement[] loginBodies = new VisualElement[7];
        VisualElement loginToday;

        static void Outline(Label l, float width, Color color)
        {
            l.style.unityTextOutlineWidth = width;
            l.style.unityTextOutlineColor = color;
        }

        static void Box(VisualElement e, Color bg, Color border, float bw, float radius)
        {
            e.style.backgroundColor = bg;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = border;
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = bw;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;
        }

        /// Üst üste üç altın: altın yığını.
        static VisualElement CoinPile(float size)
        {
            var pile = new VisualElement();
            pile.style.width = size * 1.7f;
            pile.style.height = size * 1.25f;
            pile.style.flexShrink = 0;
            foreach (var (x, y) in new[] { (0f, 0.35f), (0.7f, 0.35f), (0.35f, 0f) })
            {
                var c = new Icon(IconKind.Coin, size);
                c.style.position = Position.Absolute;
                c.style.left = x * size;
                c.style.top = y * size * 0.7f;
                pile.Add(c);
            }
            return pile;
        }

        void BuildLogin()
        {
            loginModal = new VisualElement();
            foreach (var c in new[] { "fill", "dim", "center", "pick" }) loginModal.AddToClassList(c);
            loginModal.style.display = DisplayStyle.None;
            Color wood = new Color32(0x9A, 0x62, 0x36, 0xFF), woodDark = new Color32(0x6B, 0x3F, 0x1F, 0xFF);
            Color panel = new Color32(0x1E, 0x5C, 0xAE, 0xFF), ice = new Color32(0x8F, 0xDD, 0xFF, 0xFF);
            Color tile = new Color32(0x7A, 0x48, 0x2A, 0xFF), tileEdge = new Color32(0xC8, 0x8B, 0x4E, 0xFF);
            Color gold = new Color32(0xFF, 0xD8, 0x4A, 0xFF), brownInk = new Color32(0x4A, 0x22, 0x0C, 0xFF);

            loginCard = new VisualElement();
            foreach (var c in new[] { "col", "pop", "off" }) loginCard.AddToClassList(c);
            loginCard.style.width = Length.Percent(88);
            Box(loginCard, wood, woodDark, 14, 52);
            loginCard.style.paddingTop = loginCard.style.paddingBottom = 18;
            loginCard.style.paddingLeft = loginCard.style.paddingRight = 18;

            var inner = E("col");
            Box(inner, panel, ice, 9, 40);
            inner.style.paddingTop = 96;
            inner.style.paddingLeft = inner.style.paddingRight = 18;
            inner.style.paddingBottom = 26;
            inner.style.alignItems = Align.Stretch;
            inner.style.alignSelf = Align.Stretch;   // çerçeveyi doldursun (yoksa dar sütunda kalıyordu)
            loginCard.Add(inner);

            // Kurdele başlık
            var ribbon = E("row");
            ribbon.style.position = Position.Absolute;
            ribbon.style.top = -46;
            ribbon.style.alignSelf = Align.Center;
            Box(ribbon, new Color32(0xE6, 0x3E, 0x62, 0xFF), new Color32(0xA8, 0x22, 0x47, 0xFF), 0, 28);
            ribbon.style.borderBottomWidth = 12;
            ribbon.style.paddingLeft = ribbon.style.paddingRight = 46;
            ribbon.style.paddingTop = 4;
            ribbon.style.height = 128;
            ribbon.style.alignItems = Align.Center;
            var title = L("DAILY REWARD", "heavy");
            title.style.fontSize = 78;
            title.style.color = gold;
            Outline(title, 9, new Color32(0x6B, 0x14, 0x30, 0xFF));
            ribbon.Add(title);
            loginCard.Add(ribbon);

            VisualElement Tile(int d, bool wide)
            {
                var t = E("col");
                Box(t, wide ? (Color)new Color32(0x8E, 0x2A, 0x6B, 0xFF) : tile, wide ? gold : tileEdge, 8, 30);
                t.style.flexGrow = 1;
                t.style.flexBasis = 0;
                t.style.height = wide ? 290 : 272;
                t.style.marginLeft = t.style.marginRight = 9;
                t.style.marginTop = 16;
                t.style.alignItems = Align.Center;
                t.style.justifyContent = Justify.Center;
                var body = E(wide ? "row" : "col");
                body.style.alignItems = Align.Center;
                body.style.justifyContent = Justify.Center;
                var day = L("DAY " + (d + 1), "heavy");
                day.style.fontSize = wide ? 66 : 44;
                day.style.color = Color.white;
                Outline(day, wide ? 8 : 6, brownInk);
                loginAmounts[d] = L("", "heavy");
                loginAmounts[d].style.fontSize = wide ? 52 : 42;
                loginAmounts[d].style.color = gold;
                loginAmounts[d].style.unityTextAlign = TextAnchor.MiddleCenter;
                Outline(loginAmounts[d], 6, brownInk);
                if (wide)
                {
                    var left = E("col");
                    left.style.alignItems = Align.Center;
                    left.style.marginRight = 30;
                    left.Add(day);
                    left.Add(loginAmounts[d]);
                    body.Add(left);
                    var gems = E("row");
                    var g1 = new Icon(IconKind.Gem, 130);
                    var g2 = new Icon(IconKind.Gem, 110);
                    g2.style.marginLeft = -34;
                    g2.style.marginTop = 30;
                    gems.Add(g1);
                    gems.Add(g2);
                    body.Add(gems);
                }
                else
                {
                    body.Add(day);
                    body.Add(CoinPile(62));
                    body.Add(loginAmounts[d]);
                }
                t.Add(body);
                loginBodies[d] = body;
                // Alınmış gün: büyük sarı tik
                var check = new Icon(IconKind.Check, wide ? 150 : 132, gold);
                check.style.position = Position.Absolute;
                check.style.alignSelf = Align.Center;
                check.style.top = wide ? 66 : 64;
                check.style.display = DisplayStyle.None;
                t.Add(check);
                loginChecks[d] = check;
                loginDays[d] = t;
                return t;
            }

            for (int r = 0; r < 3; r++)
            {
                var row = E("row");
                row.style.alignSelf = Align.Stretch;
                row.Add(Tile(r * 2, false));
                row.Add(Tile(r * 2 + 1, false));
                inner.Add(row);
            }
            var row7 = E("row");
            row7.style.alignSelf = Align.Stretch;
            row7.Add(Tile(6, true));
            inner.Add(row7);

            var claim = B(() => { ClaimLogin?.Invoke(); Close(loginModal, loginCard); }, "btn", "btn-big");
            claim.name = "menu-login-close";
            claim.text = "CLAIM";
            claim.style.backgroundColor = (Color)new Color32(0xFF, 0xC9, 0x3C, 0xFF);
            claim.style.borderBottomColor = (Color)new Color32(0xD2, 0x86, 0x10, 0xFF);
            claim.style.color = brownInk;
            claim.style.unityTextOutlineWidth = 0;
            claim.style.fontSize = 84;
            claim.style.alignSelf = Align.Center;
            claim.style.width = Length.Percent(62);
            claim.style.marginTop = 28;
            inner.Add(claim);

            loginModal.Add(loginCard);
            safe.Add(loginModal);
            // Bugünkü kutu nabız gibi atar
            loginModal.schedule.Execute(() =>
            {
                if (loginToday == null || loginModal.style.display == DisplayStyle.None) return;
                float k = 1f + 0.035f * Mathf.Sin(Time.unscaledTime * 6f);
                loginToday.style.scale = new Scale(new Vector2(k, k));
            }).Every(16);
        }

        /// day: bugün alınacak gün (1..7); coins/gems: 7 günün ödülleri.
        public void ShowLogin(int day, int[] coins, int[] gems)
        {
            if (loginToday != null) loginToday.style.scale = new Scale(Vector2.one);
            loginToday = null;
            for (int d = 0; d < 7; d++)
            {
                loginAmounts[d].text = d == 6 ? (gems[d] > 0 ? gems[d] + " GEMS" : "") + (coins[d] > 0 ? (gems[d] > 0 ? "\n" : "") + coins[d] : "")
                                              : coins[d] + (gems[d] > 0 ? "\n+" + gems[d] + " GEMS" : "");
                bool past = d < day - 1, today = d == day - 1;
                loginChecks[d].style.display = past ? DisplayStyle.Flex : DisplayStyle.None;
                loginBodies[d].style.opacity = past ? 0.3f : 1f;
                var t = loginDays[d];
                Color edge = today ? (Color)new Color32(0xFF, 0xD8, 0x4A, 0xFF) : d == 6 ? (Color)new Color32(0xFF, 0xD8, 0x4A, 0xFF) : (Color)new Color32(0xC8, 0x8B, 0x4E, 0xFF);
                t.style.borderTopColor = t.style.borderBottomColor = t.style.borderLeftColor = t.style.borderRightColor = edge;
                float w = today ? 12 : 8;
                t.style.borderTopWidth = t.style.borderBottomWidth = t.style.borderLeftWidth = t.style.borderRightWidth = w;
                if (d < 6) t.style.backgroundColor = past ? (Color)new Color32(0x5A, 0x33, 0x1C, 0xFF)
                                                   : today ? (Color)new Color32(0xA0, 0x62, 0x36, 0xFF) : (Color)new Color32(0x7A, 0x48, 0x2A, 0xFF);
                if (today) loginToday = t;
            }
            Open(loginModal, loginCard);
        }

        // ================================================================ kalıcı parça duyurusu

        VisualElement partModal, partCard, partRays, partImage;
        Label partName, partText;
        float partClock = -1f;

        /// Parkur bitince açılan kalıcı parça: ışık halesi önünde aracın önizlemesi, adı ve etkisi (kullanıcının
        /// gönderdiği "yeni kızak açıldı" ekranı gibi).
        void BuildPartUnlock()
        {
            partModal = E("fill", "dim", "center", "pick");
            partModal.style.display = DisplayStyle.None;
            partCard = E("card", "card-cream", "col", "pop", "off");
            partCard.style.width = Length.Percent(86);
            partCard.style.paddingTop = 0;
            partCard.style.paddingLeft = partCard.style.paddingRight = 36;
            partCard.style.paddingBottom = 36;

            var ribbon = E("ribbon");
            ribbon.style.marginTop = -50;
            ribbon.style.height = 104;
            ribbon.style.backgroundColor = (Color)new Color32(0x3F, 0xA4, 0xFF, 0xFF);
            ribbon.style.borderBottomColor = (Color)new Color32(0x1E, 0x78, 0xD0, 0xFF);
            var title = L("NEW PART UNLOCKED!");
            title.style.fontSize = 50;
            ribbon.Add(title);
            partCard.Add(ribbon);

            var stage = E();
            stage.style.width = Length.Percent(100);
            stage.style.height = 400;
            stage.style.marginTop = 10;
            stage.style.alignItems = Align.Center;
            stage.style.justifyContent = Justify.Center;
            partRays = E();
            partRays.style.position = Position.Absolute;
            partRays.style.width = partRays.style.height = 420;
            partRays.style.backgroundImage = new StyleBackground(Radial());
            partRays.style.unityBackgroundImageTintColor = new Color(1f, 0.92f, 0.55f, 0.9f);
            stage.Add(partRays);
            partImage = E();
            partImage.style.width = 520;
            partImage.style.height = 390;
            stage.Add(partImage);
            partCard.Add(stage);

            partName = L("", "outlined");
            partName.style.fontSize = 76;
            partCard.Add(partName);
            partText = L("", "heavy");
            partText.style.fontSize = 36;
            partText.style.color = Muted;
            partText.style.whiteSpace = WhiteSpace.Normal;
            partText.style.unityTextAlign = TextAnchor.MiddleCenter;
            partText.style.marginTop = 6;
            partCard.Add(partText);

            var go = B(() =>
            {
                partCard.AddToClassList("off");
                partClock = -1f;
                partModal.schedule.Execute(() => partModal.style.display = DisplayStyle.None).StartingIn(180);
            }, "btn", "btn-green", "btn-big");
            go.text = "CONTINUE";
            go.style.alignSelf = Align.Stretch;
            go.style.marginTop = 30;
            go.name = "menu-part-close";
            partCard.Add(go);

            partModal.Add(partCard);
            safe.Add(partModal);
        }

        public void DebugClosePart()
        {
            partClock = -1f;
            partModal.style.display = DisplayStyle.None;
        }

        public void ShowPartUnlock(string name, string text, Texture preview)
        {
            partName.text = name.ToUpper(Tr);
            partText.text = text;
            partImage.style.backgroundImage = Background.FromRenderTexture(preview as RenderTexture);
            partClock = 0f;
            partModal.style.display = DisplayStyle.Flex;
            partCard.AddToClassList("off");
            partCard.schedule.Execute(() => partCard.RemoveFromClassList("off")).StartingIn(30);
        }

        public void ShowChest(int coins, int gems)
        {
            chestCoinValue = coins;
            chestGemValue = gems;
            chestMultiplier = 1;
            chestClock = 0f;
            chestTriple.style.display = DisplayStyle.Flex;
            UpdateChestTexts();
            chestModal.style.display = DisplayStyle.Flex;
            chestCard.AddToClassList("off");
            chestCard.schedule.Execute(() => chestCard.RemoveFromClassList("off")).StartingIn(30);
        }

        void SetChestMultiplier(int m)
        {
            chestMultiplier = m;
            chestClock = 0f;
            chestTriple.style.display = DisplayStyle.None;
            UpdateChestTexts();
        }

        void UpdateChestTexts()
        {
            chestCoins.text = "+" + chestCoinValue * chestMultiplier;
            chestGems.text = "+" + chestGemValue * chestMultiplier;
            chestGemRow.style.display = chestGemValue > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void ShowAd(Action done)
        {
            adDone = done;
            adClock = 0f;
            adOverlay.style.display = DisplayStyle.Flex;
        }

        void TickOverlays(float dt)
        {
            if (chestModal.style.display.value == DisplayStyle.Flex)
            {
                chestClock += dt;
                var big = chestCard.Q<Icon>("chest-big");
                float bounce = 1f + 0.12f * Mathf.Exp(-chestClock * 3f) * Mathf.Sin(chestClock * 14f);
                big.style.scale = new Scale(new Vector2(bounce, bounce));
                float pulse = 1f + 0.04f * Mathf.Sin(time * 5f);
                chestTriple.style.scale = new Scale(new Vector2(pulse, pulse));
            }
            TickShop(dt);
            if (partClock >= 0f)
            {
                partClock += dt;
                partRays.style.rotate = new Rotate(Angle.Degrees(partClock * 25f));
                float s = 1f + 0.06f * Mathf.Sin(partClock * 3f);
                partRays.style.scale = new Scale(new Vector2(s, s));
                float bob = EaseOutBack(Mathf.Clamp01(partClock / 0.5f));
                partImage.style.scale = new Scale(new Vector2(bob, bob));
            }
            if (adClock >= 0f)
            {
                adClock += dt;
                adCount.text = Mathf.CeilToInt(Mathf.Max(0f, 3f - adClock)).ToString();
                if (adClock >= 3f)
                {
                    adClock = -1f;
                    adOverlay.style.display = DisplayStyle.None;
                    var d = adDone;
                    adDone = null;
                    d?.Invoke();
                }
            }
        }

        // ================================================================ karakterler

        static readonly Dictionary<int, Texture2D> portraits = new Dictionary<int, Texture2D>();

        static StyleBackground Portrait(int i)
        {
            if (!portraits.TryGetValue(i, out var tex))
                portraits[i] = tex = Resources.Load<Texture2D>("Avatars/Portraits/" + AvatarLibrary.All[i].model);
            return tex != null ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.None);
        }

        void BuildAvatars()
        {
            var p = NewPage(Page.Avatars);

            var top = E("topbar");
            var back = B(() => OpenHub?.Invoke(), "btn", "btn-white", "btn-round");
            back.name = "avatar-back";
            back.Add(new Icon(IconKind.Back, 58, Ink));
            top.Add(back);
            var title = L("HEROES", "outlined");
            title.style.fontSize = 72;
            top.Add(title);
            HeaderBand(top);
            top.Add(GemPill());
            p.Add(top);

            avName = L("", "outlined");
            avName.style.fontSize = 78;
            avName.style.unityTextAlign = TextAnchor.MiddleCenter;
            avName.style.marginTop = 6;
            p.Add(avName);
            avRarity = E("av-rarity");
            avRarityLabel = L("", "av-rarity-text");
            avRarity.Add(avRarityLabel);
            p.Add(avRarity);

            // Avantaj kartı: renkli ikon kutusu + başlık + açıklama
            avPerkCard = E("av-perk");
            avPerkIconBox = E("av-perk-icon");
            avPerkIcon = new Icon(IconKind.Glass, 64, Color.white);
            avPerkIconBox.Add(avPerkIcon);
            avPerkCard.Add(avPerkIconBox);
            var perkText = E("col");
            perkText.style.flexShrink = 1;
            var perkCap = L("PERK", "av-perk-cap");
            perkText.Add(perkCap);
            avPerkTitle = L("", "av-perk-title");
            perkText.Add(avPerkTitle);
            avPerkText = L("", "av-perk-text");
            perkText.Add(avPerkText);
            avPerkCard.Add(perkText);
            p.Add(avPerkCard);

            p.Add(E("grow"));

            avAction = B(() => AvatarAction?.Invoke(avBrowse), "btn", "btn-green", "btn-big");
            avAction.name = "avatar-action";
            avAction.style.alignSelf = Align.Center;
            avAction.style.width = Length.Percent(70);
            avAction.style.marginBottom = 26;
            avActionGem = new Icon(IconKind.Gem, 66);
            avAction.Add(avActionGem);
            avActionLabel = Sp(L(""), 12);
            avAction.Add(avActionLabel);
            p.Add(avAction);

            // Kart ızgarası: 4 sütun; kart sayısı arttıkça satır eklenir, ızgara kaydırılır
            // (~2,75 satır görünür: alttaki yarım satır kaydırılabildiğini gösterir).
            avScroll = new ScrollView(ScrollViewMode.Vertical);
            avScroll.style.height = 740;
            avScroll.style.flexShrink = 0;
            avScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            avScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            avScroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            // Tema yalnızca Game.uss'i içeriyor (Unity'nin varsayılan teması yok): ScrollView'un iç yerleşimi elle.
            avScroll.style.overflow = Overflow.Hidden;
            var inner = avScroll.Q(className: "unity-scroll-view__content-and-vertical-scroll-container");
            if (inner != null) { inner.style.flexGrow = 1; inner.style.flexShrink = 1; inner.style.flexDirection = FlexDirection.Row; }
            avScroll.contentViewport.style.flexGrow = 1;
            avScroll.contentViewport.style.flexShrink = 1;
            avScroll.contentViewport.style.overflow = Overflow.Hidden;
            avScroll.contentContainer.style.flexShrink = 0;
            p.Add(avScroll);
            int rows = (AvatarLibrary.Count + 3) / 4;
            for (int r = 0; r < rows; r++)
            {
                var row = E("row");
                row.style.alignItems = Align.Stretch;
                row.style.marginTop = r > 0 ? 14 : 0;
                row.style.flexShrink = 0;   // kaydırma alanında satırlar sıkışmasın, taşan kısım kaydırılsın
                for (int k = 0; k < 4; k++)
                {
                    int i = r * 4 + k;
                    if (i >= AvatarLibrary.Count) break;
                    var info = AvatarLibrary.All[i];
                    var rc = RarityColors[Rarity(info.price)];
                    var c = new AvatarCard { root = B(() => BrowseAvatar?.Invoke(i), "btn", "av-card") };
                    c.root.name = "avatar-card-" + i;
                    if (k > 0) c.root.style.marginLeft = 14;
                    c.root.style.borderBottomColor = Color.Lerp(rc, Color.black, 0.25f);
                    var cardTop = E("av-top");
                    cardTop.style.backgroundImage = new StyleBackground(VGrad(Color.Lerp(rc, Color.white, 0.6f), rc));
                    var glow = E("av-glow");
                    glow.style.backgroundImage = new StyleBackground(Radial());
                    cardTop.Add(glow);
                    c.portrait = E("av-portrait");
                    c.portrait.style.backgroundImage = Portrait(i);
                    cardTop.Add(c.portrait);
                    c.badge = E("av-badge");
                    c.badge.Add(new Icon(IconKind.Check, 26, Color.white));
                    cardTop.Add(c.badge);
                    c.lockBadge = E("av-lock");
                    c.lockBadge.Add(new Icon(IconKind.Lock, 22, Color.white));
                    cardTop.Add(c.lockBadge);
                    c.root.Add(cardTop);
                    c.name = L(info.name, "av-name");
                    c.root.Add(c.name);
                    c.chip = E("av-chip");
                    c.priceRow = E("row");
                    c.priceRow.Add(new Icon(IconKind.Gem, 30));
                    c.price = Sp(L(info.price.ToString(), "av-chip-text"), 4);
                    c.priceRow.Add(c.price);
                    c.chip.Add(c.priceRow);
                    c.state = L("", "av-chip-text");
                    c.chip.Add(c.state);
                    c.root.Add(c.chip);
                    avCards[i] = c;
                    row.Add(c.root);
                }
                // Son satır eksikse boş yer tutucular: kartlar öbür satırlarla aynı genişlikte kalsın
                for (int k = row.childCount; k < 4; k++)
                {
                    var pad = E("av-card");
                    pad.style.marginLeft = 14;
                    pad.style.visibility = Visibility.Hidden;
                    row.Add(pad);
                }
                avScroll.Add(row);
            }
        }

        public void ShowAvatars(SaveData save, int browse)
        {
            RefreshAvatars(save, browse);
            SetPage(Page.Avatars);
            // Seçili kart görünür olsun (yerleşim hesaplandıktan sonra)
            var card = avCards[browse].root;
            avScroll.schedule.Execute(() => avScroll.ScrollTo(card)).ExecuteLater(30);
        }

        public void RefreshAvatars(SaveData save, int browse)
        {
            avBrowse = browse;
            var info = AvatarLibrary.All[browse];
            avName.text = info.name.ToUpper(Tr);
            avPerkTitle.text = info.perkTitle;
            avPerkText.text = info.perkText;
            int rarity = Rarity(info.price);
            var rcol = RarityColors[rarity];
            avRarityLabel.text = RarityNames[rarity];
            avRarity.style.backgroundColor = rcol;
            avPerkIconBox.style.backgroundImage = new StyleBackground(VGrad(Color.Lerp(rcol, Color.white, 0.35f), rcol));
            avPerkIcon.Kind = PerkIcon(info.perk);

            bool owned = save.Owns(browse);
            bool selected = save.avatar == browse;
            bool afford = save.gems >= info.price;
            avActionGem.style.display = owned ? DisplayStyle.None : DisplayStyle.Flex;
            avActionLabel.text = selected ? "SELECTED" : owned ? "SELECT" : info.price + "  BUY";
            // Seçili: sıcak turuncu (kullanımda); sahip olunan: yeşil; alınabilir: pembe; yetmiyor: gri.
            var orange = (Color)new Color32(0xFF, 0x9A, 0x1F, 0xFF);
            var orangeDark = (Color)new Color32(0xD8, 0x62, 0x0A, 0xFF);
            Color bg = selected ? orange : owned ? (Color)new Color32(0x3F, 0xD1, 0x5B, 0xFF) : afford ? GemPink : CardGray;
            Color bd = selected ? orangeDark : owned ? (Color)new Color32(0x22, 0xA0, 0x3C, 0xFF) : afford ? GemPinkDark : CardGrayDark;
            avAction.style.backgroundImage = new StyleBackground(VGrad(Color.Lerp(bg, Color.white, 0.3f), bg));
            avAction.style.backgroundColor = bg;
            avAction.style.borderBottomColor = bd;

            for (int i = 0; i < avCards.Length; i++)
            {
                var c = avCards[i];
                bool own = save.Owns(i);
                bool sel = save.avatar == i;
                c.priceRow.style.display = own ? DisplayStyle.None : DisplayStyle.Flex;
                c.state.style.display = own ? DisplayStyle.Flex : DisplayStyle.None;
                c.state.text = sel ? "SELECTED" : "OWNED";
                c.chip.style.backgroundColor = sel ? (Color)new Color32(0xFF, 0x9A, 0x1F, 0xFF)
                                             : own ? (Color)new Color32(0x3F, 0xD1, 0x5B, 0xFF)
                                             : save.gems >= AvatarLibrary.All[i].price ? GemPink : CardGray;
                c.badge.style.display = sel ? DisplayStyle.Flex : DisplayStyle.None;
                c.lockBadge.style.display = own ? DisplayStyle.None : DisplayStyle.Flex;
                // Sahip olunmayan: hafif soluk (karanlık değil)
                c.portrait.style.unityBackgroundImageTintColor = own ? Color.white : new Color(0.82f, 0.82f, 0.88f);
                c.root.EnableInClassList("av-browse", i == browse);
                c.root.EnableInClassList("av-selected", sel);
            }
            avBump = 1f;
        }

        void TickAvatars(float dt)
        {
            avBump = Mathf.MoveTowards(avBump, 0f, dt * 3f);
            float s = 1f + 0.08f * Mathf.Sin(avBump * Mathf.PI);
            avName.style.scale = new Scale(new Vector2(s, s));
        }

        // ================================================================ harita

        /// Başlık satırının arkasında yarı saydam mavi bant (örnek görseldeki başlık şeridi).
        static void HeaderBand(VisualElement top)
        {
            top.style.backgroundColor = new Color(0.27f, 0.58f, 0.96f, 0.55f);
            top.style.borderTopLeftRadius = top.style.borderTopRightRadius = top.style.borderBottomLeftRadius = top.style.borderBottomRightRadius = 34;
            top.style.paddingLeft = top.style.paddingRight = 10;
            top.style.borderBottomWidth = 6;
            top.style.borderBottomColor = new Color(0.12f, 0.36f, 0.75f, 0.6f);
        }

        void BuildMap()
        {
            var p = NewPage(Page.Map);

            var top = E("topbar");
            var back = B(() => OpenHub?.Invoke(), "btn", "btn-white", "btn-round");
            back.name = "map-back";
            back.Add(new Icon(IconKind.Back, 58, Ink));
            top.Add(back);
            var title = L("TRACKS", "outlined");
            title.style.fontSize = 80;
            top.Add(title);
            HeaderBand(top);
            top.Add(CoinPill());
            p.Add(top);

            p.Add(L("Coins and upgrades stay with each track. Return here anytime.\nGems, heroes and unlocked parts carry across tracks.", "track-economy-note"));

            // 7 parkur ekrana sığmıyor: liste kaydırılır, açılışta güncel parkura gidilir.
            mapScroll = new ScrollView(ScrollViewMode.Vertical);
            mapScroll.style.flexGrow = 1;
            mapScroll.style.flexShrink = 1;
            mapScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            mapScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            mapScroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            mapScroll.style.overflow = Overflow.Hidden;
            var inner = mapScroll.Q(className: "unity-scroll-view__content-and-vertical-scroll-container");
            if (inner != null) { inner.style.flexGrow = 1; inner.style.flexShrink = 1; inner.style.flexDirection = FlexDirection.Row; }
            mapScroll.contentViewport.style.flexGrow = 1;
            mapScroll.contentViewport.style.flexShrink = 1;
            mapScroll.contentViewport.style.overflow = Overflow.Hidden;
            mapScroll.contentContainer.style.flexShrink = 0;
            mapScroll.contentContainer.style.paddingTop = mapScroll.contentContainer.style.paddingBottom = 20;
            var list = mapScroll.contentContainer;
            for (int i = 0; i < TrackLibrary.Count; i++)
            {
                if (i > 0)
                {
                    // Kartları birleştiren noktalı yol
                    var dots = E("col");
                    dots.style.flexShrink = 0;
                    dots.style.paddingTop = dots.style.paddingBottom = 6;
                    for (int k = 0; k < 3; k++)
                    {
                        var d = E();
                        d.style.width = d.style.height = 16;
                        d.style.borderTopLeftRadius = d.style.borderTopRightRadius = 8;
                        d.style.borderBottomLeftRadius = d.style.borderBottomRightRadius = 8;
                        d.style.backgroundColor = new Color(1f, 1f, 1f, 0.85f);
                        d.style.marginTop = d.style.marginBottom = 5;
                        dots.Add(d);
                    }
                    list.Add(dots);
                }

                int index = i;
                // Ada düğümü (kullanıcı 2026-10-23, örnek görsele göre): açık parkur, üstü parkur renginde çimen, altı toprak
                // kalınlığında oval ada; üstünde adı ve ilerleme çubuğu. Kilitli parkur: tahta disk ve altın asma kilit.
                var c = new MapCard { root = B(() => SelectTrack?.Invoke(index)) };
                c.root.name = "map-track-" + i;
                c.root.style.flexShrink = 0;
                c.root.style.backgroundColor = Color.clear;
                c.root.style.borderTopWidth = c.root.style.borderBottomWidth = c.root.style.borderLeftWidth = c.root.style.borderRightWidth = 0;
                c.root.style.flexDirection = FlexDirection.Column;
                c.root.style.alignItems = Align.Center;
                c.root.style.alignSelf = Align.Center;
                c.root.style.paddingTop = c.root.style.paddingBottom = 0;
                c.root.style.marginLeft = c.root.style.marginRight = 0;
                c.root.style.translate = new Translate((i % 2 == 0 ? -1 : 1) * 70, 0);   // yol hafifçe kıvrılır

                // Parkur manzarası (kullanıcı 2026-10-23: "görseller parkur teması olabilir; karlı, ağaçlık, Mars gibi"):
                // her ada o parkurun menü manzarasından kırpılmış bir pencere; altta toprak kalınlığı, üstte ad şeridi.
                // Kilitli parkur da manzarasını gösterir ama karartılır ve ortasında altın kilit durur.
                var theme = HomeTrackTheme.Get(i);
                c.island = E("col");
                c.island.style.width = 600;
                c.island.style.alignItems = Align.Center;
                var pic = E();
                pic.style.width = 600;
                pic.style.height = 270;
                pic.style.backgroundImage = Background.FromTexture2D(Resources.Load<Texture2D>("UI/MapThumbs/" + theme.slug));
                pic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                pic.style.backgroundColor = theme.panel;
                pic.style.borderTopLeftRadius = pic.style.borderTopRightRadius = 70;
                pic.style.borderBottomLeftRadius = pic.style.borderBottomRightRadius = 90;
                pic.style.borderTopWidth = pic.style.borderLeftWidth = pic.style.borderRightWidth = 8;
                pic.style.borderBottomWidth = 34;
                pic.style.borderTopColor = pic.style.borderLeftColor = pic.style.borderRightColor = Color.white;
                pic.style.borderBottomColor = (Color)new Color32(0x8A, 0x5A, 0x33, 0xFF);
                pic.style.overflow = Overflow.Hidden;
                c.island.Add(pic);
                c.pic = pic;
                // Üst parlaklık şeridi
                var gloss = E();
                gloss.pickingMode = PickingMode.Ignore;
                gloss.style.position = Position.Absolute;
                gloss.style.left = 30; gloss.style.right = 30; gloss.style.top = 10;
                gloss.style.height = 40;
                gloss.style.borderTopLeftRadius = gloss.style.borderTopRightRadius = 30;
                gloss.style.borderBottomLeftRadius = gloss.style.borderBottomRightRadius = 30;
                gloss.style.backgroundColor = new Color(1f, 1f, 1f, 0.22f);
                pic.Add(gloss);
                // Kilit örtüsü
                c.lockDisc = E("center");
                c.lockDisc.pickingMode = PickingMode.Ignore;
                c.lockDisc.style.position = Position.Absolute;
                c.lockDisc.style.left = c.lockDisc.style.right = c.lockDisc.style.top = c.lockDisc.style.bottom = 0;
                c.lockDisc.style.backgroundColor = new Color(0.08f, 0.06f, 0.16f, 0.4f);
                var lockRing = E("center");
                lockRing.style.width = lockRing.style.height = 130;
                lockRing.style.borderTopLeftRadius = lockRing.style.borderTopRightRadius = 65;
                lockRing.style.borderBottomLeftRadius = lockRing.style.borderBottomRightRadius = 65;
                lockRing.style.backgroundColor = (Color)new Color32(0xB0, 0x7A, 0x45, 0xFF);
                lockRing.style.borderBottomWidth = 10;
                lockRing.style.borderBottomColor = (Color)new Color32(0x7A, 0x4A, 0x22, 0xFF);
                c.lockIcon = new Icon(IconKind.Lock, 84, (Color)new Color32(0xFF, 0xC9, 0x3C, 0xFF));
                lockRing.Add(c.lockIcon);
                c.lockDisc.Add(lockRing);
                pic.Add(c.lockDisc);

                // Ad şeridi: resmin alt kenarına biner, parkurun renginde
                var plate = E("col");
                plate.style.alignItems = Align.Center;
                plate.style.marginTop = -44;
                plate.style.paddingLeft = plate.style.paddingRight = 34;
                plate.style.paddingTop = 6; plate.style.paddingBottom = 12;
                plate.style.backgroundColor = Color.Lerp(theme.panel, theme.edge, 0.3f);
                plate.style.borderTopLeftRadius = plate.style.borderTopRightRadius = 26;
                plate.style.borderBottomLeftRadius = plate.style.borderBottomRightRadius = 26;
                plate.style.borderBottomWidth = 10;
                plate.style.borderBottomColor = Color.Lerp(theme.panel, Color.black, 0.4f);
                plate.style.borderTopWidth = plate.style.borderLeftWidth = plate.style.borderRightWidth = 4;
                plate.style.borderTopColor = plate.style.borderLeftColor = plate.style.borderRightColor = Color.white;
                var name = L(TrackLibrary.Names[i], "heavy");
                name.style.fontSize = 52;
                name.style.color = Color.white;
                name.style.unityTextOutlineWidth = 6;
                name.style.unityTextOutlineColor = (Color)new Color32(0x2B, 0x23, 0x40, 0xFF);
                plate.Add(name);
                var slim = E("bar");
                slim.style.width = 380;
                slim.style.height = 26;
                slim.style.marginTop = 2;
                slim.style.borderTopWidth = slim.style.borderBottomWidth = slim.style.borderLeftWidth = slim.style.borderRightWidth = 4;
                slim.style.borderTopColor = slim.style.borderBottomColor = slim.style.borderLeftColor = slim.style.borderRightColor = (Color)new Color32(0x2B, 0x23, 0x40, 0xFF);
                slim.style.backgroundColor = (Color)new Color32(0x3A, 0x34, 0x55, 0xFF);
                c.fill = E("bar-fill");
                slim.Add(c.fill);
                plate.Add(slim);
                c.bar = slim;
                c.island.Add(plate);
                c.root.Add(c.island);
                c.lockName = new Label();

                // Durum ve kumbara: manzara üstünde okunsun diye yarı saydam koyu hap içinde
                var info = E("col");
                info.style.alignItems = Align.Center;
                info.style.marginTop = 8;
                info.style.paddingLeft = info.style.paddingRight = 26;
                info.style.paddingTop = 4; info.style.paddingBottom = 6;
                info.style.backgroundColor = new Color(0.1f, 0.08f, 0.2f, 0.62f);
                info.style.borderTopLeftRadius = info.style.borderTopRightRadius = info.style.borderBottomLeftRadius = info.style.borderBottomRightRadius = 26;
                c.status = L("", "heavy");
                c.status.style.fontSize = 32;
                info.Add(c.status);
                c.wallet = L("", "heavy");
                c.wallet.style.fontSize = 28;
                c.wallet.style.color = new Color(1f, 1f, 1f, 0.85f);
                info.Add(c.wallet);
                c.root.Add(info);
                // Eski rozet alanları (ShowMap uyumu)
                c.badge = new VisualElement();
                c.number = new Label();

                mapCards[i] = c;
                list.Add(c.root);
            }
            p.Add(mapScroll);
        }

        public void ShowMap(SaveData save)
        {
            for (int i = 0; i < TrackLibrary.Count; i++)
            {
                var c = mapCards[i];
                bool open = i <= save.unlocked;
                bool done = save.Finished(i);
                float progress = Mathf.Clamp01(save.bests[i] / TrackLibrary.Lengths[i]);
                c.lockDisc.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
                c.bar.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                // Güncel parkur: altın çerçeve ve hafif büyük
                bool isCur = i == save.track;
                Color rim = isCur ? (Color)new Color32(0xFF, 0xD8, 0x4A, 0xFF) : Color.white;
                c.pic.style.borderTopColor = c.pic.style.borderLeftColor = c.pic.style.borderRightColor = rim;
                c.pic.style.borderTopWidth = c.pic.style.borderLeftWidth = c.pic.style.borderRightWidth = isCur ? 12 : 8;
                c.root.style.scale = new Scale(isCur ? new Vector2(1.06f, 1.06f) : Vector2.one);
                c.root.style.opacity = 1f;
                c.wallet.text = open ? save.trackMoney[i].ToString("N0", Tr) + " coins saved here" : "";
                c.wallet.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                c.fill.style.width = Length.Percent(progress * 100f);
                c.fill.style.backgroundColor = done || progress > 0.66f ? (Color)new Color32(0x3F, 0xD1, 0x5B, 0xFF) : (Color)new Color32(0xFF, 0x8A, 0x2E, 0xFF);
                if (done)
                {
                    c.status.text = "Complete! · Best " + TrackLibrary.Show(save.bests[i]) + " m";
                    c.status.style.color = (Color)new Color32(0x7C, 0xF0, 0x8E, 0xFF);
                }
                else if (open)
                {
                    c.status.text = save.bests[i] > 0 ? "Best " + TrackLibrary.Show(save.bests[i]) + " m · " + Mathf.FloorToInt(progress * 100f) + "%" : "New! Try it now";
                    c.status.style.color = (Color)new Color32(0xFF, 0xC9, 0x3C, 0xFF);
                }
                else
                {
                    c.status.text = "Locked · finish " + TrackLibrary.Names[i - 1] + " first";
                    c.status.style.color = new Color(1f, 1f, 1f, 0.8f);
                }
            }
            SetPage(Page.Map);
            // Yerleşim hesaplanınca güncel parkuru görünür yap
            var cur = mapCards[Mathf.Clamp(save.track, 0, TrackLibrary.Count - 1)].root;
            mapScroll.schedule.Execute(() => mapScroll.ScrollTo(cur)).StartingIn(30);
        }

        // ================================================================ nişan

        Button PauseButton()
        {
            var button = B(() => PauseRequested?.Invoke(), "btn", "btn-white", "pause-button");
            button.text = "PAUSE";
            return button;
        }

        void BuildPause()
        {
            pauseModal = Modal(out pauseCard, "PAUSED", new Color32(0x3F, 0xA4, 0xFF, 0xFF), new Color32(0x1E, 0x78, 0xD0, 0xFF));
            var note = L("Take your time. Your run will continue\nfrom the same spot when you resume.", "pause-note");
            pauseCard.Add(note);
            var resume = B(() => ResumeRequested?.Invoke(), "btn", "btn-green", "btn-big");
            resume.name = "menu-pause-resume";
            resume.text = "RESUME";
            resume.style.alignSelf = Align.Stretch;
            resume.style.marginTop = 24;
            pauseCard.Add(resume);
            safe.Add(pauseModal);
        }

        AimArrows aimArrows;

        void BuildAim()
        {
            var p = NewPage(Page.Aim);

            var top = E("topbar");
            top.Add(BestPill());
            top.Add(PauseButton());
            top.Add(CoinPill());
            p.Add(top);

            // Nişan okları (güce göre renk ve boy, nişana göre yön); ipucu ve güç yazısının arkasında.
            aimArrows = new AimArrows();
            p.Add(aimArrows);

            // İpucu (kullanıcı 2026-10-23, örnek görsele göre): parmak yok; yarı saydam cam panel, büyük iki renkli başlık.
            hint = E("col");
            hint.style.alignSelf = Align.Center;
            hint.style.marginTop = 70;
            hint.style.width = Length.Percent(86);
            hint.style.paddingTop = 26;
            hint.style.paddingBottom = 30;
            hint.style.paddingLeft = hint.style.paddingRight = 24;
            hint.style.alignItems = Align.Center;
            hint.style.backgroundColor = new Color(1f, 1f, 1f, 0.42f);
            hint.style.borderTopColor = hint.style.borderBottomColor = hint.style.borderLeftColor = hint.style.borderRightColor = new Color(1f, 1f, 1f, 0.85f);
            hint.style.borderTopWidth = hint.style.borderBottomWidth = hint.style.borderLeftWidth = hint.style.borderRightWidth = 4;
            hint.style.borderTopLeftRadius = hint.style.borderTopRightRadius = hint.style.borderBottomLeftRadius = hint.style.borderBottomRightRadius = 44;
            hintHand = new VisualElement();   // eski parmak ikonu (artık gösterilmiyor)
            var hl1 = L("PULL BACK", "heavy");
            var hl2 = L("& RELEASE!", "heavy");
            foreach (var (l, c) in new[] { (hl1, (Color)new Color32(0xFF, 0xD2, 0x3F, 0xFF)), (hl2, (Color)new Color32(0xFF, 0x7E, 0x4A, 0xFF)) })
            {
                l.style.fontSize = 112;
                l.style.color = c;
                l.style.unityTextOutlineWidth = 9;
                l.style.unityTextOutlineColor = (Color)new Color32(0x4A, 0x24, 0x48, 0xFF);
                l.style.marginTop = -18;
                l.style.marginBottom = -18;
                hint.Add(l);
            }
            hintSub = L("", "heavy");
            hintSub.style.fontSize = 34;
            hintSub.style.color = (Color)new Color32(0x2B, 0x23, 0x40, 0xFF);
            hintSub.style.marginTop = 22;
            hintSub.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.Add(hintSub);
            p.Add(hint);

            power = E("col");
            power.style.marginTop = 70;
            power.style.alignSelf = Align.Center;
            power.style.width = Length.Percent(70);
            powerLabel = L("POWER 0%", "outlined");
            powerLabel.style.fontSize = 72;
            power.Add(powerLabel);
            powerBar = new TrackBar();
            powerBar.root.style.width = Length.Percent(100);
            power.Add(powerBar.root);
            p.Add(power);

            p.Add(E("grow"));
            var back = B(() => OpenHub?.Invoke(), "btn", "btn-blue", "btn-mid");
            back.text = "BACK";
            back.style.width = 340;
            back.style.alignSelf = Align.Center;
            back.style.marginBottom = 20;
            p.Add(back);
        }

        void TickAim(in HudInfo h)
        {
            string text = h.tiltMode
                ? "Pull sideways to launch the other way.\nCup: tilt the phone to keep it level."
                : h.mobileControls ? "Pull sideways to launch the other way.\nCup balance assist is on for this device."
                : "Pull sideways to launch the other way.\nSpace + ←/→ also works · Cup: I J K L";
            if (hintSub.text != text) hintSub.text = text;

            bool pulling = h.pull > 0.01f;
            hint.style.opacity = Mathf.MoveTowards(hint.resolvedStyle.opacity, pulling ? 0f : 1f, 0.15f);
            aimArrows.power = h.pull;
            aimArrows.aim = h.aimAngle;
            aimArrows.time = time;
            aimArrows.MarkDirtyRepaint();

            power.style.display = pulling ? DisplayStyle.Flex : DisplayStyle.None;
            if (pulling)
            {
                // Tam gerilince kısa bir an "RELEASE!": o anda bırakmak mükemmel atış (lastik %6 güçlü).
                powerLabel.text = h.perfectWindow ? "RELEASE!" : "POWER " + Mathf.RoundToInt(h.pull * 100f) + "%";
                powerBar.Set(h.pull, 0f);
                powerBar.fill.style.backgroundColor = h.perfectWindow ? (Color)new Color32(0x3F, 0xD1, 0x5B, 0xFF)
                    : Color.Lerp(new Color32(0xFF, 0xC9, 0x3C, 0xFF), new Color32(0xFF, 0x4D, 0x3D, 0xFF), h.pull);
                float s = h.perfectWindow ? 1.18f + 0.06f * Mathf.Sin(time * 30f) : 1f + 0.04f * h.pull * Mathf.Sin(time * 40f);
                powerLabel.style.scale = new Scale(new Vector2(s, s));
            }
        }

        // ================================================================ koşu

        /// Hız çizgileri (videodaki gibi): ekranın alt ve yan kenarlarından merkeze doğru akan ince beyaz çizgiler.
        /// Yoğunluk hızla artar; fırlatma anında kısa bir patlama.
        class SpeedLines : VisualElement
        {
            struct Streak { public float angle, offset, speed, length, width; }
            readonly Streak[] streaks = new Streak[38];
            readonly System.Random rnd = new System.Random(7);
            public float intensity;
            float clock;

            public SpeedLines()
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = style.right = style.top = style.bottom = 0;
                for (int i = 0; i < streaks.Length; i++) Respawn(ref streaks[i], (float)rnd.NextDouble());
                generateVisualContent += Draw;
            }

            void Respawn(ref Streak s, float offset)
            {
                // Çoğunluk altta (kızağın iki yanından), birkaçı yanlarda
                float a = (float)rnd.NextDouble();
                s.angle = a < 0.7f ? Mathf.Lerp(200f, 340f, (float)rnd.NextDouble()) : (a < 0.85f ? Mathf.Lerp(160f, 200f, (float)rnd.NextDouble())
                                                                                                    : Mathf.Lerp(-20f, 20f, (float)rnd.NextDouble()));
                s.offset = offset;
                s.speed = 0.8f + (float)rnd.NextDouble() * 0.9f;
                s.length = 0.08f + (float)rnd.NextDouble() * 0.14f;
                s.width = 2f + (float)rnd.NextDouble() * 3f;
            }

            public void Tick(float dt)
            {
                clock += dt;
                if (intensity <= 0.01f) { style.display = DisplayStyle.None; return; }
                style.display = DisplayStyle.Flex;
                for (int i = 0; i < streaks.Length; i++)
                {
                    streaks[i].offset += dt * streaks[i].speed * (0.6f + intensity);
                    if (streaks[i].offset > 1f) Respawn(ref streaks[i], 0f);
                }
                MarkDirtyRepaint();
            }

            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                if (r.width <= 0f) return;
                var c = new Vector2(r.width * 0.5f, r.height * 0.42f);
                float reach = Mathf.Max(r.width, r.height) * 0.75f;
                var p = ctx.painter2D;
                p.lineCap = LineCap.Round;
                int shown = Mathf.RoundToInt(streaks.Length * Mathf.Clamp01(intensity));
                for (int i = 0; i < shown; i++)
                {
                    var s = streaks[i];
                    var dir = new Vector2(Mathf.Cos(s.angle * Mathf.Deg2Rad), -Mathf.Sin(s.angle * Mathf.Deg2Rad));
                    // İçeriden dışarı akar (kamera ileri gidiyor): merkezden 0.35..1.1 uzaklık arasında
                    float d0 = Mathf.Lerp(0.35f, 1.1f, s.offset), d1 = d0 + s.length;
                    float fade = Mathf.Sin(s.offset * Mathf.PI) * intensity;
                    p.strokeColor = new Color(1f, 1f, 1f, 0.55f * fade);
                    p.lineWidth = s.width;
                    p.BeginPath();
                    p.MoveTo(c + dir * reach * d0);
                    p.LineTo(c + dir * reach * d1);
                    p.Stroke();
                }
            }
        }

        SpeedLines speedLines;

        void BuildRun()
        {
            var p = NewPage(Page.Run);
            speedLines = new SpeedLines();
            p.Add(speedLines);

            runColumn = new ProgressColumn();
            p.Add(runColumn.root);

            var pause = PauseButton();
            pause.style.position = Position.Absolute;
            pause.style.right = 36;
            pause.style.top = 36;
            p.Add(pause);
            // Kötü atışta durmayı beklemeden yeniden (kullanıcı 2026-10-19): yeni atış bir hak harcar.
            var again = B(() => RetryRun?.Invoke(), "btn", "btn-orange", "pause-button");
            again.text = "RETRY";
            again.style.position = Position.Absolute;
            again.style.right = 36;
            again.style.top = 132;
            p.Add(again);

            // Sol üst köşe (kullanıcı isteği): toplanan coin ve elmas; gidilen mesafe onların altında, ortada.
            var runCorner = E("col");
            runCorner.style.position = Position.Absolute;
            runCorner.style.left = 36;
            runCorner.style.top = 36;
            runCorner.style.alignItems = Align.FlexStart;
            runCorner.pickingMode = PickingMode.Ignore;
            p.Add(runCorner);

            runLoot = E("row");
            runLoot.style.transformOrigin = new TransformOrigin(0, Length.Percent(50));
            var coinBox = E("pill");
            coinBox.style.height = 72;
            coinBox.Add(new Icon(IconKind.Coin, 52));
            runCoinLabel = L("0");
            runCoinLabel.style.fontSize = 38;
            coinBox.Add(runCoinLabel);
            runLoot.Add(coinBox);
            runGemBox = E("pill");
            runGemBox.style.height = 72;
            runGemBox.style.marginLeft = 12;
            runGemBox.Add(new Icon(IconKind.Gem, 50));
            runGemLabel = L("0");
            runGemLabel.style.fontSize = 38;
            runGemBox.Add(runGemLabel);
            runLoot.Add(runGemBox);
            runCorner.Add(runLoot);

            distLabel = L("0 m", "outlined");
            distLabel.style.fontSize = 120;
            distLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            distLabel.style.marginTop = 96;   // coin/elmas satırının altında
            distLabel.pickingMode = PickingMode.Ignore;
            p.Add(distLabel);

            airLabel = L("AIRBORNE!", "outlined");
            airLabel.style.fontSize = 76;
            airLabel.style.color = Gold;
            airLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            airLabel.style.marginTop = 40;
            p.Add(airLabel);

            wingLabel = L("", "outlined", "wing-status");
            wingLabel.style.display = DisplayStyle.None;
            p.Add(wingLabel);

            toastLabel = L("LID OFF!", "outlined");
            toastLabel.style.fontSize = 84;
            toastLabel.style.color = (Color)new Color32(0xFF, 0xB3, 0x4A, 0xFF);
            toastLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            toastLabel.style.marginTop = 10;
            toastLabel.style.display = DisplayStyle.None;
            p.Add(toastLabel);

            p.Add(E("grow"));

            tutorial = E("card", "col");
            tutorial.style.alignSelf = Align.Center;
            tutorial.style.marginBottom = 470;
            tutorial.style.paddingLeft = tutorial.style.paddingRight = 40;
            p.Add(tutorial);

            // Hız göstergesi (referanstaki gibi): açık alt kısımlı dairesel yay, ortada büyük hız, altta alevli
            // turuncu roket butonu (yakıt butonun içinde dolar/boşalır).
            gaugeBox = E("gauge-box");
            gaugeBox.pickingMode = PickingMode.Ignore;
            gauge = new SpeedGauge();
            gaugeBox.Add(gauge);
            speedLabel = L("0", "gauge-speed");
            gaugeBox.Add(speedLabel);
            var unit = L("km/h", "gauge-unit");
            gaugeBox.Add(unit);
            // Roket düğmesi (kullanıcı kararı 2026-10-11: yeri aynı, görünüşü yenilendi): mavi cam balon, içinde roket;
            // hazırken çevresinde ışık halkası, yanarken alev rengi yakıt aşağı iner.
            rocketGlow = E("rocket-glow");
            rocketGlow.style.backgroundImage = new StyleBackground(Radial());
            rocketGlow.pickingMode = PickingMode.Ignore;
            gaugeBox.Add(rocketGlow);
            rocketBtn = B(() => FireRocket?.Invoke(), "btn", "rocket-btn");
            rocketBtn.style.backgroundImage = new StyleBackground(VGrad(new Color32(0x8F, 0xD8, 0xFF, 0xFF), new Color32(0x2F, 0x86, 0xE8, 0xFF)));
            rocketFill = E("rocket-fill");
            rocketFill.style.backgroundImage = new StyleBackground(VGrad(new Color32(0xFF, 0xD0, 0x3A, 0xFF), new Color32(0xFF, 0x5A, 0x1A, 0xFF)));
            rocketBtn.Add(rocketFill);
            var rocketGloss = E("rocket-gloss");
            rocketGloss.pickingMode = PickingMode.Ignore;
            rocketBtn.Add(rocketGloss);
            // Gemini örneğindeki gibi: balonun içinde roket ve altında BOOST yazısı.
            var rocketIcon = new Icon(IconKind.Rocket, 92);
            rocketIcon.style.marginTop = -22;
            rocketBtn.Add(rocketIcon);
            rocketLabel = L("BOOST", "rocket-label");
            rocketLabel.pickingMode = PickingMode.Ignore;
            rocketBtn.Add(rocketLabel);
            rocketCount = L("", "rocket-count");
            rocketBtn.Add(rocketCount);
            gaugeBox.Add(rocketBtn);
            p.Add(gaugeBox);
        }

        void TickRun(float dt, in HudInfo h)
        {
            runColumn.Set(h.progress, h.bestProgress);

            if (h.distance != shownDist)
            {
                shownDist = h.distance;
                distLabel.text = TrackLibrary.Show(h.distance) + " m";
            }
            int kmh = Mathf.RoundToInt(h.speedKmh);
            if (kmh != shownSpeed)
            {
                shownSpeed = kmh;
                speedLabel.text = kmh.ToString();
                gauge.SetValue(kmh / 150f);
            }

            if (h.airborne && !wasAirborne)
            {
                airClock = 0f;
                airLabel.text = h.canGlide ? "GLIDE!" : "AIRBORNE!";
                airLabel.style.display = DisplayStyle.Flex;
            }
            wasAirborne = h.airborne;
            if (h.airborne)
            {
                airLabel.text = h.hasWings ? h.canGlide ? "GLIDING" : "DESCENDING" : "AIRBORNE!";
                airClock += dt;
                float t = Mathf.Clamp01(airClock / 0.35f);
                float s = EaseOutBack(t) * (1f + 0.04f * Mathf.Sin(time * 9f));
                airLabel.style.scale = new Scale(new Vector2(s, s));
                airLabel.style.rotate = new Rotate(Angle.Degrees(-4f + 3f * Mathf.Sin(time * 5f)));
            }
            else airLabel.style.display = DisplayStyle.None;

            wingLabel.style.display = h.airborne && h.hasWings ? DisplayStyle.Flex : DisplayStyle.None;
            if (h.airborne && h.hasWings)
            {
                wingLabel.text = h.wingSecondsLeft > 0f
                    ? "FULL LIFT  " + h.wingSecondsLeft.ToString("0.0", Tr) + " s"
                    : "LIFT REDUCED · AIM FOR A SAFE LANDING";
                wingLabel.style.color = h.wingSecondsLeft > 2f ? Color.white : Gold;
            }

            if (toastClock >= 0f)
            {
                toastClock += dt;
                float s = EaseOutBack(Mathf.Clamp01(toastClock / 0.35f));
                toastLabel.style.scale = new Scale(new Vector2(s, s));
                toastLabel.style.opacity = Mathf.Clamp01((1.8f - toastClock) / 0.4f);
                if (toastClock > 1.8f)
                {
                    toastClock = -1f;
                    toastLabel.style.display = DisplayStyle.None;
                }
            }

            if (h.runCoins != shownRunCoins || h.runGems != shownRunGems)
            {
                if (shownRunCoins >= 0 && (h.runCoins > shownRunCoins || h.runGems > shownRunGems)) lootBump = 1f;
                shownRunCoins = h.runCoins;
                shownRunGems = h.runGems;
                runCoinLabel.text = h.runCoins.ToString();
                runGemLabel.text = h.runGems.ToString();
            }
            lootBump = Mathf.MoveTowards(lootBump, 0f, dt * 4f);
            float lb = 1f + 0.22f * Mathf.Sin(lootBump * Mathf.PI) * lootBump;
            runLoot.style.scale = new Scale(new Vector2(lb, lb));

            rocketBtn.style.display = h.hasRocket ? DisplayStyle.Flex : DisplayStyle.None;
            // Bardak kamerası hangi köşedeyse gösterge öbür köşede durur.
            gaugeBox.style.left = h.hand > 0 ? 16 : StyleKeyword.Auto;
            gaugeBox.style.right = h.hand > 0 ? StyleKeyword.Auto : 16;
            if (h.hasRocket)
            {
                bool firing = h.rocketFuel > 0f;
                rocketLabel.text = h.rocketNeedsLanding ? "LAND FIRST" : firing ? "BOOSTING" : h.rocketReady ? "BOOST" : h.rocketCharges > 0 ? "WAIT" : "EMPTY";
                rocketLabel.style.fontSize = h.rocketNeedsLanding || firing ? 27 : 34;
                rocketBtn.SetEnabled(h.rocketReady);
                rocketFill.style.height = Length.Percent(firing ? h.rocketFuel * 100f : h.rocketReady ? 100f : 0f);
                rocketBtn.style.opacity = h.rocketReady || firing ? 1f : 0.5f;
                rocketGlow.style.opacity = h.rocketReady ? 0.55f + 0.35f * Mathf.Sin(time * 6f) : 0f;
                rocketGlow.style.rotate = new Rotate(time * 30f);
                rocketCount.text = h.rocketCharges > 1 ? "x" + h.rocketCharges : "";
                float pulse = h.rocketReady ? 1f + 0.05f * Mathf.Sin(time * 6f) : 1f;
                rocketBtn.style.scale = new Scale(new Vector2(pulse, pulse));
            }

            // Aşamalı öğretim: ilgili an gelince tek ipucu (GameController.ShowHint), birkaç saniye görünür.
            hintTime -= dt;
            bool tut = hintTime > 0f && !debugHideTutorial;
            tutorial.style.display = tut ? DisplayStyle.Flex : DisplayStyle.None;
            if (tut) tutorial.style.opacity = Mathf.Clamp01(hintTime / 0.4f);
        }

        /// Koşuda tek satırlık ipucu kartı (aşamalı öğretim).
        public void ShowHint(IconKind icon, string text, float seconds = 3.2f)
        {
            tutorial.Clear();
            tutorial.Add(TutorialRow(icon, text));
            tutorial.Query<VisualElement>().ForEach(e => e.pickingMode = PickingMode.Ignore);
            hintTime = seconds;
        }

        public void HideHint() => hintTime = 0f;

        VisualElement TutorialRow(IconKind kind, string text)
        {
            var row = E("row");
            row.style.marginTop = 6;
            row.style.marginBottom = 6;
            row.Add(new Icon(kind, 64, Ink));
            var l = Sp(L(text, "heavy"), 16);
            l.style.fontSize = 42;
            row.Add(l);
            return row;
        }

        // ================================================================ bardak göstergesi

        VisualElement BuildGlassHud()
        {
            var hud = E("fill");
            glassFrame = E("glass-frame");
            glassFrame.Add(E("glass-outline"));
            glassFrame.Add(E("glass-gloss"));
            hud.Add(glassFrame);

            milkPill = E("milk-pill");
            milkFill = E("milk-fill");
            milkPill.Add(milkFill);
            milkIcon = new Icon(IconKind.Glass, 54, Ink);
            milkPill.Add(milkIcon);
            lidIcon = new Icon(IconKind.Lock, 46, (Color)new Color32(0xE0, 0x7A, 0x10, 0xFF));
            lidIcon.style.marginLeft = 4;
            milkPill.Add(lidIcon);
            milkLabel = L("MILK 100%");
            milkPill.Add(milkLabel);
            hud.Add(milkPill);
            return hud;
        }

        void UpdateGlass(float dt, in HudInfo h)
        {
            float W = root.layout.width, H = root.layout.height;
            if (!(W > 0f) || !(H > 0f)) return;
            var r = h.glassRect;
            float left = r.x * W, top = (1f - r.y - r.height) * H, width = r.width * W, height = r.height * H;
            const float b = 10f;
            glassFrame.style.left = left - b;
            glassFrame.style.top = top - b;
            glassFrame.style.width = width + 2f * b;
            glassFrame.style.height = height + 2f * b;

            // Hap çerçevenin üst kenarına sekme gibi oturur (ayrı yüzen hap yerine).
            milkPill.style.left = left + 6f;
            milkPill.style.width = width - 12f;
            milkPill.style.top = top - b - 48f;

            float fill = Mathf.Clamp01(h.fill);
            int pct = Mathf.RoundToInt(fill * 100f);
            bool lid = h.lidLeft > 0f;
            lidIcon.style.display = lid ? DisplayStyle.Flex : DisplayStyle.None;
            milkIcon.style.display = lid ? DisplayStyle.None : DisplayStyle.Flex;
            string text = lid ? "LID " + TrackLibrary.Show(h.lidLeft) + " m" : "MILK " + pct + "%";
            if (milkLabel.text != text) milkLabel.text = text;
            if (pct != shownMilk)
            {
                shownMilk = pct;
                milkIcon.SetFill(fill);
                milkFill.style.width = Length.Percent(fill * 100f);
                milkFill.style.backgroundColor = fill > 0.5f ? (Color)new Color32(0xCD, 0xEB, 0xFF, 0xFF)
                                               : fill > 0.25f ? (Color)new Color32(0xFF, 0xE0, 0x8A, 0xFF)
                                               : (Color)new Color32(0xFF, 0x9C, 0x8F, 0xFF);
            }
            // Süt ağza yaklaşınca gösterge büyür ve kızarır (dikkat bardağa); güvendeyken sade kalır.
            float risk = lid ? 0f : Mathf.Max(Mathf.InverseLerp(0.55f, 0.95f, h.milkDanger), fill < 0.3f ? 0.6f : 0f);
            milkDangerShown = Mathf.MoveTowards(milkDangerShown, risk, dt * 6f);
            milkPill.style.opacity = 1f;
            glassFrame.style.opacity = 1f;
            // Tehlikede çerçeve kızarır ve nabız gibi atar.
            float pulse = milkDangerShown * (0.5f + 0.5f * Mathf.Sin(time * 9f));
            glassFrame.style.borderTopColor = glassFrame.style.borderBottomColor = glassFrame.style.borderLeftColor = glassFrame.style.borderRightColor
                = Color.Lerp(Color.white, new Color(1f, 0.36f, 0.25f), Mathf.Max(milkDangerShown * 0.6f, pulse));
            float fs = 1f + 0.03f * pulse;
            glassFrame.style.scale = new Scale(new Vector2(fs, fs));
            float s = 1f + 0.12f * milkDangerShown + (fill < 0.3f ? 0.06f * Mathf.Abs(Mathf.Sin(time * 7f)) : 0f);
            milkPill.style.scale = new Scale(new Vector2(s, s));
            milkPill.style.backgroundColor = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.35f), milkDangerShown);
        }

        // ================================================================ sonuç

        void BuildResult()
        {
            var p = NewPage(Page.Result);
            p.AddToClassList("dim");
            p.AddToClassList("pick");
            p.style.justifyContent = Justify.Center;

            resultCard = E("card", "card-cream", "col", "pop");
            resultCard.style.paddingTop = 0;
            resultCard.style.paddingLeft = resultCard.style.paddingRight = 40;
            resultCard.style.paddingBottom = 40;

            confetti = E("fill");
            p.Add(confetti);
            var rng = new System.Random(5);
            Color[] colors =
            {
                new Color32(0xFF, 0x8F, 0x1F, 0xFF), new Color32(0x3F, 0xD1, 0x5B, 0xFF), new Color32(0x3F, 0xA4, 0xFF, 0xFF),
                new Color32(0xFF, 0xD8, 0x4A, 0xFF), new Color32(0xFF, 0x6E, 0xB4, 0xFF), new Color32(0xFF, 0x4D, 0x3D, 0xFF),
            };
            for (int i = 0; i < 46; i++)
            {
                var e = E();
                e.style.position = Position.Absolute;
                e.style.width = 16 + rng.Next(10);
                e.style.height = 28 + rng.Next(14);
                e.style.borderTopLeftRadius = e.style.borderTopRightRadius = 4;
                e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = 4;
                e.style.backgroundColor = colors[i % colors.Length];
                confetti.Add(e);
                bits.Add(new Confetti
                {
                    e = e, x = (float)rng.NextDouble(), y = -(float)rng.NextDouble() * 1.2f,
                    speed = 0.25f + (float)rng.NextDouble() * 0.25f, sway = 20f + (float)rng.NextDouble() * 40f,
                    phase = (float)rng.NextDouble() * 6f, spin = 200f + (float)rng.NextDouble() * 400f,
                });
            }

            resRibbon = E("ribbon");
            resRibbon.style.marginTop = -50;
            resRibbon.style.height = 104;
            resTitle = L("");
            resTitle.style.fontSize = 56;
            resRibbon.Add(resTitle);
            resultCard.Add(resRibbon);

            resRecord = E("ribbon");
            resRecord.style.position = Position.Absolute;
            resRecord.style.right = -24;
            resRecord.style.top = 80;
            resRecord.style.rotate = new Rotate(Angle.Degrees(10f));
            resRecord.style.backgroundColor = (Color)new Color32(0xFF, 0x4D, 0x3D, 0xFF);
            resRecord.style.borderBottomColor = (Color)new Color32(0xC0, 0x2A, 0x1E, 0xFF);
            resRecord.Add(L("NEW BEST!"));

            resDist = L("0 m", "outlined");
            resDist.style.fontSize = 170;
            resDist.style.marginTop = 20;
            resultCard.Add(resDist);

            resBar = new TrackBar();
            resBar.root.style.width = Length.Percent(100);
            resultCard.Add(resBar.root);

            var stats = E("row");
            stats.style.marginTop = 30;
            stats.style.alignSelf = Align.Stretch;
            stats.Add(Stat(IconKind.Glass, "Milk left", out resMilk));
            stats.Add(Sp(Stat(IconKind.Coin, "Earned", out resCoins), 22));
            resultCard.Add(stats);

            resLoot = L("", "heavy");
            resLoot.style.fontSize = 36;
            resLoot.style.marginTop = 14;
            resLoot.style.color = Muted;
            resultCard.Add(resLoot);

            // Bir sonraki atış için öneri (neden + yapılacak şey)
            resTip = L("", "heavy");
            resTip.style.fontSize = 34;
            resTip.style.marginTop = 16;
            resTip.style.paddingLeft = resTip.style.paddingRight = 22;
            resTip.style.paddingTop = resTip.style.paddingBottom = 12;
            resTip.style.whiteSpace = WhiteSpace.Normal;
            resTip.style.unityTextAlign = TextAnchor.MiddleCenter;
            resTip.style.backgroundColor = (Color)new Color32(0xFF, 0xF1, 0xC9, 0xFF);
            resTip.style.borderTopLeftRadius = resTip.style.borderTopRightRadius = 22;
            resTip.style.borderBottomLeftRadius = resTip.style.borderBottomRightRadius = 22;
            resTip.style.color = (Color)new Color32(0x6B, 0x4A, 0x10, 0xFF);
            resultCard.Add(resTip);

            resBonus = L("", "heavy");
            resBonus.style.color = (Color)new Color32(0x22, 0xA0, 0x3C, 0xFF);
            resBonus.style.fontSize = 40;
            resBonus.style.marginTop = 14;
            resultCard.Add(resBonus);

            unlockBox = E("card", "row");
            unlockBox.style.alignSelf = Align.Stretch;
            unlockBox.style.marginTop = 24;
            unlockBox.style.borderBottomColor = (Color)new Color32(0xFF, 0x8F, 0x1F, 0x88);
            unlockBox.Add(new Icon(IconKind.Map, 84, new Color32(0x3F, 0xA4, 0xFF, 0xFF)));
            var unlockCol = E();
            unlockCol.style.flexShrink = 1;
            unlockCol.style.flexGrow = 1;
            unlockCol.style.marginLeft = 18;
            unlockTitle = L("", "heavy");
            unlockTitle.style.fontSize = 34;
            unlockTitle.style.color = (Color)new Color32(0xD8, 0x62, 0x0A, 0xFF);
            unlockTitle.style.marginBottom = -8;
            unlockCol.Add(unlockTitle);
            unlockName = L("", "heavy");
            unlockName.style.fontSize = 42;
            unlockName.style.whiteSpace = WhiteSpace.Normal;
            unlockCol.Add(unlockName);
            unlockBox.Add(unlockCol);
            resultCard.Add(unlockBox);
            unlockProgression = L("Prepare a new vehicle on the next track. Your parts and gems carry over.\nYour previous coins and upgrades are saved — revisit them from Tracks.", "progression-note");
            resultCard.Add(unlockProgression);

            // Kullanıcı isteği: kazanç reklamla 3 katına çıkarılabilir (sandıktaki gibi test reklamı).
            resTriple = B(() => ShowAd(() =>
            {
                coinFrom = result.earned;
                result.earned *= 3;
                coinStart = resultClock;
                resTriple.style.display = DisplayStyle.None;
                TripleEarnings?.Invoke();
            }), "btn", "btn-orange", "btn-big");
            resTriple.style.alignSelf = Align.Stretch;
            resTriple.style.marginTop = 30;
            resTriple.Add(new Icon(IconKind.Video, 64));
            resTriple.Add(Sp(L("TRIPLE EARNINGS"), 14));
            resultCard.Add(resTriple);

            var go = B(() =>
            {
                if (goNext) NextTrack?.Invoke();
                else OpenHub?.Invoke();
            }, "btn", "btn-green", "btn-big");
            go.style.alignSelf = Align.Stretch;
            go.style.marginTop = 22;
            goIcon = new Icon(IconKind.Play, 60);
            go.Add(goIcon);
            goLabel = Sp(L("CONTINUE"), 16);
            go.name = "menu-result-continue";
            go.Add(goLabel);
            resultCard.Add(go);

            var retry = B(() => Retry?.Invoke(), "btn", "btn-white", "btn-mid");
            retry.style.alignSelf = Align.Stretch;
            retry.style.marginTop = 22;
            retry.Add(new Icon(IconKind.Play, 50, Ink));
            retry.Add(Sp(L("Launch again"), 12));
            resultCard.Add(retry);

            resultCard.Add(resRecord);
            p.Add(resultCard);
        }

        VisualElement Stat(IconKind kind, string caption, out Label value)
        {
            var box = E("card", "grow", "row");
            box.style.flexBasis = 0;
            box.style.paddingTop = 14;
            box.style.paddingBottom = 14;
            box.Add(new Icon(kind, 76, Ink));
            var col = E();
            col.style.marginLeft = 14;
            value = L("", "heavy");
            value.style.fontSize = 56;
            value.style.marginBottom = -14;
            col.Add(value);
            col.Add(L(caption, "muted"));
            box.Add(col);
            return box;
        }

        void TickResult(float dt)
        {
            resultClock += dt;
            if (result.finished)
            {
                float W = root.layout.width, H = root.layout.height;
                if (W > 0f && H > 0f)
                    foreach (var b in bits)
                    {
                        b.y += b.speed * dt;
                        if (b.y > 1.05f) b.y -= 1.15f;
                        b.e.style.left = b.x * W + Mathf.Sin(time * 2f + b.phase) * b.sway;
                        b.e.style.top = b.y * H;
                        b.e.style.rotate = new Rotate(Angle.Degrees(time * b.spin + b.phase * 50f));
                    }
            }
            float t = Mathf.Clamp01((resultClock - 0.25f) / 0.9f);
            float e = 1f - (1f - t) * (1f - t) * (1f - t);
            resDist.text = TrackLibrary.Show(result.distance * e) + " m";
            resBar.Set(result.progress * e, result.bestProgress);
            float tc = Mathf.Clamp01((resultClock - coinStart) / 0.8f);
            resCoins.text = "+" + Mathf.RoundToInt(Mathf.Lerp(coinFrom, result.earned, 1f - (1f - tc) * (1f - tc)));
            if (resTriple.style.display.value == DisplayStyle.Flex)
            {
                float pulse = 1f + 0.04f * Mathf.Sin(time * 5f);
                resTriple.style.scale = new Scale(new Vector2(pulse, pulse));
            }

            if (result.record)
            {
                float pop = EaseOutBack(Mathf.Clamp01((resultClock - 1.1f) / 0.35f));
                float s = pop * (1f + 0.05f * Mathf.Sin(time * 6f));
                resRecord.style.scale = new Scale(new Vector2(s, s));
            }
        }

        // ================================================================ yardımcılar

        VisualElement CoinPill()
        {
            var pill = E("pill");
            pill.Add(new Icon(IconKind.Coin, 66));
            var l = L("0");
            pill.Add(l);
            coinLabels.Add(l);
            coinPills.Add(pill);
            return pill;
        }

        /// Fırlatma hakkı göstergesi: "7/8" ve doluyorsa altında kalan süre.
        VisualElement LaunchPill()
        {
            var pill = E("pill", "pill-launch");
            pill.Add(new Icon(IconKind.Slingshot, 60));
            var col = E("col");
            var l = L(SaveData.MaxLaunches + "/" + SaveData.MaxLaunches);
            l.AddToClassList("launch-count");
            col.Add(l);
            var timer = L("");
            timer.AddToClassList("launch-timer");
            col.Add(timer);
            pill.Add(col);
            launchLabels.Add(l);
            launchTimers.Add(timer);
            return pill;
        }

        static string Clock(int seconds) => (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");

        void UpdateLaunches(in HudInfo h)
        {
            int key = h.freeLaunches ? -2 : h.launches;
            if (key != shownLaunches)
            {
                shownLaunches = key;
                foreach (var l in launchLabels) l.text = h.freeLaunches ? "∞" : h.launches + "/" + SaveData.MaxLaunches;
                if (launchModalCount != null) launchModalCount.text = h.launches + "/" + SaveData.MaxLaunches;
                // Hak geldiyse pencere kendiliğinden kapanır.
                if (h.launches > 0 && launchModal != null && launchModal.style.display.value == DisplayStyle.Flex) HideNoLaunches();
            }
            int secs = h.freeLaunches ? 0 : h.launchSeconds;
            if (secs != shownLaunchSeconds)
            {
                shownLaunchSeconds = secs;
                string t = secs > 0 ? Clock(secs) : "";
                foreach (var l in launchTimers)
                {
                    l.text = t;
                    l.style.display = secs > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                }
                if (launchModalTimer != null) launchModalTimer.text = "Next launch: " + (h.launchSeconds > 0 ? Clock(h.launchSeconds) : "ready");
            }
        }

        /// Fırlatma hakkı bitti penceresi: bekle ya da reklam izle (+1 hak).
        void BuildNoLaunches()
        {
            launchModal = E("fill", "dim", "center", "pick");
            launchModal.style.display = DisplayStyle.None;
            launchCard = E("card", "card-cream", "col", "pop", "off");
            launchCard.style.width = Length.Percent(84);
            launchCard.style.paddingTop = 0;
            launchCard.style.paddingLeft = launchCard.style.paddingRight = 40;
            launchCard.style.paddingBottom = 40;

            var ribbon = E("ribbon");
            ribbon.style.marginTop = -50;
            ribbon.style.height = 104;
            var title = L("LAUNCHES");
            title.style.fontSize = 50;
            ribbon.Add(title);
            launchCard.Add(ribbon);

            var big = new Icon(IconKind.Slingshot, 200);
            big.style.marginTop = 24;
            launchCard.Add(big);
            launchModalCount = L("0/" + SaveData.MaxLaunches, "outlined");
            launchModalCount.style.fontSize = 96;
            launchCard.Add(launchModalCount);
            var info = L("Out of launches! One refills every 25 minutes.", "heavy");
            info.style.fontSize = 34;
            info.style.color = Muted;
            info.style.whiteSpace = WhiteSpace.Normal;
            info.style.unityTextAlign = TextAnchor.MiddleCenter;
            launchCard.Add(info);
            launchModalTimer = L("Next launch: 25:00", "heavy");
            launchModalTimer.style.fontSize = 44;
            launchModalTimer.style.marginTop = 10;
            launchCard.Add(launchModalTimer);

            var watch = B(() => ShowAd(() => { WatchLaunchAd?.Invoke(); HideNoLaunches(); }), "btn", "btn-orange", "btn-big");
            watch.style.alignSelf = Align.Stretch;
            watch.style.marginTop = 30;
            watch.Add(new Icon(IconKind.Video, 70));
            watch.Add(Sp(L("WATCH · +1 LAUNCH"), 16));
            launchCard.Add(watch);

            var ok = B(HideNoLaunches, "btn", "btn-green", "btn-mid");
            ok.name = "menu-launches-close";
            ok.text = "OK";
            ok.style.alignSelf = Align.Stretch;
            ok.style.marginTop = 22;
            launchCard.Add(ok);

            launchModal.Add(launchCard);
            safe.Add(launchModal);
        }

        /// screenPos: Unity ekran koordinatı (alt-sol orijin). Baloncuğun kuyruğu bu noktayı gösterir.
        public void ShowBubble(string text, Vector3 screenPos)
        {
            var panel = root.panel;
            if (panel == null) return;
            if (bubble.style.display.value == DisplayStyle.None)
            {
                bubble.style.display = DisplayStyle.Flex;
                bubbleAge = 0f;
            }
            bubbleText.text = text;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            float w = float.IsNaN(bubble.layout.width) ? 0f : bubble.layout.width;
            float h = float.IsNaN(bubble.layout.height) ? 0f : bubble.layout.height;
            bubble.style.left = p.x - w * 0.5f;
            bubble.style.top = p.y - h - 6f;
            // Açılırken hafif büyüyerek belirir.
            bubbleAge += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(bubbleAge / 0.25f);
            float s = 0.6f + 0.4f * k + 0.08f * Mathf.Sin(k * Mathf.PI);
            bubble.style.scale = new Scale(new Vector2(s, s));
            bubble.style.opacity = k;
        }

        public void HideBubble()
        {
            if (bubble != null && bubble.style.display.value != DisplayStyle.None) bubble.style.display = DisplayStyle.None;
        }

        /// Test: arayüzün tamamını gizler/gösterir (sahne yakın çekimleri).
        public void DebugSetVisible(bool visible) => root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        public void ShowNoLaunches()
        {
            launchModal.style.display = DisplayStyle.Flex;
            launchCard.AddToClassList("off");
            launchCard.schedule.Execute(() => launchCard.RemoveFromClassList("off")).StartingIn(30);
        }

        public void HideNoLaunches()
        {
            launchCard.AddToClassList("off");
            launchModal.schedule.Execute(() => launchModal.style.display = DisplayStyle.None).StartingIn(180);
        }

        VisualElement GemPill()
        {
            var pill = E("pill");
            pill.Add(new Icon(IconKind.Gem, 62));
            var l = L("0");
            pill.Add(l);
            gemLabels.Add(l);
            gemPills.Add(pill);
            return pill;
        }

        VisualElement BestPill()
        {
            var pill = E("pill");
            pill.Add(new Icon(IconKind.Flag, 58, Color.white));
            var l = L("Best 0 m");
            l.style.fontSize = 38;
            pill.Add(l);
            bestLabels.Add(l);
            return pill;
        }

        Button B(Action action, params string[] classes)
        {
            var b = new Button(() =>
            {
                click?.Invoke();
                action();
            });
            foreach (var c in classes) b.AddToClassList(c);
            return b;
        }

        static VisualElement E(params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        static Label L(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        static T Sp<T>(T e, float marginLeft) where T : VisualElement
        {
            e.style.marginLeft = marginLeft;
            return e;
        }

        static StyleColor Col(bool on, Color c) => on ? new StyleColor(c) : new StyleColor(StyleKeyword.Null);

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
