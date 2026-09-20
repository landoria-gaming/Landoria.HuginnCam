using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Landoria.Shared;
using UnityEngine;

namespace Landoria.HuginnCam
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Provides the entry point for the autonomous cinematic camera recorder.
    public sealed class HuginnCamPlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "Landoria.HuginnCam";
        private const string PluginName = "Landoria.HuginnCam";
        private const string PluginVersion = "1.0.0";
        private bool _isRecording;

        internal static ManualLogSource Log { get; private set; }

        // Initializes the plugin logging.
        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo($"AssemblyVersion: {GetType().Assembly.GetName().Version}.");
            Preference.Initialize(Config);
            ConfigWatcher.Initialize(
                Config,
                Logger,
                "Huginn Cam",
                () => Preference.RestoreDefaults(Config));
            Log.LogInfo($"{PluginName} {PluginVersion} is loaded.");
        }

        // Reloads configuration and handles the recording shortcut.
        private void Update()
        {
            ConfigWatcher.Update();
            if (IsRecordingShortcutDown())
            {
                ToggleRecording();
            }
        }

        // Checks the configured shortcut and all its modifiers.
        private static bool IsRecordingShortcutDown()
        {
            KeyboardShortcut shortcut = Preference.RecordingShortcut;
            if (shortcut.MainKey == KeyCode.None ||
                !ZInput.GetKeyDown(shortcut.MainKey))
            {
                return false;
            }

            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!ZInput.GetKey(modifier))
                {
                    return false;
                }
            }

            return true;
        }

        // Toggles the placeholder recording state.
        private void ToggleRecording()
        {
            _isRecording = !_isRecording;
            Log.LogInfo(_isRecording
                ? "Video recording requested."
                : "Video recording stop requested.");
        }

        // Releases plugin resources when BepInEx unloads the plugin.
        private void OnDestroy()
        {
            ConfigWatcher.Dispose();
            Log?.LogInfo($"{PluginName} {PluginVersion} is unloaded.");

            Log = null;
        }
    }
}
