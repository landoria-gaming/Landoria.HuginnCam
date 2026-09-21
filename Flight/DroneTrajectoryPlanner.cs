using UnityEngine;

namespace Landoria.SagaCapture
{
    // Anticipates terrain and selects simple lateral obstacle detours.
    internal sealed class DroneTrajectoryPlanner
    {
        private const float RefreshInterval = 0.1f;
        private const float MinimumLookAhead = 4f;
        private const float LookAheadSeconds = 2f;
        internal const float CameraRadius = 0.4f;
        private const float CharacterRetreatDistance = 2f;
        private const float TerrainRiseSmoothTime = 0.35f;
        private const float TerrainFallSmoothTime = 1.5f;
        private const int TerrainSamples = 8;
        private static readonly Vector2[] RouteOffsets =
        {
            new Vector2(-1f, 0f), new Vector2(1f, 0f),
            new Vector2(-1f, 1f), new Vector2(1f, 1f),
            new Vector2(-2f, 1f), new Vector2(2f, 1f),
            new Vector2(-1f, 2f), new Vector2(1f, 2f),
            new Vector2(0f, 1f), new Vector2(0f, 2f)
        };
        private static readonly string[] IgnoredVegetationNames =
        {
            "grass", "bush", "shrub", "fern", "heath",
            "berry", "raspberry", "blueberry", "cloudberry",
            "thistle", "dandelion"
        };
        private Vector3 _plannedTarget;
        private float _nextRefreshTime;
        private float _lastRefreshTime;
        private float _smoothedTerrainLift;
        private float _terrainLiftVelocity;
        private string _lastAvoidanceDecision;
        private float _nextAvoidanceLogTime;
        private int _activeObstacleId;
        private float _avoidanceSide;
        private float _avoidanceSideHoldUntil;
        private bool _initialized;
        internal bool EmergencyAvoidance { get; private set; }

        // Reuses one plan for 100 ms before simulating routes again.
        internal Vector3 Plan(
            Vector3 origin, Vector3 desired, Vector3 probeTarget,
            Vector3 velocity,
            float terrainClearance, bool useReactiveObstacleAvoidance)
        {
            if (_initialized && Time.time < _nextRefreshTime)
            {
                return _plannedTarget;
            }

            float lookAhead = Mathf.Max(
                MinimumLookAhead, velocity.magnitude * LookAheadSeconds);
            float requiredLift = GetRequiredTerrainLift(
                origin, probeTarget, lookAhead, terrainClearance);
            float smoothTime = requiredLift > _smoothedTerrainLift
                ? TerrainRiseSmoothTime
                : TerrainFallSmoothTime;
            float elapsed = _initialized
                ? Mathf.Max(0.001f, Time.time - _lastRefreshTime)
                : RefreshInterval;
            _smoothedTerrainLift = Mathf.SmoothDamp(
                _smoothedTerrainLift, requiredLift,
                ref _terrainLiftVelocity, smoothTime,
                Mathf.Infinity, elapsed);
            Vector3 target = desired + Vector3.up * _smoothedTerrainLift;
            EmergencyAvoidance = false;
            Vector3 routeTarget = target;
            Vector3 reactiveProbe = probeTarget;
            if (useReactiveObstacleAvoidance)
            {
                reactiveProbe = GetReactiveProbe(
                    origin, probeTarget, velocity, lookAhead,
                    out bool followsMomentum);
                if (followsMomentum)
                {
                    routeTarget = reactiveProbe;
                    routeTarget.y = target.y;
                }
            }
            _plannedTarget = useReactiveObstacleAvoidance
                ? ChooseObstacleRoute(
                    origin, routeTarget, reactiveProbe,
                    target, lookAhead, terrainClearance)
                : target;
            _lastRefreshTime = Time.time;
            _nextRefreshTime = Time.time + RefreshInterval;
            _initialized = true;
            return _plannedTarget;
        }

