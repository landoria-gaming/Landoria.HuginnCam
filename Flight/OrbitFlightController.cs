using UnityEngine;

namespace Landoria.SagaCapture
{
    // Plans and advances the drone's terrain-aware orbit.
    internal sealed class OrbitFlightController
    {
        internal const float Speed = 0.5f;
        private const float MinimumRadius = 2f;
        private const float MinimumHeight = 0.5f;
        private const float CloseDistance = 3f;
        private const float CloseMaximumHeight = 2f;
        private const float CloseHeightBlendDistance = 1f;
        private const float FrontSpeedMultiplier = 0.5f;
        private const float RearSpeedMultiplier = 1.5f;
        private const float AnticipationSeconds = 2f;
        private const float MotionLeadSeconds = 1.5f;
        private const float RadiusSmoothTime = 3f;
        private const float MaximumRadialSpeed = 0.5f;
        private const int TerrainSamples = 36;
        private const int RadiusCandidates = 9;
        private float _angle;
        private float _radius;
        private float _preferredRadius;
        private float _radiusVelocity;
        private float _slopeX;
        private float _slopeZ;
        private float _direction = 1f;
        private bool _planeInitialized;

        internal Vector3 ProbeTarget { get; private set; }

        // Starts an orbit at the drone's current radial angle.
        internal void Enter(Vector3 playerPosition, Vector3 dronePosition,
                            bool alternateDirection)
        {
            if (alternateDirection)
            {
                _direction *= -1f;
            }
            Vector3 radial = dronePosition - playerPosition;
            radial.y = 0f;
            _angle = Mathf.Atan2(radial.z, radial.x);
            _radius = Mathf.Max(MinimumRadius, radial.magnitude);
            _preferredRadius = _radius;
            _radiusVelocity = 0f;
            _planeInitialized = false;
        }

        // Advances the orbit and returns its smooth look-ahead destination.
        internal Vector3 Update(Player player, Vector3 travelDirection,
                                DroneEnvironment environment,
                                out float targetSpeed)
        {
            float permittedRadius = Mathf.Clamp(
                _preferredRadius, MinimumRadius,
                environment.MaximumOrbitRadius);
            _radius = Mathf.SmoothDamp(
                _radius, permittedRadius, ref _radiusVelocity,
                RadiusSmoothTime, MaximumRadialSpeed);
            if (!_planeInitialized)
            {
                InitializePlan(player, environment);
            }
            Vector3 radial = new Vector3(
                Mathf.Cos(_angle), 0f, Mathf.Sin(_angle));
            float frontAmount = Vector3.Dot(radial, travelDirection) *
                                0.5f + 0.5f;
            targetSpeed = Speed * Mathf.Lerp(
                RearSpeedMultiplier, FrontSpeedMultiplier, frontAmount);
            _angle += _direction * targetSpeed /
                      Mathf.Max(_radius, 1f) * Time.deltaTime;
            Vector3 target = GetTargetHeight(
                player, radial, environment);
            Vector3 tangent = new Vector3(-radial.z, 0f, radial.x) *
                              _direction;
            target += tangent * targetSpeed * MotionLeadSeconds;
            ProbeTarget = target + tangent * targetSpeed *
                          AnticipationSeconds;
            return target;
        }

        // Positions one orbit target at eye height with terrain limits.
        private Vector3 GetTargetHeight(Player player, Vector3 radial,
                                        DroneEnvironment environment)
        {
            Vector3 center = player.transform.position;
            Vector3 target = center + radial * _radius;
            float closeBlend = Mathf.SmoothStep(
                0f, 1f, Mathf.InverseLerp(
                    CloseDistance, CloseDistance + CloseHeightBlendDistance,
                    _radius));
            float maximumHeight = Mathf.Lerp(
                Mathf.Min(CloseMaximumHeight, environment.MaximumHeight),
                environment.MaximumHeight, closeBlend);
            float preferredHeight = player.m_eye != null
                ? player.m_eye.position.y : center.y + 1.6f;
            preferredHeight += _slopeX * (target.x - center.x) +
                               _slopeZ * (target.z - center.z);
            target.y = Mathf.Clamp(preferredHeight,
                GroundHeight(target) + MinimumHeight,
                GroundHeight(target) + maximumHeight);
            return target;
        }

