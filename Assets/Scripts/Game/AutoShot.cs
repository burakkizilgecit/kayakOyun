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

        public static void AttachIfRequested(GameController game)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-autoshot") continue;
                var shot = game.gameObject.AddComponent<AutoShot>();
                shot.game = game;
                shot.folder = args[i + 1];
                Directory.CreateDirectory(shot.folder);
                return;
            }
        }

        IEnumerator Start()
        {
            PlayerPrefs.DeleteAll();
            float t;

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
            t = 0f;
            while (!game.DebugInResult && !game.DebugRocketFiring && t < 90f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.3f);
            yield return Shot("6_rocket");
            t = 0f;
            while (!game.DebugInResult && t < 120f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(2.5f);
            yield return Shot("7_finish");

            // Çarpma: pistin ortasında kızak durdurulur; karakter fırlar, yuvarlanır, oturup kahkaha atar.
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
            yield return new WaitForSeconds(1.3f);
            yield return Shot("7d_crash_laugh");

            // Orman kademesi tam: ana ekranda kanat + roket + yeni bardak
            game.DebugReset(new[] { 16, 16, 12, 16, 16 }, 5000);
            game.DebugTrack(1, 1);
            yield return new WaitForSeconds(1.5f);
            yield return Shot("8_forest_hub");
            game.DebugAim();
            yield return new WaitForSeconds(0.5f);
            game.DebugLaunch(1f);
            t = 0f;
            while (!game.DebugInResult && game.DebugDistance < 680f && t < 90f) { t += Time.deltaTime; yield return null; }
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

            PlayerPrefs.DeleteAll();
            Application.Quit();
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