        // Prioritizes the path the moving drone cannot instantly leave.
        private Vector3 GetReactiveProbe(
            Vector3 origin, Vector3 desiredProbe, Vector3 velocity,
            float lookAhead, out bool followsMomentum)
        {
            followsMomentum = false;
            if (velocity.sqrMagnitude < 0.04f)
            {
                return desiredProbe;
            }

            Vector3 momentumProbe = origin + velocity.normalized * lookAhead;
            if (!TryGetObstacle(
                    origin, momentumProbe, lookAhead,
                    out Collider obstacle))
            {
                return desiredProbe;
            }

            // A close obstacle needs faster steering than normal flight.
            EmergencyAvoidance = Vector3.Distance(
                origin, obstacle.ClosestPoint(origin)) < 2.5f;
            followsMomentum = true;
            return momentumProbe;
        }

        // Chooses the first clear lateral route or keeps the direct route.
        private Vector3 ChooseObstacleRoute(
            Vector3 origin, Vector3 target, Vector3 probeTarget,
            Vector3 fallbackTarget, float lookAhead,
            float terrainClearance)
        {
            if (!TryGetObstacle(
                    origin, probeTarget, lookAhead, out Collider obstacle))
            {
                LogDirectPathRestored();
                ClearExpiredAvoidanceSide();
                return target;
            }
            EmergencyAvoidance |= Vector3.Distance(
                origin, obstacle.ClosestPoint(origin)) < 2.5f;
            PrepareAvoidanceSide(obstacle);

            Vector3 direction = target - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                return target;
            }

            Vector3 right = Vector3.Cross(Vector3.up, direction.normalized);
            foreach (Vector2 offset in RouteOffsets)
            {
                if (_avoidanceSide != 0f && offset.x != 0f &&
                    Mathf.Sign(offset.x) != _avoidanceSide)
                {
                    continue;
                }
                Vector3 candidate = target + right * offset.x +
                                    Vector3.up * offset.y;
                candidate = RaiseForTerrain(
                    origin, candidate, lookAhead, terrainClearance);
                if (!HasObstacle(origin, candidate, lookAhead))
                {
                    if (_avoidanceSide == 0f && offset.x != 0f)
                    {
                        _avoidanceSide = Mathf.Sign(offset.x);
                    }
                    LogAvoidance(obstacle, DescribeManeuver(offset));
                    return candidate;
                }
            }

            if (HasCharacterObstacle(origin, probeTarget, lookAhead))
            {
                LogAvoidance(obstacle, "retreat 2.0m");
                Vector3 retreat = origin - direction.normalized *
                                  CharacterRetreatDistance;
                return RaiseForTerrain(
                    origin, retreat, lookAhead, terrainClearance);
            }

            LogAvoidance(obstacle, "no clear detour; continuing direct");
            return fallbackTarget;
        }

        // Keeps one lateral side while passing the same obstacle.
        private void PrepareAvoidanceSide(Collider obstacle)
        {
            int obstacleId = obstacle.GetInstanceID();
            if (_activeObstacleId == obstacleId)
            {
                return;
            }

            _activeObstacleId = obstacleId;
            _avoidanceSide = 0f;
            _avoidanceSideHoldUntil = 0f;
        }

        // Describes one combined lateral and vertical avoidance maneuver.
        private static string DescribeManeuver(Vector2 offset)
        {
            string side = offset.x < 0f
                ? $"left {Mathf.Abs(offset.x):F1}m"
                : offset.x > 0f
                    ? $"right {offset.x:F1}m"
                    : "straight";
            return offset.y > 0f
                ? $"{side} + climb {offset.y:F1}m"
                : side;
        }

