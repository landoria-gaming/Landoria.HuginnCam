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
        private static ConfigEntry<int> cameraMaximumFrameRate;
        private static ConfigEntry<bool> gameplayIncludeUi;

        internal const float OpenMaximumHeight = 8f;
        internal const float ForestMaximumHeight = 4f;
        internal const float TreeScanRadius = 10f;
        internal const float TreeScanIntervalSeconds = 0.5f;
        internal const int MinimumTreeCount = 2;

        internal static KeyboardShortcut CaptureModeShortcut =>
            captureModeShortcut.Value;
        internal const UnityRuntimeCameraRecorder.RecordingQualityPreset
            RecordingQuality =
                UnityRuntimeCameraRecorder.RecordingQualityPreset.Medium;
        internal static int CameraMaximumFrameRate =>
            cameraMaximumFrameRate.Value;
        internal static bool GameplayIncludeUi => gameplayIncludeUi.Value;
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
            cameraMaximumFrameRate = config.Bind(
                "CinematicCameraRendering", "MaximumFrameRate", 60,
                new ConfigDescription(
                    "Frame rate shared by CaptureMode, the " +
                    "Unity game loop, and the video recorder: 30 or 60 FPS. " +
                    "VSync is temporarily disabled so Unity does not render " +
                    "more frames than the recorder accepts.",
                    new AcceptableValueList<int>(30, 60)));
            gameplayIncludeUi = config.Bind(
                "GameplayCamera", "IncludeUI", true,
                "Include Valheim's interface in gameplay-camera shots. " +
                "When false, capture occurs before overlay UI is rendered.");
        }

        // Restores the default shortcuts and recreates the configuration file.
        internal static void RestoreDefaults(ConfigFile config)
        {
            captureModeShortcut.Value = new KeyboardShortcut(KeyCode.F8);
            cameraMaximumFrameRate.Value = 60;
            gameplayIncludeUi.Value = true;
            config.Save();
            Landoria.Shared.ConfigWatcher.IgnoreCurrentFileVersion();
        }
    }
}
