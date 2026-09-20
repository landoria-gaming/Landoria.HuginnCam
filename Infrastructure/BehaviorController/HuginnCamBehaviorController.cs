using UnityEngine;

namespace Landoria.HuginnCam
{
    // Selects and coordinates the highest-priority Huginn behavior.
    internal sealed class HuginnCamBehaviorController
    {
        private const float StationaryRadius = 3f;
        private const float MovementProgressDistance = 1f;
        private const float StationaryDelay = 3f;
        private const float InitialMovementSpeed = 0.5f;
        private const float MinimumIdleHorizontalDistance = 2f;
        private const float ForestMaximumHeight = 3f;
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
        private readonly HuginnCamCombatObserver _combatObserver =
            new HuginnCamCombatObserver();
        private readonly HuginnCamObserverFlight _observerFlight =
            new HuginnCamObserverFlight();
        private readonly HuginnCamTrailingFlight _trailingFlight = new HuginnCamTrailingFlight();
        private readonly HuginnCamForestAwareness _forestAwareness =
            new HuginnCamForestAwareness();
        internal bool IsPlayerMoving => _isMoving;
        internal bool IsDangerActive => _combatObserver.IsActive;
        internal bool IsMobileDanger => _isMoving &&
                                        _combatObserver.HasRecentDanger;
        internal bool IsInForest => _forestAwareness.IsInForest;
        internal Vector3 TravelDirection => _travelDirection;
        internal HuginnCamFlightProfile Profile => _combatObserver.IsActive
            ? _combatObserver.Profile
            : _isMoving
                ? _trailingFlight.Profile
                : _observerFlight.Profile;
        // Returns the current wandering destination in world space.
        internal Vector3 GetTarget(Player player, Vector3 cameraPosition)
        {
            _forestAwareness.Update(
                player.transform.position, cameraPosition);
            bool moving = UpdateMotion(player);
            _combatObserver.Update(player, cameraPosition, moving);
            if (!_initialized || moving != _movingTarget || Time.time >= _nextTargetTime)
            {
                SelectTarget(player, moving);
            }
            if (!moving)
            {
                _observerFlight.Update(
                    cameraPosition, player.transform.position);
            }
            Vector3 horizontalPosition = _combatObserver.IsActive
                ? _combatObserver.GetTarget(player)
                : moving
                    ? _trailingFlight.GetPosition(
                        player.transform.position, _travelDirection,
                        cameraPosition)
                    : _observerFlight.GetPosition(
                        player.transform.position, _idleForwardDirection);
            float behaviorHeight = moving
                ? _trailingFlight.Height
                : _observerFlight.Height;
            float height = _combatObserver.IsActive
                ? _combatObserver.ClampHeight(behaviorHeight)
                : behaviorHeight;
            if (_forestAwareness.IsInForest)
            {
                height = Mathf.Min(height, ForestMaximumHeight);
            }
            horizontalPosition.y = GetGroundHeight(horizontalPosition) + height;
            if (moving && !_combatObserver.IsActive)
            {
                horizontalPosition = _trailingFlight.AnticipateTerrain(
                    cameraPosition, horizontalPosition);
            }
            return horizontalPosition;
        }
        // Selects the next destination immediately before the camera can stop.
        internal void AdvanceNow()
        {
            _nextTargetTime = Time.time;
        }
        // Applies the look policy owned by the current primary behavior.
        internal void UpdateLook(
            HuginnCamBehaviorState state, HuginnCamLook look,
            Transform cameraTransform, Vector3 playerFocus,
            Vector3 flightDirection)
        {
            if (state == HuginnCamBehaviorState.CombatObserver)
            {
                look.UpdateFlightAware(
                    cameraTransform, _combatObserver.GetFocus(playerFocus),
                    flightDirection);
                return;
            }

            look.UpdateFlightAware(
                cameraTransform, playerFocus, flightDirection);
        }

        // Keeps the camera path outside the player's horizontal personal space.
        internal Vector3 ConstrainNormalFlight(Player player, Vector3 position)
        {
            Vector3 horizontal = position - player.transform.position;
            horizontal.y = 0f;
            if (horizontal.magnitude >= MinimumIdleHorizontalDistance)
            {
                return KeepBehindPlayer(player, position);
            }

            Vector3 direction = horizontal.sqrMagnitude > 0.001f
                ? horizontal.normalized
                : _idleForwardDirection;
            position.x = player.transform.position.x +
                         direction.x * MinimumIdleHorizontalDistance;
            position.z = player.transform.position.z +
                         direction.z * MinimumIdleHorizontalDistance;
            return KeepBehindPlayer(player, position);
        }

        // Applies the trailing half-space only during ordinary player travel.
        private Vector3 KeepBehindPlayer(Player player, Vector3 position)
        {
            return _isMoving && !_combatObserver.IsActive
                ? _trailingFlight.KeepBehind(
                    player.transform.position, _travelDirection, position)
                : position;
        }

        // Classifies sustained travel while ignoring movement inside a small area.
        private bool UpdateMotion(Player player)
        {
            Vector3 position = Flatten(player.transform.position);
            if (!_motionInitialized)
            {
                InitializeMotion(position, player);
                return _isMoving;
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
        private void InitializeMotion(Vector3 position, Player player)
        {
            Vector3 velocity = Flatten(player.GetVelocity());
            _stationaryAnchor = position;
            _lastProgressPosition = position;
            _isMoving = velocity.magnitude >= InitialMovementSpeed ||
                        player.IsRunning();
            _travelDirection = _isMoving && velocity.sqrMagnitude > 0.001f
                ? velocity.normalized
                : Flatten(player.transform.forward).normalized;
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
                _observerFlight.Reset();
            }
        }

        // Removes vertical movement from one world-space vector.
        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        // Chooses a new point and schedules the following choice.
        private void SelectTarget(Player player, bool moving)
        {
            _movingTarget = moving;
            if (moving)
            {
                _trailingFlight.SelectTarget();
            }
            else
            {
                _observerFlight.SelectTarget(player.transform.position);
            }

            _nextTargetTime = Time.time +
                              (moving ? 0.5f : Random.Range(4f, 8f));
            _initialized = true;
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