        // Logs a changed avoidance decision and periodically repeats it.
        private void LogAvoidance(Collider obstacle, string maneuver)
        {
            string character = obstacle != null
                ? obstacle.GetComponentInParent<Character>()?.GetType().Name
                : null;
            string kind = character ?? obstacle?.GetType().Name ?? "Unknown";
            string name = obstacle != null ? obstacle.name : "unknown";
            string decision = $"{kind}:{name}:{maneuver}";
            if (decision == _lastAvoidanceDecision &&
                Time.time < _nextAvoidanceLogTime)
            {
                return;
            }

            SagaCapturePlugin.Log.LogWarning(
                $"Drone avoidance: obstacle={kind} '{name}', " +
                $"maneuver={maneuver}.");
            _lastAvoidanceDecision = decision;
            _nextAvoidanceLogTime = Time.time + 2f;
        }

        // Logs when a previous avoidance ends and the orbit resumes directly.
        private void LogDirectPathRestored()
        {
            if (string.IsNullOrEmpty(_lastAvoidanceDecision) ||
                _lastAvoidanceDecision == "direct")
            {
                return;
            }

            SagaCapturePlugin.Log.LogInfo(
                "Drone avoidance ended: direct trajectory restored.");
            _lastAvoidanceDecision = "direct";
            _avoidanceSideHoldUntil = Time.time + 2f;
        }

        // Clears a completed avoidance only after a stable clear interval.
        private void ClearExpiredAvoidanceSide()
        {
            if (_lastAvoidanceDecision == "direct" &&
                Time.time >= _avoidanceSideHoldUntil)
            {
                _activeObstacleId = 0;
                _avoidanceSide = 0f;
            }
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
            return TryGetObstacle(origin, target, lookAhead, out _);
        }

        // Returns the nearest collider blocking one simulated route.
        private static bool TryGetObstacle(
            Vector3 origin, Vector3 target, float lookAhead,
            out Collider obstacle)
        {
            obstacle = null;
            Vector3 movement = target - origin;
            float distance = Mathf.Min(movement.magnitude, lookAhead);
            if (distance < 0.001f)
            {
                return false;
            }

            RaycastHit[] hits = Physics.SphereCastAll(
                origin, CameraRadius, movement.normalized, distance,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            float nearestDistance = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                if (IsObstacle(hit.collider) && hit.distance < nearestDistance)
                {
                    obstacle = hit.collider;
                    nearestDistance = hit.distance;
                }
            }

            return obstacle != null;
        }

        // Checks a complete preplanned segment with the camera's full radius.
        internal static bool IsRouteBlocked(Vector3 origin, Vector3 target)
        {
            return TryGetObstacle(
                origin, target, Vector3.Distance(origin, target), out _);
        }

        // Treats characters as obstacles and leaves terrain to height sampling.
        private static bool IsObstacle(Collider collider)
        {
            return collider != null &&
                   collider.GetComponentInParent<Heightmap>() == null &&
                   collider.GetComponentInParent<ItemDrop>() == null &&
                   collider.GetComponentInParent<Pickable>() == null &&
                   !IsIgnoredVegetation(collider.transform);
        }

        // Recognizes light vegetation from collider and prefab parent names.
        private static bool IsIgnoredVegetation(Transform transform)
        {
            for (Transform current = transform;
                 current != null;
                 current = current.parent)
            {
                string name = current.name.ToLowerInvariant();
                foreach (string ignoredName in IgnoredVegetationNames)
                {
                    if (name.Contains(ignoredName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Raises the target when any sampled route point approaches terrain.
        private static Vector3 RaiseForTerrain(
            Vector3 origin, Vector3 target, float lookAhead,
            float terrainClearance)
        {
            target.y += GetRequiredTerrainLift(
                origin, target, lookAhead, terrainClearance);
            return target;
        }

        // Returns the lift required by all sampled future terrain points.
        private static float GetRequiredTerrainLift(
            Vector3 origin, Vector3 target, float lookAhead,
            float terrainClearance)
        {
            Vector3 route = target - origin;
            float distance = Mathf.Min(route.magnitude, lookAhead);
            if (distance < 0.001f)
            {
                return 0f;
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

            return Mathf.Max(0f, requiredLift);
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
