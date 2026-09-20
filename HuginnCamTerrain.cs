using UnityEngine;

namespace Landoria.HuginnCam
{
    // Prevents the Huginn camera from crossing the terrain surface.
    internal static class HuginnCamTerrain
    {
        private const float CameraRadius = 0.1f;
        private const float TerrainClearance = 1f;
        private const float AvoidanceClearance = 1.5f;
        private const float SlopeSampleDistance = 1f;

        // Detects terrain that requires an accelerated climbing maneuver.
        internal static bool NeedsAvoidance(Vector3 current, Vector3 desired)
        {
            if (TryGetGroundHeight(current, out float groundHeight) &&
                current.y - groundHeight < AvoidanceClearance)
            {
                return true;
            }

            Vector3 movement = desired - current;
            float distance = movement.magnitude;
            return distance >= 0.001f &&
                   FindTerrainHit(current, movement.normalized, distance) < distance;
        }

        // Prevents one camera movement from crossing a terrain collider.
        internal static Vector3 ResolveMovement(
            Vector3 current, Vector3 desired, bool landing)
        {
            float clearance = landing ? 0.2f : TerrainClearance;
            desired = KeepAbove(desired, clearance);
            Vector3 movement = desired - current;
            float distance = movement.magnitude;
            if (distance < 0.001f)
            {
                return desired;
            }

            float hitDistance = FindTerrainHit(
                current, movement.normalized, distance);
            if (hitDistance < distance)
            {
                float safeDistance = Mathf.Max(0f, hitDistance - clearance);
                desired = current + movement.normalized * safeDistance;
            }

            return KeepAbove(desired, clearance);
        }

        // Finds the horizontal downhill direction around one terrain position.
        internal static Vector3 GetDownhillDirection(
            Vector3 position, Vector3 fallback)
        {
            Vector3 right = position + Vector3.right * SlopeSampleDistance;
            Vector3 left = position - Vector3.right * SlopeSampleDistance;
            Vector3 forward = position + Vector3.forward * SlopeSampleDistance;
            Vector3 back = position - Vector3.forward * SlopeSampleDistance;
            if (TryGetGroundHeight(right, out float rightHeight) &&
                TryGetGroundHeight(left, out float leftHeight) &&
                TryGetGroundHeight(forward, out float forwardHeight) &&
                TryGetGroundHeight(back, out float backHeight))
            {
                Vector3 downhill = new Vector3(
                    leftHeight - rightHeight, 0f, backHeight - forwardHeight);
                if (downhill.sqrMagnitude > 0.001f)
                {
                    return downhill.normalized;
                }
            }

            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.001f
                ? fallback.normalized
                : Vector3.forward;
        }

        // Raises a camera position above the local terrain when necessary.
        private static Vector3 KeepAbove(Vector3 position, float clearance)
        {
            if (!TryGetGroundHeight(position, out float groundHeight))
            {
                return position;
            }

            position.y = Mathf.Max(position.y, groundHeight + clearance);
            return position;
        }

        // Reads the heightmap elevation at one world position.
        private static bool TryGetGroundHeight(
            Vector3 position, out float groundHeight)
        {
            groundHeight = 0f;
            return ZoneSystem.instance != null &&
                   ZoneSystem.instance.GetGroundHeight(position, out groundHeight);
        }

        // Finds the nearest heightmap collider swept by the camera volume.
        private static float FindTerrainHit(
            Vector3 origin, Vector3 direction, float distance)
        {
            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                CameraRadius,
                direction,
                distance,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            float nearest = distance;
            foreach (RaycastHit hit in hits)
            {
                if (hit.distance < nearest &&
                    hit.collider.GetComponentInParent<Heightmap>() != null)
                {
                    nearest = hit.distance;
                }
            }

            return nearest;
        }
    }
}
