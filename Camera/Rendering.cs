using System;
using UnityEngine;

namespace Landoria.SagaCapture
{
    // Resolves the rendering state used by recording mode.
    internal static class SagaCaptureRendering
    {
        // Builds the graphics state selected for the cinematic camera.
        internal static GraphicsSettingsState GetGraphicsSettings()
        {
            GraphicsSettingsManager manager = GraphicsSettingsManager.Instance;
            GraphicsSettingsState gameSettings = manager.ActiveSettings;
            GraphicsSettingsState state = gameSettings;
            if (Preference.RecordingGraphicsPreset !=
                CaptureGraphicsPreset.SameAsGame)
            {
                ApplyPreset(manager, ref state);
            }
            return state;
        }

        // Resolves the configured cinematic-camera resolution and filter.
        internal static void GetResolution(GraphicsSettingsState settings,
            out int width, out int height, out FilterMode filterMode)
        {
            GetDimensions(settings.m_target3DResolutionVertical,
                out width, out height);
            filterMode = settings.m_upscalingAlgorithm ==
                         UpscalingAlgorithm.NearestNeighbor
                ? FilterMode.Point
                : FilterMode.Bilinear;
        }

        // Resolves fixed, game, or graphics-preset dimensions.
        private static void GetDimensions(int presetHeight,
            out int width, out int height)
        {
            if (TryGetFixedDimensions(out width, out height))
            {
                return;
            }
            if (Preference.CameraRenderResolution ==
                CameraRenderResolutionPreset.SameAsGame)
            {
                width = Screen.width;
                height = Screen.height;
                return;
            }
            height = ResolvePresetHeight(presetHeight);
            width = Math.Max(2, (height * Screen.width / Screen.height) & ~1);
        }

        // Resolves one fixed camera resolution.
        private static bool TryGetFixedDimensions(out int width, out int height)
        {
            height = Preference.CameraRenderResolution switch
            {
                CameraRenderResolutionPreset.HD720 => 720,
                CameraRenderResolutionPreset.FullHD1080 => 1080,
                CameraRenderResolutionPreset.QHD1440 => 1440,
                CameraRenderResolutionPreset.UHD2160 => 2160,
                _ => 0
            };
            width = height == 0 ? 0 : height * 16 / 9;
            return height != 0;
        }

        // Normalizes the vertical resolution supplied by a Valheim preset.
        private static int ResolvePresetHeight(int height)
        {
            if (height <= 0)
            {
                height = Screen.dpi <= 96f
                    ? Screen.height
                    : Mathf.RoundToInt(Screen.height * 96f / Screen.dpi);
            }
            else if (height == int.MaxValue)
            {
                height = Screen.height;
            }
            return Math.Max(2, height & ~1);
        }

        // Applies the selected Valheim preset to a camera graphics state.
        private static void ApplyPreset(GraphicsSettingsManager manager,
            ref GraphicsSettingsState state)
        {
            GraphicsModeConfiguration config =
                manager.GetCurrentGraphicsModeConfiguration();
            int presetId = Preference.RecordingGraphicsPreset switch
            {
                CaptureGraphicsPreset.VeryLow => 4,
                CaptureGraphicsPreset.Low => 0,
                CaptureGraphicsPreset.Medium => 1,
                _ => 2
            };
            manager.SetGraphicsSettingsFromPreset(
                config, ref state, config.GetPresetByID(presetId), false);
        }
    }
}
