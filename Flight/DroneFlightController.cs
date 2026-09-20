using UnityEngine;

namespace Landoria.SagaCapture
{
    // Selects flight modes and produces their continuous destinations.
    internal sealed class DroneFlightController
    {
        private const float OrbitSpeed = 0.5f;
        private const float OrbitEntryMaximumDroneSpeed = 1f;
        private const float MinimumOrbitRadius = 2f;
        private const float TrailingEntryDistance = 5f;
        private const float OrbitReturnDistance = 4f;
        private const float MinimumTrailingDistance = 1f;
        private const float MaximumTrailingDistance = 4f;
        private const float MinimumTrailingHeight = 2f;
        private const float SlowTrailingClearance = 1.5f;
        private const float MinimumOrbitHeight = 0.5f;
        private const float FullTerrainClearanceSpeed = 3f;
        private const float CloseOrbitDistance = 3f;
        private const float CloseOrbitMaximumHeight = 2f;
        private const float CloseOrbitHeightBlendDistance = 1f;
        private const float FrontOrbitSpeedMultiplier = 0.5f;
        private const float RearOrbitSpeedMultiplier = 1.5f;
        private const float OrbitAnticipationSeconds = 2f;
        private const float OrbitMotionLeadSeconds = 1.5f;
        private const float OrbitRadiusSmoothTime = 3f;
        private const float MaximumOrbitRadialSpeed = 0.5f;
        private const int OrbitTerrainSamples = 36;
        private const int OrbitRadiusCandidates = 9;
        private DroneFlightMode _mode;
        private Vector3 _travelDirection = Vector3.forward;
        private float _orbitAngle;
        private float _orbitRadius;
        private float _preferredOrbitRadius;
        private float _orbitRadiusVelocity;
        private float _orbitSlopeX;
        private float _orbitSlopeZ;
        private bool _orbitPlaneInitialized;
        private float _orbitDirection = 1f;
        private bool _initialized;

        internal DroneFlightMode Mode => _mode;
        internal Vector3 TrajectoryProbeTarget { get; private set; }

        // Returns mode-safe clearance with extra margin as orbit speed rises.
        internal float GetTerrainClearance(float speed)
        {
            if (_mode == DroneFlightMode.TrailingFlight)
            {
                float trailingSpeedBlend = Mathf.InverseLerp(
                    OrbitSpeed, FullTerrainClearanceSpeed, speed);
                return Mathf.Lerp(
                    SlowTrailingClearance, MinimumTrailingHeight,
                    trailingSpeedBlend);
            }

            float speedBlend = Mathf.InverseLerp(
                OrbitSpeed, FullTerrainClearanceSpeed, speed);
            return Mathf.Lerp(
                MinimumOrbitHeight, MinimumTrailingHeight, speedBlend);
        }

        // Selects a mode and returns its desired world position and speed.
        internal Vector3 Update(
            Player player, Vector3 dronePosition,
            float droneSpeed, DroneEnvironment environment,
            out float targetSpeed)
        {
            Vector3 playerPosition = player.transform.position;
            Vector3 playerVelocity = Flatten(player.GetVelocity());
            UpdateTravelDirection(player, playerVelocity);
            SelectMode(
                playerPosition, dronePosition,
                playerVelocity.magnitude, droneSpeed);
            if (_mode == DroneFlightMode.TrailingFlight)
            {
                targetSpeed = GetTrailingSpeed(player, dronePosition);
                Vector3 target = GetTrailingTarget(
                    playerPosition, environment,
                    GetTerrainClearance(targetSpeed));
                TrajectoryProbeTarget = target;
                return target;
            }

            return GetOrbitTarget(
                player, playerPosition, environment, out targetSpeed);
        }

        // Chooses a stable mode with distance hysteresis.
        private void SelectMode(
            Vector3 playerPosition, Vector3 dronePosition,
            float playerSpeed, float droneSpeed)
        {
            float distance = Flatten(
                dronePosition - playerPosition).magnitude;
            DroneFlightMode next = _mode;
            if (!_initialized)
            {
                next = distance >= TrailingEntryDistance
                    ? DroneFlightMode.TrailingFlight
                    : DroneFlightMode.OrbitFlight;
                InitializeMode(next, playerPosition, dronePosition);
                return;
            }
            else if (_mode == DroneFlightMode.OrbitFlight &&
                     distance >= TrailingEntryDistance)
            {
                next = DroneFlightMode.TrailingFlight;
            }
            else if (_mode == DroneFlightMode.TrailingFlight &&
                     distance <= OrbitReturnDistance &&
                     playerSpeed <= OrbitSpeed &&
                     droneSpeed <= OrbitEntryMaximumDroneSpeed)
            {
                next = DroneFlightMode.OrbitFlight;
            }

            ChangeMode(next, playerPosition, dronePosition);
        }

