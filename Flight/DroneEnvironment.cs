using UnityEngine;

namespace Landoria.SagaCapture
{
    // Classifies forest and open flight limits around the drone.
    internal sealed class DroneEnvironment
    {
        private const float DetectionRadius = 10f;
        private const float RefreshInterval = 0.5f;
        private float _nextRefreshTime;

        internal bool IsForest { get; private set; }
        internal float MaximumHeight => IsForest ? 4f : 8f;
        internal float MaximumOrbitRadius => IsForest ? 3f : 8f;

        // Refreshes the zone after a short stable interval.
        internal void Update(Vector3 dronePosition)
        {
            if (Time.time < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = Time.time + RefreshInterval;
            int trees = CountTrees(dronePosition);
            bool forest = trees >= 2;
            if (forest != IsForest)
            {
                IsForest = forest;
                SagaCapturePlugin.Log.LogInfo(
                    $"Drone environment changed: {(forest ? "Forest" : "OpenArea")}.");
            }
        }

        // Counts distinct tree roots within the environment radius.
        private static int CountTrees(Vector3 position)
        {
            Collider[] hits = Physics.OverlapSphere(
                position, DetectionRadius, Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            var trees = new System.Collections.Generic.HashSet<TreeBase>();
            foreach (Collider hit in hits)
            {
                TreeBase tree = hit.GetComponentInParent<TreeBase>();
                if (tree != null)
                {
                    trees.Add(tree);
                }
            }

            return trees.Count;
        }
    }
}
