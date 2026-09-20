using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Landoria.Shared;
using HarmonyLib;
using UnityEngine;

namespace Landoria.HuginnCam
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Provides the entry point for the autonomous Huginn camera recorder.
    public sealed class HuginnCamPlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "Landoria.HuginnCam";
        private const string PluginName = "Landoria.HuginnCam";
        private const string PluginVersion = "1.0.0";
        private HuginnCamController _cameraController;
        private RecordingController _recordingController;
        private Harmony _harmony;

        internal static ManualLogSource Log { get; private set; }

        // Initializes the plugin logging.
        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo($"AssemblyVersion: {GetType().Assembly.GetName().Version}.");
            Preference.Initialize(Config);
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            ConfigWatcher.Initialize(
                Config,
                Logger,
                "Huginn Cam",
                () => Preference.RestoreDefaults(Config));
            _cameraController = gameObject.AddComponent<HuginnCamController>();
            _recordingController = gameObject.AddComponent<RecordingController>();
            Log.LogInfo($"{PluginName} {PluginVersion} is loaded.");
        }

        // Reloads configuration and handles the recording shortcut.
        private void Update()
        {
            ConfigWatcher.Update();
            bool exitRequested = ZInput.GetKeyDown(KeyCode.Escape) ||
                                 IsMainKeyDown(Preference.RecordingShortcut);
            if (_cameraController.IsActive)
            {
                if (exitRequested)
                {
                    _cameraController.ToggleCamera();
                }

                return;
            }

            if (_recordingController.IsActive)
            {
                if (exitRequested)
                {
                    _recordingController.StopRecording();
                }

                return;
            }

            if (IsShortcutDown(Preference.HuginnCamShortcut))
            {
                _cameraController.ToggleCamera();
            }
            else if (IsShortcutDown(Preference.RecordingShortcut))
            {
                _recordingController.StartRecording();
            }
        }

        // Checks the configured shortcut and all its modifiers.
        private static bool IsShortcutDown(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None ||
                !ZInput.GetKeyDown(shortcut.MainKey))
            {
                return false;
            }

            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!IsModifierDown(modifier))
                {
                    return false;
                }
            }

            return true;
        }

        // Treats either Shift key as the default Huginn camera modifier.
        private static bool IsModifierDown(KeyCode modifier)
        {
            if (modifier == KeyCode.LeftShift || modifier == KeyCode.RightShift)
            {
                return ZInput.GetKey(KeyCode.LeftShift) ||
                       ZInput.GetKey(KeyCode.RightShift);
            }

            return ZInput.GetKey(modifier);
        }

        // Checks only the main key so every active mode can be exited.
        private static bool IsMainKeyDown(KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None &&
                   ZInput.GetKeyDown(shortcut.MainKey);
        }

        // Releases plugin resources when BepInEx unloads the plugin.
        private void OnDestroy()
        {
            ConfigWatcher.Dispose();
            _harmony?.UnpatchSelf();
            _harmony = null;
            _recordingController?.Shutdown();
            _recordingController = null;
            _cameraController?.Shutdown();
            _cameraController = null;
            Log?.LogInfo($"{PluginName} {PluginVersion} is unloaded.");

            Log = null;
        }
    }
}
