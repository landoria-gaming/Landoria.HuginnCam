using UnityEngine;

namespace Landoria.HuginnCam
{
    // Logs Huginn's player-relative position at a stable one-second interval.
    internal sealed class HuginnCamPositionLogger
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
            HuginnCamPlugin.Log.LogInfo(
                $"Huginn relative position: horizontal=({relative.x:F2}, " +
                $"{relative.z:F2}) m, vertical={relative.y:F2} m, " +
                $"horizontalDistance={horizontalDistance:F2} m.");
        }
    }
}
