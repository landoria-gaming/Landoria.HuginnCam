using UnityEngine;

namespace Landoria.HuginnCam
{
    // Prefers the less wooded of two otherwise valid flight directions.
    internal static class HuginnCamTreeAvoidance
    {
        private const float ProbeHeight = 2f;
        private const float ProbeRadius = 1.5f;
        private const int TakeoffDirectionCount = 8;

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

        // Finds the least obstructed rising corridor around a grounded Huginn.
        internal static Vector3 ChooseTakeoffDirection(
            Vector3 origin, Vector3 preferredDirection, float distance)
        {
            preferredDirection.y = 0f;
            Vector3 basis = preferredDirection.sqrMagnitude > 0.001f
                ? preferredDirection.normalized
                : Vector3.forward;
            Vector3 best = basis;
            float bestScore = float.MaxValue;
            for (int index = 0; index < TakeoffDirectionCount; index++)
            {
                float angle = index * 360f / TakeoffDirectionCount;
                Vector3 candidate = Quaternion.Euler(0f, angle, 0f) * basis;
                float score = ScoreTakeoff(origin, candidate, distance);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
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

        // Penalizes every solid obstacle along one gently rising takeoff path.
        private static float ScoreTakeoff(
            Vector3 origin, Vector3 direction, float distance)
        {
            Vector3 rising = (direction * distance +
                              Vector3.up * ProbeHeight).normalized;
            RaycastHit[] hits = Physics.SphereCastAll(
                origin + Vector3.up * ProbeRadius,
                ProbeRadius, rising, distance,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            float score = 0f;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<Player>() == null)
                {
                    float proximity = (distance - hit.distance) / distance;
                    bool tree = hit.collider.GetComponentInParent<TreeBase>() != null;
                    score += (tree ? 3f : 1f) + proximity;
                }
            }

            return score;
        }
    }
}
