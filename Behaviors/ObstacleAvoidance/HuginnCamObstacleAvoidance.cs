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

        internal bool IsActive { get; private set; }
        internal HuginnCamFlightProfile Profile => new HuginnCamFlightProfile(
            MinimumSpeed, MaximumCruiseSpeed, MaximumSpeed, 0f, 1f, 4f);

        // Detects obstructions, raises unsafe destinations, and requests a new target.
        internal bool Prepare(
            Player player, Vector3 focus, Vector3 current,
            bool landing, ref Vector3 desired)
        {
            if (landing)
            {
                IsActive = false;
                return false;
            }

            bool sightBlocked =
                !HuginnCamVisibility.HasClearSight(player, focus, desired) ||
                !HuginnCamVisibility.HasClearSight(player, focus, current);
            bool terrainBlocked = HuginnCamTerrain.NeedsAvoidance(
                current, desired);
            if (terrainBlocked)
            {
                desired.y = Mathf.Clamp(
                    Mathf.Max(desired.y, current.y + ClimbHeight),
                    current.y + MinimumClearance,
                    current.y + MaximumClimbHeight);
            }

            IsActive = sightBlocked || terrainBlocked;
            return sightBlocked;
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
