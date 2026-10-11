using System;
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
        public float progress, bestProgress, speedKmh, fill, pull, runTime;
        public bool airborne, canGlide, dragging, tiltMode, sound;
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
        Icon chestIcon;
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
        VisualElement hubRibbon, playPill;
        Icon playHand;
        Button playButton;
        VisualElement hubTitlePanel, hubUpgPanel;
        /// Mağaza ve (test) karşılama teklifi.
        public event Action OpenShop, TestOffer;
        readonly StarShape[] playStars = new StarShape[9];

        /// Beş köşeli yıldız (görsel dosyasız): OYNA düğmesinin çevresindeki parıltılar.
        class StarShape : VisualElement
        {
            public StarShape()
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.width = style.height = 48;
                generateVisualContent += ctx =>
                {
                    var p = ctx.painter2D;
                    var c = new Vector2(24f, 24f);
                    p.fillColor = new Color(1f, 0.93f, 0.45f, 1f);
                    p.strokeColor = new Color(1f, 1f, 1f, 0.95f);
                    p.lineWidth = 3f;
                    p.BeginPath();
                    for (int i = 0; i < 10; i++)
                    {
                        float a = (-90f + i * 36f) * Mathf.Deg2Rad, r = i % 2 == 0 ? 22f : 9.5f;
                        var v = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                        if (i == 0) p.MoveTo(v); else p.LineTo(v);
                    }
                    p.ClosePath();
                    p.Fill();
                    p.Stroke();
                };
            }
        }
        VisualElement hubTrackFill, settings, settingsCard;
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
        Label rocketCount;

        // Nişan
        VisualElement hint, hintHand, power;
        Label hintSub;
        TrackBar powerBar;
        Label powerLabel;

        // Koşu
        ProgressColumn runColumn;
        Label distLabel, speedLabel, airLabel;
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
        Label unlockTitle, unlockName, goLabel;
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
            public VisualElement badge, fill;
            public Label number, status;
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

            glassHud = BuildGlassHud();
            root.Add(glassHud);

            safe = E("fill");
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

        /// Ekran noktası bir düğmenin ya da modal pencerenin üstünde mi? (Sapan sürüklemesi başlamasın diye.)
        /// Ekran noktası (alt-sol orijinli) roket düğmesinin üstünde mi? Düğme arayüz tıklamasını beklemeden, her yeni
        /// dokunuşla kontrol edilir: arayüz yalnızca ilk parmağı tıklama sayar, direksiyonu tutarken ikinci parmakla
        /// basılan roket ateşlenmiyordu (kullanıcı testi 2026-10-10).
        public bool OnRocketButton(Vector2 screenPos)
        {
            var panel = root.panel;
            if (panel == null || rocketBtn == null || rocketBtn.resolvedStyle.display == DisplayStyle.None) return false;
            var pos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            var r = rocketBtn.worldBound;
            float pad = r.width * 0.2f;   // parmak için biraz pay
            return pos.x >= r.xMin - pad && pos.x <= r.xMax + pad && pos.y >= r.yMin - pad && pos.y <= r.yMax + pad;
        }

        public bool BlocksPointer(Vector2 screenPos)
        {
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
            if (glass) UpdateGlass(h);

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
            pages[p] = e;
            safe.Add(e);
            return e;
        }

        void UpdateSafeArea()
        {
            var sa = Screen.safeArea;
            float w = root.layout.width, h = root.layout.height;
            // İlk yerleşimden önce boyut NaN olur; o karede güvenli alan hesaplanmaz.
            if (sa == lastSafe || float.IsNaN(w) || float.IsNaN(h) || w <= 0f || Screen.width <= 0) return;
            lastSafe = sa;
            float sx = w / Screen.width, sy = h / Screen.height;
            safe.style.left = sa.xMin * sx;
            safe.style.right = (Screen.width - sa.xMax) * sx;
            safe.style.top = (Screen.height - sa.yMax) * sy;
            safe.style.bottom = sa.yMin * sy;
        }

        void UpdateCoins(float dt, in HudInfo h)
        {
            if (h.money != shownMoney)
            {
                if (shownMoney >= 0) coinBump = 1f;
                shownMoney = h.money;
                foreach (var l in coinLabels) l.text = h.money.ToString();
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
            }
            gemBump = Mathf.MoveTowards(gemBump, 0f, dt * 3f);
            float g = 1f + 0.2f * Mathf.Sin(gemBump * Mathf.PI) * gemBump;
            foreach (var p in gemPills) p.style.scale = new Scale(new Vector2(g, g));
        }

        // ================================================================ ana ekran

        void BuildHub()
        {
            var p = NewPage(Page.Hub);

            // Ekranın üstünde ve altında yumuşak karartma: arayüz sahnenin önünde çerçeveli ve okunaklı durur.
            var shadeTop = E("hub-shade-top");
            shadeTop.style.backgroundImage = new StyleBackground(VGrad(new Color(0.12f, 0.09f, 0.2f, 0.55f), new Color(0.12f, 0.09f, 0.2f, 0f)));
            shadeTop.pickingMode = PickingMode.Ignore;
            p.Add(shadeTop);
            var shadeBottom = E("hub-shade-bottom");
            shadeBottom.style.backgroundImage = new StyleBackground(VGrad(new Color(0.12f, 0.09f, 0.2f, 0f), new Color(0.12f, 0.09f, 0.2f, 0.6f)));
            shadeBottom.pickingMode = PickingMode.Ignore;
            p.Add(shadeBottom);

            var top = E("topbar");
            var wallet = E("row");
            wallet.Add(CoinPill());
            wallet.Add(Sp(GemPill(), 16));
            wallet.Add(Sp(LaunchPill(), 16));
            top.Add(wallet);
            var gear = B(() => ShowSettings(true), "btn", "btn-blue", "btn-round");
            gear.Add(new Icon(IconKind.Gear, 64));
            top.Add(gear);
            p.Add(top);

            // Parkur başlığı: dokununca harita açılır.
            var title = B(() => OpenMap?.Invoke(), "hub-title");
            hubTitlePanel = title;
            // Pist adı, pist renginde degrade bir kurdelenin üstünde
            hubRibbon = E("hub-ribbon");
            hubTrackName = L("", "hub-ribbon-text");
            hubRibbon.Add(hubTrackName);
            title.Add(hubRibbon);
            hubTrackSub = L("", "hub-sub");
            title.Add(hubTrackSub);
            var slim = E("hub-bar");
            hubTrackFill = E("hub-bar-fill");
            slim.Add(hubTrackFill);
            var shine = E("hub-bar-shine");
            shine.pickingMode = PickingMode.Ignore;
            slim.Add(shine);
            hubTrackPct = L("0%", "hub-bar-pct");
            slim.Add(hubTrackPct);
            var flag = new Icon(IconKind.Finish, 46);
            flag.AddToClassList("hub-bar-flag");
            slim.Add(flag);
            title.Add(slim);
            p.Add(title);

            // Orta alan: karakter görünür; sağda harita ve roket kartı.
            var mid = E("row", "grow");
            mid.style.alignItems = Align.FlexStart;
            mid.style.justifyContent = Justify.SpaceBetween;
            mid.style.marginTop = 30;
            var leftSide = E("col");
            var avatarBtn = SideCard(() => OpenAvatars?.Invoke(), new Color32(0x6C, 0x8C, 0xFF, 0xFF), new Color32(0x3B, 0x55, 0xD0, 0xFF));
            avatarBtn.style.alignSelf = Align.FlexStart;
            hubAvatarPortrait = E("avatar-portrait");
            hubAvatarPortrait.style.width = 128;
            hubAvatarPortrait.style.height = 128;
            avatarBtn.Add(hubAvatarPortrait);
            avatarBtn.Add(L("HERO", "side-label"));
            leftSide.Add(avatarBtn);
            chestBtn = SideCard(() => OpenChest?.Invoke(), new Color32(0xFF, 0xB0, 0x3A, 0xFF), new Color32(0xD8, 0x6C, 0x0A, 0xFF));
            chestBtn.style.alignSelf = Align.FlexStart;
            chestIcon = new Icon(IconKind.Chest, 118);
            chestBtn.Add(chestIcon);
            chestLabel = L("OPEN!", "side-label");
            chestBtn.Add(chestLabel);
            leftSide.Add(chestBtn);
            var questBtn = SideCard(() => OpenQuests?.Invoke(), new Color32(0x9B, 0x6B, 0xFF, 0xFF), new Color32(0x6A, 0x3D, 0xD0, 0xFF));
            questBtn.style.alignSelf = Align.FlexStart;
            questBtn.Add(new Icon(IconKind.Check, 104, Color.white));
            questBtn.Add(L("QUESTS", "side-label"));
            questDot = E();
            questDot.style.position = Position.Absolute;
            questDot.style.right = -6;
            questDot.style.top = -6;
            questDot.style.width = questDot.style.height = 34;
            questDot.style.borderTopLeftRadius = questDot.style.borderTopRightRadius = 17;
            questDot.style.borderBottomLeftRadius = questDot.style.borderBottomRightRadius = 17;
            questDot.style.backgroundColor = (Color)new Color32(0xFF, 0x3D, 0x3D, 0xFF);
            questDot.style.display = DisplayStyle.None;
            questDot.pickingMode = PickingMode.Ignore;
            questBtn.Add(questDot);
            leftSide.Add(questBtn);
            mid.Add(leftSide);
            var side = E("col");
            var map = SideCard(() => OpenMap?.Invoke(), new Color32(0x3B, 0xD0, 0xB8, 0xFF), new Color32(0x1A, 0x93, 0x80, 0xFF));
            map.Add(new Icon(IconKind.Map, 108, Color.white));
            map.Add(L("MAP", "side-label"));
            side.Add(map);
            var shop = SideCard(() => OpenShop?.Invoke(), new Color32(0xFF, 0x7A, 0xB8, 0xFF), new Color32(0xD8, 0x3F, 0x86, 0xFF));
            shop.Add(new Icon(IconKind.Gem, 104));
            shop.Add(L("SHOP", "side-label"));
            side.Add(shop);
            var rocket = BuildUpgradeCard((int)UpgradeType.Rocket);
            rocket.root.style.width = 214;
            // Roket kartı sağda kalır ama küçülür (kullanıcı kararı 2026-10-11): sağ üst köşesinden %80.
            rocket.root.style.transformOrigin = new TransformOrigin(Length.Percent(100), Length.Percent(0));
            rocket.root.style.scale = new Scale(new Vector2(0.8f, 0.8f));
            rocket.root.style.marginBottom = -70;
            side.Add(rocket.root);
            cards[(int)UpgradeType.Rocket] = rocket;
            mid.Add(side);
            p.Add(mid);

            // Aşama bitince: kutlama yazısı
            stageLabel = L("", "outlined");
            stageLabel.style.position = Position.Absolute;
            stageLabel.style.left = 0;
            stageLabel.style.right = 0;
            stageLabel.style.top = 560;
            stageLabel.style.fontSize = 74;
            stageLabel.style.color = Gold;
            stageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            stageLabel.style.display = DisplayStyle.None;
            stageLabel.pickingMode = PickingMode.Ignore;
            p.Add(stageLabel);

            // Oynamak için dokun: koyu hap içinde, yanında zıplayan dokunma eli
            // OYNA düğmesi (kullanıcı kararı 2026-10-11): büyük sarı düğme, çevresinde parlayıp sönen yıldızlar.
            playPill = E("play-wrap");
            playPill.pickingMode = PickingMode.Ignore;
            for (int k = 0; k < playStars.Length; k++)
            {
                playStars[k] = new StarShape();
                playPill.Add(playStars[k]);
            }
            var play = B(() => Play?.Invoke(), "btn", "play-btn");
            play.style.backgroundImage = new StyleBackground(VGrad(new Color32(0xFF, 0xE0, 0x5A, 0xFF), new Color32(0xFF, 0xA8, 0x1A, 0xFF)));
            var playGloss = E("play-gloss");
            playGloss.pickingMode = PickingMode.Ignore;
            play.Add(playGloss);
            playHint = L("PLAY", "play-text");
            play.Add(playHint);
            playButton = play;
            playPill.Add(play);
            p.Add(playPill);

            // UPGRADES paneli: kartların arkasında koyu, yuvarlak köşeli zemin ve başlık.
            var panel = E("upg-panel");
            hubUpgPanel = panel;
            var head = E("row", "upg-head");
            head.Add(E("upg-line"));
            head.Add(L("UPGRADES", "upg-title"));
            head.Add(E("upg-line"));
            panel.Add(head);
            var row = E("row");
            row.style.alignItems = Align.Stretch;
            panel.Add(row);
            p.Add(panel);
            for (int i = 0; i < (int)UpgradeType.Rocket; i++)
            {
                var c = BuildUpgradeCard(i);
                c.root.style.flexGrow = 1;
                c.root.style.flexBasis = 0;
                if (i > 0) c.root.style.marginLeft = 12;
                cards[i] = c;
                row.Add(c.root);
            }
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
            // Çerçeve kartın kendi renginde (beyaz çerçeve dört kartı birbirine benzetiyordu), üstte cam parlaması.
            var frame = Color.Lerp(main, Color.white, 0.25f);
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
            var name = c.title = L(Upgrades.Names[i].ToUpper(Tr), "outlined");
            name.style.fontSize = 30;
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
            c.shine = E("pcard-shine");
            c.shine.pickingMode = PickingMode.Ignore;
            c.buy.Add(c.shine);
            // Üstte "UPGRADE", altında ikon + fiyat (iki satır).
            c.buy.Add(L("UPGRADE", "pcard-btn-cap"));
            var priceRow = E("row");
            priceRow.pickingMode = PickingMode.Ignore;
            priceRow.style.marginTop = -12;
            c.coin = new Icon(IconKind.Coin, 38);
            priceRow.Add(c.coin);
            c.gem = new Icon(IconKind.Gem, 36);
            priceRow.Add(c.gem);
            c.lockIcon = new Icon(IconKind.Lock, 30, Color.white);
            priceRow.Add(c.lockIcon);
            c.cost = Sp(L(""), 6);
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
                c.title.text = Upgrades.Name(i).ToUpper(Tr);
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
                c.cost.style.fontSize = tierLocked ? 26 : 34;

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
            hubTrackName.text = TrackLibrary.Names[t].ToUpper(Tr);
            hubTrackSub.text = "Track " + (t + 1) + "  ·  Target: " + TrackLibrary.Show(TrackLibrary.Lengths[t]) + " m";
            hubTrackPct.text = Mathf.FloorToInt(progress * 100f) + "%";
            hubTrackFill.style.width = Length.Percent(Mathf.Max(progress * 100f, 4f));
            var tc = TrackColors[Mathf.Clamp(t, 0, TrackColors.Length - 1)];
            // Başlık ve yükseltme panelleri parkurun koyu tonunda (ormanda koyu yeşil, çölde koyu kum...).
            var dark = Color.Lerp(tc, new Color(0.05f, 0.1f, 0.12f), 0.62f);
            hubTitlePanel.style.backgroundColor = new Color(dark.r, dark.g, dark.b, 0.9f);
            hubTitlePanel.style.borderBottomColor = Color.Lerp(dark, Color.black, 0.35f);
            hubUpgPanel.style.backgroundColor = new Color(dark.r, dark.g, dark.b, 0.9f);
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
            float s = 1f + 0.035f * Mathf.Sin(time * 4f);
            playButton.style.scale = new Scale(new Vector2(s, s));
            // Yıldızlar: düğmenin çevresinde sabit noktalarda sırayla büyüyüp söner, hafifçe döner.
            for (int k = 0; k < playStars.Length; k++)
            {
                float ph = time * 1.6f + k * 0.83f;
                float tw = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(ph)), 2f);
                float ang = k * (360f / playStars.Length) + 20f * Mathf.Sin(time * 0.7f + k);
                float rx = 300f + 26f * Mathf.Sin(k * 2.1f), ry = 118f + 18f * Mathf.Cos(k * 1.7f);
                var st = playStars[k];
                st.style.left = 330f + Mathf.Cos(ang * Mathf.Deg2Rad) * rx - 24f;
                st.style.top = 100f + Mathf.Sin(ang * Mathf.Deg2Rad) * ry - 24f;
                st.style.scale = new Scale(new Vector2(0.35f + 0.85f * tw, 0.35f + 0.85f * tw));
                st.style.opacity = 0.25f + 0.75f * tw;
                st.style.rotate = new Rotate(time * 40f + k * 50f);
            }

            bool chestReady = h.chestSeconds <= 0;
            string chestText = chestReady ? "OPEN!" : (h.chestSeconds / 60) + ":" + (h.chestSeconds % 60).ToString("00");
            if (chestLabel.text != chestText) chestLabel.text = chestText;
            chestLabel.style.color = Color.white;
            float wiggle = chestReady ? 8f * Mathf.Sin(time * 9f) * Mathf.Max(0f, Mathf.Sin(time * 1.6f)) : 0f;
            chestIcon.style.rotate = new Rotate(Angle.Degrees(wiggle));

            for (int ci = 0; ci < cards.Length; ci++)
            {
                var c = cards[ci];
                c.bump = Mathf.MoveTowards(c.bump, 0f, dt * 3f);
                c.shake = Mathf.MoveTowards(c.shake, 0f, dt * 2.5f);
                float k = 1f + 0.12f * Mathf.Sin(c.bump * Mathf.PI);
                c.root.style.scale = new Scale(new Vector2(k, k));
                c.root.style.translate = new Translate(Mathf.Sin(time * 60f) * 14f * c.shake, 0f);
                // Alınabilir kart: butonun üstünden parıltı geçer, buton hafifçe nabız atar.
                float phase = Mathf.Repeat(time * 0.55f + ci * 0.19f, 1f);
                c.shine.style.display = c.canBuy && phase < 0.45f ? DisplayStyle.Flex : DisplayStyle.None;
                c.shine.style.left = Length.Percent(Mathf.Lerp(-30f, 120f, phase / 0.45f));
                float pulse = c.canBuy ? 1f + 0.035f * Mathf.Sin(time * 5f + ci) : 1f;
                c.buy.style.scale = new Scale(new Vector2(pulse, pulse));
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
                reset.text = "Reset upgrades";
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

        void BuildChest()
        {
            chestModal = E("fill", "dim", "center", "pick");
            chestModal.style.display = DisplayStyle.None;

            chestCard = E("card", "card-cream", "col", "pop", "off");
            chestCard.style.width = Length.Percent(84);
            chestCard.style.paddingTop = 0;
            chestCard.style.paddingLeft = chestCard.style.paddingRight = 40;
            chestCard.style.paddingBottom = 40;

            var ribbon = E("ribbon");
            ribbon.style.marginTop = -50;
            ribbon.style.height = 104;
            var title = L("CHEST");
            title.style.fontSize = 56;
            ribbon.Add(title);
            chestCard.Add(ribbon);

            var big = new Icon(IconKind.Chest, 230);
            big.style.marginTop = 20;
            big.name = "chest-big";
            chestCard.Add(big);

            var won = L("YOU WON!", "heavy");
            won.style.fontSize = 44;
            won.style.color = Muted;
            chestCard.Add(won);

            var coinRow = E("row");
            coinRow.Add(new Icon(IconKind.Coin, 90));
            chestCoins = Sp(L("0", "outlined"), 14);
            chestCoins.style.fontSize = 110;
            coinRow.Add(chestCoins);
            chestCard.Add(coinRow);

            chestGemRow = E("row");
            chestGemRow.Add(new Icon(IconKind.Gem, 70));
            chestGems = Sp(L("+1", "outlined"), 12);
            chestGems.style.fontSize = 72;
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
        Label shopNote, shopGems;
        readonly Label[] shopCoinAmounts = new Label[3];
        float shopNoteClock = -1f;

        static readonly int[] GemPacks = { 10, 30, 100, 150, 200, 500 };
        static readonly string[] GemPrices = { "$1.99", "$4.99", "$9.99", "$13.99", "$17.99", "$19.99" };
        static readonly int[] LaunchPacks = { 20, 80, 200 };
        static readonly string[] LaunchPrices = { "$0.99", "$2.49", "$4.99" };
        /// Elmasla coin paketleri (ödeme gerektirmez, hemen çalışır): elmas fiyatı; coin miktarı parkura göre (GameController).
        public static readonly int[] CoinPackGems = { 3, 10, 25 };

        VisualElement Section(string text)
        {
            var l = L(text, "shop-section");
            return l;
        }

        Button PriceButton(string price, Action onClick)
        {
            var b = B(onClick, "btn", "btn-green", "shop-price");
            b.text = price;
            return b;
        }

        void BuildShop()
        {
            shopModal = E("fill", "pick", "shop-page");
            shopModal.style.display = DisplayStyle.None;
            // Arka plan: kullanıcının verdiği karlı manzara (Resources/UI/Shop/shop_bg); yoksa mor-mavi geçiş.
            var shopBg = Resources.Load<Texture2D>("UI/Shop/shop_bg");
            shopModal.style.backgroundImage = new StyleBackground(shopBg != null ? shopBg : VGrad(new Color32(0x5C, 0x7C, 0xF0, 0xFF), new Color32(0x8A, 0x70, 0xE8, 0xFF)));
            if (shopBg != null) shopModal.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;

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
            close.text = "X";
            top.Add(close);
            shopModal.Add(top);

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

            // Coin: elmasla (ödeme gerektirmez; elmasa kullanım alanı)
            shopBody.Add(Section("COINS"));
            var coinRow = E("row", "shop-grid");
            for (int k = 0; k < CoinPackGems.Length; k++)
            {
                int index = k;
                var cell = E("card", "col", "shop-cell");
                shopCoinAmounts[k] = L("", "outlined");
                shopCoinAmounts[k].style.fontSize = 52;
                cell.Add(shopCoinAmounts[k]);
                cell.Add(ShopArt("coins_" + (k + 1), IconKind.Coin, 96 + 14 * k));
                var buy = B(() => ShopBuyCoins?.Invoke(index), "btn", "btn-blue", "shop-price");
                buy.Add(new Icon(IconKind.Gem, 48));
                buy.Add(Sp(L(CoinPackGems[k].ToString()), 8));
                cell.Add(buy);
                coinRow.Add(cell);
            }
            shopBody.Add(coinRow);

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
                cell.Add(PriceButton(Store.Price(id), () => ShopPurchase?.Invoke(id)));
                if (tagAt != null && System.Array.IndexOf(tagAt, k) >= 0 && tagIndex < tags.Length) cell.Add(L(tags[tagIndex++], "shop-tag"));
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
            dealBuy = DealBuyButton(out dealCaption, out dealPriceLabel, () => ShopPurchase?.Invoke(currentDeal.productId));
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
            dealTitle.text = deal.title;
            SetHeadline(deal.headline);
            dealInfo.text = deal.info;
            dealCaption.text = deal.buttonCaption;
            dealPriceLabel.text = Store.Price(deal.productId);   // fiyat mağazadan
            bool hasOld = !string.IsNullOrEmpty(deal.regularId);
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
                float s2 = 1f + 0.03f * Mathf.Sin(time * 5f);
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
            questModal = Modal(out questCard, "DAILY QUESTS", new Color32(0x9B, 0x6B, 0xFF, 0xFF), new Color32(0x6A, 0x3D, 0xD0, 0xFF));
            questTimer = L("", "heavy");
            questTimer.style.fontSize = 32;
            questTimer.style.color = Muted;
            questTimer.style.marginTop = 10;
            questCard.Add(questTimer);
            for (int i = 0; i < DailyQuests.PerDay; i++)
            {
                int index = i;
                var row = E("card", "row");
                row.style.alignSelf = Align.Stretch;
                row.style.marginTop = 18;
                row.style.paddingTop = row.style.paddingBottom = 16;
                row.style.paddingLeft = row.style.paddingRight = 20;
                var col = E("grow");
                questTexts[i] = L("", "heavy");
                questTexts[i].style.fontSize = 34;
                questTexts[i].style.whiteSpace = WhiteSpace.Normal;
                col.Add(questTexts[i]);
                var barRow = E("row");
                barRow.style.marginTop = 8;
                var bar = E("bar", "grow");
                bar.style.height = 30;
                questFills[i] = E("bar-fill");
                bar.Add(questFills[i]);
                barRow.Add(bar);
                questCounts[i] = L("", "heavy");
                questCounts[i].style.fontSize = 28;
                questCounts[i].style.marginLeft = 12;
                questCounts[i].style.minWidth = 110;
                barRow.Add(questCounts[i]);
                col.Add(barRow);
                questRewards[i] = L("", "heavy");
                questRewards[i].style.fontSize = 30;
                questRewards[i].style.color = (Color)new Color32(0xD8, 0x8A, 0x0A, 0xFF);
                questRewards[i].style.marginTop = 4;
                col.Add(questRewards[i]);
                row.Add(col);
                questClaims[i] = B(() => ClaimQuest?.Invoke(index), "btn", "btn-green", "btn-mid");
                questClaims[i].text = "CLAIM";
                questClaims[i].style.width = 210;
                questClaims[i].style.fontSize = 40;
                questClaims[i].style.marginLeft = 16;
                row.Add(questClaims[i]);
                questRows[i] = row;
                questCard.Add(row);
            }
            var close = B(() => Close(questModal, questCard), "btn", "btn-white", "btn-mid");
            close.text = "CLOSE";
            close.style.alignSelf = Align.Stretch;
            close.style.marginTop = 26;
            questCard.Add(close);
            safe.Add(questModal);
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
                questFills[i].style.backgroundColor = done ? (Color)new Color32(0x3F, 0xD1, 0x5B, 0xFF) : (Color)new Color32(0xFF, 0xB0, 0x20, 0xFF);
                questRewards[i].text = "+" + q.coins + " coins" + (q.gems > 0 ? "  ·  +" + q.gems + " gems" : "");
                questClaims[i].SetEnabled(done && !got);
                questClaims[i].text = got ? "CLAIMED" : "CLAIM";
                questClaims[i].style.opacity = done && !got ? 1f : 0.45f;
                questRows[i].style.opacity = got ? 0.6f : 1f;
                if (done && !got) any = true;
            }
            questDot.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            if (open) Open(questModal, questCard);
        }

        void BuildLogin()
        {
            loginModal = Modal(out loginCard, "DAILY REWARD", new Color32(0xFF, 0x8F, 0x1F, 0xFF), new Color32(0xD8, 0x62, 0x0A, 0xFF));
            var sub = L("Come back every day for bigger rewards! Day 7 is the big one.", "heavy");
            sub.style.fontSize = 32;
            sub.style.color = Muted;
            sub.style.marginTop = 10;
            sub.style.whiteSpace = WhiteSpace.Normal;
            sub.style.unityTextAlign = TextAnchor.MiddleCenter;
            loginCard.Add(sub);
            var grid = E("row");
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.Center;
            grid.style.marginTop = 10;
            for (int d = 0; d < 7; d++)
            {
                var cell = E("card", "col");
                cell.style.width = d == 6 ? 300 : 142;
                cell.style.height = 196;
                cell.style.overflow = Overflow.Hidden;
                cell.style.justifyContent = Justify.FlexStart;
                cell.style.marginLeft = cell.style.marginRight = 6;
                cell.style.marginTop = 12;
                cell.style.paddingTop = 6;
                cell.style.paddingLeft = cell.style.paddingRight = 4;
                cell.style.paddingBottom = 8;
                cell.style.alignItems = Align.Center;
                var day = L("DAY " + (d + 1), "heavy");
                day.style.fontSize = 24;
                day.style.flexShrink = 0;
                day.style.marginTop = 4;
                cell.Add(day);
                var icon = new Icon(d == 6 ? IconKind.Chest : IconKind.Coin, d == 6 ? 56 : 44);
                icon.style.flexShrink = 0;
                icon.style.marginTop = 4;
                icon.style.marginBottom = 4;
                cell.Add(icon);
                loginAmounts[d] = L("", "heavy");
                loginAmounts[d].style.fontSize = 24;
                loginAmounts[d].style.flexShrink = 0;
                loginAmounts[d].style.whiteSpace = WhiteSpace.Normal;
                loginAmounts[d].style.unityTextAlign = TextAnchor.MiddleCenter;
                cell.Add(loginAmounts[d]);
                loginDays[d] = cell;
                grid.Add(cell);
            }
            loginCard.Add(grid);
            var claim = B(() => { ClaimLogin?.Invoke(); Close(loginModal, loginCard); }, "btn", "btn-green", "btn-big");
            claim.text = "CLAIM REWARD";
            claim.style.alignSelf = Align.Stretch;
            claim.style.marginTop = 28;
            loginCard.Add(claim);
            safe.Add(loginModal);
        }

        /// day: bugün alınacak gün (1..7); coins/gems: 7 günün ödülleri.
        public void ShowLogin(int day, int[] coins, int[] gems)
        {
            for (int d = 0; d < 7; d++)
            {
                loginAmounts[d].text = coins[d] + (gems[d] > 0 ? "\n+" + gems[d] + " gems" : "");
                bool past = d < day - 1, today = d == day - 1;
                loginDays[d].style.opacity = past ? 0.45f : 1f;
                loginDays[d].style.backgroundColor = today ? (Color)new Color32(0xFF, 0xE0, 0x8A, 0xFF) : Color.white;
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
            back.Add(new Icon(IconKind.Back, 58, Ink));
            top.Add(back);
            var title = L("HEROES", "outlined");
            title.style.fontSize = 72;
            top.Add(title);
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

        void BuildMap()
        {
            var p = NewPage(Page.Map);

            var top = E("topbar");
            var back = B(() => OpenHub?.Invoke(), "btn", "btn-white", "btn-round");
            back.Add(new Icon(IconKind.Back, 58, Ink));
            top.Add(back);
            var title = L("TRACKS", "outlined");
            title.style.fontSize = 80;
            top.Add(title);
            top.Add(CoinPill());
            p.Add(top);

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
                var c = new MapCard { root = B(() => SelectTrack?.Invoke(index), "btn", "btn-white", "map-card") };
                c.root.style.flexShrink = 0;
                c.badge = E("map-badge");
                c.badge.style.backgroundColor = TrackColors[i];
                c.number = L((i + 1).ToString(), "outlined");
                c.number.style.fontSize = 72;
                c.badge.Add(c.number);
                c.lockIcon = new Icon(IconKind.Lock, 64, Color.white);
                c.badge.Add(c.lockIcon);
                c.root.Add(c.badge);

                var info = E("grow");
                info.style.marginLeft = 24;
                var name = L(TrackLibrary.Names[i] + "  ·  " + TrackLibrary.Show(TrackLibrary.Lengths[i]) + " m", "heavy");
                name.style.fontSize = 50;
                name.style.marginBottom = -6;
                info.Add(name);
                c.status = L("", "heavy");
                c.status.style.fontSize = 32;
                info.Add(c.status);
                var slim = E("bar");
                slim.style.height = 24;
                slim.style.borderTopWidth = slim.style.borderBottomWidth = slim.style.borderLeftWidth = slim.style.borderRightWidth = 3;
                slim.style.marginTop = 6;
                slim.style.backgroundColor = (Color)new Color32(0xED, 0xE6, 0xDC, 0xFF);
                c.fill = E("bar-fill");
                slim.Add(c.fill);
                info.Add(slim);
                c.root.Add(info);

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
                c.number.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                c.lockIcon.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
                c.badge.style.backgroundColor = open ? TrackColors[i] : (Color)new Color32(0xB8, 0xB0, 0xA6, 0xFF);
                c.root.EnableInClassList("map-current", i == save.track);
                c.root.style.opacity = open ? 1f : 0.75f;
                c.fill.style.width = Length.Percent(progress * 100f);
                c.fill.style.backgroundColor = Col(done, new Color32(0x3F, 0xD1, 0x5B, 0xFF));
                if (done)
                {
                    c.status.text = "Complete! · Best " + TrackLibrary.Show(save.bests[i]) + " m";
                    c.status.style.color = (Color)new Color32(0x22, 0xA0, 0x3C, 0xFF);
                }
                else if (open)
                {
                    c.status.text = save.bests[i] > 0 ? "Best " + TrackLibrary.Show(save.bests[i]) + " m · " + Mathf.FloorToInt(progress * 100f) + "%" : "New! Try it now";
                    c.status.style.color = (Color)new Color32(0xD8, 0x62, 0x0A, 0xFF);
                }
                else
                {
                    c.status.text = "Locked · finish " + TrackLibrary.Names[i - 1] + " first";
                    c.status.style.color = Muted;
                }
            }
            SetPage(Page.Map);
            // Yerleşim hesaplanınca güncel parkuru görünür yap
            var cur = mapCards[Mathf.Clamp(save.track, 0, TrackLibrary.Count - 1)].root;
            mapScroll.schedule.Execute(() => mapScroll.ScrollTo(cur)).StartingIn(30);
        }

        // ================================================================ nişan

        void BuildAim()
        {
            var p = NewPage(Page.Aim);

            var top = E("topbar");
            top.Add(BestPill());
            top.Add(CoinPill());
            p.Add(top);

            hint = E("card", "col");
            hint.style.alignSelf = Align.Center;
            hint.style.marginTop = 90;
            hint.style.width = Length.Percent(84);
            hint.style.paddingTop = 18;
            var handBox = E();
            handBox.style.height = 150;
            hintHand = new Icon(IconKind.Hand, 120, new Color32(0xFF, 0xB8, 0x6B, 0xFF));
            handBox.Add(hintHand);
            hint.Add(handBox);
            var hl = L("Pull back and release!", "heavy");
            hl.style.fontSize = 58;
            hint.Add(hl);
            hintSub = L("", "muted");
            hintSub.style.fontSize = 32;
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
                : "Pull sideways to launch the other way.\nSpace + ←/→ also works · Cup: I J K L";
            if (hintSub.text != text) hintSub.text = text;

            bool pulling = h.pull > 0.01f;
            hint.style.opacity = Mathf.MoveTowards(hint.resolvedStyle.opacity, pulling ? 0f : 1f, 0.15f);
            float cycle = (time % 1.4f) / 1.4f;
            float down = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(cycle / 0.7f));
            hintHand.style.translate = new Translate(0f, 70f * down - 10f);
            hintHand.style.opacity = cycle < 0.85f ? 1f : 1f - (cycle - 0.85f) / 0.15f;

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
            airLabel.style.fontSize = 96;
            airLabel.style.color = Gold;
            airLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            airLabel.style.marginTop = 40;
            p.Add(airLabel);

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
            var boost = L("BOOST", "rocket-label");
            boost.pickingMode = PickingMode.Ignore;
            rocketBtn.Add(boost);
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
                airClock += dt;
                float t = Mathf.Clamp01(airClock / 0.35f);
                float s = EaseOutBack(t) * (1f + 0.04f * Mathf.Sin(time * 9f));
                airLabel.style.scale = new Scale(new Vector2(s, s));
                airLabel.style.rotate = new Rotate(Angle.Degrees(-4f + 3f * Mathf.Sin(time * 5f)));
            }
            else airLabel.style.display = DisplayStyle.None;

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

        void UpdateGlass(in HudInfo h)
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

            milkPill.style.left = left - b;
            milkPill.style.width = width + 2f * b;
            milkPill.style.top = top - b - 84f;

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
            milkDangerShown = Mathf.MoveTowards(milkDangerShown, Mathf.InverseLerp(0.55f, 0.95f, h.milkDanger), Time.deltaTime * 6f);
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
            unlockCol.style.marginLeft = 18;
            unlockTitle = L("", "heavy");
            unlockTitle.style.fontSize = 34;
            unlockTitle.style.color = (Color)new Color32(0xD8, 0x62, 0x0A, 0xFF);
            unlockTitle.style.marginBottom = -8;
            unlockCol.Add(unlockTitle);
            unlockName = L("", "heavy");
            unlockName.style.fontSize = 48;
            unlockCol.Add(unlockName);
            unlockBox.Add(unlockCol);
            resultCard.Add(unlockBox);

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
