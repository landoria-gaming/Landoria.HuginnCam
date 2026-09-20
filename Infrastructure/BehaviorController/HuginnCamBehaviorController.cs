using UnityEngine;

namespace Landoria.HuginnCam
{
    // Selects and coordinates the highest-priority Huginn behavior.
    internal sealed class HuginnCamBehaviorController
    {
        private const float StationaryRadius = 3f;
        private const float MovementProgressDistance = 1f;
        private const float StationaryDelay = 3f;
        private const float MinimumIdleHorizontalDistance = 2f;
        private float _height;
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
        private readonly HuginnCamOrbitFlight _orbitFlight = new HuginnCamOrbitFlight();
        private readonly HuginnCamLanding _landingBehavior = new HuginnCamLanding();
        private readonly HuginnCamResting _restingBehavior = new HuginnCamResting();
        private readonly HuginnCamTrailingFlight _trailingFlight = new HuginnCamTrailingFlight();
        internal bool IsLanding => _landingBehavior.IsActive;
        internal bool IsResting => _restingBehavior.IsActive;
        internal bool IsPlayerMoving => _isMoving;
        internal bool IsDangerActive => _combatObserver.IsActive;
        internal Vector3 TravelDirection => _travelDirection;
        internal HuginnCamFlightProfile Profile => _combatObserver.IsActive
            ? _combatObserver.Profile
            : _restingBehavior.IsActive
                ? _restingBehavior.Profile
                : _landingBehavior.IsActive
                    ? _landingBehavior.Profile
                    : _isMoving
                        ? _trailingFlight.Profile
                        : _orbitFlight.Profile;
        // Returns the current wandering destination in world space.
        internal Vector3 GetTarget(Player player, Vector3 cameraPosition)
        {
            _combatObserver.Update(player, cameraPosition);
            if (_landingBehavior.CancelForDanger(
                _combatObserver.IsActive))
            {
                AdvanceNow();
            }
            bool moving = UpdateMotion(player);
            if (_restingBehavior.CancelFor(
                moving, _combatObserver.IsActive))
            {
                AdvanceNow();
            }
            if (_landingBehavior.UpdateSafeIdleState(
                moving || _restingBehavior.IsActive,
                _combatObserver.IsActive))
            {
                AdvanceNow();
            }
            if (!_initialized || moving != _movingTarget || Time.time >= _nextTargetTime)
            {
                SelectTarget(player, moving);
            }
            Vector3 horizontalPosition = _combatObserver.IsActive
                ? _combatObserver.GetTarget(player)
                : moving
                    ? _trailingFlight.GetPosition(player.transform.position, _travelDirection)
                    : _orbitFlight.GetPosition(player.transform.position, _idleForwardDirection);
            float behaviorHeight = moving
                ? _trailingFlight.Height
                : _landingBehavior.IsActive || _restingBehavior.IsActive
                    ? _height
                    : _orbitFlight.Height;
            float height = _combatObserver.IsActive
                ? _combatObserver.ClampHeight(behaviorHeight)
                : behaviorHeight;
            horizontalPosition.y = GetGroundHeight(horizontalPosition) + height;
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
        // Transfers a completed landing into a grounded resting session.
        internal bool UpdateResting(Vector3 cameraPosition, Vector3 target)
        {
            if (_landingBehavior.TryComplete(cameraPosition, target))
            {
                _restingBehavior.Begin();
                return true;
            }

            if (_restingBehavior.Update())
            {
                AdvanceNow();
            }
            return _restingBehavior.IsActive;
        }

        // Applies the look policy owned by the current primary behavior.
        internal void UpdateLook(
            HuginnCamBehaviorState state, HuginnCamLook look,
            Transform cameraTransform, Vector3 playerFocus)
        {
            switch (state)
            {
                case HuginnCamBehaviorState.Resting:
                    _restingBehavior.UpdateLook(
                        look, cameraTransform, playerFocus);
                    break;
                case HuginnCamBehaviorState.Landing:
                    look.UpdateLanding(cameraTransform, playerFocus);
                    break;
                case HuginnCamBehaviorState.CombatObserver:
                    look.Update(cameraTransform,
                        _combatObserver.GetFocus(playerFocus));
                    break;
                case HuginnCamBehaviorState.OrbitFlight:
                    _orbitFlight.UpdateLook(
                        look, cameraTransform, playerFocus);
                    break;
                default:
                    look.Update(cameraTransform, playerFocus);
                    break;
            }
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
            return _isMoving && !_combatObserver.IsActive &&
                   !_landingBehavior.IsActive && !_restingBehavior.IsActive
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
                _orbitFlight.Reset();
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
            bool initialLanding = !_initialized;
            _landingBehavior.SelectState(
                moving, initialLanding, _combatObserver.IsActive);
            if (moving)
            {
                _trailingFlight.SelectTarget();
            }
            else
            {
                if (_landingBehavior.IsActive)
                {
                    _height = _landingBehavior.Height;
                }
                if (_landingBehavior.IsActive &&
                    !_landingBehavior.TrySelectTarget(
                    player, _orbitFlight, _idleForwardDirection))
                {
                    _landingBehavior.Cancel();
                    _orbitFlight.SelectTarget();
                }
                else if (!_landingBehavior.IsActive)
                {
                    _orbitFlight.SelectTarget();
                }
            }

            _nextTargetTime = _landingBehavior.IsActive
                ? float.PositiveInfinity
                : Time.time + (moving ? 0.5f : Random.Range(4f, 8f));
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
