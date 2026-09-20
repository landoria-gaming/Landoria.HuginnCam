using UnityEngine;

namespace Landoria.SagaCapture
{
    // Logs the recorder camera's player-relative position once per second.
    internal sealed class SagaCapturePositionLogger
    {
        private float _nextLogTime;

        // Writes horizontal coordinates, vertical offset, and horizontal distance.
        internal void Update(Player player, Vector3 cameraPosition)
        {
            if (Time.time < _nextLogTime)
            {
                return;
            }

            _nextLogTime = Time.time + 1f;
            Vector3 relative = cameraPosition - player.transform.position;
            float horizontalDistance = new Vector2(
                relative.x, relative.z).magnitude;
            SagaCapturePlugin.Log.LogInfo(
                $"Camera relative position: horizontal=({relative.x:F2}, " +
                $"{relative.z:F2}) m, vertical={relative.y:F2} m, " +
                $"horizontalDistance={horizontalDistance:F2} m.");
        }
    }
}
