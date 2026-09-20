using UnityEngine;

namespace Landoria.HuginnCam
{
    // Alternates cautious combat approaches with fast curved retreats.
    internal sealed class HuginnCamCombatObserver
    {
        private const float DamageAlertDuration = 5f;
        private const float MinimumObservationDistance = 4f;
        private const float RetreatReleaseDistance = 10f;
        private const float RetreatTargetDistance = 12f;
        private const float ApproachTargetDistance = 3.5f;
        private const float MinimumFlightHeight = 3f;
        private const float MaximumFlightHeight = 7f;
        private const float TurnAngle = 55f;
        private const float OpponentSearchInterval = 0.5f;
        private const float MaximumOpponentDistance = 30f;
        private const float ApproachMinimumSpeed = 0.2f;
        private const float ApproachCruiseSpeed = 0.7f;
        private const float ApproachMaximumSpeed = 1f;
        private const float RetreatMinimumSpeed = 2f;
        private const float RetreatCruiseSpeed = 3f;
        private const float RetreatMaximumSpeed = 8f;
        private float _lastHealth;
        private float _dangerUntil;
        private bool _healthInitialized;
        private bool _retreating;
        private float _turnSide;
        private Vector3 _flightDirection;
        private Character _opponent;
        private float _nextOpponentSearchTime;

        internal bool IsActive { get; private set; }
        internal HuginnCamFlightProfile Profile => _retreating
            ? new HuginnCamFlightProfile(
                RetreatMinimumSpeed, RetreatCruiseSpeed,
                RetreatMaximumSpeed, 0f, 1f, 4f)
            : new HuginnCamFlightProfile(
                ApproachMinimumSpeed, ApproachCruiseSpeed,
                ApproachMaximumSpeed, float.MaxValue, 0f, 0.35f);

        // Updates combat detection and switches between approach and retreat.
        internal void Update(Player player, Vector3 cameraPosition)
        {
            UpdateDamage(player.GetHealth());
            UpdateOpponent(player);
            bool wasActive = IsActive;
            IsActive = player.InAttack() || player.IsDrawingBow() ||
                       Time.time < _dangerUntil;
            if (!IsActive)
            {
                _retreating = false;
                return;
            }

            Vector3 offset = Flatten(
                cameraPosition - player.transform.position);
            if (!wasActive)
            {
                BeginApproach(offset);
            }
            if (!_retreating && offset.magnitude <= MinimumObservationDistance)
            {
                BeginRetreat(offset);
            }
            else if (_retreating &&
                     offset.magnitude >= RetreatReleaseDistance)
            {
                BeginApproach(offset);
            }
        }

        // Returns the current moving observation or retreat destination.
        internal Vector3 GetTarget(Player player)
        {
            float distance = _retreating
                ? RetreatTargetDistance
                : ApproachTargetDistance;
            return player.transform.position + _flightDirection * distance;
        }

        // Keeps combat observation inside its elevated viewing zone.
        internal float ClampHeight(float height)
        {
            return Mathf.Clamp(height, MinimumFlightHeight, MaximumFlightHeight);
        }

        // Returns the midpoint that keeps player and opponent in the frame.
        internal Vector3 GetFocus(Vector3 playerFocus)
        {
            if (_opponent == null || _opponent.IsDead())
            {
                return playerFocus;
            }

            return Vector3.Lerp(
                playerFocus, _opponent.GetCenterPoint(), 0.5f);
        }

        // Starts a slow approach from a new side after the retreating turn.
        private void BeginApproach(Vector3 offset)
        {
            Vector3 radial = NormalizeOrFallback(offset);
            _flightDirection = Quaternion.Euler(
                0f, _turnSide * TurnAngle, 0f) * radial;
            _retreating = false;
        }

        // Starts a fast lateral retreat instead of reversing in a straight line.
        private void BeginRetreat(Vector3 offset)
        {
            _turnSide = Random.value < 0.5f ? -1f : 1f;
            Vector3 radial = NormalizeOrFallback(offset);
            _flightDirection = Quaternion.Euler(
                0f, _turnSide * TurnAngle, 0f) * radial;
            _retreating = true;
        }

        // Records recent health losses as active combat danger.
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

        // Refreshes the closest hostile combat participant twice per second.
        private void UpdateOpponent(Player player)
        {
            if (Time.time < _nextOpponentSearchTime)
            {
                return;
            }

            _nextOpponentSearchTime = Time.time + OpponentSearchInterval;
            _opponent = BaseAI.FindClosestEnemy(
                player, player.transform.position, MaximumOpponentDistance);
        }

        // Produces a stable horizontal direction for degenerate offsets.
        private static Vector3 NormalizeOrFallback(Vector3 value)
        {
            return value.sqrMagnitude > 0.001f
                ? value.normalized
                : Vector3.back;
        }

        // Removes vertical displacement from one combat-relative vector.
        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
