using UnityEngine;

namespace Landoria.SagaCapture
{
    // Places a higher trailing drone farther behind the player.
    internal sealed class TrailingFlightController
    {
        private readonly TrailingRoutePlanner _route =
            new TrailingRoutePlanner();
        private const float MinimumDistance = 2f;
        private const float MaximumDistance = 6f;
        private const float HighAltitudeExtraDistance = 7f;
        private const float AltitudeBlendStart = 2f;
        private const float AltitudeBlendEnd = 8f;
        private const float AltitudeCycleRate = 0.08f;
        private const float AltitudeSmoothTime = 3f;
        private const float MaximumAltitudeChangeSpeed = 0.4f;
        private const float PreferredAltitudeVariation = 1f;
        private float _altitude;
        private float _altitudeVelocity;
        private bool _altitudeInitialized;

        // Predicts a short trailing route around nearby obstacles.
        internal Vector3 PlanRoute(
            Vector3 origin, Vector3 target, Vector3 droneVelocity,
            Vector3 playerPosition, Vector3 playerVelocity)
        {
            return _route.Plan(
                origin, target, droneVelocity,
                playerPosition, playerVelocity);
        }

        // Clears any route state when trailing flight ends.
        internal void Leave()
        {
            _route.Reset();
            _altitudeInitialized = false;
            _altitudeVelocity = 0f;
        }

        // Builds a trailing destination with altitude-dependent distance.
        internal Vector3 GetTarget(
            Vector3 playerPosition, Vector3 dronePosition,
            Vector3 direction,
            DroneEnvironment environment, float terrainClearance)
        {
            float horizontalDistance = Flatten(
                dronePosition - playerPosition).magnitude;
            float allowedHeight = Mathf.Min(
                environment.MaximumHeight,
                Mathf.Lerp(2f, 8f, Mathf.InverseLerp(
                    2f, 10f, horizontalDistance)));
            float altitude = GetAltitude(
                terrainClearance, allowedHeight);
            float distance = Mathf.Lerp(
                MinimumDistance, MaximumDistance,
                0.75f + Mathf.Sin(Time.time * 0.17f) * 0.25f);
            distance += HeightDistance(altitude);
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            float lateral = Mathf.Sin(Time.time * 0.11f) * 0.75f;
            Vector3 target = playerPosition - direction * distance +
                             right * lateral;
            target.y = GroundHeight(target) + altitude;
            return target;
        }

        // Moves toward the trailing target even when the player stands still.
        internal float GetSpeed(
            Player player, Vector3 dronePosition,
            Vector3 target)
        {
            float playerSpeed = Flatten(player.GetVelocity()).magnitude;
            float error = Flatten(target - dronePosition).magnitude;
            return Mathf.Min(
                player.m_runSpeed * 1.5f,
                playerSpeed + error * 0.75f);
        }

        // Prefers low flight while respecting the distance-based height cap.
        private float GetAltitude(float minimum, float maximum)
        {
            float amount = 0.5f +
                           Mathf.Sin(Time.time * AltitudeCycleRate) * 0.5f;
            float desired = Mathf.Min(
                maximum, minimum + amount * PreferredAltitudeVariation);
            if (!_altitudeInitialized)
            {
                _altitude = desired;
                _altitudeInitialized = true;
            }
            _altitude = Mathf.SmoothDamp(
                _altitude, desired, ref _altitudeVelocity,
                AltitudeSmoothTime, MaximumAltitudeChangeSpeed);
            return _altitude;
        }

        // Converts height above ground into extra room behind the player.
        private static float HeightDistance(float altitude)
        {
            return Mathf.InverseLerp(
                AltitudeBlendStart, AltitudeBlendEnd, altitude) *
                HighAltitudeExtraDistance;
        }

        // Reads terrain height beneath a trailing destination.
        private static float GroundHeight(Vector3 position)
        {
            return ZoneSystem.instance != null &&
                   ZoneSystem.instance.GetGroundHeight(
                       position, out float height)
                ? height : position.y;
        }

        // Removes the vertical component from a velocity or offset.
        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