        // Chooses the least blocked complete ellipse before the orbit starts.
        private void InitializePlan(Player player,
                                    DroneEnvironment environment)
        {
            Vector3 center = player.transform.position;
            float preferredRadius = Mathf.Clamp(
                _preferredRadius, MinimumRadius,
                environment.MaximumOrbitRadius);
            float headHeight = player.m_eye != null
                ? player.m_eye.position.y : center.y + 1.6f;
            int bestObstacles = int.MaxValue;
            float bestDistance = float.MaxValue;
            for (int index = 0; index < RadiusCandidates; index++)
            {
                float amount = index / (RadiusCandidates - 1f);
                float radius = Mathf.Lerp(
                    MinimumRadius, environment.MaximumOrbitRadius, amount);
                Vector2 slope = FitPlane(center, radius);
                int obstacles = CountObstacles(
                    center, headHeight, radius, slope);
                float distance = Mathf.Abs(radius - preferredRadius);
                if (obstacles < bestObstacles ||
                    obstacles == bestObstacles && distance < bestDistance)
                {
                    bestObstacles = obstacles;
                    bestDistance = distance;
                    _preferredRadius = radius;
                    _slopeX = slope.x;
                    _slopeZ = slope.y;
                }
            }
            _planeInitialized = true;
            LogPlan(bestObstacles);
        }

        // Fits one terrain plane from a complete candidate orbit.
        private static Vector2 FitPlane(Vector3 center, float radius)
        {
            float sumXHeight = 0f;
            float sumZHeight = 0f;
            float sumSquared = 0f;
            for (int index = 0; index < TerrainSamples; index++)
            {
                float angle = Mathf.PI * 2f * index / TerrainSamples;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                float height = GroundHeight(
                    center + new Vector3(x, 0f, z));
                sumXHeight += x * height;
                sumZHeight += z * height;
                sumSquared += x * x;
            }
            return sumSquared > 0.001f
                ? new Vector2(
                    sumXHeight / sumSquared,
                    sumZHeight / sumSquared)
                : Vector2.zero;
        }

        // Counts blocked segments on a full candidate ellipse.
        private static int CountObstacles(
            Vector3 center, float headHeight, float radius, Vector2 slope)
        {
            int obstacles = 0;
            Vector3 previous = GetPlannedPoint(
                center, headHeight, radius, slope, TerrainSamples - 1);
            for (int index = 0; index < TerrainSamples; index++)
            {
                Vector3 point = GetPlannedPoint(
                    center, headHeight, radius, slope, index);
                if (DroneTrajectoryPlanner.IsRouteBlocked(previous, point))
                {
                    obstacles++;
                }
                previous = point;
            }
            return obstacles;
        }

        // Creates one point on a terrain-inclined candidate ellipse.
        private static Vector3 GetPlannedPoint(
            Vector3 center, float headHeight, float radius,
            Vector2 slope, int index)
        {
            float angle = Mathf.PI * 2f * index / TerrainSamples;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            return new Vector3(center.x + x,
                headHeight + slope.x * x + slope.y * z, center.z + z);
        }

        // Logs the planned ellipse incline for trajectory diagnostics.
        private void LogPlan(int obstacleCount)
        {
            float slope = Mathf.Sqrt(_slopeX * _slopeX + _slopeZ * _slopeZ);
            float angle = Mathf.Atan(slope) * Mathf.Rad2Deg;
            SagaCapturePlugin.Log.LogInfo(
                $"Orbit ellipse planned: incline={angle:F1} degrees, " +
                $"radius={_radius:F2}m, obstacles={obstacleCount}, " +
                $"slope=({_slopeX:F3}, {_slopeZ:F3}).");
        }

        // Reads terrain height or preserves the input height if unavailable.
        private static float GroundHeight(Vector3 position)
        {
            return ZoneSystem.instance != null &&
                   ZoneSystem.instance.GetGroundHeight(
                       position, out float height)
                ? height : position.y;
        }
    }
}
