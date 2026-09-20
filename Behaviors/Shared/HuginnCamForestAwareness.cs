using System.Collections.Generic;
using UnityEngine;

namespace Landoria.HuginnCam
{
    // Detects dense nearby trees so flight can remain below the canopy.
    internal sealed class HuginnCamForestAwareness
    {
        private const float DetectionRadius = 12f;
        private const float RefreshInterval = 0.5f;
        private const int RequiredTrees = 1;
        private float _nextRefreshTime;

        internal bool IsInForest { get; private set; }

        // Refreshes the forest classification around player and camera.
        internal void Update(Vector3 playerPosition, Vector3 cameraPosition)
        {
            if (Time.time < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = Time.time + RefreshInterval;
            var trees = new HashSet<TreeBase>();
            CollectTrees(playerPosition, trees);
            CollectTrees(cameraPosition, trees);
            bool wasInForest = IsInForest;
            IsInForest = trees.Count >= RequiredTrees;
            if (wasInForest != IsInForest)
            {
                HuginnCamPlugin.Log.LogInfo(
                    $"Huginn forest mode: {(IsInForest ? "enabled" : "disabled")}.");
            }
        }

        // Adds distinct trees whose colliders overlap one detection sphere.
        private static void CollectTrees(
            Vector3 position, HashSet<TreeBase> trees)
        {
            Collider[] colliders = Physics.OverlapSphere(
                position, DetectionRadius,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            foreach (Collider collider in colliders)
            {
                TreeBase tree = collider.GetComponentInParent<TreeBase>();
                if (tree != null)
                {
                    trees.Add(tree);
                }
            }
        }
    }
}
