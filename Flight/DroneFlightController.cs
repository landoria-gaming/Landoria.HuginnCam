using UnityEngine;

namespace Landoria.SagaCapture
{
    // Selects flight modes and produces their continuous destinations.
    internal sealed class DroneFlightController
    {
        private const float OrbitSpeed = 0.5f;
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
        private const float OrbitRadiusPeriod = 30f;
        private const float FrontOrbitSpeedMultiplier = 0.5f;
        private const float RearOrbitSpeedMultiplier = 1.5f;
        private DroneFlightMode _mode;
        private Vector3 _travelDirection = Vector3.forward;
        private float _orbitAngle;
        private float _orbitDirection = 1f;
        private bool _initialized;

        internal DroneFlightMode Mode => _mode;

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
            DroneEnvironment environment, out float targetSpeed)
        {
            Vector3 playerPosition = player.transform.position;
            Vector3 playerVelocity = Flatten(player.GetVelocity());
            UpdateTravelDirection(player, playerVelocity);
            SelectMode(playerPosition, dronePosition, playerVelocity.magnitude);
            if (_mode == DroneFlightMode.TrailingFlight)
            {
                targetSpeed = GetTrailingSpeed(player, dronePosition);
                return GetTrailingTarget(
                    playerPosition, environment,
                    GetTerrainClearance(targetSpeed));
            }

            targetSpeed = OrbitSpeed;
            return GetOrbitTarget(playerPosition, environment);
        }

        // Chooses a stable mode with distance hysteresis.
        private void SelectMode(
            Vector3 playerPosition, Vector3 dronePosition, float playerSpeed)
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
                     playerSpeed <= OrbitSpeed)
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
            Vector3 playerPosition, DroneEnvironment environment)
        {
            float radiusPhase = 0.5f +
                                Mathf.Sin(Time.time * Mathf.PI * 2f /
                                          OrbitRadiusPeriod) * 0.5f;
            float radius = Mathf.Lerp(
                1f, environment.MaximumOrbitRadius, radiusPhase);
            Vector3 radial = new Vector3(
                Mathf.Cos(_orbitAngle), 0f, Mathf.Sin(_orbitAngle));
            float frontAmount = Vector3.Dot(radial, _travelDirection) *
                                0.5f + 0.5f;
            float orbitSpeedMultiplier = Mathf.Lerp(
                RearOrbitSpeedMultiplier, FrontOrbitSpeedMultiplier,
                frontAmount);
            _orbitAngle += _orbitDirection * OrbitSpeed *
                           orbitSpeedMultiplier /
                           Mathf.Max(radius, 1f) * Time.deltaTime;
            Vector3 target = playerPosition + radial * radius;
            float heightPhase = 0.5f + Mathf.Sin(Time.time * 0.13f) * 0.5f;
            float closeBlend = Mathf.SmoothStep(
                0f, 1f,
                Mathf.InverseLerp(
                    CloseOrbitDistance,
                    CloseOrbitDistance + CloseOrbitHeightBlendDistance,
                    radius));
            float maximumHeight = Mathf.Lerp(
                Mathf.Min(CloseOrbitMaximumHeight, environment.MaximumHeight),
                environment.MaximumHeight, closeBlend);
            target.y = GroundHeight(target) + Mathf.Lerp(
                MinimumOrbitHeight, maximumHeight, heightPhase);
            return target;
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
