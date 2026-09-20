using UnityEngine;

namespace Landoria.HuginnCam
{
    // Prevents the Huginn camera from crossing the terrain surface.
    internal static class HuginnCamTerrain
    {
        private const float CameraRadius = 0.1f;
        private const float TerrainClearance = 1f;
        private const int LookAheadSamples = 8;

        // Prevents one camera movement from crossing a terrain collider.
        internal static Vector3 ResolveMovement(
            Vector3 current, Vector3 desired)
        {
            float clearance = TerrainClearance;
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

        // Raises a destination in advance of rising terrain along the flight path.
        internal static Vector3 AnticipateRise(
            Vector3 origin, Vector3 desired, float lookAhead,
            float clearance)
        {
            Vector3 horizontal = desired - origin;
            horizontal.y = 0f;
            float distance = Mathf.Min(horizontal.magnitude, lookAhead);
            if (distance < 0.001f)
            {
                return desired;
            }

            Vector3 direction = horizontal.normalized;
            float requiredHeight = desired.y;
            for (int sample = 1; sample <= LookAheadSamples; sample++)
            {
                Vector3 point = origin + direction *
                    (distance * sample / LookAheadSamples);
                if (TryGetGroundHeight(point, out float groundHeight))
                {
                    requiredHeight = Mathf.Max(
                        requiredHeight, groundHeight + clearance);
                }
            }

            desired.y = requiredHeight;
            return desired;
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
