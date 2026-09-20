using UnityEngine;

namespace Landoria.HuginnCam
{
    // Detects obstacles that hide the player without blocking camera movement.
    internal static class HuginnCamVisibility
    {
        private const float VisibilityRadius = 0.15f;
        private const float OpenAreaDistance = 15f;
        private const int OpenDirectionCount = 8;
        private const int RequiredOpenDirections = 6;

        // Returns whether the camera position has a clear view of the player.
        internal static bool HasClearSight(
            Player player, Vector3 focus, Vector3 cameraPosition)
        {
            Vector3 direction = cameraPosition - focus;
            float distance = direction.magnitude;
            if (distance < 0.001f)
            {
                return true;
            }

            RaycastHit[] hits = Physics.SphereCastAll(
                focus,
                VisibilityRadius,
                direction.normalized,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (!IsPlayerCollider(player, hit.collider))
                {
                    return false;
                }
            }

            return true;
        }

        // Detects a broad clearing with open sky and few nearby obstructions.
        internal static bool IsOpenForOverhead(Player player, Vector3 cameraPosition)
        {
            Vector3 head = player.transform.position + Vector3.up * 1.7f;
            Vector3 overhead = player.transform.position + Vector3.up * 6f;
            if (!HasClearSight(player, head, overhead) ||
                !HasClearSight(player, cameraPosition, overhead))
            {
                return false;
            }

            int openDirections = 0;
            for (int index = 0; index < OpenDirectionCount; index++)
            {
                float angle = index * 360f / OpenDirectionCount;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                if (HasClearSight(
                    player, head, head + direction * OpenAreaDistance))
                {
                    openDirections++;
                }
            }

            return openDirections >= RequiredOpenDirections;
        }

        // Excludes the followed player's own colliders from visibility checks.
        private static bool IsPlayerCollider(Player player, Collider collider)
        {
            return collider != null &&
                   (collider.transform == player.transform ||
                    collider.transform.IsChildOf(player.transform));
        }
    }
}
