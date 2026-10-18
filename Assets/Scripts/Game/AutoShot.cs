using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace SledSurfers
{
    /// Otomatik görsel test: oyun "-autoshot <klasör>" ile başlatılırsa kendi kendine fırlatır,
    /// ekran görüntüleri alır ve kapanır. Normal oyunda hiçbir etkisi yoktur.
    public class AutoShot : MonoBehaviour
    {
        GameController game;
        string folder;
        string mode = "";   // "-shots avatars": yalnızca karakter çekimleri (hızlı deneme)
        bool homeTesting, homeFailed;
        float homeDeadline;
        readonly System.Text.StringBuilder snowReport = new System.Text.StringBuilder();

        /// Oyun otomatik test için mi başlatıldı ("-autoshot <klasör>")? Kayıt yüklenmeden önce sorulur.
        public static bool Requested => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autoshot") >= 0;

        public static void AttachIfRequested(GameController game)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-autoshot") continue;
                var shot = game.gameObject.AddComponent<AutoShot>();
                shot.game = game;
                game.debugUnlimitedLaunches = true;
                Application.runInBackground = true;   // pencere odakta değilken de sürsün (kullanıcı bilgisayarı kullanırken test duruyordu)
                // Kullanıcı isteği: otomatik test oyunları kısık sesle oynar.
                SoundFx.MaxVolume = 0.1f;
                AudioListener.volume = Mathf.Min(AudioListener.volume, SoundFx.MaxVolume);
                shot.folder = args[i + 1];
                Directory.CreateDirectory(shot.folder);
                for (int j = 0; j < args.Length - 1; j++)
                    if (args[j] == "-shots") shot.mode = args[j + 1];
                return;
            }
        }

        IEnumerator Start()
        {
            SaveData.ClearAll();   // yalnız test önekli kayıt (gerçek kayıt korunur)
            float t;
            if (mode == "equipment")
            {
                homeTesting = true; homeDeadline = Time.realtimeSinceStartup + 150f;
                Application.logMessageReceived += HomeLogFailure;
                EquipmentReview.Capture(folder);
                game.DebugHideTutorial(); game.debugAutopilot = false;
                for (int stage = 0; stage < 6; stage++)
                {
                    int level = Mathf.Max(1, stage * 5);
                    game.DebugReset(new[] { level, Mathf.Max(5, level), level, level, level }, 10000);
                    game.DebugTrack(3, Mathf.Max(3, stage + 1));
                    game.DebugHub(); yield return new WaitForSecondsRealtime(.4f);
                    yield return Shot("equipment_home_" + stage);
                    game.DebugAim(); game.DebugSetPull(.8f); yield return new WaitForSecondsRealtime(.3f);
                    yield return Shot("equipment_aim_" + stage);
                    game.DebugLaunch(1); yield return new WaitForSecondsRealtime(1.2f);
                    game.DebugPressRocket(); yield return new WaitForSecondsRealtime(.15f);
                    HomeCheck(game.DebugRocketFiring, "Equipment rocket did not fire at stage " + stage);
                    yield return Shot("equipment_fire_" + stage);
                }
                Debug.Log("[EQUIPMENT TEST] Finished six live stages and model gallery.");
                homeTesting = false; Application.logMessageReceived -= HomeLogFailure;
                SaveData.ClearAll(); Application.Quit(homeFailed ? 1 : 0); yield break;
            }
            if (mode == "snow")
            {
                homeTesting = true;
                homeDeadline = Time.realtimeSinceStartup + 120f;
                Application.logMessageReceived += HomeLogFailure;
                yield return SnowShots();
                homeTesting = false;
                Application.logMessageReceived -= HomeLogFailure;
                game.DebugSnowView(null);
                SaveData.ClearAll();
                Application.Quit(homeFailed ? 1 : 0);
                yield break;
            }
            if (mode == "home" || mode == "themes" || mode == "menus")
            {
                homeTesting = true;
                homeDeadline = Time.realtimeSinceStartup + (mode == "menus" ? 300f : mode == "themes" ? 240f : 180f);
                Application.logMessageReceived += HomeLogFailure;
                if (mode == "menus") yield return MenuShots();
                else if (mode == "themes") yield return ThemeShots();
                else yield return HomeShots();
                homeTesting = false;
                Application.logMessageReceived -= HomeLogFailure;
                SaveData.ClearAll();
                Application.Quit();
                yield break;
            }
            if (mode == "review" || mode == "cups")
            {
                if (mode == "cups") yield return CupFramingShots();
                else yield return ReviewShots();
                SaveData.ClearAll();
                Application.Quit();
                yield break;
            }
            if (mode == "avatars")
            {
                yield return AvatarShots();
                SaveData.ClearAll();   // yalnız test önekli kayıt (gerçek kayıt korunur)
                Application.Quit();
                yield break;
            }

            if (mode == "parts")
            {
                // Kalıcı parçalar: duyuru önizlemeleri + Çayır'da köprü öncesi yükselti (yaklaşırken ve inerken)
                game.DebugHideTutorial();
                game.debugAutopilot = true;
                game.DebugReset(new[] { 22, 22, 12, 22, 14 }, 0);
                game.DebugTrack(0, 0);
                yield return new WaitForSeconds(1.5f);
                game.DebugAim();
                yield return new WaitForSeconds(0.5f);
                game.DebugLaunch(1f);
                float gapZ = game.DebugGapZ(0), pt = 0f;
                while (!game.DebugInResult && game.DebugDistance < gapZ - 48f && pt < 120f) { pt += Time.deltaTime; yield return null; }
                yield return Shot("p_bridge0_rise");
                while (!game.DebugInResult && game.DebugDistance < gapZ - 22f && pt < 120f) { pt += Time.deltaTime; yield return null; }
                yield return Shot("p_bridge1_dive");
                while (!game.DebugInResult && game.DebugDistance < gapZ + 4f && pt < 120f) { pt += Time.deltaTime; yield return null; }
                yield return Shot("p_bridge2_on");
                for (int part = 1; part < Upgrades.PartNames.Length; part++)
                {
                    game.DebugHub();
                    game.DebugPartUnlock(part);
                    yield return new WaitForSeconds(1.2f);
                    yield return Shot("p_part" + part);
                }
                foreach (int tr in new[] { 3, 4, 5, 6 })
                {
                    game.DebugReset(new[] { 20, 20, 12, 20, 14 }, 0);
                    game.DebugTrack(tr, tr);
                    yield return new WaitForSeconds(1.5f);
                    game.DebugAim();
                    yield return new WaitForSeconds(0.5f);
                    game.DebugLaunch(1f);
                    yield return new WaitForSeconds(4f);
                    yield return Shot("p_run" + tr);
                }
                SaveData.ClearAll();   // yalnız test önekli kayıt (gerçek kayıt korunur)
                Application.Quit();
                yield break;
            }
            if (mode == "rocket")
            {
                // Roket art arda atışlarda ateşlenebiliyor mu (oyuncu gibi: otomatik pilot yok, düğme yolu)?
                game.DebugHideTutorial();
                game.DebugReset(new[] { 8, 8, 4, 0, 6 }, 0);
                for (int run = 1; run <= 3; run++)
                {
                    if (run == 1) game.DebugAim(); else game.DebugRetry();
                    yield return new WaitForSeconds(0.6f);
                    game.DebugLaunch(1f);
                    yield return new WaitForSeconds(2.5f);
                    Debug.Log("[ROKET] atış " + run + " basmadan önce: " + game.DebugRocketState);
                    game.DebugPressRocket();
                    yield return new WaitForSeconds(0.3f);
                    Debug.Log("[ROKET] atış " + run + " bastıktan sonra: " + game.DebugRocketState);
                    yield return Shot("r" + run + "_fired");
                    float rt = 0f;
                    while (!game.DebugInResult && rt < 60f) { rt += Time.deltaTime; yield return null; }
                    yield return new WaitForSeconds(0.7f);
                    if (run == 1) yield return Shot("r1_flag");
                    yield return new WaitForSeconds(run == 2 ? 6f : 1.5f);
                    Debug.Log("[ROKET] atış " + run + " bitti: " + game.DebugRocketState);
                }
                SaveData.ClearAll();   // yalnız test önekli kayıt (gerçek kayıt korunur)
                Application.Quit();
                yield break;
            }
            if (mode == "wings")
            {
                // Çayır'da kızak aşamaları: ip → minik kanat → büyüyen kanat (ana ekran)
                game.DebugHideTutorial();
                foreach (int lv in new[] { 0, 5, 10, 15, 20, 25 })
                {
                    game.DebugReset(new[] { lv, lv, 0, 0, 0 }, 0);
                    game.DebugHub();
                    yield return new WaitForSeconds(1.5f);
                    yield return Shot("k" + lv.ToString("00") + "_hub");
                }
                game.DebugSettings(true);
                yield return new WaitForSeconds(0.8f);
                yield return Shot("k_settings");
                game.DebugSettings(false);
                game.DebugShop();
                yield return new WaitForSeconds(0.8f);
                yield return Shot("k_shop");
                game.DebugShopScroll();
                yield return new WaitForSeconds(0.5f);
                yield return Shot("k_shop2");
                for (int d = 0; d < Store.Deals.Length; d++)
                {
                    game.DebugHub();
                    game.DebugOffer(d);
                    yield return new WaitForSeconds(1f);
                    yield return Shot("k_deal" + d);
                }
                SaveData.ClearAll();   // yalnız test önekli kayıt (gerçek kayıt korunur)
                Application.Quit();
                yield break;
            }
            if (mode == "wall")
            {
                // Mars krater duvarı: yaklaşırken ve tırmanırken
                game.DebugHideTutorial();
                game.debugAutopilot = true;
                game.DebugReset(new[] { 26, 26, 16, 26, 26 }, 0);
                game.DebugTrack(6, 6);
                yield return new WaitForSeconds(1.5f);
                game.DebugAim();
                yield return new WaitForSeconds(0.5f);
                game.DebugLaunch(1f);
                float wallZ = game.DebugRocketHillZ, wt = 0f;
                while (!game.DebugInResult && game.DebugDistance < wallZ - 75f && wt < 240f) { wt += Time.deltaTime; yield return null; }
                yield return Shot("w_0ahead");
                while (!game.DebugInResult && game.DebugDistance < wallZ - 25f && wt < 240f) { wt += Time.deltaTime; yield return null; }
                yield return Shot("w_1climb");
                while (!game.DebugInResult && game.DebugDistance < wallZ + 5f && wt < 240f) { wt += Time.deltaTime; yield return null; }
                yield return Shot("w_2top");
                SaveData.ClearAll();   // yalnız test önekli kayıt (gerçek kayıt korunur)
                Application.Quit();
                yield break;
            }
            if (mode == "surfaces")
            {
                yield return SurfaceShots();
                SaveData.ClearAll();   // yalnız test önekli kayıt (gerçek kayıt korunur)
                Application.Quit();
                yield break;
            }
            if (mode == "reset")
            {
                // Test sıfırlaması: ileri pistte tam yükseltilmiş araç, sonra "Reset progress" ile başa dönüş.
                game.DebugHideTutorial();
                game.DebugReset(new[] { 30, 30, 30, 30, 30 }, 5000);
                game.DebugTrack(5, 5);
                yield return new WaitForSeconds(1.5f);
                yield return Shot("r1_before");
                game.DebugResetProgress();
                yield return new WaitForSeconds(1.5f);
                yield return Shot("r2_after");
                SaveData.ClearAll();   // yalnız test önekli kayıt (gerçek kayıt korunur)
                Application.Quit();
                yield break;
            }
            if (mode == "gallery")
            {
                // İnceleme galerisi: her pistin aracı (kızak kartı, her kademe: kanatlar her kademede büyür), sapan, roket,
                // bardak ve gelir kartları (her aşama), kalıcı parçalar. Kartlardaki 3D önizlemeyle aynı kurucular.
                yield return new WaitForSeconds(1f);
                var pv = PartPreview.Instance;
                int savedTrack = Upgrades.Track, savedTier = Upgrades.PartTier;
                var dir = Path.Combine(folder, "gallery");
                Directory.CreateDirectory(dir);
                var lines = new System.Text.StringBuilder();
                void Card(int type, int level, string file, string label)
                {
                    pv.SaveCard(type, level, Path.Combine(dir, file));
                    lines.AppendLine(file + "	" + label);
                }
                Upgrades.Track = 0;
                Upgrades.PartTier = 0;
                foreach (var type in new[] { UpgradeType.Slingshot, UpgradeType.Rocket, UpgradeType.Glass, UpgradeType.Income })
                    for (int st = 0; st <= 5; st++)
                    {
                        int lv = Mathf.Min(st * Upgrades.StageSize, Upgrades.MaxFor(6));
                        Upgrades.Track = st >= 5 ? 6 : 0;   // 6. aşama yalnız 30 kademeli pistlerde
                        Card((int)type, lv, type.ToString().ToLowerInvariant() + "_" + st + ".png",
                             type + "|" + (st + 1) + ". aşama · " + PartVisuals.StageName((int)type, st) + " (kademe " + lv + ")");
                        yield return null;
                    }
                for (int tr = 0; tr < TrackLibrary.Count; tr++)
                {
                    Upgrades.Track = tr;
                    Upgrades.PartTier = tr;   // o piste gelen oyuncunun kalıcı parçaları
                    for (int lv = 0; lv <= Upgrades.MaxFor(tr); lv++)
                    {
                        Card((int)UpgradeType.Runners, lv, "vehicle_t" + tr + "_" + lv.ToString("00") + ".png",
                             "Vehicle " + tr + "|" + TrackLibrary.Names[tr] + " · kademe " + lv + " · " + PartVisuals.StageName((int)UpgradeType.Runners, Upgrades.Stage(lv)));
                        yield return null;
                    }
                }
                for (int parts = 0; parts <= Upgrades.PartNames.Length; parts++)
                {
                    int tr = Mathf.Min(parts, TrackLibrary.Count - 1);
                    Upgrades.Track = tr;
                    pv.SaveUnlock(TrackLibrary.Vehicles[tr], parts, Path.Combine(dir, "parts_" + parts + ".png"));
                    lines.AppendLine("parts_" + parts + ".png	Parts|" + (parts == 0 ? "Parçasız" : parts + " parça · son: " + Upgrades.PartNames[parts - 1]) + " (" + TrackLibrary.Names[tr] + " aracı)");
                    yield return null;
                }
                Upgrades.Track = savedTrack;
                Upgrades.PartTier = savedTier;
                File.WriteAllText(Path.Combine(dir, "index.tsv"), lines.ToString());
                Application.Quit();
                yield break;
            }
            if (mode == "gems")
            {
                // Elmaslar: her elmasa 18 m kala (rotanın dışında mı, yamaçta mı, derenin üstünde mi).
                game.DebugHideTutorial();
                game.debugAutopilot = true;
                foreach (int tr in new[] { 0, 1, 2 })
                {
                    game.DebugReset(new[] { 25, 25, 20, 25, 20 }, 0);
                    game.DebugTrack(tr, tr);
                    yield return new WaitForSeconds(1.5f);
                    var gemZs = game.DebugGemZs();
                    gemZs.Sort();
                    game.DebugAim();
                    yield return new WaitForSeconds(0.5f);
                    game.DebugLaunch(1f);
                    float wt = 0f;
                    for (int g = 0; g < gemZs.Count && g < 5; g++)
                    {
                        while (!game.DebugInResult && game.DebugDistance < gemZs[g] - 18f && wt < 150f) { wt += Time.deltaTime; yield return null; }
                        if (game.DebugInResult) break;
                        yield return Shot("g" + tr + "_" + g);
                    }
                }
                SaveData.ClearAll();   // yalnız test önekli kayıt (gerçek kayıt korunur)
                Application.Quit();
                yield break;
            }
            if (mode == "bridges")
            {
                // Kıvrımlı köprüler: her pistte ilk kıvrımlı köprüye 22 m kala ve üstündeyken.
                game.DebugHideTutorial();
                game.debugAutopilot = true;
                foreach (int tr in new[] { 2, 4, 5 })
                {
                    game.DebugReset(new[] { 25, 25, 20, 25, 20 }, 0);
                    game.DebugTrack(tr, tr);
                    yield return new WaitForSeconds(1.5f);
                    float cbz = game.DebugCurvedBridgeZ(0);
                    if (cbz < 0f) continue;
                    game.DebugAim();
                    yield return new WaitForSeconds(0.5f);
                    game.DebugLaunch(1f);
                    float wt = 0f;
                    while (!game.DebugInResult && game.DebugDistance < cbz - 22f && wt < 150f) { wt += Time.deltaTime; yield return null; }
                    if (game.DebugInResult) continue;
                    yield return Shot("b" + tr + "_ahead");
                    while (!game.DebugInResult && game.DebugDistance < cbz + 5f && wt < 150f) { wt += Time.deltaTime; yield return null; }
                    if (!game.DebugInResult) yield return Shot("b" + tr + "_on");
                    while (!game.DebugInResult && game.DebugDistance < cbz + 16f && wt < 150f) { wt += Time.deltaTime; yield return null; }
                    if (!game.DebugInResult) yield return Shot("b" + tr + "_mid");
                }
                SaveData.ClearAll();   // yalnız test önekli kayıt (gerçek kayıt korunur)
                Application.Quit();
                yield break;
            }
            if (mode == "woods" || mode == "drawn")
            {
                yield return DrawnShots(1, new[] { 25, 25, 25, 25, 25 }, new (float, string)[]
                {
                    (60f, "w1_bowl"), (320f, "w2_creek1_ahead"), (405f, "w3_peak_ahead"), (480f, "w4_kicker_ahead"),
                    (530f, "w5_glide"), (672f, "w6_creek2_ahead"), (750f, "w7_edge_air"), (1000f, "w8_creek3_ahead"),
                });
                if (mode == "drawn")
                    yield return DrawnShots(2, new[] { 25, 25, 20, 25, 20 }, new (float, string)[]
                    {
                        (60f, "c1_bowl"), (200f, "c2_edge_ahead"), (235f, "c3_edge_air"), (372f, "c4_creek1_ahead"),
                        (440f, "c5_fork"), (490f, "c6_edge2_ahead"), (520f, "c7_glide"), (780f, "c8_creek2_ahead"),
                        (850f, "c9_edge3_ahead"), (1180f, "c10_valley"),
                    });
                SaveData.ClearAll();   // yalnız test önekli kayıt (gerçek kayıt korunur)
                Application.Quit();
                yield break;
            }
            if (mode == "tracks")
            {
                yield return TrackShots();
                SaveData.ClearAll();   // yalnız test önekli kayıt (gerçek kayıt korunur)
                Application.Quit();
                yield break;
            }

            // Ana ekran (başlangıç)
            game.DebugReset(new[] { 0, 0, 0, 0, 0 }, 300);
            game.DebugHub();
            yield return new WaitForSeconds(1.5f);
            yield return Shot("0_hub");
            game.DebugSettings(true);
            yield return new WaitForSeconds(0.7f);
            yield return Shot("0b_settings");
            game.DebugSettings(false);
            yield return new WaitForSeconds(0.3f);
            game.DebugBuy(0);
            game.DebugBuy(4);
            game.DebugBuy(2);   // bardak: coin yetmez, kart titrer
            yield return new WaitForSeconds(0.12f);
            yield return Shot("1_hub_buy");
            game.DebugMap();
            yield return new WaitForSeconds(0.8f);
            yield return Shot("1b_map");
            game.DebugHub();
            for (int g = 0; g < 5; g++)
            {
                game.DebugGlass(g);
                yield return new WaitForSeconds(0.4f);
                yield return Shot("1g_glass" + g);
            }
            game.DebugGlass(0);   // bardak seviyesi başa döner
            game.DebugGlass(-1);
            game.DebugGems(18);
            game.DebugAvatars(2);
            yield return new WaitForSeconds(1.2f);
            yield return Shot("1a_avatars");
            game.DebugAvatars(9);
            yield return new WaitForSeconds(1.2f);
            yield return Shot("1a2_avatar_knight");
            game.DebugSelectAvatar(2);   // Kaptan Mira ile devam
            game.DebugGems(5);   // coin yetmeyen kartlarda elmasla ödeme görünür
            game.DebugHub();
            yield return new WaitForSeconds(1f);
            yield return Shot("1c_hub_gems");
            game.DebugChest();
            yield return new WaitForSeconds(0.9f);
            yield return Shot("1d_chest");
            game.DebugTripleChest();
            yield return new WaitForSeconds(1f);
            yield return Shot("1e_ad");
            yield return new WaitForSeconds(2.6f);
            yield return Shot("1f_chest_x3");
            // Yüzey yakın çekimleri (Çayır): buz, su, çamur
            foreach (var (type, name) in new[] { (Surface.Ice, "1i_ice_close"), (Surface.Puddle, "1i_water_close"), (Surface.Mud, "1i_mud_close") })
            {
                if (!game.DebugViewSurface(type)) continue;
                yield return new WaitForSeconds(1.2f);
                yield return Shot(name);
            }
            game.DebugViewSurface(Surface.Ice, false);
            game.DebugNoLaunches();
            yield return new WaitForSeconds(1f);
            yield return Shot("1h_no_launches");
            game.DebugRestoreLaunches();
            yield return new WaitForSeconds(0.4f);

            game.DebugHideTutorial();   // ipucu kartı kızağı ve karakteri örtmesin
            // Nişan ve koşu
            game.DebugAim();
            yield return new WaitForSeconds(1.2f);
            yield return Shot("2_aim_hint");
            game.DebugSetPull(0.85f, -0.35f);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("2b_aim_pull");
            game.DebugLaunch(0.85f, -0.35f);
            yield return new WaitForSeconds(2f);
            yield return Shot("3_run");

            t = 0f;
            while (!game.DebugInResult && t < 30f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.7f);
            yield return Shot("3b_flag");
            yield return new WaitForSeconds(1.5f);
            yield return Shot("4_result");

            // Çayır kademesi tam: otomatik oyuncu; kapak, roket ve bitiş
            game.DebugReset(new[] { 8, 8, 4, 8, 8 }, 0);
            game.DebugHub();   // 1. aşama görünümü: kızak boyası, roket ve sapan renkleri
            yield return new WaitForSeconds(1.2f);
            yield return Shot("4a_hub_stage1");
            game.DebugAim();
            game.debugAutopilot = true;
            yield return new WaitForSeconds(0.5f);
            game.DebugLaunch(1f);
            t = 0f;
            while (!game.DebugInResult && game.DebugDistance < 52f && t < 30f) { t += Time.deltaTime; yield return null; }
            yield return Shot("4b_splash");
            t = 0f;
            while (!game.DebugInResult && game.DebugDistance < 104f && t < 30f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.25f);
            yield return Shot("5_lid_open");
            // İlk ince köprü: yaklaşırken ve tablanın üstünde
            float bz = game.DebugGapZ(0);
            if (bz > 0f)
            {
                t = 0f;
                while (!game.DebugInResult && game.DebugDistance < bz - 14f && t < 90f) { t += Time.deltaTime; yield return null; }
                yield return Shot("5e_bridge_ahead");
                t = 0f;
                while (!game.DebugInResult && game.DebugDistance < bz + 6f && t < 30f) { t += Time.deltaTime; yield return null; }
                yield return Shot("5f_bridge_on");
            }
            // Tepe aşılıp dik inişe geçerken kamera (zeminin altına girmemeli)
            float cz = game.DebugCrestZ(300f);
            if (cz > 0f)
            {
                t = 0f;
                while (!game.DebugInResult && game.DebugDistance < cz + 8f && t < 90f) { t += Time.deltaTime; yield return null; }
                yield return Shot("5d_crest_down");
                t = 0f;
                while (!game.DebugInResult && game.DebugDistance < cz + 22f && t < 30f) { t += Time.deltaTime; yield return null; }
                yield return Shot("5d_crest_down2");
            }
            // Yüzeyler: ilk buz ve ilk çamur lekesi (önünden)
            float iz = game.DebugSurfaceZ(Surface.Ice), dz = game.DebugSurfaceZ(Surface.Mud);
            foreach (var (z, name) in iz < dz || dz < 0f ? new[] { (iz, "5a_ice"), (dz, "5a_mud") } : new[] { (dz, "5a_mud"), (iz, "5a_ice") })
            {
                if (z < 0f) continue;
                t = 0f;
                while (!game.DebugInResult && game.DebugDistance < z - 14f && t < 90f) { t += Time.deltaTime; yield return null; }
                yield return Shot(name);
            }
            // Engebe: ilk tümsek dizisi ve ilk zorlu kambur (z sırasıyla)
            float mz = game.DebugBumpZ(false), hz = game.DebugBumpZ(true);
            foreach (var (z, name) in mz < hz || hz < 0f ? new[] { (mz, "5b_moguls"), (hz, "5c_hump") } : new[] { (hz, "5c_hump"), (mz, "5b_moguls") })
            {
                if (z < 0f) continue;
                t = 0f;
                while (!game.DebugInResult && game.DebugDistance < z - 8f && t < 90f) { t += Time.deltaTime; yield return null; }
                yield return Shot(name);
                t = 0f;
                while (!game.DebugInResult && game.DebugDistance < z + 6f && t < 20f) { t += Time.deltaTime; yield return null; }
                yield return Shot(name + "_on");
            }
            t = 0f;
            while (!game.DebugInResult && !game.DebugRocketFiring && t < 90f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.3f);
            yield return Shot("6_rocket");
            // Havada alınan roket hakkı: rampaya yaklaşırken ve hemen sonra
            float pz = game.DebugRocketPickupZ;
            if (pz > 0f)
            {
                t = 0f;
                while (!game.DebugInResult && game.DebugDistance < pz - 22f && t < 90f) { t += Time.deltaTime; yield return null; }
                yield return Shot("6b_pickup_ahead");
                t = 0f;
                while (!game.DebugInResult && game.DebugDistance < pz + 3f && t < 30f) { t += Time.deltaTime; yield return null; }
                yield return Shot("6c_pickup_taken");
            }
            // Çayır vadisi: kanatla süzülürken
            float gz = game.DebugGapZ(0);
            if (gz > 0f)
            {
                t = 0f;
                while (!game.DebugInResult && game.DebugDistance < gz + 15f && t < 90f) { t += Time.deltaTime; yield return null; }
                yield return Shot("6d_valley");
            }
            t = 0f;
            while (!game.DebugInResult && t < 120f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(2.5f);
            yield return Shot("7_finish");

            // Çarpma: pistin ortasında kızak durdurulur; karakter fırlar, yuvarlanır, ayağa kalkıp "Tekrar deneyelim!" der.
            game.DebugReset(new[] { 8, 8, 8, 8, 8 }, 0);
            game.debugAutopilot = true;
            yield return new WaitForSeconds(0.5f);
            game.DebugLaunch(1f);
            t = 0f;
            while (!game.DebugInResult && game.DebugDistance < 140f && t < 30f) { t += Time.deltaTime; yield return null; }
            game.DebugCrash();
            yield return new WaitForSeconds(0.45f);
            yield return Shot("7b_crash_air");
            yield return new WaitForSeconds(1.1f);
            yield return Shot("7c_crash_roll");
            yield return new WaitForSeconds(1.0f);
            yield return Shot("7d_crash_getup");
            yield return new WaitForSeconds(0.9f);
            yield return Shot("7e_crash_bubble");
            yield return new WaitForSeconds(1.0f);
            yield return Shot("7f_crash_bubble2");

            // İnce köprüden kayıp dereye düşme: köprünün üstünde direksiyon bilerek sağa kırılır.
            game.DebugReset(new[] { 16, 16, 8, 16, 16 }, 0);
            yield return new WaitForSeconds(0.5f);
            game.DebugLaunch(1f);
            float kz = game.DebugGapZ(0);
            if (kz > 0f)
            {
                t = 0f;
                while (!game.DebugInResult && game.DebugDistance < kz + 2f && t < 90f) { t += Time.deltaTime; yield return null; }
                game.debugSteer = 1f;
                t = 0f;
                while (!game.DebugInResult && t < 0.5f) { t += Time.deltaTime; yield return null; }
                yield return Shot("7g_bridge_slip");
                t = 0f;
                while (!game.DebugInResult && t < 3f && game.DebugRunning) { t += Time.deltaTime; yield return null; }
                yield return new WaitForSeconds(0.25f);
                yield return Shot("7h_creek_fall");
                yield return new WaitForSeconds(1.6f);
                yield return Shot("7i_creek_after");
                game.debugSteer = float.NaN;
            }

            // Orman kademesi tam: ana ekranda kanat + roket + yeni bardak
            game.DebugReset(new[] { 16, 16, 12, 16, 16 }, 5000);
            game.DebugTrack(1, 1);
            yield return new WaitForSeconds(1.5f);
            yield return Shot("8_forest_hub");
            game.DebugAim();
            yield return new WaitForSeconds(0.5f);
            game.DebugLaunch(1f);
            t = 0f;
            float fz = game.DebugGapZ(0) + 30f;   // ilk vadinin üstünde süzülürken
            while (!game.DebugInResult && game.DebugDistance < fz && t < 120f) { t += Time.deltaTime; yield return null; }
            yield return Shot("9_forest_glide");

            // Kanyon
            game.DebugReset(new[] { 20, 20, 16, 20, 20 }, 0);
            game.DebugTrack(2, 2);
            yield return new WaitForSeconds(1.5f);
            yield return Shot("10_canyon_hub");
            game.DebugAim();
            yield return new WaitForSeconds(0.5f);
            game.DebugLaunch(1f);
            yield return new WaitForSeconds(8f);
            yield return Shot("11_canyon_run");

            // Karlı Dağ: kar zemin, buz/çamur lekeleri, kar yağışı
            game.DebugReset(new[] { 20, 20, 20, 20, 20 }, 0);
            game.DebugTrack(3, 3);
            yield return new WaitForSeconds(1.5f);
            yield return Shot("12_snow_hub");
            game.DebugAim();
            yield return new WaitForSeconds(0.5f);
            game.DebugLaunch(1f);
            yield return new WaitForSeconds(6f);
            yield return Shot("13_snow_run");
            t = 0f;
            float sz = game.DebugGapZ(0) + 40f;
            while (!game.DebugInResult && game.DebugDistance < sz && t < 120f) { t += Time.deltaTime; yield return null; }
            yield return Shot("14_snow_gap");

            SaveData.ClearAll();   // yalnız test önekli kayıt (gerçek kayıt korunur)
            Application.Quit();
        }

        /// Su birikintisi, çamur ve buz lekelerine yaklaşırken (Çayır, Karlı Dağ, Çöl, Volkan).
        IEnumerator SurfaceShots()
        {
            game.DebugHideTutorial();
            game.debugAutopilot = true;
            foreach (int tr in new[] { 0, 3, 4, 5 })
                foreach (var type in new[] { Surface.Puddle, Surface.Mud, Surface.Ice })
                {
                    game.DebugReset(new[] { 20, 20, 16, 20, 20 }, 0);
                    game.DebugTrack(tr, tr);
                    yield return new WaitForSeconds(1f);
                    float sz = game.DebugSurfaceZ(type, 40f);
                    if (sz < 0f) continue;
                    game.DebugAim();
                    yield return new WaitForSeconds(0.5f);
                    game.DebugLaunch(1f);
                    float t = 0f;
                    while (!game.DebugInResult && game.DebugDistance < sz - 2f && t < 90f) { t += Time.deltaTime; yield return null; }
                    if (game.DebugDistance < sz - 2f) continue;   // ulaşamadı
                    yield return Shot("s" + tr + "_" + type.ToString().ToLowerInvariant());
                }
        }

        /// Çizimden pist (Whispering Woods, Sunset Canyon): bir koşuda verilen mesafelerde görüntü (çanak, dere, kenar, süzülme).
        IEnumerator DrawnShots(int track, int[] levels, (float z, string name)[] marks)
        {
            game.DebugHideTutorial();
            game.debugAutopilot = true;
            game.DebugReset(levels, 0);
            game.DebugTrack(track, track);
            yield return new WaitForSeconds(1.5f);
            game.DebugAim();
            yield return new WaitForSeconds(0.5f);
            game.DebugLaunch(1f);
            float t = 0f;
            foreach (var m in marks)
            {
                while (!game.DebugInResult && game.DebugDistance < m.z && t < 150f) { t += Time.deltaTime; yield return null; }
                if (game.DebugInResult) break;
                yield return Shot(m.name);
            }
        }

        /// Her parkur: ana ekran (araç), koşu ve ilk çatal (yaklaşırken ve içinde); en sonda harita listesi.
        IEnumerator TrackShots()
        {
            game.DebugHideTutorial();
            game.debugAutopilot = true;
            for (int tr = 0; tr < TrackLibrary.Count; tr++)
            {
                game.DebugReset(new[] { 20, 20, 16, 20, 20 }, 0);   // çatala ulaşsın
                game.DebugTrack(tr, tr);
                yield return new WaitForSeconds(1.5f);
                yield return Shot("t" + tr + "_0hub");
                game.DebugAim();
                yield return new WaitForSeconds(0.5f);
                game.DebugLaunch(1f);
                yield return new WaitForSeconds(5f);
                yield return Shot("t" + tr + "_1run");
                float fz = game.DebugForkZ(0);
                if (fz < 0f) continue;
                float t = 0f;
                while (!game.DebugInResult && game.DebugDistance < fz - 16f && t < 120f) { t += Time.deltaTime; yield return null; }
                yield return Shot("t" + tr + "_2fork_ahead");
                while (!game.DebugInResult && game.DebugDistance < fz + 22f && t < 120f) { t += Time.deltaTime; yield return null; }
                yield return Shot("t" + tr + "_3fork_in");
                if (tr == 0)
                {
                    // Gerçek akış: parkur biter → sonuç kartı (3 kat düğmesi) → yeni parça duyurusu
                    game.DebugForceFinish();
                    yield return new WaitForSeconds(3.5f);
                    yield return Shot("t0_4unlock");
                    game.DebugClosePart();
                    yield return new WaitForSeconds(0.6f);
                    yield return Shot("t0_5result");
                }
            }
            game.DebugReset(new[] { 16, 16, 12, 16, 16 }, 0);
            game.DebugTrack(4, 4);
            game.DebugMap();
            yield return new WaitForSeconds(1.2f);
            yield return Shot("t_map");
            for (int part = 0; part < Upgrades.PartNames.Length; part++)
            {
                game.DebugHub();
                game.DebugPartUnlock(part);
                yield return new WaitForSeconds(1.2f);
                yield return Shot("u" + part + "_part");
            }
            // Günlük görevler (biri tamam, biri yarıda) ve günlük ödül (seride 4. gün)
            game.DebugHub();
            game.DebugQuests(new[] { 99999, 1, 0 });
            yield return new WaitForSeconds(1.2f);
            yield return Shot("q_quests");
            game.DebugHub();
            game.DebugLogin(3);
            yield return new WaitForSeconds(1.2f);
            yield return Shot("q_login");
        }

        /// Meshy karakterleri: kart, ana ekran, nişan, koşu ve düşüp kalkma sahnesi.
        IEnumerator AvatarShots()
        {
            game.DebugHideTutorial();
            for (int i = 0; i < AvatarLibrary.Count; i++)
            {
                if (!AvatarLibrary.All[i].model.StartsWith("Meshy/")) continue;
                string tag = "a" + i + "_" + AvatarLibrary.All[i].model.Substring(6).ToLowerInvariant();
                game.DebugReset(new[] { 8, 8, 8, 8, 8 }, 0);
                game.DebugSelectAvatar(i);
                game.DebugHub();   // karakter sayfası ana ekrandan açılır (kamera kızağın önünde)
                yield return new WaitForSeconds(0.5f);
                game.DebugAvatars(i);
                yield return new WaitForSeconds(1.2f);
                yield return Shot(tag + "_0card");
                game.DebugHub();
                yield return new WaitForSeconds(1.2f);
                yield return Shot(tag + "_1hub");
                game.DebugAim();
                yield return new WaitForSeconds(1.0f);
                yield return Shot(tag + "_2aim");
                game.debugAutopilot = true;
                game.DebugLaunch(1f);
                yield return new WaitForSeconds(2.5f);
                yield return Shot(tag + "_3run");
                float t = 0f;
                while (!game.DebugInResult && game.DebugDistance < 140f && t < 30f) { t += Time.deltaTime; yield return null; }
                game.DebugCrash();
                foreach (var (wait, name) in new[] { (0.45f, "_4air"), (1.0f, "_5roll"), (0.6f, "_6getup"), (0.5f, "_7getup2"), (0.6f, "_8stand"), (0.8f, "_9cheer") })
                {
                    yield return new WaitForSeconds(wait);
                    yield return Shot(tag + name);
                }
                game.debugAutopilot = false;
                yield return new WaitForSeconds(2f);
            }
        }

        // Bounded post-change visual and lifecycle check; all saves use the autotest prefix.
        IEnumerator ReviewShots()
        {
            game.DebugHideTutorial();
            game.debugAutopilot = true;
            game.DebugReset(new[] { 0, 0, 0, 0, 0 }, 300);
            game.DebugHub();
            yield return new WaitForSeconds(0.8f);
            yield return Shot("review_hub");
            game.DebugAim();
            game.DebugSetPull(0.8f);
            game.DebugPause(true);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("review_pause_aim");
            game.DebugPause(false);
            yield return new WaitForSeconds(0.3f);
            if (game.DebugRunning) throw new System.Exception("Pause released an aim into a launch");
            game.DebugLaunch(1f);
            yield return new WaitForSeconds(1f);
            game.DebugPause(true);
            float clock = game.DebugSimulationTime;
            yield return new WaitForSecondsRealtime(0.6f);
            if (!game.DebugPaused || game.DebugSimulationTime != clock)
                throw new System.Exception("Simulation advanced while paused");
            yield return Shot("review_pause_run");
            game.DebugPause(false);
            yield return new WaitForSeconds(0.5f);
            if (game.DebugSimulationTime <= clock)
                throw new System.Exception("Simulation did not resume");
            Debug.Log("[REVIEW] Pause freezes/resumes simulation; aim interruption does not launch.");
            yield return Shot("review_cup_start");
            game.DebugReset(new[] { 20, 20, 20, 20, 20 }, 5000);
            for (int tr = 0; tr < TrackLibrary.Count; tr++)
            {
                game.DebugTrack(tr, TrackLibrary.Count - 1);
                game.DebugAim();
                yield return new WaitForSeconds(0.3f);
                game.DebugLaunch(1f);
                yield return new WaitForSeconds(3.5f);
                yield return Shot("review_track_" + tr);
            }
            game.DebugForceFinish();
            yield return new WaitForSeconds(4f);
            yield return Shot("review_result");
            game.DebugHub();
            game.DebugMap();
            yield return new WaitForSeconds(0.5f);
            yield return Shot("review_map");
            game.DebugHub();
            yield return CupFramingShots();
            game.DebugHub();
            game.DebugShop();
            yield return new WaitForSeconds(0.5f);
            yield return Shot("review_shop");
            game.DebugHub();
            game.DebugOffer(0);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("review_offer");
            Debug.Log("[REVIEW] Completed all seven tracks, pause, map, shop and settled result captures.");
        }

        IEnumerator CupFramingShots()
        {
            game.DebugHideTutorial();
            game.debugAutopilot = true;
            game.DebugReset(new[] { 20, 20, 20, 20, 20 }, 5000);
            game.DebugLaunch(1f);
            yield return new WaitForSeconds(0.8f);
            game.DebugForceFinish();
            yield return new WaitForSeconds(4f);
            game.DebugClosePart();
            yield return new WaitForSeconds(0.4f);
            yield return Shot("review_first_track_result");
            game.DebugTrack(6, 6);
            int cupChecks = 0;
            foreach (int stage in new[] { 0, 1, 2, 3, 4, 6 })
                foreach (int hand in new[] { -1, 1 })
                    foreach (var pose in new[] { Vector2.zero, new Vector2(40f, 40f), new Vector2(-40f, -40f) })
                    {
                        game.DebugCupPose(stage, hand, pose.x, pose.y);
                        yield return new WaitForSeconds(0.12f);
                        if (!game.DebugCupFits(out string detail))
                            throw new System.Exception("Cup clipped: stage=" + stage + " hand=" + hand + " pose=" + pose + " " + detail);
                        cupChecks++;
                        if (stage == 0 || stage == 2 || stage == 6)
                            yield return Shot("review_cup_" + stage + "_" + hand + "_" + pose.x);
                    }
            Debug.Log("[REVIEW] " + cupChecks + " cup framing checks passed across six models, both hands and extreme tilts.");
        }

        // Snow inspection never moves the sled or modifies terrain/physics. Missing events remain explicit.
        IEnumerator SnowShots()
        {
            if (SaveData.Prefix != "autotest.")
                throw new System.InvalidOperationException("Snow screenshots require isolated player data.");
            snowReport.Clear();
            snowReport.AppendLine("Snow visual inspection: track 3, native physics, real distances in metres.");
            snowReport.AppendLine("event\tz\theight\tspeed\tclearance\tslope\tcurvature\tmilk\ttime\tflightTime\ttotalAir\tlandings\timpact\trunning\tgrounded\tlaunching\tend\tcause");
            game.DebugHideTutorial();
            game.debugAutopilot = true;
            game.DebugReset(new[] { 10, 10, 10, 10, 10 }, 0);
            game.DebugTrack(3, 3);
            game.DebugAim();
            foreach (float z in new[] { 60f, 200f, 450f, 750f })
            {
                game.DebugSnowView(z);
                yield return new WaitForEndOfFrame();
                string name = "snow_profile_" + z.ToString("000");
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
                snowReport.AppendLine("PROFILE " + name + ": static oblique camera, no run-distance claim.");
                SaveSnowReport();
                yield return null;
            }
            game.DebugSnowView(null);

            foreach (int level in new[] { 0, 10, 20 })
            {
                game.DebugReset(new[] { level, level, level, level, level }, 0);
                game.DebugTrack(3, 3);
                game.DebugAim();
                yield return new WaitForSecondsRealtime(0.2f);
                game.DebugLaunch(1f);
                string prefix = "snow_lv" + level.ToString("00");
                float deadline = Mathf.Min(homeDeadline - 1f, Time.realtimeSinceStartup + 30f);
                float[] targets = { 60f, 120f, 220f, 350f, 550f };
                int nextTarget = 0, flightLandings = 0;
                bool observedAir = false;
                string flight = null;
                var takeoffs = new bool[2];
                var landings = new bool[2];
                int flightSlot = -1;
                var previous = game.DebugSnowState();
                SnowLog(prefix + "_launch", previous);
                while (Time.realtimeSinceStartup < deadline)
                {
                    // Read after GameController.Update so pixels and telemetry describe the same rendered frame.
                    yield return new WaitForEndOfFrame();
                    var now = game.DebugSnowState();
                    if (now.landings > previous.landings)
                    {
                        if (flight != null)
                        {
                            bool landed = now.grounded && now.running && now.end == RunEnd.None;
                            SnowCapture(prefix + "_" + flight + (landed ? "_landing" : "_landing_unsuccessful"), now);
                            landings[flightSlot] = landed;
                        }
                        else SnowLog(prefix + "_landing_without_observed_takeoff", now);
                        flight = null;
                        observedAir = false;
                    }
                    if (now.running && !now.launching && !now.grounded && !observedAir
                        && now.airTime >= 0.08f && now.clearance >= 0.03f)
                    {
                        observedAir = true;
                        flightSlot = now.z < 30f ? -1 : !takeoffs[0] ? 0 : now.z >= 116f && !takeoffs[1] ? 1 : -1;
                        if (flightSlot >= 0)
                        {
                            flight = flightSlot == 0 ? "first" : "middle";
                            takeoffs[flightSlot] = true;
                            flightLandings = now.landings;
                            SnowCapture(prefix + "_" + flight + "_takeoff", now);
                        }
                    }
                    if (now.grounded) observedAir = false;
                    while (nextTarget < targets.Length && now.running && now.z >= targets[nextTarget])
                    {
                        SnowCapture(prefix + "_distance_" + targets[nextTarget].ToString("000"), now);
                        nextTarget++;
                    }
                    previous = now;
                    if (!now.running) break;
                    if (nextTarget == targets.Length && landings[0] && landings[1]) break;
                }
                var last = game.DebugSnowState();
                SnowLog(prefix + (last.running ? "_window_ended" : "_run_ended"), last);
                for (int i = nextTarget; i < targets.Length; i++)
                    snowReport.AppendLine("NOT CAPTURED " + prefix + " distance=" + targets[i] + "; actual=" + last.z.ToString("F2") + "; running=" + last.running);
                for (int i = 0; i < 2; i++)
                    snowReport.AppendLine("FLIGHT " + prefix + " " + (i == 0 ? "first" : "middle")
                        + " takeoffObserved=" + takeoffs[i] + " successfulLandingObserved=" + landings[i]);
                if (flight != null && last.landings == flightLandings)
                    snowReport.AppendLine("UNFINISHED " + prefix + " " + flight + ": no landing observed before run/window ended.");
                SaveSnowReport();
            }
            Debug.Log("[SNOW SHOT] Capture completed. Consult snow-report.txt for reached distances, observed flights and missing coverage; missing captures are not passes.");
        }

        void SnowCapture(string name, GameController.SnowDebugState state)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            SnowLog(name, state);
        }

        void SnowLog(string name, GameController.SnowDebugState s)
        {
            string line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0}\t{1:F2}\t{2:F2}\t{3:F2}\t{4:F3}\t{5:F3}\t{6:F5}\t{7:F3}\t{8:F3}\t{9:F3}\t{10:F3}\t{11}\t{12:F3}\t{13}\t{14}\t{15}\t{16}\t{17}",
                name, s.z, s.height, s.speed, s.clearance, s.slope, s.curvature, s.fill, s.clock, s.airTime,
                s.totalAir, s.landings, s.impact, s.running, s.grounded, s.launching, s.end, s.cause);
            snowReport.AppendLine(line);
            SaveSnowReport();
            Debug.Log("[SNOW SHOT] " + line);
        }

        void SaveSnowReport() => File.WriteAllText(Path.Combine(folder, "snow-report.txt"), snowReport.ToString());

        // HOME-only visual/callback verification: real game data, real UI buttons, isolated save prefix.
        void HomeLogFailure(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) homeFailed = true;
        }

        void Update()
        {
            if (!homeTesting || (!homeFailed && Time.realtimeSinceStartup < homeDeadline)) return;
            homeTesting = false;
            Application.logMessageReceived -= HomeLogFailure;
            StopAllCoroutines();
            if (!homeFailed) Debug.LogError((mode == "snow" ? "[SNOW SHOT]" : "[HOME TEST]") + " Failed: bounded visual test budget exceeded (" + mode + ").");
            if (mode == "snow")
            {
                snowReport.AppendLine("ABORTED: error or 120-second real-time limit; remaining captures are unverified.");
                SaveSnowReport();
            }
            if (SaveData.Prefix == "autotest.") SaveData.ClearAll();
            Application.Quit(1);
        }

        void OnDestroy() => Application.logMessageReceived -= HomeLogFailure;

        static readonly string[] HomeButtons =
        {
            "home-play", "home-map", "home-shop", "home-hero", "home-quests", "home-settings", "home-chest",
            "home-coins-plus", "home-gems-plus", "home-track",
            "home-upgrade-0", "home-upgrade-1", "home-upgrade-2", "home-upgrade-3", "home-upgrade-4"
        };
        VisualElement HomeRoot => game.GetComponent<UIDocument>().rootVisualElement;

        static void HomeCheck(bool valid, string message)
        {
            if (!valid) throw new System.InvalidOperationException("[HOME TEST] " + message);
        }

        static bool HomeVisible(VisualElement e)
        {
            if (e == null || e.worldBound.width <= 0f || e.worldBound.height <= 0f) return false;
            for (var p = e; p != null; p = p.parent)
                if (p.resolvedStyle.display == DisplayStyle.None || p.resolvedStyle.visibility == Visibility.Hidden) return false;
            return true;
        }

        void HomePress(string name, bool allowDisabled = false)
        {
            var button = HomeRoot.Q<Button>(name);
            HomeCheck(HomeVisible(button), name + " is missing or hidden");
            HomeCheck(allowDisabled || button.enabledInHierarchy, name + " is unexpectedly disabled");
            button.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled()) button.SendEvent(submit);
        }

        void HomeFixture(int[] levels = null, int money = 2625, int gems = 7, int trackIndex = 3)
        {
            game.DebugHomeFixture(trackIndex, 1, levels ?? new[] { 8, 7, 0, 4, 1 }, money, gems, 0.32f, 7);
        }

        IEnumerator HomeResize(int width, int height)
        {
            game.DebugHomeSafeArea(null);
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            float until = Time.realtimeSinceStartup + 4f;
            while ((Screen.width != width || Screen.height != height) && Time.realtimeSinceStartup < until) yield return null;
            HomeCheck(Screen.width == width && Screen.height == height,
                "requested " + width + "x" + height + ", platform returned " + Screen.width + "x" + Screen.height);
            game.DebugHub();
            yield return new WaitForSecondsRealtime(0.8f);
        }

        void HomeLayoutReport(string name)
        {
            var safe = HomeRoot.Q<VisualElement>("home-safe");
            HomeCheck(HomeVisible(safe), "safe area not available");
            Rect bounds = safe.worldBound;
            var report = new System.Text.StringBuilder("Screen " + Screen.width + "x" + Screen.height + "\nSafe " + bounds + "\n");
            foreach (string id in HomeButtons)
            {
                var button = HomeRoot.Q<Button>(id);
                HomeCheck(HomeVisible(button), id + " is missing/hidden at " + name);
                Rect r = button.worldBound;
                report.AppendLine(id + " " + r + " enabled=" + button.enabledInHierarchy);
                HomeCheck(r.xMin >= bounds.xMin - 1f && r.yMin >= bounds.yMin - 1f && r.xMax <= bounds.xMax + 1f && r.yMax <= bounds.yMax + 1f,
                    id + " exceeds safe area at " + name + ": " + r + " vs " + bounds);
            }
            HomeRoot.Query<Label>().ForEach(label => { if (HomeVisible(label)) report.AppendLine("TEXT " + label.text); });
            File.WriteAllText(Path.Combine(folder, name + "_layout.txt"), report.ToString());
        }

        int themeElementCount = -1;

        void ThemeReport(int trackIndex, string name)
        {
            var canvas = HomeRoot.Q<VisualElement>("home-canvas");
            HomeCheck(canvas != null && HomeTrackTheme.AppliedTrack(canvas) == trackIndex, "selected track theme was not applied");
            int classes = 0;
            for (int i = 0; i < TrackLibrary.Count; i++)
                if (canvas.ClassListContains("home-theme-" + HomeTrackTheme.Slug(i))) classes++;
            HomeCheck(classes == 1 && canvas.ClassListContains("home-theme-" + HomeTrackTheme.Slug(trackIndex)), "stale theme classes accumulated");
            int elements = 0;
            canvas.Query<VisualElement>().ForEach(_ => elements++);
            if (themeElementCount < 0) themeElementCount = elements;
            HomeCheck(elements == themeElementCount, "home visual elements accumulated across theme switches");
            HomeCheck(HomeTrackTheme.CachedThemeCount <= 2, "theme assets exceeded the two-theme cache budget");
            var report = new System.Text.StringBuilder("Track " + trackIndex + " " + HomeTrackTheme.Slug(trackIndex)
                + "\nCanvas elements " + elements + "\nCached themes " + HomeTrackTheme.CachedThemeCount + "\n");
            string[] names = { "home-art-banner", "home-art-play", "home-art-tray" };
            string[] files = { "track-plaque", "play-button", "upgrade-panel" };
            for (int i = 0; i < names.Length; i++)
            {
                int matches = 0;
                canvas.Query<VisualElement>(names[i]).ForEach(_ => matches++);
                var art = canvas.Q<VisualElement>(names[i]);
                HomeCheck(matches == 1 && HomeVisible(art), names[i] + " must remain one visible artwork layer");
                string expected = HomeTrackTheme.AssetPath(trackIndex, files[i]);
                HomeCheck(art.userData as string == expected, names[i] + " references the wrong theme resource");
                var background = art.style.backgroundImage.value;
                var texture = background.texture != null ? background.texture : background.sprite != null ? background.sprite.texture : null;
                HomeCheck(texture != null && texture.name == files[i], names[i] + " is missing its imported artwork");
                int callbacks = HomeTrackTheme.SliceCallbackCount(art);
                HomeCheck(callbacks == (i == 2 ? 1 : 0), names[i] + " has accumulated resize callbacks");
                Vector4 expectedSlices = i != 2 ? Vector4.zero : trackIndex == 3 ? new Vector4(180f, 170f, 180f, 250f)
                    : new Vector4(Mathf.RoundToInt(texture.width * 0.1f), Mathf.RoundToInt(texture.height * 0.19f),
                                  Mathf.RoundToInt(texture.width * 0.1f), Mathf.RoundToInt(texture.height * 0.28f));
                var actualSlices = new Vector4(art.style.unitySliceLeft.value, art.style.unitySliceBottom.value,
                    art.style.unitySliceRight.value, art.style.unitySliceTop.value);
                HomeCheck(actualSlices == expectedSlices, names[i] + " retained slices from a previous theme");
                float sliceScale = art.style.unitySliceScale.value;
                HomeCheck(!float.IsNaN(sliceScale) && !float.IsInfinity(sliceScale) && sliceScale > 0f, names[i] + " has invalid slice scale");
                report.AppendLine(names[i] + ": " + expected + " | " + texture.width + "x" + texture.height
                    + " | slices " + actualSlices + " | scale " + sliceScale + " | callbacks " + callbacks);
            }
            File.WriteAllText(Path.Combine(folder, name + "_theme.txt"), report.ToString());
        }

        IEnumerator ThemeShots()
        {
            HomeCheck(SaveData.Prefix == "autotest.", "theme screenshots require isolated player data");
            for (int trackIndex = 0; trackIndex < TrackLibrary.Count; trackIndex++)
            {
                HomeFixture(trackIndex: trackIndex);
                yield return HomeResize(540, 1170);
                string name = "theme_" + trackIndex + "_" + HomeTrackTheme.Slug(trackIndex) + "_540x1170";
                HomeLayoutReport(name);
                ThemeReport(trackIndex, name);
                yield return Shot(name);
            }
            foreach (int trackIndex in new[] { 1, 6 })
            {
                HomeFixture(trackIndex: trackIndex);
                foreach (int height in new[] { 960, 1200 })
                {
                    yield return HomeResize(540, height);
                    string name = "theme_" + HomeTrackTheme.Slug(trackIndex) + "_540x" + height;
                    HomeLayoutReport(name);
                    ThemeReport(trackIndex, name);
                    yield return Shot(name);
                }
                yield return HomeResize(540, 1170);
                game.DebugHomeSafeArea(new Rect(0f, 28f, 540f, 1100f));
                yield return new WaitForSecondsRealtime(0.4f);
                string safeName = "theme_" + HomeTrackTheme.Slug(trackIndex) + "_safe_area";
                HomeLayoutReport(safeName);
                ThemeReport(trackIndex, safeName);
                yield return Shot(safeName);
                game.DebugHomeSafeArea(null);
            }
            // Revisit winter after eviction; its original images and exact slicing must return.
            foreach (int trackIndex in new[] { 3, 1, 6, 3 })
            {
                HomeFixture(trackIndex: trackIndex);
                yield return HomeResize(540, 1170);
                ThemeReport(trackIndex, "theme_cycle_" + HomeTrackTheme.Slug(trackIndex));
            }
            yield return Shot("theme_cycle_winter_restored");
            Debug.Log("[HOME THEME TEST] PASS: seven track themes, forest/Mars short+tall+safe layouts, exact winter restoration, stable UI tree/resize callbacks and two-theme cache. Player data isolated.");
        }

        static readonly string[] MenuBackgrounds =
        {
            "map", "result", "settings", "chest", "quests", "login", "part", "shop", "offer",
            "launches", "pause", "ad", "heroes-top", "heroes-grid"
        };

        void MenuThemeReport(int trackIndex, string name)
        {
            string slug = HomeTrackTheme.Slug(trackIndex);
            string expected = trackIndex == 3 ? "UI/Shop/shop_bg" : "UI/MenuThemes/" + slug + "/background";
            int count = 0;
            HomeRoot.Query<VisualElement>(className: "menu-theme-background").ForEach(_ => count++);
            HomeCheck(count == MenuBackgrounds.Length, "menu background layers accumulated or are missing: " + count);
            var report = new System.Text.StringBuilder("Track " + trackIndex + " " + slug + "\nBackgrounds " + count + "\n");
            Texture2D shared = null;
            foreach (string key in MenuBackgrounds)
            {
                string id = "menu-bg-" + key;
                var background = HomeRoot.Q<VisualElement>(id);
                int matches = 0;
                HomeRoot.Query<VisualElement>(id).ForEach(_ => matches++);
                HomeCheck(matches == 1 && background.ClassListContains("menu-theme-background"), id + " is not unique");
                HomeCheck(background.pickingMode == PickingMode.Ignore, id + " intercepts menu input");
                HomeCheck(background.userData as string == expected, id + " retained the wrong track resource");
                int classes = 0;
                for (int i = 0; i < TrackLibrary.Count; i++)
                    if (background.ClassListContains("menu-theme-" + HomeTrackTheme.Slug(i))) classes++;
                HomeCheck(classes == 1 && background.ClassListContains("menu-theme-" + slug), id + " retained a stale theme class");
                var texture = background.style.backgroundImage.value.texture;
                HomeCheck(texture != null && texture.name == (trackIndex == 3 ? "shop_bg" : "background"), id + " has no imported menu texture");
                if (shared == null) shared = texture;
                HomeCheck(texture == shared, id + " duplicated the selected track texture");
                HomeCheck(trackIndex == 3 || (texture.width <= 1024 && texture.height <= 1024), id + " exceeds the new menu texture budget");
                report.AppendLine(id + ": " + expected + " | " + texture.width + "x" + texture.height
                    + " | entity " + texture.GetEntityId() + " | visible " + HomeVisible(background));
            }
            File.WriteAllText(Path.Combine(folder, name + "_menu_theme.txt"), report.ToString());
        }

        void MenuLayoutReport(string name, string surface, params string[] controls)
        {
            var page = HomeRoot.Q<VisualElement>(surface);
            HomeCheck(HomeVisible(page), surface + " is missing/hidden at " + name);
            var safe = HomeRoot.Q<VisualElement>("home-safe");
            Rect bounds = safe.worldBound;
            var report = new System.Text.StringBuilder("Screen " + Screen.width + "x" + Screen.height
                + "\nSafe " + bounds + "\nSurface " + surface + " " + page.worldBound + "\n");
            foreach (string id in controls)
            {
                var control = HomeRoot.Q<Button>(id);
                HomeCheck(HomeVisible(control), id + " is missing/hidden at " + name);
                Rect r = control.worldBound;
                HomeCheck(r.xMin >= bounds.xMin - 1f && r.yMin >= bounds.yMin - 1f
                    && r.xMax <= bounds.xMax + 1f && r.yMax <= bounds.yMax + 1f, id + " exceeds safe bounds at " + name);
                report.AppendLine(id + " " + r + " enabled=" + control.enabledInHierarchy);
            }
            page.Query<Label>().ForEach(label => { if (HomeVisible(label)) report.AppendLine("TEXT " + label.text); });
            File.WriteAllText(Path.Combine(folder, name + "_layout.txt"), report.ToString());
        }

        IEnumerator MenuCapture(int trackIndex, string name, string surface, string backgroundKey, params string[] controls)
        {
            yield return new WaitForSecondsRealtime(0.45f);
            MenuThemeReport(trackIndex, name);
            MenuLayoutReport(name, surface, controls);
            HomeCheck(HomeVisible(HomeRoot.Q<VisualElement>("menu-bg-" + backgroundKey)), backgroundKey + " backdrop is hidden");
            yield return Shot(name);
        }

        IEnumerator MenuClose(string surface, string button)
        {
            HomePress(button);
            yield return new WaitForSecondsRealtime(0.4f);
            HomeCheck(!HomeVisible(HomeRoot.Q<VisualElement>(surface)), surface + " did not close through its button");
        }

        void CheckMenuNavigationWallet(int money, int gems)
        {
            HomeCheck(game.DebugMoney == money && game.DebugGemBalance == gems, "menu navigation changed currency balances");
        }

        IEnumerator MenuPages(int trackIndex, string suffix, bool includeShop)
        {
            int money = game.DebugMoney, gems = game.DebugGemBalance;
            string prefix = "menu_" + HomeTrackTheme.Slug(trackIndex) + "_";
            if (includeShop)
            {
                HomePress("home-shop");
                yield return MenuCapture(trackIndex, prefix + "shop_" + suffix, "home-shop-modal", "shop", "home-shop-close");
                yield return MenuClose("home-shop-modal", "home-shop-close");
            }
            HomePress("home-map");
            yield return MenuCapture(trackIndex, prefix + "map_" + suffix, "menu-map", "map", "map-back");
            HomeCheck(game.DebugUiPage == GameUI.Page.Map, "MAP did not select the map page");
            yield return MenuClose("menu-map", "map-back");
            HomeCheck(game.DebugUiPage == GameUI.Page.Hub, "map back did not return home");
            HomePress("home-hero");
            yield return MenuCapture(trackIndex, prefix + "heroes_" + suffix, "menu-avatars", "heroes-grid", "avatar-back");
            HomeCheck(game.DebugUiPage == GameUI.Page.Avatars, "HERO did not select the avatar page");
            var top = HomeRoot.Q<VisualElement>("menu-bg-heroes-top");
            var grid = HomeRoot.Q<VisualElement>("menu-bg-heroes-grid");
            HomeCheck(HomeVisible(top) && top.worldBound.yMax < grid.worldBound.yMin,
                "hero menu backgrounds cover the open center used by the 3D preview");
            yield return MenuClose("menu-avatars", "avatar-back");
            HomeCheck(game.DebugUiPage == GameUI.Page.Hub, "avatar back did not return home");
            CheckMenuNavigationWallet(money, gems);
        }

        IEnumerator MenuPopups(int trackIndex)
        {
            string prefix = "menu_" + HomeTrackTheme.Slug(trackIndex) + "_";
            foreach (string key in new[] { "settings", "quests", "chest" })
            {
                HomePress("home-" + key);
                string surface = "home-" + key + "-modal";
                string close = "home-" + key + (key == "chest" ? "-claim" : "-close");
                yield return MenuCapture(trackIndex, prefix + key, surface, key, close);
                yield return MenuClose(surface, close);
            }
            game.DebugLogin(3);
            yield return MenuCapture(trackIndex, prefix + "login", "menu-login", "login", "menu-login-close");
            yield return MenuClose("menu-login", "menu-login-close");
            game.DebugOffer(0);
            yield return MenuCapture(trackIndex, prefix + "offer", "menu-offer", "offer", "menu-offer-close");
            HomeRoot.Q<VisualElement>("menu-offer").Query<Button>(className: "purchase-unavailable")
                .ForEach(button => HomeCheck(!button.enabledInHierarchy, "preview offer enabled a purchase"));
            yield return MenuClose("menu-offer", "menu-offer-close");
            game.DebugPartUnlock(1);
            yield return MenuCapture(trackIndex, prefix + "part", "menu-part", "part", "menu-part-close");
            yield return MenuClose("menu-part", "menu-part-close");
            // Unlimited test launches would auto-dismiss this surface; test the ordinary quota path.
            game.debugUnlimitedLaunches = false;
            game.DebugNoLaunches();
            yield return MenuCapture(trackIndex, prefix + "launches", "menu-launches", "launches", "menu-launches-close");
            yield return MenuClose("menu-launches", "menu-launches-close");
            game.DebugRestoreLaunches();
            game.debugUnlimitedLaunches = true;

            game.DebugAim();
            yield return new WaitForSecondsRealtime(0.4f);
            game.DebugPause(true);
            yield return MenuCapture(trackIndex, prefix + "pause", "menu-pause", "pause", "menu-pause-resume");
            HomeCheck(game.DebugPaused, "pause modal did not pause the game");
            yield return MenuClose("menu-pause", "menu-pause-resume");
            HomeCheck(!game.DebugPaused && game.DebugUiPage == GameUI.Page.Aim, "resume did not restore aim");

            // A normal crash reaches Result without unlocking/changing the selected track.
            game.DebugLaunch(1f);
            yield return new WaitForSecondsRealtime(0.3f);
            game.DebugCrash();
            float resultDeadline = Time.realtimeSinceStartup + 12f;
            while (game.DebugUiPage != GameUI.Page.Result && Time.realtimeSinceStartup < resultDeadline) yield return null;
            HomeCheck(game.DebugUiPage == GameUI.Page.Result, "crash did not reach the result menu in time");
            yield return MenuCapture(trackIndex, prefix + "result", "menu-result", "result", "menu-result-continue");
            yield return MenuClose("menu-result", "menu-result-continue");
            HomeCheck(game.DebugUiPage == GameUI.Page.Hub, "result continue did not return home");

            game.DebugChest();
            yield return new WaitForSecondsRealtime(0.4f);
            game.DebugTripleChest();
            yield return MenuCapture(trackIndex, prefix + "ad", "menu-ad", "ad");
            yield return new WaitForSecondsRealtime(3.2f);
            HomeCheck(!HomeVisible(HomeRoot.Q<VisualElement>("menu-ad")), "test ad overlay failed to dismiss");
            yield return MenuClose("home-chest-modal", "home-chest-claim");
            HomeFixture(trackIndex: trackIndex); // Restore rewards/result mutations only in the isolated fixture.
            yield return new WaitForSecondsRealtime(0.4f);
        }

        IEnumerator MenuShots()
        {
            HomeCheck(SaveData.Prefix == "autotest.", "menu screenshots require isolated player data");
            HomeCheck(!Store.PurchasesAvailable, "menu preview tests must not enable real purchases");
            for (int trackIndex = 0; trackIndex < TrackLibrary.Count; trackIndex++)
            {
                HomeFixture(trackIndex: trackIndex);
                yield return HomeResize(540, 1170);
                HomePress("home-shop");
                yield return MenuCapture(trackIndex, "menu_" + trackIndex + "_" + HomeTrackTheme.Slug(trackIndex) + "_shop_540x1170",
                    "home-shop-modal", "shop", "home-shop-close");
                HomeRoot.Q<VisualElement>("home-shop-modal").Query<Button>(className: "purchase-unavailable")
                    .ForEach(button => HomeCheck(!button.enabledInHierarchy, "shop preview enabled a purchase"));
                yield return MenuClose("home-shop-modal", "home-shop-close");
            }
            foreach (int trackIndex in new[] { 1, 6 })
            {
                HomeFixture(trackIndex: trackIndex);
                yield return HomeResize(540, 1170);
                yield return MenuPages(trackIndex, "540x1170", false);
                yield return MenuPopups(trackIndex);

                HomePress("home-shop");
                yield return new WaitForSecondsRealtime(0.4f);
                game.DebugShopScroll();
                yield return MenuCapture(trackIndex, "menu_" + HomeTrackTheme.Slug(trackIndex) + "_shop_bottom",
                    "home-shop-modal", "shop", "home-shop-close");
                yield return MenuClose("home-shop-modal", "home-shop-close");

                // Shop also opens over Heroes when an unowned hero costs more gems than the wallet.
                int browse = -1;
                for (int i = 2; i < AvatarLibrary.Count; i++)
                    if (AvatarLibrary.All[i].price > game.DebugGemBalance) { browse = i; break; }
                HomeCheck(browse >= 0, "no unaffordable hero exists for the shop-return fixture");
                game.DebugAvatars(browse);
                yield return new WaitForSecondsRealtime(0.4f);
                int money = game.DebugMoney, gems = game.DebugGemBalance;
                HomePress("avatar-action");
                yield return MenuCapture(trackIndex, "menu_" + HomeTrackTheme.Slug(trackIndex) + "_shop_from_hero",
                    "home-shop-modal", "shop", "home-shop-close");
                yield return MenuClose("home-shop-modal", "home-shop-close");
                HomeCheck(game.DebugUiPage == GameUI.Page.Avatars && HomeVisible(HomeRoot.Q<VisualElement>("menu-bg-heroes-grid")),
                    "closing shop lost the underlying hero page");
                CheckMenuNavigationWallet(money, gems);
                yield return MenuClose("menu-avatars", "avatar-back");

                foreach (int height in new[] { 960, 1200 })
                {
                    yield return HomeResize(540, height);
                    yield return MenuPages(trackIndex, "540x" + height, true);
                }
                yield return HomeResize(540, 1170);
                game.DebugHomeSafeArea(new Rect(0f, 28f, 540f, 1100f));
                yield return new WaitForSecondsRealtime(0.4f);
                yield return MenuPages(trackIndex, "safe_area", true);
                game.DebugHomeSafeArea(null);
            }
            foreach (int trackIndex in new[] { 3, 1, 6, 3 })
            {
                HomeFixture(trackIndex: trackIndex);
                yield return HomeResize(540, 1170);
                MenuThemeReport(trackIndex, "menu_cycle_" + HomeTrackTheme.Slug(trackIndex));
            }
            HomePress("home-shop");
            yield return MenuCapture(3, "menu_cycle_winter_restored", "home-shop-modal", "shop", "home-shop-close");
            yield return MenuClose("home-shop-modal", "home-shop-close");
            Debug.Log("[MENU THEME TEST] PASS: seven shops, all 14 shared-background bindings, forest/Mars menus and popups, short/tall/safe layouts, hero-shop return, pause/resume, winter restoration and isolated player data.");
        }

        IEnumerator HomeShots()
        {
            HomeCheck(SaveData.Prefix == "autotest.", "real player save must never be used");
            HomeFixture();
            foreach (int height in new[] { 1170, 960, 1200 })
            {
                yield return HomeResize(540, height);
                string name = "home_540x" + height;
                HomeLayoutReport(name);
                yield return Shot(name);
            }
            yield return HomeResize(540, 1170);
            game.DebugHomeSafeArea(new Rect(0f, 28f, 540f, 1100f));
            yield return new WaitForSecondsRealtime(0.4f);
            HomeLayoutReport("home_safe_area");
            yield return Shot("home_safe_area");
            game.DebugHomeSafeArea(null);
            yield return new WaitForSecondsRealtime(0.3f);

            HomeFixture(money: 1234567, gems: 12345);
            yield return new WaitForSecondsRealtime(0.5f);
            HomeLayoutReport("home_large_wallet");
            HomeCheck(game.DebugMoney == 1234567 && game.DebugGemBalance == 12345,
                "compact wallet display must preserve exact currency balances");
            yield return Shot("home_large_wallet");
            HomeFixture();
            yield return new WaitForSecondsRealtime(0.4f);

            // Upgrade through the same registered Button callbacks that touch/keyboard input uses.
            int oldLevel = game.DebugUpgradeLevel(0), oldMoney = game.DebugMoney;
            int coinPrice = Upgrades.Cost(0, oldLevel);
            HomePress("home-upgrade-0");
            yield return new WaitForSecondsRealtime(0.25f);
            HomeCheck(game.DebugUpgradeLevel(0) == oldLevel + 1 && game.DebugMoney == oldMoney - coinPrice,
                "coin upgrade callback did not apply the live cost/level");
            yield return Shot("home_coin_upgrade");

            HomeFixture(money: 0, gems: 100);
            yield return new WaitForSecondsRealtime(0.3f);
            int gemPrice = Upgrades.GemCost(0, game.DebugUpgradeLevel(0));
            yield return Shot("home_gem_fallback");
            HomePress("home-upgrade-0");
            yield return new WaitForSecondsRealtime(0.2f);
            HomeCheck(game.DebugUpgradeLevel(0) == 9 && game.DebugMoney == 0 && game.DebugGemBalance == 100 - gemPrice,
                "gem fallback did not charge the live gem price");

            HomeFixture(money: 0, gems: 0);
            yield return new WaitForSecondsRealtime(0.3f);
            HomePress("home-upgrade-0", true);
            yield return new WaitForSecondsRealtime(0.3f);
            HomeCheck(game.DebugUpgradeLevel(0) == 8 && game.DebugMoney == 0 && game.DebugGemBalance == 0,
                "insufficient funds must not buy an upgrade");
            yield return Shot("home_insufficient_funds");

            int max = Upgrades.MaxFor(3);
            HomeFixture(new[] { max, max, max, max, max });
            yield return new WaitForSecondsRealtime(0.3f);
            HomePress("home-upgrade-0", true);
            yield return new WaitForSecondsRealtime(0.2f);
            HomeCheck(game.DebugUpgradeLevel(0) == max && game.DebugMoney == 2625 && game.DebugGemBalance == 7,
                "MAX upgrade must not charge currency");
            yield return Shot("home_max_upgrades");

            HomeFixture();
            yield return new WaitForSecondsRealtime(0.4f);
            HomePress("home-map");
            yield return new WaitForSecondsRealtime(0.25f);
            HomeCheck(game.DebugUiPage == GameUI.Page.Map, "MAP callback did not open map");
            game.DebugHub();
            yield return new WaitForSecondsRealtime(0.35f);
            HomePress("home-track");
            yield return new WaitForSecondsRealtime(0.3f);
            HomeCheck(game.DebugUiPage == GameUI.Page.Map, "track plaque callback did not open map");
            game.DebugHub();
            yield return new WaitForSecondsRealtime(0.35f);
            HomePress("home-hero");
            yield return new WaitForSecondsRealtime(0.25f);
            HomeCheck(game.DebugUiPage == GameUI.Page.Avatars, "HERO callback did not open avatars");
            game.DebugHub();
            yield return new WaitForSecondsRealtime(0.35f);

            foreach (string walletButton in new[] { "home-coins-plus", "home-gems-plus" })
            {
                HomePress(walletButton);
                yield return new WaitForSecondsRealtime(0.3f);
                var shop = HomeRoot.Q<VisualElement>("home-shop-modal");
                HomeCheck(HomeVisible(shop), walletButton + " callback did not open shop");
                HomePress("home-shop-close");
                yield return new WaitForSecondsRealtime(0.3f);
                HomeCheck(!HomeVisible(shop), walletButton + " shop overlay did not close");
            }

            foreach (string panel in new[] { "settings", "shop", "quests", "chest" })
            {
                HomePress("home-" + panel);
                yield return new WaitForSecondsRealtime(0.5f);
                var modal = HomeRoot.Q<VisualElement>("home-" + panel + "-modal");
                HomeCheck(HomeVisible(modal), panel + " callback did not open its overlay");
                yield return Shot("home_nav_" + panel);
                HomePress("home-" + panel + (panel == "chest" ? "-claim" : "-close"));
                yield return new WaitForSecondsRealtime(0.4f);
                HomeCheck(!HomeVisible(modal), panel + " overlay did not close");
            }
            HomePress("home-play");
            yield return new WaitForSecondsRealtime(0.3f);
            HomeCheck(game.DebugUiPage == GameUI.Page.Aim && !game.DebugRunning, "PLAY must enter aim without launching automatically");
            Debug.Log("[HOME TEST] PASS: three portrait sizes, simulated safe area, live coin/gem/MAX/insufficient upgrade paths, navigation and PLAY. Player save untouched.");
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return null;
            Debug.Log("[AUTOSHOT] " + name + " mesafe=" + game.DebugDistance.ToString("F0"));
        }
    }
}
