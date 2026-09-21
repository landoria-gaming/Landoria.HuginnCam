using UnityEngine;

namespace Landoria.SagaCapture
{
    // Switches between orbit and trailing without owning either flight path.
    internal sealed class DroneFlightController
    {
        private const float OrbitEntryMaximumDroneSpeed = 1f;
        private const float TrailingEntryDistance = 5f;
        private const float OrbitReturnDistance = 4f;
        private const float MinimumTrailingHeight = 2f;
        private const float SlowTrailingClearance = 1.5f;
        private const float MinimumOrbitHeight = 0.5f;
        private const float FullTerrainClearanceSpeed = 3f;
        private readonly OrbitFlightController _orbit =
            new OrbitFlightController();
        private readonly TrailingFlightController _trailing =
            new TrailingFlightController();
        private DroneFlightMode _mode;
        private Vector3 _travelDirection = Vector3.forward;
        private bool _initialized;

        internal DroneFlightMode Mode => _mode;
        internal Vector3 TrajectoryProbeTarget { get; private set; }

        // Predicts the trailing route without involving the orbit controller.
        internal Vector3 PlanTrailingRoute(
            Vector3 origin, Vector3 target, Vector3 droneVelocity,
            Vector3 playerPosition, Vector3 playerVelocity)
        {
            return _trailing.PlanRoute(
                origin, target, droneVelocity,
                playerPosition, playerVelocity);
        }

        // Returns the active mode's terrain clearance at the current speed.
        internal float GetTerrainClearance(float speed)
        {
            float blend = Mathf.InverseLerp(
                OrbitFlightController.Speed,
                FullTerrainClearanceSpeed, speed);
            return _mode == DroneFlightMode.TrailingFlight
                ? Mathf.Lerp(
                    SlowTrailingClearance, MinimumTrailingHeight, blend)
                : Mathf.Lerp(
                    MinimumOrbitHeight, MinimumTrailingHeight, blend);
        }

        // Selects a mode and delegates its desired position and speed.
        internal Vector3 Update(
            Player player, Vector3 dronePosition,
            float droneSpeed, DroneEnvironment environment,
            out float targetSpeed)
        {
            Vector3 playerPosition = player.transform.position;
            Vector3 playerVelocity = Flatten(player.GetVelocity());
            UpdateTravelDirection(player, playerVelocity);
            SelectMode(playerPosition, dronePosition,
                       playerVelocity.magnitude, droneSpeed,
                       environment.MaximumHeight);
            if (_mode == DroneFlightMode.TrailingFlight)
            {
                Vector3 target = _trailing.GetTarget(
                    playerPosition, dronePosition, _travelDirection,
                    environment,
                    GetTerrainClearance(droneSpeed));
                targetSpeed = _trailing.GetSpeed(
                    player, dronePosition, target);
                TrajectoryProbeTarget = target;
                return target;
            }

            Vector3 orbitTarget = _orbit.Update(
                player, _travelDirection, environment, out targetSpeed);
            TrajectoryProbeTarget = _orbit.ProbeTarget;
            return orbitTarget;
        }

        // Chooses a stable mode with distance and speed hysteresis.
        private void SelectMode(
            Vector3 playerPosition, Vector3 dronePosition,
            float playerSpeed, float droneSpeed,
            float maximumOrbitHeight)
        {
            float distance = Flatten(
                dronePosition - playerPosition).magnitude;
            DroneFlightMode next = _mode;
            if (!_initialized)
            {
                next = distance >= TrailingEntryDistance ||
                       dronePosition.y - playerPosition.y >
                           maximumOrbitHeight
                    ? DroneFlightMode.TrailingFlight
                    : DroneFlightMode.OrbitFlight;
                InitializeMode(next, playerPosition, dronePosition);
                return;
            }
            if (_mode == DroneFlightMode.OrbitFlight &&
                distance >= TrailingEntryDistance)
            {
                next = DroneFlightMode.TrailingFlight;
            }
            else if (_mode == DroneFlightMode.TrailingFlight &&
                     distance <= OrbitReturnDistance &&
                     dronePosition.y - playerPosition.y <=
                         maximumOrbitHeight &&
                     playerSpeed <= OrbitFlightController.Speed &&
                     droneSpeed <= OrbitEntryMaximumDroneSpeed)
            {
                next = DroneFlightMode.OrbitFlight;
            }
            ChangeMode(next, playerPosition, dronePosition);
        }

        // Initializes the first mode without alternating orbit direction.
        private void InitializeMode(
            DroneFlightMode mode, Vector3 playerPosition,
            Vector3 dronePosition)
        {
            _mode = mode;
            _initialized = true;
            if (mode == DroneFlightMode.OrbitFlight)
            {
                _orbit.Enter(playerPosition, dronePosition, false);
            }
            SagaCapturePlugin.Log.LogInfo(
                $"Drone flight initialized: {mode}.");
        }

        // Logs mode changes and alternates the next orbit direction.
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
            if (previous == DroneFlightMode.TrailingFlight)
            {
                _trailing.Leave();
            }
            if (next == DroneFlightMode.OrbitFlight)
            {
                _orbit.Enter(playerPosition, dronePosition, true);
            }
            SagaCapturePlugin.Log.LogInfo(
                $"Drone flight changed: {previous} -> {next}.");
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

        // Removes vertical movement from one vector.
        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
