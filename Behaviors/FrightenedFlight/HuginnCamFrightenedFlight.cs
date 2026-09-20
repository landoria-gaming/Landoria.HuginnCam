using UnityEngine;

namespace Landoria.HuginnCam
{
    // Makes Huginn flee after prolonged proximity to an active combat.
    internal sealed class HuginnCamFrightenedFlight
    {
        private const float DamageAlertDuration = 5f;
        private const float CloseDistance = 6f;
        private const float RetreatReleaseDistance = 8f;
        private const float RetreatDistance = 9f;
        private const float MinimumFlightHeight = 2f;
        private const float MaximumFlightHeight = 8f;
        private const float MinimumSpeed = 1f;
        private const float MaximumCruiseSpeed = 3f;
        private const float MaximumSpeed = 8f;
        private const float CloseTolerance = 3f;
        private float _lastHealth;
        private float _dangerUntil;
        private float _closeSince = -1f;
        private bool _healthInitialized;
        private Vector3 _retreatDirection;

        internal bool IsActive { get; private set; }
        internal bool ShouldRetreat { get; private set; }
        internal HuginnCamFlightProfile Profile => new HuginnCamFlightProfile(
            MinimumSpeed, MaximumCruiseSpeed, MaximumSpeed, 2f, 1f, 2f);

        // Keeps a frightened flight destination within its vertical zone.
        internal float ClampHeight(float height)
        {
            return Mathf.Clamp(height, MinimumFlightHeight, MaximumFlightHeight);
        }

        // Updates attack, recent-damage, and prolonged-proximity state.
        internal void Update(Player player, Vector3 cameraPosition)
        {
            UpdateDamage(player.GetHealth());
            IsActive = player.InAttack() || player.IsDrawingBow() ||
                       Time.time < _dangerUntil;
            if (!IsActive)
            {
                ResetProximity();
                return;
            }

            Vector3 offset = cameraPosition - player.transform.position;
            offset.y = 0f;
            UpdateProximity(offset);
        }

        // Returns the moving world-space retreat point away from the player.
        internal Vector3 GetRetreatTarget(Player player)
        {
            return player.transform.position + _retreatDirection * RetreatDistance;
        }

        // Records health losses as danger for several seconds.
        private void UpdateDamage(float health)
        {
            if (!_healthInitialized)
            {
                _healthInitialized = true;
            }
            else if (health < _lastHealth - 0.01f)
            {
                _dangerUntil = Time.time + DamageAlertDuration;
            }

            _lastHealth = health;
        }

        // Starts retreat only after remaining close for several seconds.
        private void UpdateProximity(Vector3 offset)
        {
            if (ShouldRetreat)
            {
                if (offset.magnitude >= RetreatReleaseDistance)
                {
                    ResetProximity();
                }

                return;
            }

            if (offset.magnitude >= CloseDistance)
            {
                ResetProximity();
                return;
            }

            if (_closeSince < 0f)
            {
                _closeSince = Time.time;
            }

            if (!ShouldRetreat && Time.time - _closeSince >= CloseTolerance)
            {
                ShouldRetreat = true;
                _retreatDirection = offset.sqrMagnitude > 0.001f
                    ? offset.normalized
                    : Vector3.back;
            }
        }

        // Clears proximity state after danger ends or distance is restored.
        private void ResetProximity()
        {
            _closeSince = -1f;
            ShouldRetreat = false;
        }
    }
}
