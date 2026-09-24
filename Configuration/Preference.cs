using BepInEx.Configuration;
using BepInEx;
using System.IO;
using Landoria.Shared;
using UnityEngine;

namespace Landoria.SagaCapture
{
    // Stores and restores the Saga Capture controls.
    internal static class Preference
    {
        private static ConfigEntry<KeyboardShortcut> captureModeShortcut;
        private static ConfigEntry<string> cameraFrameRate;
        private static ConfigEntry<bool> showFrameRate;
        private static ConfigEntry<
            UnityRuntimeCameraRecorder.RecordingQualityPreset> recordingQuality;
        private static ConfigEntry<bool> gameplayIncludeUi;
        private static ConfigEntry<float> minimumShotDuration;
        private static ConfigEntry<float> maximumShotDuration;

        internal const float TreeScanRadius = 10f;
        internal const float TreeScanIntervalSeconds = 0.5f;
        internal const int MinimumTreeCount = 2;

        internal static KeyboardShortcut CaptureModeShortcut =>
            captureModeShortcut.Value;
        internal static UnityRuntimeCameraRecorder.RecordingQualityPreset
            RecordingQuality => recordingQuality.Value;
        internal static int CameraMaximumFrameRate =>
            cameraFrameRate.Value == "30" ? 30 : 60;
        internal static bool LimitGameFrameRate =>
            cameraFrameRate.Value != "SameAsGame";
        internal static bool ShowFrameRate => showFrameRate.Value;
        internal static bool GameplayIncludeUi => gameplayIncludeUi.Value;
        internal static float MinimumShotDuration => minimumShotDuration.Value;
        internal static float MaximumShotDuration => Mathf.Max(
            MinimumShotDuration, maximumShotDuration.Value);
        internal static string CameraOperatorConfigPath => Path.Combine(
            Paths.ConfigPath, "SagaCapture", "config.yaml");

        // Creates the user-facing configuration entries.
        internal static void Initialize(ConfigFile config)
        {
            captureModeShortcut = config.Bind(
                "Controls",
                "CaptureModeShortcut",
                new KeyboardShortcut(KeyCode.F8),
                "Shortcut used to enter or leave CaptureMode.\n" +
                "\nhttps://docs.unity3d.com/ScriptReference/KeyCode.html");
            cameraFrameRate = config.Bind(
                "CinematicCameraRendering", "MaximumFrameRate", "60",
                new ConfigDescription(
                    "Capture frame rate: 30, 60, or SameAsGame. " +
                    "SameAsGame preserves the game's VSync and frame-rate " +
                    "limit while recording video at up to 60 FPS.",
                    new AcceptableValueList<string>(
                        "30", "60", "SameAsGame")));
            showFrameRate = config.Bind(
                "CinematicCameraRendering", "ShowFrameRate", false,
                "Show the cinematic-camera frame rate in the top-left corner.");
            recordingQuality = config.Bind(
                "Recording", "Quality",
                UnityRuntimeCameraRecorder.RecordingQualityPreset.Medium,
                "Recording quality: Low, Medium, High, or Highest. " +
                "Lower quality reduces file size and encoding load.");
            gameplayIncludeUi = config.Bind(
                "GameplayCamera", "IncludeUI", true,
                "Include Valheim's interface in gameplay-camera shots. " +
                "When false, capture occurs before overlay UI is rendered.");
            InitializeDirector(config);
        }

        // Creates the user-facing shot direction settings.
        private static void InitializeDirector(ConfigFile config)
        {
            minimumShotDuration = config.Bind(
                "Director", "MinimumShotDuration", 8f,
                new ConfigDescription("Minimum shot duration in seconds.",
                    new AcceptableValueRange<float>(1f, 60f)));
            maximumShotDuration = config.Bind(
                "Director", "MaximumShotDuration", 15f,
                new ConfigDescription("Maximum shot duration in seconds.",
                    new AcceptableValueRange<float>(1f, 120f)));
        }

        // Restores the default shortcuts and recreates the configuration file.
        internal static void RestoreDefaults(ConfigFile config)
        {
            captureModeShortcut.Value = new KeyboardShortcut(KeyCode.F8);
            cameraFrameRate.Value = "60";
            showFrameRate.Value = false;
            recordingQuality.Value =
                UnityRuntimeCameraRecorder.RecordingQualityPreset.Medium;
            gameplayIncludeUi.Value = true;
            minimumShotDuration.Value = 8f;
            maximumShotDuration.Value = 15f;
            config.Save();
            Landoria.Shared.ConfigWatcher.IgnoreCurrentFileVersion();
        }
    }
}
