using UnityEngine;

namespace Landoria.HuginnCam
{
    // Selects smooth camera destinations around a moving or stationary player.
    internal sealed class HuginnCamWander
    {
        private const float StationaryRadius = 3f;
        private const float MovementProgressDistance = 1f;
        private const float StationaryDelay = 3f;
        private const float MinimumMovingHeight = 1f;
        private const float MaximumMovingHeight = 8f;
        private const float MinimumIdleHeight = 1f;
        private const float MaximumIdleHeight = 3f;
        private const float LandingHeight = 0.2f;
        private const float LandingChance = 0.2f;
        private const float LandingArrivalDistance = 0.3f;
        private const float MinimumMovingDistance = 0f;
        private const float MaximumMovingDistance = 6f;
        private const float MaximumLateralDistance = 5f;
        private const float MinimumIdleDistance = 3f;
        private const float MaximumIdleDistance = 10f;
        private const float MinimumIdleHorizontalDistance = 2f;
        private float _distance;
        private float _height;
        private float _lateral;
        private float _idleLateral;
        private float _idleLongitudinal;
        private float _nextTargetTime;
        private bool _movingTarget;
        private bool _initialized;
        private bool _motionInitialized;
        private bool _isMoving;
        private Vector3 _stationaryAnchor;
        private Vector3 _lastProgressPosition;
        private Vector3 _travelDirection;
        private Vector3 _idleForwardDirection;
        private float _lastProgressTime;
        private bool _idleCoordinatesInitialized;
        private bool _changeIdleLateralNext;
        private bool _landing;
        private bool _perched;
        private float _perchEndTime;

        internal bool IsLanding => _landing;
        internal bool IsPlayerMoving => _isMoving;

        // Returns the current wandering destination in world space.
        internal Vector3 GetTarget(Player player)
        {
            bool moving = UpdateMotion(player);
            if (!_initialized || moving != _movingTarget || Time.time >= _nextTargetTime)
            {
                SelectTarget(moving);
            }

            Vector3 horizontalPosition = moving
                ? GetMovingPosition(player.transform.position, _travelDirection)
                : GetIdlePosition(player.transform.position);
            horizontalPosition.y = GetGroundHeight(horizontalPosition) + _height;
            return horizontalPosition;
        }

        // Schedules an early destination change after an obstructed choice.
        internal void RetrySoon()
        {
            _nextTargetTime = Mathf.Min(_nextTargetTime, Time.time + 0.5f);
        }

        // Selects the next destination immediately before the camera can stop.
        internal void AdvanceNow()
        {
            _nextTargetTime = Time.time;
        }

        // Holds Huginn still for several seconds after a gentle landing.
        internal bool UpdatePerch(Vector3 cameraPosition, Vector3 target)
        {
            if (!_landing)
            {
                return false;
            }

            if (!_perched &&
                Vector3.Distance(cameraPosition, target) <= LandingArrivalDistance)
            {
                _perched = true;
                _perchEndTime = Time.time + Random.Range(3f, 7f);
            }

            if (!_perched || Time.time < _perchEndTime)
            {
                return _perched;
            }

            _landing = false;
            _perched = false;
            AdvanceNow();
            return false;
        }

        // Keeps the camera path outside the player's horizontal personal space.
        internal Vector3 KeepOutsideMinimumRadius(Player player, Vector3 position)
        {
            Vector3 horizontal = position - player.transform.position;
            horizontal.y = 0f;
            if (horizontal.magnitude >= MinimumIdleHorizontalDistance)
            {
                return position;
            }

            Vector3 direction = horizontal.sqrMagnitude > 0.001f
                ? horizontal.normalized
                : _idleForwardDirection;
            position.x = player.transform.position.x +
                         direction.x * MinimumIdleHorizontalDistance;
            position.z = player.transform.position.z +
                         direction.z * MinimumIdleHorizontalDistance;
            return position;
        }

        // Classifies sustained travel while ignoring movement inside a small area.
        private bool UpdateMotion(Player player)
        {
            Vector3 position = Flatten(player.transform.position);
            if (!_motionInitialized)
            {
                InitializeMotion(position, player.transform.forward);
                return false;
            }

            if (!_isMoving)
            {
                TryStartMoving(position);
            }
            else
            {
                UpdateMovingState(position);
            }

            return _isMoving;
        }

        // Initializes the movement tracker at the player's current position.
        private void InitializeMotion(Vector3 position, Vector3 facing)
        {
            _stationaryAnchor = position;
            _lastProgressPosition = position;
            _travelDirection = Flatten(facing).normalized;
            if (_travelDirection.sqrMagnitude < 0.001f)
            {
                _travelDirection = Vector3.forward;
            }

            _idleForwardDirection = _travelDirection;
            _lastProgressTime = Time.time;
            _motionInitialized = true;
        }

