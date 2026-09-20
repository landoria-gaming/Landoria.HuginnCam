using System.Globalization;
using UnityEngine;

namespace Landoria.SagaCapture
{
    // Records camera state at creation and autonomous-flight startup.
    internal static class SagaCaptureCameraLogger
    {
        // Logs source, Saga camera, and player-relative framing parameters.
        internal static void LogSnapshot(
            string phase, Camera source, Camera sagaCamera)
        {
            if (!Preference.DebugLogs ||
                source == null || sagaCamera == null)
            {
                return;
            }

            Player player = Player.m_localPlayer;
            Vector3 relative = player != null
                ? sagaCamera.transform.position - player.transform.position
                : Vector3.zero;
            float horizontal = new Vector2(relative.x, relative.z).magnitude;
            SagaCapturePlugin.Log.LogInfo(string.Format(
                CultureInfo.InvariantCulture,
                "Camera {0}: source={1}, sourcePosition={2}, " +
                "sourceRotation={3}, sourceFOV={4:F1}, sagaPosition={5}, " +
                "sagaRotation={6}, sagaFOV={7:F1}, relative={8}, " +
                "horizontalDistance={9:F2}m, verticalOffset={10:F2}m, " +
                "aspect={11:F3}, clip={12:F2}-{13:F1}m, pixels={14}x{15}.",
                phase, source.name, Format(source.transform.position),
                Format(source.transform.eulerAngles), source.fieldOfView,
                Format(sagaCamera.transform.position),
                Format(sagaCamera.transform.eulerAngles),
                sagaCamera.fieldOfView, Format(relative), horizontal,
                relative.y, sagaCamera.aspect, sagaCamera.nearClipPlane,
                sagaCamera.farClipPlane, sagaCamera.pixelWidth,
                sagaCamera.pixelHeight));
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
