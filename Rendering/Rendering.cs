using UnityEngine;

namespace Landoria.SagaCapture
{
    // Resolves the current game rendering state for the cinematic camera.
    internal static class SagaCaptureRendering
    {
        // Returns the active Valheim graphics settings without overriding them.
        internal static GraphicsSettingsState GetGraphicsSettings()
        {
            return GraphicsSettingsManager.Instance.ActiveSettings;
        }

        // Uses the current game resolution and matching texture filter.
        internal static void GetResolution(GraphicsSettingsState settings,
            out int width, out int height, out FilterMode filterMode)
        {
            width = Screen.width;
            height = Screen.height;
            filterMode = settings.m_upscalingAlgorithm ==
                         UpscalingAlgorithm.NearestNeighbor
                ? FilterMode.Point
                : FilterMode.Bilinear;
        }
    }
}
