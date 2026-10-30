using UnityEditor;
using UnityEngine;

namespace SlingRiders.EditorTools
{
    /// Müzik (Resources/Audio): Vorbis, orta kalite, bellekte sıkıştırılmış (60 sn ≈ 0.6 MB; açılışta takılma olmasın diye
    /// arka planda yüklenir). Ses efektleri kodla üretildiği için yalnız müzik dosyası var.
    public class MusicImport : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            ai.loadInBackground = true;
            var s = ai.defaultSampleSettings;
            s.loadType = AudioClipLoadType.CompressedInMemory;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.45f;
            s.preloadAudioData = true;
            ai.defaultSampleSettings = s;
        }
    }
}
