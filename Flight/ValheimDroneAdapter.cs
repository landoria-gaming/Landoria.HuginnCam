using System;
using System.Collections.Generic;
using DronePilot;
using UnityEngine;

namespace Landoria.SagaCapture
{
    // Adapts Valheim terrain, objects, and forest density to generic drone inputs.
    internal sealed class ValheimDroneAdapter
    {
        private static readonly string[] LightVegetationNames =
        {
            "grass", "bush", "shrub", "fern", "heath",
            "berry", "raspberry", "blueberry", "cloudberry",
            "thistle", "dandelion"
        };
        private float _nextTreeScan;
        private bool _forest;

        // Provides game-specific callbacks without leaking Valheim types.
        internal DroneWorld CreateWorld()
        {
            return new DroneWorld
            {
                GroundHeight = GroundHeight,
                IgnoreObstacle = IgnoreObstacle,
                IsActor = collider =>
                    collider.GetComponentInParent<Character>() != null,
                LogInfo = message => SagaCapturePlugin.Log.LogInfo(message),
                LogWarning = message => SagaCapturePlugin.Log.LogWarning(message)
            };
        }

        // Provides forest first, then an unconditional open-area fallback.
        internal PilotProfile[] CreateProfiles()
        {
            return new[]
            {
                new PilotProfile("Forest", Preference.ForestMaximumHeight,
                    Preference.ForestMaximumOrbitRadius, IsForest),
                new PilotProfile("OpenArea", Preference.OpenMaximumHeight,
                    Preference.OpenMaximumOrbitRadius, position => true)
            };
        }

        // Queries Valheim's world height at one Unity position.
        private static float? GroundHeight(Vector3 position)
        {
            return ZoneSystem.instance != null &&
                ZoneSystem.instance.GetGroundHeight(position, out float height)
                    ? height : (float?)null;
        }

        // Excludes terrain, pickups, and light vegetation from obstacle casts.
        private static bool IgnoreObstacle(Collider collider)
        {
            if (collider == null ||
                collider.GetComponentInParent<Heightmap>() != null ||
                collider.GetComponentInParent<ItemDrop>() != null ||
                collider.GetComponentInParent<Pickable>() != null)
            {
                return true;
            }
            for (Transform current = collider.transform; current != null;
                 current = current.parent)
            {
                string name = current.name.ToLowerInvariant();
                foreach (string fragment in LightVegetationNames)
                {
                    if (name.Contains(fragment)) return true;
                }
            }
            return false;
        }

        // Uses a cached count of distinct Valheim trees near the drone.
        private bool IsForest(Vector3 position)
        {
            if (Time.time < _nextTreeScan) return _forest;
            _nextTreeScan = Time.time + Preference.TreeScanIntervalSeconds;
            Collider[] hits = Physics.OverlapSphere(position,
                Preference.TreeScanRadius, Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            var trees = new HashSet<TreeBase>();
            foreach (Collider collider in hits)
            {
                TreeBase tree = collider.GetComponentInParent<TreeBase>();
                if (tree != null) trees.Add(tree);
            }
            _forest = trees.Count >= Preference.MinimumTreeCount;
            return _forest;
        }
    }
}
