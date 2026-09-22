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
        private SagaCaptureController _cameraController;
        private RecordingController _recordingController;
        private InterruptedRecordingRecovery _recordingRecovery;
        private Harmony _harmony;
        private static SagaCapturePlugin _instance;
        internal static ManualLogSource Log { get; private set; }
        internal static bool IsPreviewModeActive =>
            _instance?._cameraController?.IsActive == true;
        internal static bool IsDroneCameraActive =>
            IsPreviewModeActive ||
            _instance?._recordingController?.IsCameraActive == true;

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
            _cameraController = gameObject.AddComponent<SagaCaptureController>();
            _recordingController = gameObject.AddComponent<RecordingController>();
            _recordingRecovery =
                gameObject.AddComponent<InterruptedRecordingRecovery>();
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            Log.LogInfo($"{PluginName} {PluginVersion} is loaded.");
        }

        // Reloads configuration and handles camera and recording controls.
        private void Update()
        {
            ConfigWatcher.Update();
            bool escapePressed = ZInput.GetKeyDown(KeyCode.Escape);
            bool captureKeyPressed =
                IsMainKeyDown(Preference.CaptureModeShortcut);
            if (_cameraController.IsActive)
            {
                if (escapePressed)
                {
                    SagaCaptureMenuPatch.SuppressThisFrame();
                }
                if (escapePressed || captureKeyPressed)
                {
                    _cameraController.ToggleCamera();
                }
                return;
            }

            if (_recordingController.IsActive)
            {
                if (captureKeyPressed)
                {
                    _recordingController.StopRecording();
                }
                return;
            }

            if (IsShortcutDown(Preference.PreviewModeShortcut))
            {
                _cameraController.ToggleCamera();
            }
            else if (IsShortcutDown(Preference.CaptureModeShortcut))
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
            _recordingController?.Shutdown();
            _recordingController = null;
            _recordingRecovery?.Shutdown();
            _recordingRecovery = null;
            _cameraController?.Shutdown();
            _cameraController = null;
            Log?.LogInfo($"{PluginName} {PluginVersion} is unloaded.");
            Log = null;
            _instance = null;
        }
    }
}
