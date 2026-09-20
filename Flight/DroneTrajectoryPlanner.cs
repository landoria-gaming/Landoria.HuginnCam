using UnityEngine;

namespace Landoria.SagaCapture
{
    // Anticipates terrain and selects simple lateral obstacle detours.
    internal sealed class DroneTrajectoryPlanner
    {
        private const float RefreshInterval = 0.1f;
        private const float MinimumLookAhead = 4f;
        private const float LookAheadSeconds = 2f;
        internal const float CameraRadius = 0.75f;
        private const float CharacterRetreatDistance = 2f;
        private const int TerrainSamples = 8;
        private static readonly float[] SideOffsets = { -1f, 1f, -2f, 2f };
        private Vector3 _plannedTarget;
        private float _nextRefreshTime;
        private bool _initialized;

        // Reuses one plan for 100 ms before simulating routes again.
        internal Vector3 Plan(
            Vector3 origin, Vector3 desired, float speed,
            float terrainClearance)
        {
            if (_initialized && Time.time < _nextRefreshTime)
            {
                return _plannedTarget;
            }

            float lookAhead = Mathf.Max(
                MinimumLookAhead, speed * LookAheadSeconds);
            Vector3 target = RaiseForTerrain(
                origin, desired, lookAhead, terrainClearance);
            _plannedTarget = ChooseObstacleRoute(
                origin, target, lookAhead, terrainClearance);
            _nextRefreshTime = Time.time + RefreshInterval;
            _initialized = true;
            return _plannedTarget;
        }

        // Chooses the first clear lateral route or keeps the direct route.
        private static Vector3 ChooseObstacleRoute(
            Vector3 origin, Vector3 target, float lookAhead,
            float terrainClearance)
        {
            if (!HasObstacle(origin, target, lookAhead))
            {
                return target;
            }

            Vector3 direction = target - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                return target;
            }

            Vector3 right = Vector3.Cross(Vector3.up, direction.normalized);
            foreach (float offset in SideOffsets)
            {
                Vector3 candidate = target + right * offset;
                candidate = RaiseForTerrain(
                    origin, candidate, lookAhead, terrainClearance);
                if (!HasObstacle(origin, candidate, lookAhead))
                {
                    return candidate;
                }
            }

            if (HasCharacterObstacle(origin, target, lookAhead))
            {
                Vector3 retreat = origin - direction.normalized *
                                  CharacterRetreatDistance;
                return RaiseForTerrain(
                    origin, retreat, lookAhead, terrainClearance);
            }

            return target;
        }

        // Detects a character so a failed detour never continues through it.
        private static bool HasCharacterObstacle(
            Vector3 origin, Vector3 target, float lookAhead)
        {
            Vector3 movement = target - origin;
            float distance = Mathf.Min(movement.magnitude, lookAhead);
            if (distance < 0.001f)
            {
                return false;
            }

            RaycastHit[] hits = Physics.SphereCastAll(
                origin, CameraRadius, movement.normalized, distance,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider != null &&
                    hit.collider.GetComponentInParent<Character>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        // Detects scenery along the speed-scaled start of one route.
        private static bool HasObstacle(
            Vector3 origin, Vector3 target, float lookAhead)
        {
            Vector3 movement = target - origin;
            float distance = Mathf.Min(movement.magnitude, lookAhead);
            if (distance < 0.001f)
            {
                return false;
            }

            RaycastHit[] hits = Physics.SphereCastAll(
                origin, CameraRadius, movement.normalized, distance,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (IsObstacle(hit.collider))
                {
                    return true;
                }
            }

            return false;
        }

        // Treats characters as obstacles and leaves terrain to height sampling.
        private static bool IsObstacle(Collider collider)
        {
            return collider != null &&
                   collider.GetComponentInParent<Heightmap>() == null;
        }

        // Raises the target when any sampled route point approaches terrain.
        private static Vector3 RaiseForTerrain(
            Vector3 origin, Vector3 target, float lookAhead,
            float terrainClearance)
        {
            Vector3 route = target - origin;
            float distance = Mathf.Min(route.magnitude, lookAhead);
            if (distance < 0.001f)
            {
                return target;
            }

            float requiredLift = 0f;
            for (int index = 1; index <= TerrainSamples; index++)
            {
                float amount = distance * index / TerrainSamples;
                Vector3 point = origin + route.normalized * amount;
                if (TryGroundHeight(point, out float ground))
                {
                    requiredLift = Mathf.Max(
                        requiredLift, ground + terrainClearance - point.y);
                }
            }

            target.y += Mathf.Max(0f, requiredLift);
            return target;
        }

        // Reads the heightmap elevation below one point.
        private static bool TryGroundHeight(
            Vector3 position, out float groundHeight)
        {
            groundHeight = 0f;
            return ZoneSystem.instance != null &&
                   ZoneSystem.instance.GetGroundHeight(position, out groundHeight);
        }
    }
}
