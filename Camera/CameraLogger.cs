using System.Globalization;
using UnityEngine;

namespace Landoria.SagaCapture
{
    // Records camera state at creation and autonomous-flight startup.
    internal static class SagaCaptureCameraLogger
    {
        // Logs the effective gameplay and drone rendering configurations.
        internal static void LogEffectiveConfiguration(
            string mode, Camera source, Camera droneCamera,
            GraphicsSettingsState sourceSettings,
            GraphicsSettingsState droneSettings)
        {
            if (source == null || droneCamera == null)
            {
                return;
            }
            SagaCapturePlugin.Log.LogInfo(
                $"{mode} effective camera configuration:");
            LogCamera("Game", source, sourceSettings,
                Application.targetFrameRate,
                QualitySettings.vSyncCount > 0);
            LogCamera("Drone", droneCamera, droneSettings,
                Preference.CameraMaximumFrameRate,
                QualitySettings.vSyncCount > 0);
        }

        // Logs one camera and the graphics values that affect its image.
        private static void LogCamera(string label, Camera camera,
            GraphicsSettingsState settings, int maximumFrameRate, bool vSync)
        {
            GetPixels(camera, out int width, out int height);
            SagaCapturePlugin.Log.LogInfo(string.Format(
                CultureInfo.InvariantCulture,
                "  {0}: pixels={1}x{2}, maximumFrameRate={3}, FOV={4:F1}, " +
                "aspect={5:F3}, clip={6:F2}-{7:F1}m, " +
                "target3DResolutionHeight={8}, upscaling={9}, vSync={10}.",
                label, width, height, maximumFrameRate, camera.fieldOfView,
                camera.aspect, camera.nearClipPlane, camera.farClipPlane,
                settings.m_target3DResolutionVertical,
                settings.m_upscalingAlgorithm, vSync));
            LogQualitySettings(label, settings);
        }

        // Logs every resolved Valheim quality value without preset names.
        private static void LogQualitySettings(string label,
            GraphicsSettingsState settings)
        {
            SagaCapturePlugin.Log.LogInfo(
                $"  {label} quality: vegetation={settings.m_vegetation}, " +
                $"LOD={settings.m_lod}, particleLights={settings.m_lights}, " +
                $"shadowQuality={settings.m_shadowQuality}, " +
                $"pointLights={settings.m_pointLights}, " +
                $"pointLightShadows={settings.m_pointLightShadows}, " +
                $"SSAO={settings.m_ssao}, clothQuality={settings.m_clothQuality}, " +
                $"drawDistance={settings.m_simulationDistance}.");
            SagaCapturePlugin.Log.LogInfo(
                $"  {label} effects: distantShadows={settings.m_distantShadows}, " +
                $"tessellation={settings.m_tesselation}, bloom={settings.m_bloom}, " +
                $"depthOfField={settings.m_depthOfField}, " +
                $"motionBlur={settings.m_motionBlur}, " +
                $"chromaticAberration={settings.m_chromaticAberration}, " +
                $"sunShafts={settings.m_sunShafts}, " +
                $"softParticles={settings.m_softParticles}, " +
                $"antiAliasing={settings.m_antiAliasing}, " +
                $"anisotropicTextures={settings.m_anisotropicTextures}.");
        }

        // Resolves actual render-target dimensions for one camera.
        private static void GetPixels(Camera camera, out int width,
            out int height)
        {
            if (camera.targetTexture != null)
            {
                width = camera.targetTexture.width;
                height = camera.targetTexture.height;
                return;
            }
            width = camera.pixelWidth;
            height = camera.pixelHeight;
        }

        // Logs source, drone camera, and player-relative framing parameters.
        internal static void LogSnapshot(
            string phase, Camera source, Camera droneCamera,
            bool enabled)
        {
            if (!enabled || source == null || droneCamera == null)
            {
                return;
            }

            Player player = Player.m_localPlayer;
            Vector3 relative = player != null
                ? droneCamera.transform.position - player.transform.position
                : Vector3.zero;
            float horizontal = new Vector2(relative.x, relative.z).magnitude;
            SagaCapturePlugin.Log.LogInfo(string.Format(
                CultureInfo.InvariantCulture,
                "Camera {0}: source={1}, sourcePosition={2}, " +
                "sourceRotation={3}, sourceFOV={4:F1}, dronePosition={5}, " +
                "droneRotation={6}, droneFOV={7:F1}, relative={8}, " +
                "horizontalDistance={9:F2}m, verticalOffset={10:F2}m, " +
                "aspect={11:F3}, clip={12:F2}-{13:F1}m, pixels={14}x{15}.",
                phase, source.name, Format(source.transform.position),
                Format(source.transform.eulerAngles), source.fieldOfView,
                Format(droneCamera.transform.position),
                Format(droneCamera.transform.eulerAngles),
                droneCamera.fieldOfView, Format(relative), horizontal,
                relative.y, droneCamera.aspect, droneCamera.nearClipPlane,
                droneCamera.farClipPlane, droneCamera.pixelWidth,
                droneCamera.pixelHeight));
        }

        // Formats vectors consistently regardless of the system locale.
        private static string Format(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "({0:F2}, {1:F2}, {2:F2})",
                value.x, value.y, value.z);
        }
    }
}
