using UnityEngine;

namespace Landoria.HuginnCam
{
    // Prefers the less wooded of two otherwise valid flight directions.
    internal static class HuginnCamTreeAvoidance
    {
        private const float ProbeHeight = 2f;
        private const float ProbeRadius = 1.5f;

        // Chooses a left or right turn by comparing trees along both paths.
        internal static Vector3 ChooseTurn(
            Vector3 origin, Vector3 radial, float angle,
            float preferredSide, float distance)
        {
            Vector3 left = Quaternion.Euler(0f, -angle, 0f) * radial;
            Vector3 right = Quaternion.Euler(0f, angle, 0f) * radial;
            float leftScore = Score(origin, left, distance);
            float rightScore = Score(origin, right, distance);
            if (Mathf.Approximately(leftScore, rightScore))
            {
                return preferredSide < 0f ? left : right;
            }

            return leftScore < rightScore ? left : right;
        }


        // Scores nearby trees more heavily than distant trees in one corridor.
        private static float Score(
            Vector3 origin, Vector3 direction, float distance)
        {
            origin += Vector3.up * ProbeHeight;
            RaycastHit[] hits = Physics.SphereCastAll(
                origin, ProbeRadius, direction.normalized, distance,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            float score = 0f;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<TreeBase>() != null)
                {
                    score += 1f + (distance - hit.distance) / distance;
                }
            }

            return score;
        }

    }
}
