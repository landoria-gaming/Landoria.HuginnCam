using UnityEngine;

namespace Landoria.HuginnCam
{
    // Alternates calm approaches with curved retreats around an idle player.
    internal sealed class HuginnCamObserverFlight
    {
        private const float ApproachTargetDistance = 2.5f;
        private const float RetreatStartDistance = 3f;
        private const float RetreatReleaseDistance = 8f;
        private const float RetreatTargetDistance = 10f;
        private const float MinimumHeight = 1f;
        private const float MaximumHeight = 3f;
        private const float TurnAngle = 55f;
        private const float ApproachMinimumSpeed = 0.2f;
        private const float ApproachCruiseSpeed = 0.3f;
        private const float ApproachMaximumSpeed = 0.4f;
        private const float RetreatMinimumSpeed = 0.6f;
        private const float RetreatCruiseSpeed = 0.8f;
        private const float RetreatMaximumSpeed = 1f;
        private Vector3 _flightDirection;
        private float _height;
        private float _turnSide;
        private bool _initialized;
        private bool _pinned;
        private bool _retreating;

        internal float Height => _height;
        internal HuginnCamFlightProfile Profile => _retreating
            ? new HuginnCamFlightProfile(
                RetreatMinimumSpeed, RetreatCruiseSpeed,
                RetreatMaximumSpeed, 0f, 0.5f, 1.5f)
            : new HuginnCamFlightProfile(
                ApproachMinimumSpeed, ApproachCruiseSpeed,
                ApproachMaximumSpeed, float.MaxValue, 0f, 0.35f);

        // Forgets the previous idle-flight cycle after a movement transition.
        internal void Reset()
        {
            _initialized = false;
            _pinned = false;
            _retreating = false;
        }

        // Starts or resumes the approach-and-retreat idle flight.
        internal void SelectTarget(Vector3 origin)
        {
            _pinned = false;
            if (_initialized)
            {
                return;
            }

            _flightDirection = HuginnCamTreeAvoidance.ChooseTurn(
                origin, RandomHorizontalDirection(), 60f,
                Random.value < 0.5f ? -1f : 1f,
                RetreatTargetDistance);
            _height = Random.Range(MinimumHeight, MaximumHeight);
            _turnSide = Random.value < 0.5f ? -1f : 1f;
            _initialized = true;
        }

        // Pins an explicit player-relative point for a landing approach.
        internal void SetTarget(float lateral, float longitudinal)
        {
            _flightDirection = new Vector3(lateral, 0f, longitudinal);
            _pinned = true;
            _initialized = true;
        }

        // Advances the calm observation cycle from the current camera position.
        internal void Update(Vector3 cameraPosition, Vector3 playerPosition)
        {
            if (_pinned)
            {
                return;
            }

            Vector3 offset = Flatten(cameraPosition - playerPosition);
            if (!_retreating && offset.magnitude <= RetreatStartDistance)
            {
                BeginRetreat(offset, playerPosition);
            }
            else if (_retreating &&
                     offset.magnitude >= RetreatReleaseDistance)
            {
                BeginApproach(offset, playerPosition);
            }
        }

        // Resolves the current observation or pinned landing destination.
        internal Vector3 GetPosition(Vector3 origin, Vector3 forward)
        {
            if (_pinned)
            {
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                return origin + right * _flightDirection.x +
                       forward * _flightDirection.z;
            }

            float distance = _retreating
                ? RetreatTargetDistance
                : ApproachTargetDistance;
            return origin + _flightDirection * distance;
        }

        // Starts a faster curved retreat rather than reversing directly.
        private void BeginRetreat(Vector3 offset, Vector3 playerPosition)
        {
            _turnSide = Random.value < 0.5f ? -1f : 1f;
            _flightDirection = ChooseTurn(
                playerPosition, NormalizeOrFallback(offset),
                RetreatTargetDistance);
            _height = Random.Range(MinimumHeight, MaximumHeight);
            _retreating = true;
        }

        // Starts another slow approach from the side reached by the retreat.
        private void BeginApproach(Vector3 offset, Vector3 playerPosition)
        {
            _flightDirection = ChooseTurn(
                playerPosition, NormalizeOrFallback(offset),
                RetreatReleaseDistance);
            _height = Random.Range(MinimumHeight, MaximumHeight);
            _retreating = false;
        }

        // Applies the current smooth turn direction to a radial vector.
        private Vector3 ChooseTurn(
            Vector3 origin, Vector3 radial, float distance)
        {
            Vector3 direction = HuginnCamTreeAvoidance.ChooseTurn(
                origin, radial, TurnAngle, _turnSide, distance);
            _turnSide = Mathf.Sign(Vector3.SignedAngle(
                radial, direction, Vector3.up));
            return direction;
        }

        // Produces a stable direction when camera and player overlap.
        private static Vector3 NormalizeOrFallback(Vector3 value)
        {
            return value.sqrMagnitude > 0.001f
                ? value.normalized
                : RandomHorizontalDirection();
        }

        // Chooses one normalized direction parallel to the terrain plane.
        private static Vector3 RandomHorizontalDirection()
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }

        // Removes vertical displacement from a player-relative vector.
        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
