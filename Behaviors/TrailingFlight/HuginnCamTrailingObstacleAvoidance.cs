using UnityEngine;

namespace Landoria.HuginnCam
{
    // Selects a clearer lateral trailing destination when the direct route is obstructed.
    internal static class HuginnCamTrailingObstacleAvoidance
    {
        private const float ProbeRadius = 0.5f;
        private const float SideStep = 3f;
        private const float RequiredClearanceRatio = 0.95f;

        // Returns a left or right lateral offset only when that route clears the obstacle.
        internal static bool TryChooseOffset(
            Vector3 origin, Vector3 target, Vector3 right,
            float currentLateral, out float lateral)
        {
            lateral = currentLateral;
            if (GetClearanceRatio(origin, target) >= RequiredClearanceRatio)
            {
                return false;
            }

            Vector3 leftTarget = target - right * SideStep;
            Vector3 rightTarget = target + right * SideStep;
            float leftClearance = GetClearanceRatio(origin, leftTarget);
            float rightClearance = GetClearanceRatio(origin, rightTarget);
            bool leftClear = leftClearance >= RequiredClearanceRatio;
            bool rightClear = rightClearance >= RequiredClearanceRatio;
            if (!leftClear && !rightClear)
            {
                return false;
            }

            lateral = SelectSide(
                currentLateral, leftClear, rightClear,
                leftClearance, rightClearance);
            return true;
        }

        // Chooses the sole clear side, or the clearest side when both are usable.
        private static float SelectSide(
            float currentLateral, bool leftClear, bool rightClear,
            float leftClearance, float rightClearance)
        {
            if (leftClear != rightClear)
            {
                return currentLateral + (leftClear ? -SideStep : SideStep);
            }

            return currentLateral +
                   (leftClearance > rightClearance ? -SideStep : SideStep);
        }

        // Measures the unobstructed proportion of one camera route.
        private static float GetClearanceRatio(Vector3 origin, Vector3 target)
        {
            Vector3 movement = target - origin;
            float distance = movement.magnitude;
            if (distance < 0.001f)
            {
                return 1f;
            }

            RaycastHit[] hits = Physics.SphereCastAll(
                origin, ProbeRadius, movement.normalized, distance,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            float clearance = distance;
            foreach (RaycastHit hit in hits)
            {
                if (IsIgnored(hit.collider))
                {
                    continue;
                }

                clearance = Mathf.Min(clearance, hit.distance);
            }

            return clearance / distance;
        }

        // Ignores living characters because this policy only avoids scenery.
        private static bool IsIgnored(Collider collider)
        {
            return collider == null ||
                   collider.GetComponentInParent<Character>() != null;
        }
    }
}
