using UnityEngine;

namespace Landoria.HuginnCam
{
    // Selects destinations while the player is travelling.
    internal sealed class HuginnCamTrailingFlight
    {
        private const float MinimumDistance = 3f;
        private const float MaximumDistance = 6f;
        private const float MaximumLateralDistance = 5f;
        private const float MinimumHeight = 3f;
        private const float MaximumHeight = 10f;
        private const float MinimumSpeed = 0.2f;
        private const float MaximumCruiseSpeed = 1f;
        private const float MaximumSpeed = 1f;
        private const float OffsetSmoothTime = 2f;
        private const float MaximumOffsetSpeed = 1f;
        private const float MaximumHeightSpeed = 0.75f;
        private const float ObstacleCheckInterval = 0.5f;
        private const float TerrainLookAheadDistance = 10f;
        private const float TerrainLookAheadClearance = 3f;
        private float _distance;
        private float _lateral;
        private float _height;
        private float _targetDistance;
        private float _targetLateral;
        private float _targetHeight;
        private float _distanceVelocity;
        private float _lateralVelocity;
        private float _heightVelocity;
        private float _nextObstacleCheckTime;
        private bool _initialized;

        internal HuginnCamFlightProfile Profile => new HuginnCamFlightProfile(
            MinimumSpeed, MaximumCruiseSpeed, MaximumSpeed,
            float.MaxValue, 0f, 0.4f);

        internal float Height => _height;

        // Chooses a point in the moving flight box.
        internal void SelectTarget()
        {
            if (!_initialized)
            {
                _targetDistance = Random.Range(MinimumDistance, MaximumDistance);
                _targetLateral = Random.Range(
                    -MaximumLateralDistance, MaximumLateralDistance);
                _targetHeight = Random.Range(MinimumHeight, MaximumHeight);
                _distance = _targetDistance;
                _lateral = _targetLateral;
                _height = _targetHeight;
                _initialized = true;
                return;
            }

            _targetDistance = Mathf.Clamp(
                _targetDistance + Random.Range(-1f, 1f),
                MinimumDistance, MaximumDistance);
            _targetLateral = Mathf.Clamp(
                _targetLateral + Random.Range(-1f, 1f),
                -MaximumLateralDistance, MaximumLateralDistance);
            _targetHeight = Mathf.Clamp(
                _targetHeight + Random.Range(-0.5f, 0.5f),
                MinimumHeight, MaximumHeight);
        }

        // Resolves the selected point relative to the player's travel direction.
        internal Vector3 GetPosition(
            Vector3 origin, Vector3 direction, Vector3 cameraPosition)
        {
            UpdateOffsets();
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            Vector3 target = origin - direction * _distance + right * _lateral;
            if (Time.time >= _nextObstacleCheckTime)
            {
                ApplyObstacleAvoidance(cameraPosition, target, right);
                _nextObstacleCheckTime = Time.time + ObstacleCheckInterval;
            }

            return origin - direction * _distance + right * _lateral;
        }

        // Starts climbing as soon as rising terrain appears along the route.
        internal Vector3 AnticipateTerrain(
            Vector3 cameraPosition, Vector3 target)
        {
            return HuginnCamTerrain.AnticipateRise(
                cameraPosition, target, TerrainLookAheadDistance,
                TerrainLookAheadClearance);
        }

        // Smoothly redirects the destination sideways when one side clears the route.
        private void ApplyObstacleAvoidance(
            Vector3 cameraPosition, Vector3 target, Vector3 right)
        {
            if (!HuginnCamTrailingObstacleAvoidance.TryChooseOffset(
                    cameraPosition, target, right, _targetLateral,
                    out float lateral))
            {
                return;
            }

            _targetLateral = Mathf.Clamp(
                lateral, -MaximumLateralDistance, MaximumLateralDistance);
        }

        // Smooths all relative offsets toward the latest half-second target.
        private void UpdateOffsets()
        {
            _distance = Mathf.SmoothDamp(
                _distance, _targetDistance, ref _distanceVelocity,
                OffsetSmoothTime, MaximumOffsetSpeed);
            _lateral = Mathf.SmoothDamp(
                _lateral, _targetLateral, ref _lateralVelocity,
                OffsetSmoothTime, MaximumOffsetSpeed);
            _height = Mathf.SmoothDamp(
                _height, _targetHeight, ref _heightVelocity,
                OffsetSmoothTime, MaximumHeightSpeed);
        }

        // Keeps one movement step behind the player's travelling plane.
        internal Vector3 KeepBehind(
            Vector3 playerPosition, Vector3 direction, Vector3 position)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                return position;
            }

            direction.Normalize();
            Vector3 offset = position - playerPosition;
            float forwardDistance = Vector3.Dot(offset, direction);
            float maximumForwardDistance = -MinimumDistance;
            if (forwardDistance > maximumForwardDistance)
            {
                position -= direction *
                            (forwardDistance - maximumForwardDistance);
            }

            return position;
        }
    }
}
