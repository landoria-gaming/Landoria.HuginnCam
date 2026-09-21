using UnityEngine;

namespace Landoria.SagaCapture
{
    // Logs the recorder camera's player-relative position twice per second.
    internal sealed class SagaCapturePositionLogger
    {
        private float _nextLogTime;

        // Writes relative position, vertical pitch, and player screen position.
        internal void Update(Player player, Camera camera)
        {
            if (!Preference.DebugLogs || Time.time < _nextLogTime)
            {
                return;
            }

            _nextLogTime = Time.time + 0.5f;
            Vector3 relative = camera.transform.position -
                               player.transform.position;
            float horizontalDistance = new Vector2(
                relative.x, relative.z).magnitude;
            float pitch = Mathf.DeltaAngle(
                0f, camera.transform.eulerAngles.x);
            Vector3 focus = player.transform.position + Vector3.up * 1.25f;
            Vector3 viewport = camera.WorldToViewportPoint(focus);
            bool inFrame = viewport.z > 0f &&
                           viewport.x >= 0f && viewport.x <= 1f &&
                           viewport.y >= 0f && viewport.y <= 1f;
            SagaCapturePlugin.Log.LogInfo(
                $"Camera relative position: horizontal=({relative.x:F2}, " +
                $"{relative.z:F2}) m, vertical={relative.y:F2} m, " +
                $"cameraWorldY={camera.transform.position.y:F2} m, " +
                $"playerWorldY={player.transform.position.y:F2} m, " +
                $"worldYDifference={relative.y:F2} m, " +
                $"horizontalDistance={horizontalDistance:F2} m, " +
                $"pitch={pitch:F1} deg, " +
                $"playerScreen=({viewport.x * 100f:F1}, " +
                $"{viewport.y * 100f:F1}), inFrame={inFrame}.");
        }
    }
}
