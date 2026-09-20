using UnityEngine;

namespace Landoria.HuginnCam
{
    // Occasionally holds Huginn above a moving player while facing the horizon.
    internal sealed class HuginnCamOverwatch
    {
        private const float HeightAbovePlayer = 6f;
        private const float ArrivalDistance = 0.75f;
        private const float OpenAreaOpportunityChance = 0.8f;
        private const float MinimumRepeatDelay = 60f;
        private Vector3 _followVelocity;
        private float _nextOpportunityTime;
        private float _holdEndTime;
        private bool _initialized;
        private bool _exitVelocityReady;

        internal bool IsActive { get; private set; }
        internal bool IsHolding { get; private set; }
        internal bool StartedThisFrame { get; private set; }
        internal bool ArrivedThisFrame { get; private set; }
        internal Vector3 FollowVelocity => _followVelocity;

        // Replaces the normal destination during an active overhead visit.
        internal void Update(
            Player player, bool moving, Vector3 cameraPosition,
            Vector3 incomingVelocity, ref Vector3 target)
        {
            StartedThisFrame = false;
            ArrivedThisFrame = false;
            if (!moving)
            {
                Cancel();
                return;
            }

            InitializeSchedule();
            if (!IsActive && Time.time >= _nextOpportunityTime)
            {
                TryStart(player, cameraPosition);
            }

            if (!IsActive)
            {
                return;
            }

            target = player.transform.position + Vector3.up * HeightAbovePlayer;
            if (!IsHolding && Vector3.Distance(cameraPosition, target) <= ArrivalDistance)
            {
                IsHolding = true;
                ArrivedThisFrame = true;
                _followVelocity = incomingVelocity;
                _holdEndTime = Time.time + Random.Range(3f, 6f);
            }

            if (IsHolding && Time.time >= _holdEndTime)
            {
                Complete();
            }
        }

        // Smoothly follows the moving player while Huginn holds overhead.
        internal Vector3 Follow(Vector3 current, Vector3 target)
        {
            return Vector3.SmoothDamp(
                current, target, ref _followVelocity, 0.75f);
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

        // Starts an overhead visit or schedules another random attempt.
        private void TryStart(Player player, Vector3 cameraPosition)
        {
            bool openArea = HuginnCamVisibility.IsOpenForOverhead(
                player, cameraPosition);
            if (openArea && Random.value < OpenAreaOpportunityChance)
            {
                IsActive = true;
                IsHolding = false;
                StartedThisFrame = true;
                _followVelocity = Vector3.zero;
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

        // Cancels an overhead visit when the player stops travelling.
        private void Cancel()
        {
            if (IsActive)
            {
                Complete();
            }
        }

        // Chooses when Huginn may next attempt an overhead visit.
        private void ScheduleNextOpportunity()
        {
            _nextOpportunityTime = Time.time + Random.Range(8f, 15f);
        }

        // Enforces a one-minute minimum delay after an overhead visit.
        private void ScheduleRepeatOpportunity()
        {
            _nextOpportunityTime = Time.time +
                                   Random.Range(MinimumRepeatDelay, 75f);
        }
    }
}
