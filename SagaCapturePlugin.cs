using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Landoria.Shared;
using UnityEngine;

namespace Landoria.SagaCapture
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Provides the entry point for the Saga Capture plugin.
    public sealed class SagaCapturePlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "Landoria.SagaCapture";
        private const string PluginName = "Landoria.SagaCapture";
        private const string PluginVersion = "1.0.0";
        private ShotDirector _shotDirector;
        private InterruptedRecordingRecovery _recordingRecovery;
        private Harmony _harmony;
        private static SagaCapturePlugin _instance;
        internal static ManualLogSource Log { get; private set; }
        internal static bool IsCinematicCameraActive =>
            _instance?._shotDirector?.IsCameraActive == true;
        internal static bool IsCinematicImageActive =>
            _instance?._shotDirector?.IsCinematicImageActive == true;

        // Stops an active recording before a menu exit action continues.
        internal static void StopRecordingForMenuExit()
        {
            if (_instance?._shotDirector?.IsActive == true)
            {
                Log.LogInfo("Stopping recording before leaving the game.");
                _instance._shotDirector.StopRecording();
            }
        }

        // Initializes the plugin logging.
        private void Awake()
        {
            Log = Logger;
            _instance = this;
            Logger.LogInfo($"AssemblyVersion: {GetType().Assembly.GetName().Version}.");
            Preference.Initialize(Config);
            ConfigWatcher.Initialize(
                Config, Logger, "Saga Capture",
                () => Preference.RestoreDefaults(Config));
            _shotDirector = gameObject.AddComponent<ShotDirector>();
            _recordingRecovery =
                gameObject.AddComponent<InterruptedRecordingRecovery>();
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            Log.LogInfo($"{PluginName} {PluginVersion} is loaded.");
        }

        // Reloads configuration and handles the recording control.
        private void Update()
        {
            ConfigWatcher.Update();
            bool captureKeyPressed =
                IsMainKeyDown(Preference.CaptureModeShortcut);
            if (_shotDirector.IsActive)
            {
                if (captureKeyPressed)
                {
                    _shotDirector.StopRecording();
                }
                return;
            }

            if (IsShortcutDown(Preference.CaptureModeShortcut))
            {
                _shotDirector.StartRecording();
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

        // Treats either Shift key as the configured Shift modifier.
        private static bool IsModifierDown(KeyCode modifier)
        {
            if (modifier == KeyCode.LeftShift ||
                modifier == KeyCode.RightShift)
            {
                return ZInput.GetKey(KeyCode.LeftShift) ||
                       ZInput.GetKey(KeyCode.RightShift);
            }

            return ZInput.GetKey(modifier);
        }

        // Checks only the main key so active modes share their exit control.
        private static bool IsMainKeyDown(KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None &&
                   ZInput.GetKeyDown(shortcut.MainKey);
        }

        // Logs plugin shutdown when BepInEx unloads the assembly.
        private void OnDestroy()
        {
            ConfigWatcher.Dispose();
            _harmony?.UnpatchSelf();
            _harmony = null;
            _shotDirector?.Shutdown();
            _shotDirector = null;
            _recordingRecovery?.Shutdown();
            _recordingRecovery = null;
            Log?.LogInfo($"{PluginName} {PluginVersion} is unloaded.");
            Log = null;
            _instance = null;
        }
    }
}
