using BepInEx.Configuration;
using BepInEx;
using DronePilot;
using DronePilot.Telemetry;
using System.Globalization;
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
        private static ConfigEntry<RecordingResolutionPreset> recordingResolution;
        private static ConfigEntry<CaptureGraphicsPreset> captureGraphicsPreset;
        private static ConfigEntry<CameraRenderResolutionPreset>
            cameraRenderResolution;
        private static ConfigEntry<CameraEffectOverride> depthOfFieldOverride;
        private static ConfigEntry<CameraEffectOverride> motionBlurOverride;
        private static ConfigEntry<CameraEffectOverride> bloomOverride;
        private static ConfigEntry<string> sagaCameraFov;
        private static ConfigEntry<bool> showDroneVisual;
        private static ConfigEntry<DroneShellColor> droneColor;
        private static ConfigEntry<bool> previewTelemetry;
        private static ConfigEntry<bool> captureTelemetry;
        private static ConfigEntry<float> sampleInterval;

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
        internal static UnityRuntimeCameraRecorder.RecordingQualityPreset
            RecordingQuality => recordingQuality.Value;
        internal static int MaximumFrameRate => maximumFrameRate.Value;
        internal static RecordingResolutionPreset RecordingResolution =>
            recordingResolution.Value;
        internal static CaptureGraphicsPreset RecordingGraphicsPreset =>
            captureGraphicsPreset.Value;
        internal static CameraRenderResolutionPreset CameraRenderResolution =>
            cameraRenderResolution.Value;
        internal static bool ShowDroneVisual => showDroneVisual.Value;
        internal static DroneShellColor ShellColor => droneColor.Value;
        internal static string DroneConfigPath => GetDroneConfigPath();

        // Applies user overrides after resolving the camera graphics preset.
        internal static void ApplyCameraEffectOverrides(
            ref GraphicsSettingsState settings)
        {
            settings.m_depthOfField = ResolveEffect(
                depthOfFieldOverride.Value, settings.m_depthOfField);
            settings.m_motionBlur = ResolveEffect(
                motionBlurOverride.Value, settings.m_motionBlur);
            settings.m_bloom = ResolveEffect(
                bloomOverride.Value, settings.m_bloom);
        }

        // Resolves one effect override against its preset value.
        private static bool ResolveEffect(CameraEffectOverride value,
            bool presetValue)
        {
            return value == CameraEffectOverride.Enabled ||
                   value == CameraEffectOverride.Preset && presetValue;
        }

        // Resolves SameAsGame or a custom vertical field of view.
        internal static float GetSagaCameraFov(Camera sourceCamera)
        {
            if (string.Equals(sagaCameraFov.Value, "SameAsGame",
                System.StringComparison.OrdinalIgnoreCase))
            {
                return sourceCamera.fieldOfView;
            }
            if (float.TryParse(sagaCameraFov.Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float value))
            {
                return Mathf.Clamp(value, 40f, 120f);
            }
            return sourceCamera.fieldOfView;
        }

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
                RootDirectory = Path.Combine(Paths.ConfigPath, "SagaCapture"),
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
            recordingQuality = config.Bind(
                "VideoEncoding", "Quality",
                UnityRuntimeCameraRecorder.RecordingQualityPreset.Low,
                "Encoder quality preset for the output video: Low, Medium, or High.");
            maximumFrameRate = config.Bind(
                "VideoEncoding", "MaximumFrameRate", 60,
                new ConfigDescription(
                    "Maximum frame rate written to the output video, from 30 to 60 FPS.",
                    new AcceptableValueRange<int>(30, 60)));
            recordingResolution = config.Bind(
                "VideoEncoding", "Resolution",
                RecordingResolutionPreset.SameAsGame,
                "Output video resolution: SameAsGame, " +
                "HD720 (1280x720), FullHD1080 (1920x1080), " +
                "QHD1440 (2560x1440), or UHD2160 (3840x2160).");
            captureGraphicsPreset = config.Bind(
                "CameraRendering", "GraphicsPreset",
                CaptureGraphicsPreset.SameAsGame,
                "Valheim graphics preset used to render the drone camera before " +
                "video encoding: SameAsGame, VeryLow, Low, Medium, or High.");
            cameraRenderResolution = config.Bind(
                "CameraRendering", "Resolution",
                CameraRenderResolutionPreset.SameAsGame,
                "Internal drone-camera resolution: SameAsGame, " +
                "Preset (graphics preset value), " +
                "HD720 (1280x720), " +
                "FullHD1080 (1920x1080), QHD1440 (2560x1440), " +
                "or UHD2160 (3840x2160).");
            depthOfFieldOverride = BindEffectOverride(
                config, "DepthOfField", "depth of field");
            motionBlurOverride = BindEffectOverride(
                config, "MotionBlur", "motion blur");
            bloomOverride = BindEffectOverride(
                config, "Bloom", "bloom");
            sagaCameraFov = config.Bind(
                "CameraRendering",
                "SagaCameraFOV",
                "SameAsGame",
                "Drone-camera vertical field of view: SameAsGame or a number from 40 to 120 degrees.");
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
            sampleInterval = config.Bind("Telemetry", "SampleIntervalSeconds",
                0.5f, new ConfigDescription(
                    "Seconds between flight samples, from 0.1 to 5.",
                    new AcceptableValueRange<float>(0.1f, 5f)));
        }

        // Creates one capture-camera effect override setting.
        private static ConfigEntry<CameraEffectOverride> BindEffectOverride(
            ConfigFile config, string key, string displayName)
        {
            return config.Bind("CameraRendering", key,
                CameraEffectOverride.Preset,
                $"Override camera {displayName}: Preset, Enabled, or Disabled.");
        }

        // Removes settings that became implementation constants from existing files.
        private static void RemoveLegacyConstants(ConfigFile config)
        {
            bool saveOnConfigSet = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try
            {
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
                RemoveLegacy(config, "Telemetry", "RootDirectory", "");
                RemoveLegacy(config, "Recording", "Quality",
                    UnityRuntimeCameraRecorder.RecordingQualityPreset.Low);
                RemoveLegacy(config, "Recording", "MaximumFrameRate", 60);
                RemoveLegacy(config, "Recording", "Resolution",
                    RecordingResolutionPreset.SameAsGame);
                RemoveLegacy(config, "Recording", "GraphicsPreset",
                    CaptureGraphicsPreset.High);
                RemoveLegacy(config, "Recording.VideoEncoding", "Quality",
                    UnityRuntimeCameraRecorder.RecordingQualityPreset.Low);
                RemoveLegacy(config, "Recording.VideoEncoding",
                    "MaximumFrameRate", 60);
                RemoveLegacy(config, "Recording.VideoEncoding", "Resolution",
                    RecordingResolutionPreset.SameAsGame);
                RemoveLegacy(config, "Recording.CameraRendering",
                    "GraphicsPreset", CaptureGraphicsPreset.High);
                RemoveLegacy(config, "Recording.CameraRendering", "Resolution",
                    CameraRenderResolutionPreset.Preset);
                RemoveLegacy(config, "Camera", "SagaCameraFOV", 65f);
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
            recordingQuality.Value =
                UnityRuntimeCameraRecorder.RecordingQualityPreset.Low;
            maximumFrameRate.Value = 60;
            recordingResolution.Value = RecordingResolutionPreset.SameAsGame;
            captureGraphicsPreset.Value = CaptureGraphicsPreset.SameAsGame;
            cameraRenderResolution.Value =
                CameraRenderResolutionPreset.SameAsGame;
            depthOfFieldOverride.Value = CameraEffectOverride.Preset;
            motionBlurOverride.Value = CameraEffectOverride.Preset;
            bloomOverride.Value = CameraEffectOverride.Preset;
            sagaCameraFov.Value = "SameAsGame";
            showDroneVisual.Value = true;
            droneColor.Value = DroneShellColor.Metal;
            previewTelemetry.Value = true;
            captureTelemetry.Value = false;
            sampleInterval.Value = 0.5f;
            config.Save();
            Landoria.Shared.ConfigWatcher.IgnoreCurrentFileVersion();
        }
    }
}
