using UnityEngine;

namespace Landoria.HuginnCam
{
    // Temporarily overrides normal flight to restore clearance and player visibility.
    internal sealed class HuginnCamObstacleAvoidance
    {
        private const float ClimbHeight = 2f;
        private const float MinimumClearance = 1f;
        private const float MaximumClimbHeight = 8f;
        private const float MinimumSpeed = 4f;
        private const float MaximumCruiseSpeed = 4f;
        private const float MaximumSpeed = 8f;
        private const float AnticipationDistance = 8f;
        private const float MaximumWaypointDistance = 4f;
        private const float AnticipationArrivalDistance = 1f;
        private const float AnticipationTurnAngle = 40f;
        private const float AnticipationClimbRatio = 0.6f;
        private const float EmergencyObstacleDistance = 0.5f;
        private bool _anticipating;
        private Vector3 _anticipationWaypoint;

        internal bool IsActive { get; private set; }
        internal HuginnCamFlightProfile Profile => new HuginnCamFlightProfile(
            MinimumSpeed, MaximumCruiseSpeed, MaximumSpeed, 0f, 1f, 4f);

        // Detects obstructions, raises unsafe destinations, and requests a new target.
        internal bool Prepare(
            Player player, Vector3 focus, Vector3 current,
            bool landing, bool resting, bool requirePlayerVisibility,
            ref Vector3 desired)
        {
            if (resting)
            {
                IsActive = false;
                _anticipating = false;
                return false;
            }

            ApplyAnticipation(
                player, focus, current,
                requirePlayerVisibility, ref desired);
            if (landing)
            {
                IsActive = false;
                return false;
            }
            bool sightBlocked = requirePlayerVisibility &&
                (
                !HuginnCamVisibility.HasClearSight(player, focus, desired) ||
                !HuginnCamVisibility.HasClearSight(player, focus, current));
            bool terrainBlocked = HuginnCamTerrain.NeedsAvoidance(
                current, desired);
            if (terrainBlocked)
            {
                desired.y = Mathf.Clamp(
                    Mathf.Max(desired.y, current.y + ClimbHeight),
                    current.y + MinimumClearance,
                    current.y + MaximumClimbHeight);
            }

            bool obstacleImminent = IsObstacleImminent(
                player, current, desired);
            IsActive = terrainBlocked || obstacleImminent;
            return sightBlocked;
        }

        // Detects a solid obstacle inside the short emergency corridor.
        private static bool IsObstacleImminent(
            Player player, Vector3 current, Vector3 desired)
        {
            Vector3 route = desired - current;
            float distance = Mathf.Min(
                route.magnitude, EmergencyObstacleDistance);
            return distance > 0.001f &&
                   HuginnCamVisibility.GetFlightClearance(
                       player, current, route, distance) < distance;
        }

        // Holds or creates a clear waypoint before the direct route is blocked.
        private void ApplyAnticipation(
            Player player, Vector3 focus, Vector3 current,
            bool requirePlayerVisibility, ref Vector3 desired)
        {
            Vector3 route = desired - current;
            float probeDistance = Mathf.Min(
                route.magnitude, AnticipationDistance);
            bool directRouteClear = probeDistance < AnticipationArrivalDistance ||
                HuginnCamVisibility.GetFlightClearance(
                    player, current, route, probeDistance) >= probeDistance;
            if (_anticipating && !directRouteClear &&
                Vector3.Distance(current, _anticipationWaypoint) >
                    AnticipationArrivalDistance)
            {
                desired = _anticipationWaypoint;
                return;
            }

            _anticipating = false;
            if (directRouteClear)
            {
                return;
            }

            _anticipationWaypoint = SelectWaypoint(
                player, focus, current, route.normalized,
                Mathf.Min(probeDistance, MaximumWaypointDistance),
                requirePlayerVisibility);
            _anticipating = true;
            desired = _anticipationWaypoint;
        }

        // Selects the visible candidate with the longest clear flight corridor.
        private static Vector3 SelectWaypoint(
            Player player, Vector3 focus, Vector3 current,
            Vector3 route, float distance, bool requirePlayerVisibility)
        {
            Vector3[] directions = CreateCandidateDirections(route);
            Vector3 best = current + directions[0] * distance;
            float bestScore = -1f;
            foreach (Vector3 direction in directions)
            {
                Vector3 candidate = current + direction * distance;
                float score = HuginnCamVisibility.GetFlightClearance(
                    player, current, direction, distance);
                if (requirePlayerVisibility &&
                    HuginnCamVisibility.HasClearSight(
                    player, focus, candidate))
                {
                    score += distance * 2f;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        // Builds lateral and climbing alternatives around the intended route.
        private static Vector3[] CreateCandidateDirections(Vector3 route)
        {
            Vector3 left = Quaternion.Euler(
                0f, -AnticipationTurnAngle, 0f) * route;
            Vector3 right = Quaternion.Euler(
                0f, AnticipationTurnAngle, 0f) * route;
            return new[]
            {
                left.normalized,
                right.normalized,
                (route + Vector3.up * AnticipationClimbRatio).normalized,
                (left + Vector3.up * AnticipationClimbRatio).normalized,
                (right + Vector3.up * AnticipationClimbRatio).normalized
            };
        }

        // Requests another destination if the player is still hidden after movement.
        internal bool ShouldRetryAfterMovement(
            Player player, Vector3 focus, Vector3 current, bool landing)
        {
            return !landing &&
                   !HuginnCamVisibility.HasClearSight(player, focus, current);
        }
    }
}
