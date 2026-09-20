using UnityEngine;

namespace Landoria.HuginnCam
{
    // Lets Huginn break free into open, high-altitude flight sessions.
    internal sealed class HuginnCamFreedomFlight
    {
        private const float HeightAbovePlayer = 10f;
        private const float ArrivalDistance = 0.75f;
        private const float OpenAreaOpportunityChance = 0.8f;
        private const float MinimumRepeatDelay = 60f;
        private const float MaximumApproachSpeed = 12f;
        private const float MaximumNormalTrailingDistance = 6f;
        private const float MaximumSessionRadius = 7f;
        private const float SessionMovementSmoothTime = 1.2f;
        private const float MaximumSessionSpeed = 2f;
        private const float MinimumSessionSpeed = 0.2f;
        private const float ApproachMovementSmoothTime = 0.65f;
        private const float SessionTransitionDuration = 1.5f;
        private Vector3 _followVelocity;
        private float _nextOpportunityTime;
        private float _holdEndTime;
        private bool _initialized;
        private bool _exitVelocityReady;
        private Vector3 _sessionOffset;
        private float _sessionStartTime;
        private float _sessionSeed;

        internal bool IsActive { get; private set; }
        internal bool IsHolding { get; private set; }
        internal bool StartedThisFrame { get; private set; }
        internal bool ArrivedThisFrame { get; private set; }
        internal Vector3 FollowVelocity => _followVelocity;
        internal HuginnCamFlightProfile Profile => new HuginnCamFlightProfile(
            MinimumSessionSpeed, MaximumSessionSpeed,
            MaximumApproachSpeed, 0f, 1f, 2f);

        // Plays the calls owned by freedom-flight departure and arrival transitions.
        internal void HandleAudio(HuginnCamAudio audio)
        {
            if (StartedThisFrame)
            {
                audio.PlayCall(Random.value < 0.5f ? 1 : 5);
            }

            if (ArrivedThisFrame)
            {
                int[] arrivalCalls = { 2, 3, 4, 6 };
                audio.PlayCall(arrivalCalls[Random.Range(0, arrivalCalls.Length)]);
            }
        }

        // Replaces the normal destination during an active freedom flight.
        internal void Update(
            Player player, bool moving, bool danger, Vector3 travelDirection,
            Vector3 cameraPosition,
            Vector3 incomingVelocity, ref Vector3 target)
        {
            StartedThisFrame = false;
            ArrivedThisFrame = false;
            InitializeSchedule();
            bool lagging = IsBehindMovingZone(
                player, cameraPosition, travelDirection);
            if (!IsActive && !Preference.DisableFreedomFlights &&
                moving && !danger && lagging &&
                Time.time >= _nextOpportunityTime)
            {
                TryStart(player, cameraPosition, incomingVelocity);
            }

            if (!IsActive)
            {
                return;
            }

            target = player.transform.position + Vector3.up * HeightAbovePlayer;
            if (IsHolding)
            {
                UpdateSessionTarget();
                target += _sessionOffset;
            }

            if (!IsHolding && Vector3.Distance(cameraPosition, target) <= ArrivalDistance)
            {
                IsHolding = true;
                ArrivedThisFrame = true;
                _sessionStartTime = Time.time;
                _sessionSeed = Random.Range(0f, 1000f);
                _sessionOffset = Vector3.zero;
                _holdEndTime = Time.time + Random.Range(3f, 6f);
            }

            if (IsHolding && Time.time >= _holdEndTime)
            {
                Complete();
            }
        }

        // Aims toward Huginn's independent free-flight interest.
        internal void UpdateLook(
            HuginnCamLook look, Transform cameraTransform, Vector3 playerForward)
        {
            if (IsHolding)
            {
                look.UpdateFree(
                    cameraTransform, GetSessionLookDirection(playerForward));
            }
            else
            {
                look.UpdateHorizon(cameraTransform, playerForward);
            }
        }

        // Produces a continuously evolving aerial path without target jumps.
        private void UpdateSessionTarget()
        {
            float elapsed = Time.time - _sessionStartTime;
            float sample = elapsed * 0.18f;
            Vector2 horizontal = new Vector2(
                SampleSigned(_sessionSeed, sample),
                SampleSigned(_sessionSeed + 17f, sample));
            horizontal = Vector2.ClampMagnitude(horizontal, 1f);
            float blend = Mathf.SmoothStep(
                0f, 1f, Mathf.Clamp01(elapsed / SessionTransitionDuration));
            _sessionOffset = new Vector3(
                horizontal.x * MaximumSessionRadius,
                Mathf.Lerp(-2f, 3f, Mathf.PerlinNoise(
                    _sessionSeed + 31f, sample * 0.7f)),
                horizontal.y * MaximumSessionRadius) * blend;
        }

