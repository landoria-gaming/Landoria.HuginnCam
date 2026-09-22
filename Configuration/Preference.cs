using BepInEx.Configuration;
using BepInEx;
using DronePilot;
using DronePilot.Telemetry;
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
        private static ConfigEntry<float> sagaCameraFov;
        private static ConfigEntry<bool> showDroneVisual;
        private static ConfigEntry<DroneShellColor> droneColor;
        private static ConfigEntry<bool> previewTelemetry;
        private static ConfigEntry<bool> captureTelemetry;
        private static ConfigEntry<string> telemetryRoot;
        private static ConfigEntry<float> sampleInterval;

        internal const UnityRuntimeCameraRecorder.RecordingQualityPreset
            RecordingQuality = UnityRuntimeCameraRecorder.RecordingQualityPreset.Low;
        internal const int MaximumFrameRate = 60;
        internal const float OpenMaximumHeight = 8f;
        internal const float OpenMaximumOrbitRadius = 8f;
        internal const float ForestMaximumHeight = 4f;
        internal const float ForestMaximumOrbitRadius = 3f;
        internal const float TreeScanRadius = 10f;
        internal const float TreeScanIntervalSeconds = 0.5f;
        internal const int MinimumTreeCount = 2;
        internal const float FlushIntervalSeconds = 60f;
        internal const float OrbitDirectionThreshold = 0.05f;

        internal static KeyboardShortcut CaptureModeShortcut =>
            captureModeShortcut.Value;
        internal static KeyboardShortcut PreviewModeShortcut =>
            previewModeShortcut.Value;
        internal static float SagaCameraFOV => sagaCameraFov.Value;
        internal static bool ShowDroneVisual => showDroneVisual.Value;
        internal static DroneShellColor ShellColor => droneColor.Value;
        internal static string DroneConfigPath => GetDroneConfigPath();

        // Preserves existing flight settings when adopting the shorter name.
        private static string GetDroneConfigPath()
        {
            string directory = Path.Combine(Paths.ConfigPath, "SagaCapture");
            string path = Path.Combine(directory, "config.yaml");
            string legacy = Path.Combine(directory, "drone-config.yaml");
            if (!File.Exists(path) && File.Exists(legacy))
            {
                File.Copy(legacy, path);
                SagaCapturePlugin.Log?.LogInfo(
                    "Copied drone-config.yaml to config.yaml.");
            }
            return path;
        }

        // Creates per-session diagnostics options from shared BepInEx settings.
        internal static Options CreateTelemetry(bool preview)
        {
            return new Options
            {
                Enabled = preview ? previewTelemetry.Value : captureTelemetry.Value,
                RootDirectory = string.IsNullOrWhiteSpace(telemetryRoot.Value)
                    ? Path.Combine(Paths.ConfigPath, "SagaCapture")
                    : telemetryRoot.Value,
                SampleIntervalSeconds = sampleInterval.Value,
                FlushIntervalSeconds = FlushIntervalSeconds,
                OrbitDirectionThreshold = OrbitDirectionThreshold
            };
        }

        // Creates the user-facing configuration entries.
        internal static void Initialize(ConfigFile config)
        {
            RemoveLegacyConstants(config);
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
            sagaCameraFov = config.Bind(
                "Camera",
                "SagaCameraFOV",
                65f,
                new ConfigDescription(
                    "Saga camera vertical field of view, from 40 to 120 degrees.",
                    new AcceptableValueRange<float>(40f, 120f)));
            showDroneVisual = config.Bind(
                "Camera", "ShowDroneVisual", true,
                "Show the camera-sized glowing drone to the player in CaptureMode. It has no shadow and is hidden from the recording.");
            droneColor = config.Bind(
                "Camera", "DroneColor", DroneShellColor.Metal,
                "Drone shell appearance: Metal (Valheim iron texture) or Yellow (the original solid color).");
            previewTelemetry = config.Bind("Telemetry", "PreviewEnabled",
                true, "Collect drone diagnostics in PreviewMode. " +
                "Enabling diagnostics can noticeably slow down the game.");
            captureTelemetry = config.Bind("Telemetry", "CaptureEnabled",
                false, "Collect drone diagnostics in CaptureMode. " +
                "Enabling diagnostics can noticeably slow down the game.");
            telemetryRoot = config.Bind("Telemetry", "RootDirectory", "",
                "Empty uses BepInEx/config/SagaCapture; DronePilot creates Sessions inside it.");
            sampleInterval = config.Bind("Telemetry", "SampleIntervalSeconds",
                0.5f, new ConfigDescription(
                    "Seconds between flight samples, from 0.1 to 5.",
                    new AcceptableValueRange<float>(0.1f, 5f)));
        }

        // Removes settings that became implementation constants from existing files.
        private static void RemoveLegacyConstants(ConfigFile config)
        {
            bool saveOnConfigSet = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try
            {
                RemoveLegacy(config, "Recording", "Quality",
                    UnityRuntimeCameraRecorder.RecordingQualityPreset.Low);
                RemoveLegacy(config, "Recording", "MaximumFrameRate", 60);
                RemoveLegacy(config, "PilotProfile.OpenArea", "MaximumHeight", 8f);
                RemoveLegacy(config, "PilotProfile.OpenArea", "MaximumOrbitRadius", 8f);
                RemoveLegacy(config, "PilotProfile.Forest", "MaximumHeight", 4f);
                RemoveLegacy(config, "PilotProfile.Forest", "MaximumOrbitRadius", 3f);
                RemoveLegacy(config, "PilotProfile.Forest", "TreeScanRadius", 10f);
                RemoveLegacy(config, "PilotProfile.Forest",
                    "TreeScanIntervalSeconds", 0.5f);
                RemoveLegacy(config, "PilotProfile.Forest", "MinimumTreeCount", 2);
                RemoveLegacy(config, "Telemetry", "FlushIntervalSeconds", 60f);
                RemoveLegacy(config, "Telemetry", "OrbitDirectionThreshold", 0.05f);
            }
            finally
            {
                config.SaveOnConfigSet = saveOnConfigSet;
            }
            config.Save();
        }

        // Removes one former setting from the configuration file.
        private static void RemoveLegacy<T>(ConfigFile config, string section,
            string key, T defaultValue)
        {
            config.Bind(section, key, defaultValue);
            config.Remove(new ConfigDefinition(section, key));
        }

        // Restores the default shortcuts and recreates the configuration file.
        internal static void RestoreDefaults(ConfigFile config)
        {
            captureModeShortcut.Value = new KeyboardShortcut(KeyCode.F8);
            previewModeShortcut.Value = new KeyboardShortcut(
                KeyCode.F8, KeyCode.LeftShift);
            sagaCameraFov.Value = 65f;
            showDroneVisual.Value = true;
            droneColor.Value = DroneShellColor.Metal;
            previewTelemetry.Value = true;
            captureTelemetry.Value = false;
            telemetryRoot.Value = "";
            sampleInterval.Value = 0.5f;
            config.Save();
            Landoria.Shared.ConfigWatcher.IgnoreCurrentFileVersion();
        }
    }
}
