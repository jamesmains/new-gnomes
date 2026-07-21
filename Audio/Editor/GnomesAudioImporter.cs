#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GNOMES.Audio.Editor
{
    public class GnomesAudioImporter : AssetPostprocessor
    {
        // This automatically runs whenever any audio asset is imported or re-imported
        private void OnPreprocessAudio()
        {
            AudioImporter audioImporter = (AudioImporter)assetImporter;

            // Optional: Only apply this to tracks in your music folder, or blanket apply to long files
            // For example, if the file is likely a music track (longer than 10-15 seconds)
            if (assetPath.Contains("Music") || assetPath.Contains("Soundtracks"))
            {
                // Force the settings that eliminate the main-thread stutter
                audioImporter.loadInBackground = true;
                
                // Best practice for long music tracks to prevent high RAM usage
                AudioImporterSampleSettings settings = audioImporter.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                audioImporter.defaultSampleSettings = settings;
                
                Debug.Log($"[Gnomes Audio] Automatically optimized import settings for background music: {assetPath}");
            }
        }
    }
}
#endif