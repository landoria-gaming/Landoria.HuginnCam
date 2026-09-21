using BepInEx.Configuration;
using BepInEx;
using DronePilot;
using System.IO;
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
        private static ConfigEntry<float> sagaCameraFov;
        private static ConfigEntry<bool> previewTelemetry;
        private static ConfigEntry<bool> captureTelemetry;
        private static ConfigEntry<string> telemetryRoot;
        private static ConfigEntry<float> sampleInterval;
        private static ConfigEntry<float> flushInterval;
        private static ConfigEntry<float> directionThreshold;
        private static ConfigEntry<float> openMaximumHeight;
        private static ConfigEntry<float> openMaximumRadius;
        private static ConfigEntry<float> forestMaximumHeight;
        private static ConfigEntry<float> forestMaximumRadius;
        private static ConfigEntry<float> treeScanRadius;
        private static ConfigEntry<float> treeScanInterval;
        private static ConfigEntry<int> minimumTreeCount;

        internal static KeyboardShortcut CaptureModeShortcut =>
            captureModeShortcut.Value;
        internal static KeyboardShortcut PreviewModeShortcut =>
            previewModeShortcut.Value;
        internal static UnityRuntimeCameraRecorder.RecordingQualityPreset RecordingQuality =>
            recordingQuality.Value;
        internal static int MaximumFrameRate => maximumFrameRate.Value;
        internal static float SagaCameraFOV => sagaCameraFov.Value;
        internal static float OpenMaximumHeight => openMaximumHeight.Value;
        internal static float OpenMaximumOrbitRadius => openMaximumRadius.Value;
        internal static float ForestMaximumHeight => forestMaximumHeight.Value;
        internal static float ForestMaximumOrbitRadius => forestMaximumRadius.Value;
        internal static float TreeScanRadius => treeScanRadius.Value;
        internal static float TreeScanIntervalSeconds => treeScanInterval.Value;
        internal static int MinimumTreeCount => minimumTreeCount.Value;
        internal static string DroneConfigPath => Path.Combine(
            Paths.ConfigPath, "SagaCapture", "drone-config.yaml");

        // Creates per-session diagnostics options from shared BepInEx settings.
        internal static TelemetryOptions CreateTelemetry(bool preview)
        {
            return new TelemetryOptions
            {
                Enabled = preview ? previewTelemetry.Value : captureTelemetry.Value,
                RootDirectory = string.IsNullOrWhiteSpace(telemetryRoot.Value)
                    ? Path.Combine(Paths.ConfigPath, "SagaCapture")
                    : telemetryRoot.Value,
                SampleIntervalSeconds = sampleInterval.Value,
                FlushIntervalSeconds = flushInterval.Value,
                OrbitDirectionThreshold = directionThreshold.Value
            };
        }

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
                UnityRuntimeCameraRecorder.RecordingQualityPreset.Low,
                "Video recording quality: Low, Medium, or High.");
            maximumFrameRate = config.Bind(
                "Recording",
                "MaximumFrameRate",
                30,
                new ConfigDescription(
                    "Maximum recording frame rate, from 30 to 60 FPS.",
                    new AcceptableValueRange<int>(30, 60)));
            sagaCameraFov = config.Bind(
                "Camera",
                "SagaCameraFOV",
                65f,
                new ConfigDescription(
                    "Saga camera vertical field of view, from 40 to 120 degrees.",
                    new AcceptableValueRange<float>(40f, 120f)));
            previewTelemetry = config.Bind("Telemetry", "PreviewEnabled",
                true, "Collect drone diagnostics in PreviewMode.");
            captureTelemetry = config.Bind("Telemetry", "CaptureEnabled",
                false, "Collect drone diagnostics in CaptureMode.");
            telemetryRoot = config.Bind("Telemetry", "RootDirectory", "",
                "Empty uses BepInEx/config/SagaCapture; DronePilot creates Sessions inside it.");
            sampleInterval = config.Bind("Telemetry", "SampleIntervalSeconds",
                0.5f, "Seconds between flight samples.");
            flushInterval = config.Bind("Telemetry", "FlushIntervalSeconds",
                60f, "Seconds between JSON diagnostic files.");
            directionThreshold = config.Bind("Telemetry", "OrbitDirectionThreshold",
                0.05f, "Orbit speed threshold in m/s for stationary direction.");
            openMaximumHeight = config.Bind("PilotProfile.OpenArea", "MaximumHeight",
                8f, "Maximum drone height over terrain, in meters.");
            openMaximumRadius = config.Bind("PilotProfile.OpenArea", "MaximumOrbitRadius",
                8f, "Maximum orbit radius, in meters.");
            forestMaximumHeight = config.Bind("PilotProfile.Forest", "MaximumHeight",
                4f, "Maximum drone height over terrain, in meters.");
            forestMaximumRadius = config.Bind("PilotProfile.Forest", "MaximumOrbitRadius",
                3f, "Maximum orbit radius, in meters.");
            treeScanRadius = config.Bind("PilotProfile.Forest", "TreeScanRadius",
                10f, "Tree density scan radius, in meters.");
            treeScanInterval = config.Bind("PilotProfile.Forest", "TreeScanIntervalSeconds",
                0.5f, "Seconds between tree density scans.");
            minimumTreeCount = config.Bind("PilotProfile.Forest", "MinimumTreeCount",
                2, "Distinct Valheim trees required to select Forest.");
        }

        // Restores the default shortcuts and recreates the configuration file.
        internal static void RestoreDefaults(ConfigFile config)
        {
            captureModeShortcut.Value = new KeyboardShortcut(KeyCode.F8);
            previewModeShortcut.Value = new KeyboardShortcut(
                KeyCode.F8, KeyCode.LeftShift);
            recordingQuality.Value =
                UnityRuntimeCameraRecorder.RecordingQualityPreset.Low;
            maximumFrameRate.Value = 30;
            sagaCameraFov.Value = 65f;
            previewTelemetry.Value = true;
            captureTelemetry.Value = false;
            telemetryRoot.Value = "";
            sampleInterval.Value = 0.5f;
            flushInterval.Value = 60f;
            directionThreshold.Value = 0.05f;
            openMaximumHeight.Value = 8f;
            openMaximumRadius.Value = 8f;
            forestMaximumHeight.Value = 4f;
            forestMaximumRadius.Value = 3f;
            treeScanRadius.Value = 10f;
            treeScanInterval.Value = 0.5f;
            minimumTreeCount.Value = 2;
            config.Save();
            ConfigWatcher.IgnoreCurrentFileVersion();
        }
    }
}
