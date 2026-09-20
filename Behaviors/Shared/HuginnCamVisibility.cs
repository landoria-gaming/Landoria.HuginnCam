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
        private const float FlightProbeRadius = 0.4f;

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
        internal static bool IsOpenForFreedomFlight(
            Player player, Vector3 cameraPosition)
        {
            Vector3 head = player.transform.position + Vector3.up * 1.7f;
            Vector3 freedomPoint = player.transform.position + Vector3.up * 10f;
            if (!HasClearSight(player, head, freedomPoint) ||
                !HasClearSight(player, cameraPosition, freedomPoint))
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

        // Measures the unobstructed distance along a camera flight corridor.
        internal static float GetFlightClearance(
            Player player, Vector3 origin, Vector3 direction, float distance)
        {
            if (direction.sqrMagnitude < 0.001f)
            {
                return distance;
            }

            RaycastHit[] hits = Physics.SphereCastAll(
                origin, FlightProbeRadius, direction.normalized, distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            float nearest = distance;
            foreach (RaycastHit hit in hits)
            {
                if (!IsPlayerCollider(player, hit.collider) &&
                    hit.distance < nearest)
                {
                    nearest = hit.distance;
                }
            }

            return nearest;
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