        // Starts travel mode only after leaving the stationary area.
        private void TryStartMoving(Vector3 position)
        {
            Vector3 displacement = position - _stationaryAnchor;
            if (displacement.magnitude <= StationaryRadius)
            {
                return;
            }

            _isMoving = true;
            _travelDirection = displacement.normalized;
            _lastProgressPosition = position;
            _lastProgressTime = Time.time;
        }

        // Tracks meaningful progress and returns to idle after lingering nearby.
        private void UpdateMovingState(Vector3 position)
        {
            Vector3 progress = position - _lastProgressPosition;
            if (progress.magnitude >= MovementProgressDistance)
            {
                _travelDirection = progress.normalized;
                _lastProgressPosition = position;
                _lastProgressTime = Time.time;
                return;
            }

            if (Time.time - _lastProgressTime >= StationaryDelay)
            {
                _isMoving = false;
                _stationaryAnchor = position;
                _idleForwardDirection = _travelDirection;
                _idleCoordinatesInitialized = false;
            }
        }

        // Removes vertical movement from one world-space vector.
        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        // Chooses a new point and schedules the following choice.
        private void SelectTarget(bool moving)
        {
            _movingTarget = moving;
            _landing = !moving && _initialized && !_landing &&
                       Random.value < LandingChance;
            _perched = false;
            _height = _landing
                ? LandingHeight
                : Random.Range(
                    moving ? MinimumMovingHeight : MinimumIdleHeight,
                    moving ? MaximumMovingHeight : MaximumIdleHeight);
            if (moving)
            {
                _distance = Random.Range(MinimumMovingDistance, MaximumMovingDistance);
                _lateral = Random.Range(-MaximumLateralDistance, MaximumLateralDistance);
            }
            else
            {
                SelectIdleCoordinates();
            }

            _nextTargetTime = _landing
                ? float.PositiveInfinity
                : Time.time + Random.Range(4f, 8f);
            _initialized = true;
        }

        // Changes only one horizontal idle axis for each new destination.
        private void SelectIdleCoordinates()
        {
            if (!_idleCoordinatesInitialized)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(MinimumIdleDistance, MaximumIdleDistance);
                _idleLateral = Mathf.Sin(angle) * radius;
                _idleLongitudinal = Mathf.Cos(angle) * radius;
                _idleCoordinatesInitialized = true;
                _changeIdleLateralNext = Random.value >= 0.5f;
                return;
            }

            if (_changeIdleLateralNext)
            {
                _idleLateral = SelectCoordinate(
                    _idleLongitudinal, _idleLateral);
            }
            else
            {
                _idleLongitudinal = SelectCoordinate(
                    _idleLateral, _idleLongitudinal);
            }

            _changeIdleLateralNext = !_changeIdleLateralNext;
        }

        // Selects one axis while keeping the point inside the idle annulus.
        private static float SelectCoordinate(
            float fixedCoordinate, float currentCoordinate)
        {
            float fixedSquared = fixedCoordinate * fixedCoordinate;
            float maximum = Mathf.Sqrt(Mathf.Max(
                0f, MaximumIdleDistance * MaximumIdleDistance - fixedSquared));
            float minimum = fixedSquared >= MinimumIdleDistance * MinimumIdleDistance
                ? 0f
                : Mathf.Sqrt(MinimumIdleDistance * MinimumIdleDistance - fixedSquared);
            float magnitude = Random.Range(minimum, maximum);
            bool preserveSide = Mathf.Abs(fixedCoordinate) <
                                MinimumIdleHorizontalDistance;
            bool positive = preserveSide
                ? currentCoordinate >= 0f
                : Random.value >= 0.5f;
            return positive ? magnitude : -magnitude;
        }

        // Places a moving target inside the box behind the travel direction.
        private Vector3 GetMovingPosition(Vector3 origin, Vector3 direction)
        {
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            return origin - direction * _distance + right * _lateral;
        }

        // Places an idle target on stable axes around the player.
        private Vector3 GetIdlePosition(Vector3 origin)
        {
            Vector3 right = Vector3.Cross(Vector3.up, _idleForwardDirection);
            return origin + right * _idleLateral +
                   _idleForwardDirection * _idleLongitudinal;
        }

        // Resolves the terrain height below one horizontal camera position.
        private static float GetGroundHeight(Vector3 position)
        {
            if (ZoneSystem.instance != null &&
                ZoneSystem.instance.GetGroundHeight(position, out float height))
            {
                return height;
            }

            return position.y;
        }
    }
}
