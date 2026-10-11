using System.Collections;
using System.IO;
using UnityEngine;

namespace SledSurfers
{
    /// Otomatik görsel test: oyun "-autoshot <klasör>" ile başlatılırsa kendi kendine fırlatır,
    /// ekran görüntüleri alır ve kapanır. Normal oyunda hiçbir etkisi yoktur.
    public class AutoShot : MonoBehaviour
    {
        GameController game;
        string folder;
        string mode = "";   // "-shots avatars": yalnızca karakter çekimleri (hızlı deneme)

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

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return null;
            Debug.Log("[AUTOSHOT] " + name + " mesafe=" + game.DebugDistance.ToString("F0"));
        }
    }
}