        // Initializes the first mode without consuming an orbit alternation.
        private void InitializeMode(
            DroneFlightMode mode, Vector3 playerPosition,
            Vector3 dronePosition)
        {
            _mode = mode;
            _initialized = true;
            if (mode == DroneFlightMode.OrbitFlight)
            {
                InitializeOrbitAngle(playerPosition, dronePosition);
            }
            SagaCapturePlugin.Log.LogInfo(
                $"Drone flight initialized: {mode}.");
        }

        // Logs a mode change and alternates the next orbit direction.
        private void ChangeMode(
            DroneFlightMode next, Vector3 playerPosition,
            Vector3 dronePosition)
        {
            if (next == _mode)
            {
                return;
            }

            DroneFlightMode previous = _mode;
            _mode = next;
            if (next == DroneFlightMode.OrbitFlight)
            {
                _orbitDirection *= -1f;
                InitializeOrbitAngle(playerPosition, dronePosition);
            }
            SagaCapturePlugin.Log.LogInfo(
                $"Drone flight changed: {previous} -> {next}.");
        }

        // Starts an orbit from the drone's current radial angle.
        private void InitializeOrbitAngle(
            Vector3 playerPosition, Vector3 dronePosition)
        {
            Vector3 radial = Flatten(dronePosition - playerPosition);
            _orbitAngle = Mathf.Atan2(radial.z, radial.x);
            _orbitRadius = Mathf.Max(MinimumOrbitRadius, radial.magnitude);
            _preferredOrbitRadius = _orbitRadius;
            _orbitRadiusVelocity = 0f;
            _orbitPlaneInitialized = false;
        }

        // Updates the horizontal direction from meaningful player velocity.
        private void UpdateTravelDirection(Player player, Vector3 velocity)
        {
            if (velocity.sqrMagnitude > 0.04f)
            {
                _travelDirection = velocity.normalized;
                return;
            }

            Vector3 forward = Flatten(player.transform.forward);
            if (forward.sqrMagnitude > 0.001f)
            {
                _travelDirection = forward.normalized;
            }
        }

        // Creates a target behind the player's direction of travel.
        private Vector3 GetTrailingTarget(
            Vector3 playerPosition, DroneEnvironment environment,
            float terrainClearance)
        {
            float phase = Time.time * 0.17f;
            float distance = Mathf.Lerp(
                MinimumTrailingDistance, MaximumTrailingDistance,
                0.5f + Mathf.Sin(phase) * 0.5f);
            Vector3 right = Vector3.Cross(Vector3.up, _travelDirection);
            float lateral = Mathf.Sin(Time.time * 0.11f) * 0.75f;
            Vector3 target = playerPosition - _travelDirection * distance +
                             right * lateral;
            target.y = GroundHeight(target) +
                       Mathf.Lerp(
                           terrainClearance,
                           environment.MaximumHeight, 0.35f);
            return target;
        }

        // Advances a slowly changing circular orbit around the player.
        private Vector3 GetOrbitTarget(
            Player player, Vector3 playerPosition,
            DroneEnvironment environment, out float targetSpeed)
        {
            float permittedRadius = Mathf.Clamp(
                _preferredOrbitRadius, MinimumOrbitRadius,
                environment.MaximumOrbitRadius);
            _orbitRadius = Mathf.SmoothDamp(
                _orbitRadius, permittedRadius, ref _orbitRadiusVelocity,
                OrbitRadiusSmoothTime, MaximumOrbitRadialSpeed);
            float radius = _orbitRadius;
            if (!_orbitPlaneInitialized)
            {
                InitializeOrbitPlan(player, playerPosition, environment);
                radius = _orbitRadius;
            }
            Vector3 radial = new Vector3(
                Mathf.Cos(_orbitAngle), 0f, Mathf.Sin(_orbitAngle));
            float frontAmount = Vector3.Dot(radial, _travelDirection) *
                                0.5f + 0.5f;
            float orbitSpeedMultiplier = Mathf.Lerp(
                RearOrbitSpeedMultiplier, FrontOrbitSpeedMultiplier,
                frontAmount);
            float orbitLinearSpeed = OrbitSpeed * orbitSpeedMultiplier;
            targetSpeed = orbitLinearSpeed;
            _orbitAngle += _orbitDirection * orbitLinearSpeed /
                           Mathf.Max(radius, 1f) * Time.deltaTime;
            Vector3 target = playerPosition + radial * radius;
            float closeBlend = Mathf.SmoothStep(
                0f, 1f,
                Mathf.InverseLerp(
                    CloseOrbitDistance,
                    CloseOrbitDistance + CloseOrbitHeightBlendDistance,
                    radius));
            float maximumHeight = Mathf.Lerp(
                Mathf.Min(CloseOrbitMaximumHeight, environment.MaximumHeight),
                environment.MaximumHeight, closeBlend);
            float ground = GroundHeight(target);
            float preferredHeight = player.m_eye != null
                ? player.m_eye.position.y
                : playerPosition.y + 1.6f;
            preferredHeight += _orbitSlopeX * (target.x - playerPosition.x) +
                               _orbitSlopeZ * (target.z - playerPosition.z);
            target.y = Mathf.Clamp(
                preferredHeight,
                ground + MinimumOrbitHeight,
                ground + maximumHeight);
            Vector3 tangent = new Vector3(-radial.z, 0f, radial.x) *
                              _orbitDirection;
            target += tangent * orbitLinearSpeed * OrbitMotionLeadSeconds;
            TrajectoryProbeTarget = target + tangent * orbitLinearSpeed *
                                    OrbitAnticipationSeconds;
            return target;
        }

