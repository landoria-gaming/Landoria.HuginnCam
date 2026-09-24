using UnityEngine;

namespace Landoria.SagaCapture
{
    // Applies global Unity quality values only while the capture camera renders.
    internal sealed class SagaCaptureQualityOverride : MonoBehaviour
    {
        private GraphicsSettingsState _settings;
        private float _lodBias;
        private int _pixelLightCount;
        private int _shadowCascades;
        private float _shadowDistance;
        private ShadowResolution _shadowResolution;
        private bool _softParticles;
        private bool _tessellation;

        // Stores the capture-specific graphics state.
        internal void Initialize(GraphicsSettingsState settings)
        {
            _settings = settings;
        }

        // Applies the capture quality immediately before camera rendering.
        private void OnPreCull()
        {
            SaveQuality();
            QualitySettings.lodBias = GetLodBias(_settings.m_lod);
            QualitySettings.pixelLightCount = GetLightLimit(_settings.m_lights);
            ApplyShadows(_settings.m_shadowQuality);
            QualitySettings.softParticles = _settings.m_softParticles;
            SetTessellation(_settings.m_tesselation);
        }

        // Restores the gameplay quality immediately after camera rendering.
        private void OnPostRender()
        {
            QualitySettings.lodBias = _lodBias;
            QualitySettings.pixelLightCount = _pixelLightCount;
            QualitySettings.shadowCascades = _shadowCascades;
            QualitySettings.shadowDistance = _shadowDistance;
            QualitySettings.shadowResolution = _shadowResolution;
            QualitySettings.softParticles = _softParticles;
            SetTessellation(_tessellation);
        }

        // Saves the quality values used by the gameplay camera.
        private void SaveQuality()
        {
            _lodBias = QualitySettings.lodBias;
            _pixelLightCount = QualitySettings.pixelLightCount;
            _shadowCascades = QualitySettings.shadowCascades;
            _shadowDistance = QualitySettings.shadowDistance;
            _shadowResolution = QualitySettings.shadowResolution;
            _softParticles = QualitySettings.softParticles;
            _tessellation = Shader.IsKeywordEnabled("TESSELATION_ON");
        }

        // Applies one Valheim shadow-quality level.
        private static void ApplyShadows(int level)
        {
            QualitySettings.shadowCascades = level == 0 ? 2 : level == 1 ? 3 : 4;
            QualitySettings.shadowDistance = level == 0 ? 80f : level == 1 ? 120f : 150f;
            QualitySettings.shadowResolution = level == 0
                ? ShadowResolution.Low
                : level == 1 ? ShadowResolution.Medium : ShadowResolution.High;
        }

        // Converts a Valheim LOD level into Unity's bias value.
        private static float GetLodBias(int level)
        {
            return level == 0 ? 1f : level == 1 ? 1.5f : level == 3 ? 5f : 2f;
        }

        // Converts a Valheim light level into Unity's pixel-light limit.
        private static int GetLightLimit(int level)
        {
            return level == 0 ? 2 : level == 1 ? 4 : 8;
        }

        // Sets Valheim's tessellation shader keyword.
        private static void SetTessellation(bool enabled)
        {
            if (enabled)
            {
                Shader.EnableKeyword("TESSELATION_ON");
            }
            else
            {
                Shader.DisableKeyword("TESSELATION_ON");
            }
        }
    }
}
