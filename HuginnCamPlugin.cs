using BepInEx;
using BepInEx.Logging;
using UnityMediaRecorder;

namespace Landoria.HuginnCam
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Provides the entry point for the autonomous cinematic camera recorder.
    public sealed class HuginnCamPlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "Landoria.HuginnCam";
        private const string PluginName = "Landoria.HuginnCam";
        private const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log { get; private set; }

        // Initializes the plugin and attaches the recording controller.
        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo($"AssemblyVersion: {GetType().Assembly.GetName().Version}.");
            MediaRecorderLog.Info = Log.LogInfo;
            MediaRecorderLog.Warning = Log.LogWarning;
            MediaRecorderLog.Error = Log.LogError;
            HuginnCamPreference.Initialize(Config);
            gameObject.AddComponent<ScreenRecorder>();
            Log.LogInfo($"{PluginName} {PluginVersion} is loaded.");
        }

        // Releases plugin resources when BepInEx unloads the plugin.
        private void OnDestroy()
        {
            Log?.LogInfo($"{PluginName} {PluginVersion} is unloaded.");

            MediaRecorderLog.Info = null;
            MediaRecorderLog.Warning = null;
            MediaRecorderLog.Error = null;
            Log = null;
        }
    }
}
