using BepInEx.Configuration;
using Landoria.Shared;
using UnityEngine;

namespace Landoria.HuginnCam
{
    // Stores and restores the Huginn Cam configuration.
    internal static class Preference
    {
        private static ConfigEntry<KeyboardShortcut> recordingShortcut;
        private static ConfigEntry<KeyboardShortcut> huginnCamShortcut;
        private static ConfigEntry<bool> enableRavenCalls;
        private static ConfigEntry<UnityRuntimeCameraRecorder.RecordingQualityPreset>
            recordingQuality;
        private static ConfigEntry<int> maximumFrameRate;

        internal static KeyboardShortcut RecordingShortcut => recordingShortcut.Value;
        internal static KeyboardShortcut HuginnCamShortcut => huginnCamShortcut.Value;
        internal static bool RavenCallsEnabled => enableRavenCalls.Value;
        internal static UnityRuntimeCameraRecorder.RecordingQualityPreset RecordingQuality =>
            recordingQuality.Value;
        internal static int MaximumFrameRate => maximumFrameRate.Value;

        // Creates the saved configuration entries used by the mod.
        internal static void Initialize(ConfigFile config)
        {
            recordingShortcut = config.Bind(
                "Controls",
                "RecordingShortcut",
                new KeyboardShortcut(KeyCode.F8),
                "Shortcut used to start or stop video recording.\n" +
                "\nhttps://docs.unity3d.com/ScriptReference/KeyCode.html");
            huginnCamShortcut = config.Bind(
                "Controls",
                "HuginnCamShortcut",
                new KeyboardShortcut(KeyCode.F8, KeyCode.LeftShift),
                "Shortcut used to enter or leave the Huginn camera.\n" +
                "\nhttps://docs.unity3d.com/ScriptReference/KeyCode.html");
            enableRavenCalls = config.Bind(
                "Audio",
                "EnableRavenCalls",
                true,
                "Play Huginn's raven calls while the camera is active.");
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
                    "Maximum recording frame rate, up to 60 FPS.",
                    new AcceptableValueRange<int>(1, 60)));
        }

        // Restores every setting and recreates the configuration file.
        internal static void RestoreDefaults(ConfigFile config)
        {
            recordingShortcut.Value = new KeyboardShortcut(KeyCode.F8);
            huginnCamShortcut.Value = new KeyboardShortcut(
                KeyCode.F8, KeyCode.LeftShift);
            enableRavenCalls.Value = true;
            recordingQuality.Value =
                UnityRuntimeCameraRecorder.RecordingQualityPreset.Medium;
            maximumFrameRate.Value = 30;
            config.Save();
            ConfigWatcher.IgnoreCurrentFileVersion();
        }
    }
}
