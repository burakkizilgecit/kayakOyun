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
    }

    /// UI Toolkit ile koddan kurulan oyun arayüzü: Ana ekran (yükseltmeler) → Nişan → Koşu → Sonuç → Ana ekran.
    /// Oyun mantığı içermez; düğmeler olay (event) yayınlar, GameController dinler.
    public class GameUI
    {
        public enum Page { Hub, Map, Avatars, Aim, Run, Result }

        public event Action OpenHub, OpenMap, NextTrack, Retry, FireRocket, OpenChest, OpenAvatars;
        public event Action<int> BrowseAvatar, AvatarAction;
        public event Action<int> ClaimChest;   // çarpan: 1 ya da 3 (reklamla)
        public event Action<int> Buy, SetHand, SelectTrack;
        public event Action<bool> SetSound;
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
        static readonly System.Globalization.CultureInfo Tr = new System.Globalization.CultureInfo("tr-TR");
        static readonly Color[] TrackColors =
        {
            new Color32(0x5C, 0xC4, 0x5A, 0xFF), new Color32(0x2E, 0x8B, 0x57, 0xFF), new Color32(0xE8, 0x74, 0x3B, 0xFF),
            new Color32(0x4F, 0x9F, 0xE0, 0xFF),
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
        Button chestTriple;
        int chestCoinValue, chestGemValue, chestMultiplier = 1;
        float chestClock, adClock = -1f;
        Action adDone;

        // Sonuç: toplananlar
        Label resLoot;

        // Karakterler
        VisualElement hubAvatarPortrait;
        Label avName, avPerkTitle, avPerkText, avActionLabel;
        Button avAction;
        Icon avActionGem;
        readonly AvatarCard[] avCards = new AvatarCard[AvatarLibrary.Count];
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
        static readonly string[] RarityNames = { "ÜCRETSİZ", "NADİR", "EPİK", "EFSANE" };
        static int Rarity(int price) => price <= 0 ? 0 : price < 20 ? 1 : price < 30 ? 2 : 3;

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
        VisualElement hubTrackFill, settings, settingsCard;
        Button handLeft, handRight, soundOn, soundOff;

        // Harita
        readonly MapCard[] mapCards = new MapCard[TrackLibrary.Count];

        // Yükseltme kartları
        readonly UpgradeCard[] cards = new UpgradeCard[Upgrades.Count];

        // Roket butonu (koşu)
        Button rocketBtn;
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
            public Label badge, sub, cost, level;
            public bool canBuy;
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
                bestLabel = new Label("REKOR");
                bestLabel.AddToClassList("vbar-label");
                bestLabel.AddToClassList("outlined");
                bestMark.Add(bestLabel);
                root.Add(bestMark);

                knob = new VisualElement();
                knob.AddToClassList("vbar-knob");
                percent = new Label("%0");
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
                string text = "%" + Mathf.FloorToInt(progress * 100f);
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
            resTitle.text = summary.title.ToUpper(new System.Globalization.CultureInfo("tr-TR"));
            resRecord.style.display = summary.record ? DisplayStyle.Flex : DisplayStyle.None;
            resBonus.style.display = summary.finished ? DisplayStyle.Flex : DisplayStyle.None;
            resBonus.text = "Bitiş ödülü: +" + summary.finishBonus;
            resLoot.text = "Toplanan: " + summary.coins + " coin" + (summary.gems > 0 ? "  ·  " + summary.gems + " pembe elmas" : "");
            resLoot.style.display = summary.coins > 0 || summary.gems > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            resMilk.text = "%" + Mathf.RoundToInt(Mathf.Clamp01(summary.fill) * 100f);
            resDist.text = "0 m";
            resCoins.text = "+0";
            resBar.Set(0f, summary.bestProgress);

            bool finished = summary.finished;
            resRibbon.style.backgroundColor = Col(finished, new Color32(0x3F, 0xD1, 0x5B, 0xFF));
            resRibbon.style.borderBottomColor = Col(finished, new Color32(0x22, 0xA0, 0x3C, 0xFF));
            confetti.style.display = finished ? DisplayStyle.Flex : DisplayStyle.None;

            goNext = summary.unlockedName != null;
            goLabel.text = goNext ? summary.unlockedName.ToUpper(new System.Globalization.CultureInfo("tr-TR")) + "'A GEÇ" : "DEVAM";
            unlockBox.style.display = goNext || summary.lastTrack ? DisplayStyle.Flex : DisplayStyle.None;
            if (goNext)
            {
                unlockTitle.text = "YENİ PARKUR AÇILDI!";
                unlockName.text = summary.unlockedName + " · " + summary.unlockedLength + " m";
            }
            else if (summary.lastTrack)
            {
                unlockTitle.text = "BÜTÜN PARKURLAR BİTTİ!";
                unlockName.text = "Şampiyon sensin!";
            }
            resultCard.AddToClassList("off");
            resultCard.schedule.Execute(() => resultCard.RemoveFromClassList("off")).StartingIn(140);
            SetPage(Page.Result);
        }

        /// Parkur yüklenince: elmasların yerleri ilerleme çubuğuna işlenir.
        public void SetRunGems(IList<float> fractions) => runColumn.SetGems(fractions);

        /// Koşuda kapak açılınca kısa bir kutlama yazısı.
        public void LidOpened()
        {
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
                foreach (var l in bestLabels) l.text = "En iyi " + h.best + " m";
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
            hubTrackPct = L("%0", "hub-bar-pct");
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
            hubAvatarPortrait.style.width = 104;
            hubAvatarPortrait.style.height = 104;
            avatarBtn.Add(hubAvatarPortrait);
            avatarBtn.Add(L("KARAKTER", "side-label"));
            leftSide.Add(avatarBtn);
            chestBtn = SideCard(() => OpenChest?.Invoke(), new Color32(0xFF, 0xB0, 0x3A, 0xFF), new Color32(0xD8, 0x6C, 0x0A, 0xFF));
            chestBtn.style.alignSelf = Align.FlexStart;
            chestIcon = new Icon(IconKind.Chest, 92);
            chestBtn.Add(chestIcon);
            chestLabel = L("AÇ!", "side-label");
            chestBtn.Add(chestLabel);
            leftSide.Add(chestBtn);
            mid.Add(leftSide);
            var side = E("col");
            var map = SideCard(() => OpenMap?.Invoke(), new Color32(0x3B, 0xD0, 0xB8, 0xFF), new Color32(0x1A, 0x93, 0x80, 0xFF));
            map.Add(new Icon(IconKind.Map, 84, Color.white));
            map.Add(L("HARİTA", "side-label"));
            side.Add(map);
            var rocket = BuildUpgradeCard((int)UpgradeType.Rocket);
            rocket.root.style.width = 214;
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
            playPill = E("play-pill");
            playPill.pickingMode = PickingMode.Ignore;
            playHand = new Icon(IconKind.Hand, 76, new Color32(0xFF, 0xD3, 0x4D, 0xFF));
            playPill.Add(playHand);
            playHint = L("OYNAMAK İÇİN DOKUN", "play-text");
            playPill.Add(playHint);
            p.Add(playPill);

            var row = E("row");
            row.style.alignItems = Align.Stretch;
            for (int i = 0; i < (int)UpgradeType.Rocket; i++)
            {
                var c = BuildUpgradeCard(i);
                c.root.style.flexGrow = 1;
                c.root.style.flexBasis = 0;
                if (i > 0) c.root.style.marginLeft = 12;
                cards[i] = c;
                row.Add(c.root);
            }
            p.Add(row);
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

            var top = c.top = E("pcard-top");
            top.style.backgroundImage = new StyleBackground(VGrad(Color.Lerp(main, Color.white, 0.55f), main));
            var glow = E("pcard-glow");
            glow.style.backgroundImage = new StyleBackground(Radial());
            top.Add(glow);
            c.preview = E("pcard-preview");
            top.Add(c.preview);

            var lvl = E("pcard-lvl");
            lvl.style.backgroundColor = dark;
            var lvlCap = L("SV", "pcard-lvl-cap");
            lvl.Add(lvlCap);
            c.level = L("1", "pcard-lvl-num");
            lvl.Add(c.level);
            top.Add(lvl);
            c.badge = L("0/20", "pcard-count");
            top.Add(c.badge);

            var ribbon = E("pcard-name");
            var name = L(Upgrades.Names[i].ToUpper(Tr), "outlined");
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
            c.coin = new Icon(IconKind.Coin, 42);
            c.buy.Add(c.coin);
            c.gem = new Icon(IconKind.Gem, 40);
            c.buy.Add(c.gem);
            c.lockIcon = new Icon(IconKind.Lock, 34, Color.white);
            c.buy.Add(c.lockIcon);
            c.cost = Sp(L(""), 6);
            c.buy.Add(c.cost);
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
        public void RefreshHub(SaveData save, int bought)
        {
            int tier = save.unlocked;
            for (int i = 0; i < Upgrades.Count; i++)
            {
                var c = cards[i];
                int lvl = save.levels[i], cap = Upgrades.Cap(i, tier), max = Upgrades.MaxLevel(i);
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
                if (maxed) c.cost.text = "MAKS";
                else if (tierLocked) c.cost.text = TrackLibrary.Names[Mathf.Max(0, Upgrades.UnlockTier(i, lvl + 1))].ToUpper(Tr);
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

            int t = save.track;
            float progress = Mathf.Clamp01(save.bests[t] / TrackLibrary.Lengths[t]);
            hubTrackName.text = TrackLibrary.Names[t].ToUpper(Tr);
            hubTrackSub.text = (t + 1) + ". PARKUR  ·  " + Mathf.RoundToInt(TrackLibrary.Lengths[t]) + " m";
            hubTrackPct.text = "%" + Mathf.FloorToInt(progress * 100f);
            hubTrackFill.style.width = Length.Percent(Mathf.Max(progress * 100f, 4f));
            var tc = TrackColors[Mathf.Clamp(t, 0, TrackColors.Length - 1)];
            hubRibbon.style.backgroundImage = new StyleBackground(VGrad(Color.Lerp(tc, Color.white, 0.35f), tc));
            hubRibbon.style.borderBottomColor = Color.Lerp(tc, Color.black, 0.3f);
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
            float s = 1f + 0.04f * Mathf.Sin(time * 4f);
            playPill.style.scale = new Scale(new Vector2(s, s));
            // El dokunur gibi aşağı yukarı zıplar
            float tap = Mathf.Abs(Mathf.Sin(time * 3.2f));
            playHand.style.translate = new Translate(0, -14f * tap);
            playHand.style.rotate = new Rotate(-12f + 10f * tap);

            bool chestReady = h.chestSeconds <= 0;
            string chestText = chestReady ? "AÇ!" : (h.chestSeconds / 60) + ":" + (h.chestSeconds % 60).ToString("00");
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
        }

        void BuildSettings()
        {
            settings = E("fill", "dim", "center", "pick");
            settings.style.display = DisplayStyle.None;

            settingsCard = E("card", "card-cream", "pop", "off");
            settingsCard.style.width = Length.Percent(86);
            settingsCard.style.paddingLeft = settingsCard.style.paddingRight = 44;
            settingsCard.style.paddingBottom = 44;

            var title = L("AYARLAR", "outlined");
            title.style.fontSize = 84;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = 10;
            settingsCard.Add(title);

            settingsCard.Add(SettingLabel("Bardak hangi elinde?"));
            var hand = E("seg");
            handLeft = Seg("SOL EL", () => SetHand?.Invoke(-1));
            handRight = Seg("SAĞ EL", () => SetHand?.Invoke(1));
            hand.Add(handLeft);
            hand.Add(handRight);
            settingsCard.Add(hand);

            settingsCard.Add(SettingLabel("Ses"));
            var sound = E("seg");
            soundOn = Seg("AÇIK", () => SetSound?.Invoke(true));
            soundOff = Seg("KAPALI", () => SetSound?.Invoke(false));
            sound.Add(soundOn);
            sound.Add(soundOff);
            settingsCard.Add(sound);

            var ok = B(() => ShowSettings(false), "btn", "btn-green", "btn-mid");
            ok.text = "TAMAM";
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
            var title = L("SANDIK");
            title.style.fontSize = 56;
            ribbon.Add(title);
            chestCard.Add(ribbon);

            var big = new Icon(IconKind.Chest, 230);
            big.style.marginTop = 20;
            big.name = "chest-big";
            chestCard.Add(big);

            var won = L("KAZANDIN!", "heavy");
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
            chestTriple.Add(Sp(L("3 KATINI AL"), 16));
            chestCard.Add(chestTriple);

            var go = B(() =>
            {
                ClaimChest?.Invoke(chestMultiplier);
                chestCard.AddToClassList("off");
                chestModal.schedule.Execute(() => chestModal.style.display = DisplayStyle.None).StartingIn(180);
            }, "btn", "btn-green", "btn-mid");
            go.text = "DEVAM";
            go.style.alignSelf = Align.Stretch;
            go.style.marginTop = 22;
            chestCard.Add(go);

            chestModal.Add(chestCard);
            safe.Add(chestModal);

            // Test reklamı: gerçek reklam ağı bağlanana kadar ödüllü reklamın yerini tutar.
            adOverlay = E("fill", "center", "pick");
            adOverlay.style.backgroundColor = new Color(0.05f, 0.04f, 0.08f, 0.94f);
            adOverlay.style.display = DisplayStyle.None;
            var adTitle = L("REKLAM", "outlined");
            adTitle.style.fontSize = 90;
            adOverlay.Add(adTitle);
            var adSub = L("(test modu: gerçek reklam yayına yakın bağlanacak)", "heavy");
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
            var title = L("KARAKTERLER", "outlined");
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
            var perkCap = L("AVANTAJ", "av-perk-cap");
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

            for (int r = 0; r < 3; r++)
            {
                var row = E("row");
                row.style.alignItems = Align.Stretch;
                row.style.marginTop = r > 0 ? 14 : 0;
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
                p.Add(row);
            }
        }

        public void ShowAvatars(SaveData save, int browse)
        {
            RefreshAvatars(save, browse);
            SetPage(Page.Avatars);
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
            avActionLabel.text = selected ? "SEÇİLİ" : owned ? "SEÇ" : info.price + "  SATIN AL";
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
                c.state.text = sel ? "SEÇİLİ" : "SENİN";
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
            var title = L("PARKURLAR", "outlined");
            title.style.fontSize = 80;
            top.Add(title);
            top.Add(CoinPill());
            p.Add(top);

            var list = E("grow");
            list.style.justifyContent = Justify.Center;
            for (int i = 0; i < TrackLibrary.Count; i++)
            {
                if (i > 0)
                {
                    // Kartları birleştiren noktalı yol
                    var dots = E("col");
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
                var name = L(TrackLibrary.Names[i] + "  ·  " + TrackLibrary.Lengths[i] + " m", "heavy");
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
            p.Add(list);
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
                    c.status.text = "Tamamlandı! · En iyi " + save.bests[i] + " m";
                    c.status.style.color = (Color)new Color32(0x22, 0xA0, 0x3C, 0xFF);
                }
                else if (open)
                {
                    c.status.text = save.bests[i] > 0 ? "En iyi " + save.bests[i] + " m · %" + Mathf.FloorToInt(progress * 100f) : "Yeni! Hemen dene";
                    c.status.style.color = (Color)new Color32(0xD8, 0x62, 0x0A, 0xFF);
                }
                else
                {
                    c.status.text = "Kilitli · önce " + TrackLibrary.Names[i - 1] + " parkurunu bitir";
                    c.status.style.color = Muted;
                }
            }
            SetPage(Page.Map);
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
            var hl = L("Geri çek ve bırak!", "heavy");
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
            powerLabel = L("GÜÇ %0", "outlined");
            powerLabel.style.fontSize = 72;
            power.Add(powerLabel);
            powerBar = new TrackBar();
            powerBar.root.style.width = Length.Percent(100);
            power.Add(powerBar.root);
            p.Add(power);

            p.Add(E("grow"));
            var back = B(() => OpenHub?.Invoke(), "btn", "btn-blue", "btn-mid");
            back.text = "GERİ";
            back.style.width = 340;
            back.style.alignSelf = Align.Center;
            back.style.marginBottom = 20;
            p.Add(back);
        }

        void TickAim(in HudInfo h)
        {
            string text = h.tiltMode
                ? "Yana çekersen ters yöne fırlarsın.\nBardak: telefonu eğerek dengede tut."
                : "Yana çekersen ters yöne fırlarsın.\nBoşluk + ←/→ da olur · Bardak: I J K L";
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
                powerLabel.text = "GÜÇ %" + Mathf.RoundToInt(h.pull * 100f);
                powerBar.Set(h.pull, 0f);
                powerBar.fill.style.backgroundColor = Color.Lerp(new Color32(0xFF, 0xC9, 0x3C, 0xFF), new Color32(0xFF, 0x4D, 0x3D, 0xFF), h.pull);
                float s = 1f + 0.04f * h.pull * Mathf.Sin(time * 40f);
                powerLabel.style.scale = new Scale(new Vector2(s, s));
            }
        }

        // ================================================================ koşu

        void BuildRun()
        {
            var p = NewPage(Page.Run);

            runColumn = new ProgressColumn();
            p.Add(runColumn.root);

            distLabel = L("0 m", "outlined");
            distLabel.style.fontSize = 140;
            distLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            distLabel.style.marginTop = -6;
            p.Add(distLabel);


            // Toplanan coin/elmas (sol üstte, ilerleme çubuğunun altında)
            runLoot = E("row");
            runLoot.style.position = Position.Absolute;
            runLoot.style.left = 36;
            runLoot.style.top = 230;
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
            p.Add(runLoot);

            airLabel = L("HAVADA!", "outlined");
            airLabel.style.fontSize = 96;
            airLabel.style.color = Gold;
            airLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            airLabel.style.marginTop = 40;
            p.Add(airLabel);

            toastLabel = L("KAPAK AÇILDI!", "outlined");
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
            var unit = L("km/sa", "gauge-unit");
            gaugeBox.Add(unit);
            rocketBtn = B(() => FireRocket?.Invoke(), "btn", "rocket-btn");
            rocketFill = E("rocket-fill");
            rocketBtn.Add(rocketFill);
            rocketBtn.Add(new Icon(IconKind.Flame, 92));
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
                distLabel.text = h.distance + " m";
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
                airLabel.text = h.canGlide ? "SÜZÜL!" : "HAVADA!";
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
            runGemBox.style.display = h.runGems > 0 ? DisplayStyle.Flex : DisplayStyle.None;
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
                rocketBtn.style.opacity = h.rocketReady || firing ? 1f : 0.45f;
                rocketCount.text = h.rocketCharges > 1 ? "x" + h.rocketCharges : "";
                float pulse = h.rocketReady ? 1f + 0.05f * Mathf.Sin(time * 6f) : 1f;
                rocketBtn.style.scale = new Scale(new Vector2(pulse, pulse));
            }

            bool tut = h.runTime < 3.5f && !debugHideTutorial;
            tutorial.style.display = tut ? DisplayStyle.Flex : DisplayStyle.None;
            if (tut && tutorial.childCount == 0)
            {
                tutorial.Add(TutorialRow(IconKind.Swap, h.tiltMode ? "Sağa-sola sürükle: yön" : "A / D: yön · W / S: burun"));
                tutorial.Add(TutorialRow(IconKind.Glass, h.tiltMode ? "Telefonu eğ: bardak dengesi" : "I J K L: bardak dengesi"));
                if (h.hasRocket)
                    tutorial.Add(TutorialRow(IconKind.Rocket, h.tiltMode ? "Roket butonu: kısa itiş" : "R: roketi ateşle"));
                tutorial.Query<VisualElement>().ForEach(e => e.pickingMode = PickingMode.Ignore);
            }
            if (tut) tutorial.style.opacity = Mathf.Clamp01((3.5f - h.runTime) / 0.4f);
        }

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
            milkLabel = L("SÜT %100");
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
            string text = lid ? "KAPAK " + Mathf.CeilToInt(h.lidLeft) + " m" : "SÜT %" + pct;
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
            float s = fill < 0.3f ? 1f + 0.06f * Mathf.Abs(Mathf.Sin(time * 7f)) : 1f;
            milkPill.style.scale = new Scale(new Vector2(s, s));
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
            resRecord.Add(L("YENİ REKOR!"));

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
            stats.Add(Stat(IconKind.Glass, "Kalan süt", out resMilk));
            stats.Add(Sp(Stat(IconKind.Coin, "Kazanç", out resCoins), 22));
            resultCard.Add(stats);

            resLoot = L("", "heavy");
            resLoot.style.fontSize = 36;
            resLoot.style.marginTop = 14;
            resLoot.style.color = Muted;
            resultCard.Add(resLoot);

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

            var go = B(() =>
            {
                if (goNext) NextTrack?.Invoke();
                else OpenHub?.Invoke();
            }, "btn", "btn-green", "btn-big");
            go.style.alignSelf = Align.Stretch;
            go.style.marginTop = 36;
            goIcon = new Icon(IconKind.Play, 60);
            go.Add(goIcon);
            goLabel = Sp(L("DEVAM"), 16);
            go.Add(goLabel);
            resultCard.Add(go);

            var retry = B(() => Retry?.Invoke(), "btn", "btn-white", "btn-mid");
            retry.style.alignSelf = Align.Stretch;
            retry.style.marginTop = 22;
            retry.Add(new Icon(IconKind.Play, 50, Ink));
            retry.Add(Sp(L("Hemen tekrar fırlat"), 12));
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
            resDist.text = Mathf.RoundToInt(result.distance * e) + " m";
            resBar.Set(result.progress * e, result.bestProgress);
            float tc = Mathf.Clamp01((resultClock - 0.6f) / 0.8f);
            resCoins.text = "+" + Mathf.RoundToInt(result.earned * (1f - (1f - tc) * (1f - tc)));

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
            if (h.launches != shownLaunches)
            {
                shownLaunches = h.launches;
                foreach (var l in launchLabels) l.text = h.launches + "/" + SaveData.MaxLaunches;
                if (launchModalCount != null) launchModalCount.text = h.launches + "/" + SaveData.MaxLaunches;
                // Hak geldiyse pencere kendiliğinden kapanır.
                if (h.launches > 0 && launchModal != null && launchModal.style.display.value == DisplayStyle.Flex) HideNoLaunches();
            }
            if (h.launchSeconds != shownLaunchSeconds)
            {
                shownLaunchSeconds = h.launchSeconds;
                string t = h.launchSeconds > 0 ? Clock(h.launchSeconds) : "";
                foreach (var l in launchTimers)
                {
                    l.text = t;
                    l.style.display = h.launchSeconds > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                }
                if (launchModalTimer != null) launchModalTimer.text = "Yeni hak: " + (h.launchSeconds > 0 ? Clock(h.launchSeconds) : "hazır");
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
            var title = L("FIRLATMA HAKKI");
            title.style.fontSize = 50;
            ribbon.Add(title);
            launchCard.Add(ribbon);

            var big = new Icon(IconKind.Slingshot, 200);
            big.style.marginTop = 24;
            launchCard.Add(big);
            launchModalCount = L("0/" + SaveData.MaxLaunches, "outlined");
            launchModalCount.style.fontSize = 96;
            launchCard.Add(launchModalCount);
            var info = L("Hakların bitti! Her 25 dakikada bir hak dolar.", "heavy");
            info.style.fontSize = 34;
            info.style.color = Muted;
            info.style.whiteSpace = WhiteSpace.Normal;
            info.style.unityTextAlign = TextAnchor.MiddleCenter;
            launchCard.Add(info);
            launchModalTimer = L("Yeni hak: 25:00", "heavy");
            launchModalTimer.style.fontSize = 44;
            launchModalTimer.style.marginTop = 10;
            launchCard.Add(launchModalTimer);

            var watch = B(() => ShowAd(() => { WatchLaunchAd?.Invoke(); HideNoLaunches(); }), "btn", "btn-orange", "btn-big");
            watch.style.alignSelf = Align.Stretch;
            watch.style.marginTop = 30;
            watch.Add(new Icon(IconKind.Video, 70));
            watch.Add(Sp(L("İZLE · +1 HAK"), 16));
            launchCard.Add(watch);

            var ok = B(HideNoLaunches, "btn", "btn-green", "btn-mid");
            ok.text = "TAMAM";
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
            var l = L("En iyi 0 m");
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
