using UnityEngine;

namespace Landoria.HuginnCam
{
    // Owns landing decisions, destinations, and approach state.
    internal sealed class HuginnCamLanding
    {
        private const float SafeIdleLandingChance = 0.95f;
        private const float SafeIdleDuration = 5f;
        private const float ArrivalDistance = 0.3f;
        private const float MinimumDistance = 4f;
        private const float MaximumDistance = 10f;
        private const float MinimumHeight = 0.2f;
        private const float MaximumHeight = 0.2f;
        private const float MinimumSpeed = 0f;
        private const float MaximumCruiseSpeed = 0.7f;
        private const float MaximumSpeed = 1f;
        private const int MaximumAttempts = 8;
        private bool _preferLanding;
        private bool _safeOpportunityUsed;
        private float _safeIdleStartTime;

        internal bool IsActive { get; private set; }
        internal Vector3 Target { get; private set; }
        internal float Height => Random.Range(MinimumHeight, MaximumHeight);
        internal HuginnCamFlightProfile Profile => new HuginnCamFlightProfile(
            MinimumSpeed, MaximumCruiseSpeed, MaximumSpeed, 2f, 0.25f, 0.4f);

        // Chooses whether the next idle destination is a landing.
        internal void SelectState(bool moving, bool initial, bool danger)
        {
            if (IsActive)
            {
                return;
            }

            IsActive = !Preference.DisableLandings && !moving &&
                       !danger &&
                       (initial || _preferLanding &&
                           Random.value < SafeIdleLandingChance);
            _preferLanding = false;
        }

        // Schedules an almost-certain landing after five ObserverFlight seconds.
        internal bool UpdateObserverState(bool observing)
        {
            if (!observing)
            {
                ResetSafeIdle();
                return false;
            }

            if (_safeIdleStartTime <= 0f)
            {
                _safeIdleStartTime = Time.time;
                return false;
            }

            if (_safeOpportunityUsed ||
                Time.time - _safeIdleStartTime < SafeIdleDuration)
            {
                return false;
            }

            _safeOpportunityUsed = true;
            _preferLanding = true;
            return true;
        }

        // Completes the landing when Huginn reaches its ground destination.
        internal bool TryComplete(Vector3 cameraPosition, Vector3 target)
        {
            if (!IsActive ||
                Vector3.Distance(cameraPosition, target) > ArrivalDistance)
            {
                return false;
            }

            Cancel();
            return true;
        }

        // Cancels an active landing as soon as combat danger appears.
        internal bool CancelForDanger(bool danger)
        {
            if (!danger || !IsActive)
            {
                return false;
            }

            Cancel();
            return true;
        }

        // Clears the current landing and perch state.
        internal void Cancel()
        {
            IsActive = false;
        }

        // Samples dry candidates and installs the flattest landing destination.
        internal bool TrySelectTarget(
            Player player, HuginnCamObserverFlight observerFlight, Vector3 forward)
        {
            float bestUnevenness = float.MaxValue;
            Vector2 bestOffset = Vector2.zero;
            Vector3 bestPosition = Vector3.zero;
            bool found = false;
            for (int attempt = 0; attempt < MaximumAttempts; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(MinimumDistance, MaximumDistance);
                Vector2 offset = new Vector2(
                    Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius);
                observerFlight.SetTarget(offset.x, offset.y);
                Vector3 position = observerFlight.GetPosition(
                    player.transform.position, forward);
                if (HuginnCamTerrain.IsDryLandingPoint(position) &&
                    HuginnCamTerrain.TryGetLandingUnevenness(
                        position, out float unevenness) &&
                    unevenness < bestUnevenness)
                {
                    bestUnevenness = unevenness;
                    bestOffset = offset;
                    bestPosition = position;
                    found = true;
                }
            }

            if (found)
            {
                observerFlight.SetTarget(bestOffset.x, bestOffset.y);
                Target = bestPosition;
            }
            return found;
        }

        // Clears the continuous ObserverFlight landing timer.
        private void ResetSafeIdle()
        {
            _safeIdleStartTime = 0f;
            _safeOpportunityUsed = false;
            _preferLanding = false;
        }
    }
}