        // Produces a continuously wandering view blended from the entry heading.
        private Vector3 GetSessionLookDirection(Vector3 playerForward)
        {
            float elapsed = Time.time - _sessionStartTime;
            float sample = elapsed * 0.12f;
            float yaw = Mathf.Lerp(0f, 360f, Mathf.PerlinNoise(
                _sessionSeed + 47f, sample));
            float pitch = Mathf.Lerp(-20f, 35f, Mathf.PerlinNoise(
                _sessionSeed + 63f, sample));
            Vector3 freeDirection = Quaternion.Euler(pitch, yaw, 0f) *
                                    Vector3.forward;
            float blend = Mathf.SmoothStep(
                0f, 1f, Mathf.Clamp01(elapsed / SessionTransitionDuration));
            return Vector3.Slerp(playerForward.normalized, freeDirection, blend);
        }

        // Converts one Perlin sample into a smooth signed value.
        private static float SampleSigned(float seed, float sample)
        {
            return Mathf.PerlinNoise(seed, sample) * 2f - 1f;
        }

        // Detects when Huginn has fallen behind the normal moving-camera box.
        private static bool IsBehindMovingZone(
            Player player, Vector3 cameraPosition, Vector3 travelDirection)
        {
            Vector3 offset = cameraPosition - player.transform.position;
            offset.y = 0f;
            travelDirection.y = 0f;
            if (travelDirection.sqrMagnitude < 0.001f)
            {
                return false;
            }

            float trailingDistance = -Vector3.Dot(
                offset, travelDirection.normalized);
            return trailingDistance > MaximumNormalTrailingDistance;
        }

        // Approaches smoothly, then stays horizontally above the moving player.
        internal Vector3 Move(Vector3 current, Vector3 target)
        {
            float transition = IsHolding
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(
                    (Time.time - _sessionStartTime) / SessionTransitionDuration))
                : 0f;
            float smoothTime = Mathf.Lerp(
                ApproachMovementSmoothTime, SessionMovementSmoothTime, transition);
            float maximumSpeed = Mathf.Lerp(
                MaximumApproachSpeed, MaximumSessionSpeed, transition);
            Vector3 next = Vector3.SmoothDamp(
                current, target, ref _followVelocity,
                smoothTime, maximumSpeed);
            return next;
        }

        // Returns the final hover velocity once when normal travel resumes.
        internal bool TryTakeExitVelocity(out Vector3 velocity)
        {
            velocity = _followVelocity;
            if (!_exitVelocityReady)
            {
                return false;
            }

            _exitVelocityReady = false;
            return true;
        }

        // Creates the first delayed opportunity without triggering immediately.
        private void InitializeSchedule()
        {
            if (_initialized)
            {
                return;
            }

            ScheduleNextOpportunity();
            _initialized = true;
        }

        // Starts a freedom flight or schedules another random attempt.
        private void TryStart(
            Player player, Vector3 cameraPosition, Vector3 incomingVelocity)
        {
            bool openArea = HuginnCamVisibility.IsOpenForFreedomFlight(
                player, cameraPosition);
            if (openArea && Random.value < OpenAreaOpportunityChance)
            {
                IsActive = true;
                IsHolding = false;
                StartedThisFrame = true;
                _followVelocity = incomingVelocity;
                return;
            }

            ScheduleNextOpportunity();
        }

        // Completes one visit and schedules the next opportunity.
        private void Complete()
        {
            _exitVelocityReady = IsHolding;
            IsActive = false;
            IsHolding = false;
            ScheduleRepeatOpportunity();
        }

        // Chooses when Huginn may next attempt a freedom flight.
        private void ScheduleNextOpportunity()
        {
            _nextOpportunityTime = Time.time + Random.Range(8f, 15f);
        }

        // Enforces a one-minute minimum delay after a freedom flight.
        private void ScheduleRepeatOpportunity()
        {
            _nextOpportunityTime = Time.time +
                                   Random.Range(MinimumRepeatDelay, 75f);
        }
    }
}