        // Selects the clearest complete ellipse before an orbit begins.
        private void InitializeOrbitPlan(
            Player player, Vector3 playerPosition,
            DroneEnvironment environment)
        {
            float preferredRadius = Mathf.Clamp(
                _preferredOrbitRadius, MinimumOrbitRadius,
                environment.MaximumOrbitRadius);
            float headHeight = player.m_eye != null
                ? player.m_eye.position.y
                : playerPosition.y + 1.6f;
            int bestObstacles = int.MaxValue;
            float bestDistance = float.MaxValue;
            for (int index = 0; index < OrbitRadiusCandidates; index++)
            {
                float amount = index / (OrbitRadiusCandidates - 1f);
                float radius = Mathf.Lerp(
                    MinimumOrbitRadius,
                    environment.MaximumOrbitRadius, amount);
                Vector2 slope = FitOrbitPlane(playerPosition, radius);
                int obstacles = CountOrbitObstacles(
                    playerPosition, headHeight, radius, slope);
                float distance = Mathf.Abs(radius - preferredRadius);
                if (obstacles < bestObstacles ||
                    obstacles == bestObstacles && distance < bestDistance)
                {
                    bestObstacles = obstacles;
                    bestDistance = distance;
                    _preferredOrbitRadius = radius;
                    _orbitSlopeX = slope.x;
                    _orbitSlopeZ = slope.y;
                }
            }

            _orbitPlaneInitialized = true;
            LogOrbitPlane(bestObstacles);
        }

        // Fits one terrain plane from a complete candidate orbit.
        private static Vector2 FitOrbitPlane(
            Vector3 playerPosition, float radius)
        {
            float sumXHeight = 0f;
            float sumZHeight = 0f;
            float sumSquared = 0f;
            for (int index = 0; index < OrbitTerrainSamples; index++)
            {
                float angle = Mathf.PI * 2f * index / OrbitTerrainSamples;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                float height = GroundHeight(
                    playerPosition + new Vector3(x, 0f, z));
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

        // Counts blocked segments over one complete candidate ellipse.
        private static int CountOrbitObstacles(
            Vector3 center, float headHeight, float radius, Vector2 slope)
        {
            int obstacles = 0;
            Vector3 previous = GetPlannedOrbitPoint(
                center, headHeight, radius, slope,
                OrbitTerrainSamples - 1);
            for (int index = 0; index < OrbitTerrainSamples; index++)
            {
                Vector3 point = GetPlannedOrbitPoint(
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
        private static Vector3 GetPlannedOrbitPoint(
            Vector3 center, float headHeight, float radius,
            Vector2 slope, int index)
        {
            float angle = Mathf.PI * 2f * index / OrbitTerrainSamples;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            return new Vector3(
                center.x + x, headHeight + slope.x * x + slope.y * z,
                center.z + z);
        }

        // Logs the planned ellipse incline for trajectory diagnostics.
        private void LogOrbitPlane(int obstacleCount)
        {
            float slope = Mathf.Sqrt(
                _orbitSlopeX * _orbitSlopeX + _orbitSlopeZ * _orbitSlopeZ);
            float angle = Mathf.Atan(slope) * Mathf.Rad2Deg;
            SagaCapturePlugin.Log.LogInfo(
                $"Orbit ellipse planned: incline={angle:F1} degrees, " +
                $"radius={_orbitRadius:F2}m, obstacles={obstacleCount}, " +
                $"slope=({_orbitSlopeX:F3}, {_orbitSlopeZ:F3}).");
        }

        // Scales catch-up speed with trailing error up to the sprint limit.
        private static float GetTrailingSpeed(
            Player player, Vector3 dronePosition)
        {
            float playerSpeed = Flatten(player.GetVelocity()).magnitude;
            float distance = Flatten(
                dronePosition - player.transform.position).magnitude;
            float error = Mathf.Max(0f, distance - MaximumTrailingDistance);
            return Mathf.Min(
                player.m_runSpeed * 1.5f,
                playerSpeed + error * 0.75f);
        }

        // Returns terrain height or the input height when unavailable.
        private static float GroundHeight(Vector3 position)
        {
            return ZoneSystem.instance != null &&
                   ZoneSystem.instance.GetGroundHeight(position, out float height)
                ? height
                : position.y;
        }

        // Removes vertical movement from a vector.
        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
