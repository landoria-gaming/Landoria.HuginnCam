using UnityEngine;

namespace Landoria.SagaCapture
{
    // Detects lost framing and produces a fast recovery position.
    internal sealed class DroneFraming
    {
        private const float RecoveryDistance = 4f;
        private const float RecoveryHeight = 2f;
        private const float ComfortableRecoveryPitch = 55f;

        // Returns whether the player focus remains inside the camera view.
        internal bool IsVisible(Camera camera, Vector3 playerFocus)
        {
            Vector3 viewport = camera.WorldToViewportPoint(playerFocus);
            return viewport.z > 0f &&
                   viewport.x >= 0f && viewport.x <= 1f &&
                   viewport.y >= 0f && viewport.y <= 1f;
        }

        // Chooses a nearby position compatible with the pitch restriction.
        internal Vector3 GetRecoveryTarget(
            Player player, Vector3 dronePosition)
        {
            Vector3 radial = dronePosition - player.transform.position;
            radial.y = 0f;
            if (radial.sqrMagnitude < 0.001f)
            {
                radial = -player.transform.forward;
                radial.y = 0f;
            }

            radial.Normalize();
            float height = Mathf.Max(0f,
                dronePosition.y - player.transform.position.y);
            float distanceForHeight = height /
                Mathf.Tan(ComfortableRecoveryPitch * Mathf.Deg2Rad);
            float recoveryDistance = Mathf.Max(
                RecoveryDistance, distanceForHeight);
            Vector3 target = player.transform.position +
                             radial * recoveryDistance;
            target.y = GetGroundHeight(target) + RecoveryHeight;
            return target;
        }

        // Returns terrain height or the input height when unavailable.
        private static float GetGroundHeight(Vector3 position)
        {
            return ZoneSystem.instance != null &&
                   ZoneSystem.instance.GetGroundHeight(position, out float height)
                ? height
                : position.y;
        }
    }
}
