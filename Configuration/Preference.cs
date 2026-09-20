using BepInEx.Configuration;
using Landoria.Shared;
using UnityEngine;

namespace Landoria.SagaCapture
{
    // Stores and restores the Saga Capture controls.
    internal static class Preference
    {
        private static ConfigEntry<KeyboardShortcut> captureModeShortcut;
        private static ConfigEntry<KeyboardShortcut> previewModeShortcut;
        private static ConfigEntry<UnityRuntimeCameraRecorder.RecordingQualityPreset>
            recordingQuality;
        private static ConfigEntry<int> maximumFrameRate;

        internal static KeyboardShortcut CaptureModeShortcut =>
            captureModeShortcut.Value;
        internal static KeyboardShortcut PreviewModeShortcut =>
            previewModeShortcut.Value;
        internal static UnityRuntimeCameraRecorder.RecordingQualityPreset RecordingQuality =>
            recordingQuality.Value;
        internal static int MaximumFrameRate => maximumFrameRate.Value;

        // Creates the saved shortcut configuration entries.
        internal static void Initialize(ConfigFile config)
        {
            captureModeShortcut = config.Bind(
                "Controls",
                "CaptureModeShortcut",
                new KeyboardShortcut(KeyCode.F8),
                "Shortcut used to enter or leave CaptureMode.\n" +
                "\nhttps://docs.unity3d.com/ScriptReference/KeyCode.html");
            previewModeShortcut = config.Bind(
                "Controls",
                "PreviewModeShortcut",
                new KeyboardShortcut(KeyCode.F8, KeyCode.LeftShift),
                "Shortcut used to enter or leave PreviewMode.\n" +
                "\nhttps://docs.unity3d.com/ScriptReference/KeyCode.html");
            recordingQuality = config.Bind(
                "Recording",
                "Quality",
                UnityRuntimeCameraRecorder.RecordingQualityPreset.Medium,
                "Video recording quality: Low, Medium, or High.");
            maximumFrameRate = config.Bind(
                "Recording",
                "MaximumFrameRate",
                30,
                new ConfigDescription(
                    "Maximum recording frame rate, from 30 to 60 FPS.",
                    new AcceptableValueRange<int>(30, 60)));
        }

        // Restores the default shortcuts and recreates the configuration file.
        internal static void RestoreDefaults(ConfigFile config)
        {
            captureModeShortcut.Value = new KeyboardShortcut(KeyCode.F8);
            previewModeShortcut.Value = new KeyboardShortcut(
                KeyCode.F8, KeyCode.LeftShift);
            recordingQuality.Value =
                UnityRuntimeCameraRecorder.RecordingQualityPreset.Medium;
            maximumFrameRate.Value = 30;
            config.Save();
            ConfigWatcher.IgnoreCurrentFileVersion();
        }
    }
}
