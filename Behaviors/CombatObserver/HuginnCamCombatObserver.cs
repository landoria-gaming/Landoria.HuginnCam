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
        private const float RetreatMinimumSpeed = 1f;
        private const float RetreatCruiseSpeed = 2f;
        private const float RetreatMaximumSpeed = 4f;
        private float _dangerUntil;
        private bool _retreating;
        private float _turnSide;
        private Vector3 _flightDirection;
        private Character _opponent;
        private float _nextOpponentSearchTime;
        private float _lastHandledDamageDealtTime = -1f;
        private float _lastHandledDamageReceivedTime = -1f;

        internal bool IsActive { get; private set; }
        internal HuginnCamFlightProfile Profile => _retreating
            ? new HuginnCamFlightProfile(
                RetreatMinimumSpeed, RetreatCruiseSpeed,
                RetreatMaximumSpeed, 0f, 0.5f, 1.5f)
            : new HuginnCamFlightProfile(
                ApproachMinimumSpeed, ApproachCruiseSpeed,
                ApproachMaximumSpeed, float.MaxValue, 0f, 0.35f);

        // Updates combat detection and switches between approach and retreat.
        internal void Update(Player player, Vector3 cameraPosition)
        {
            bool receivedDamage = UpdateDamageReceived();
            bool dealtDamage = UpdateDamageDealt();
            UpdateOpponent(player);
            bool wasActive = IsActive;
            IsActive = Time.time < _dangerUntil;
            if (!IsActive)
            {
                _retreating = false;
                _opponent = null;
                return;
            }

            Vector3 offset = Flatten(
                cameraPosition - player.transform.position);
            if (!wasActive)
            {
                string reason = dealtDamage
                    ? "damage dealt"
                    : receivedDamage ? "damage received" : "recent damage";
                HuginnCamPlugin.Log.LogInfo(
                    $"CombatObserver activated: {reason}.");
                BeginApproach(offset, player.transform.position);
            }
            if (!_retreating && offset.magnitude <= MinimumObservationDistance)
            {
                BeginRetreat(offset, player.transform.position);
            }
            else if (_retreating &&
                     offset.magnitude >= RetreatReleaseDistance)
            {
                BeginApproach(offset, player.transform.position);
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
        private void BeginApproach(Vector3 offset, Vector3 playerPosition)
        {
            Vector3 radial = NormalizeOrFallback(offset);
            _flightDirection = ChooseTurn(
                playerPosition, radial, RetreatReleaseDistance);
            _retreating = false;
        }

        // Starts a fast lateral retreat instead of reversing in a straight line.
        private void BeginRetreat(Vector3 offset, Vector3 playerPosition)
        {
            _turnSide = Random.value < 0.5f ? -1f : 1f;
            Vector3 radial = NormalizeOrFallback(offset);
            _flightDirection = ChooseTurn(
                playerPosition, radial, RetreatTargetDistance);
            _retreating = true;
        }

        // Chooses the less wooded side while preserving the preferred turn on ties.
        private Vector3 ChooseTurn(
            Vector3 origin, Vector3 radial, float distance)
        {
            Vector3 direction = HuginnCamTreeAvoidance.ChooseTurn(
                origin, radial, TurnAngle, _turnSide, distance);
            _turnSide = Mathf.Sign(Vector3.SignedAngle(
                radial, direction, Vector3.up));
            return direction;
        }

        // Extends observation after confirmed damage from another character.
        private bool UpdateDamageReceived()
        {
            float eventTime = HuginnCamCombatEvents.LastDamageReceivedTime;
            if (eventTime <= _lastHandledDamageReceivedTime)
            {
                return false;
            }

            _lastHandledDamageReceivedTime = eventTime;
            if (Time.time - eventTime > DamageAlertDuration)
            {
                return false;
            }

            _dangerUntil = Mathf.Max(
                _dangerUntil, eventTime + DamageAlertDuration);
            _opponent = HuginnCamCombatEvents.LastAttacker;
            return true;
        }

        // Extends combat observation after confirmed damage dealt by the player.
        private bool UpdateDamageDealt()
        {
            float eventTime = HuginnCamCombatEvents.LastDamageDealtTime;
            if (eventTime <= _lastHandledDamageDealtTime)
            {
                return false;
            }

            _lastHandledDamageDealtTime = eventTime;
            if (Time.time - eventTime > DamageAlertDuration)
            {
                return false;
            }
            _dangerUntil = Mathf.Max(
                _dangerUntil, eventTime + DamageAlertDuration);
            _opponent = HuginnCamCombatEvents.LastDamagedOpponent;
            return true;
        }

        // Refreshes the closest hostile combat participant twice per second.
        private void UpdateOpponent(Player player)
        {
            if (Time.time < _nextOpponentSearchTime)
            {
                return;
            }

            _nextOpponentSearchTime = Time.time + OpponentSearchInterval;
            if (_opponent == null || _opponent.IsDead())
            {
                _opponent = BaseAI.FindClosestEnemy(
                    player, player.transform.position,
                    MaximumOpponentDistance);
            }
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
