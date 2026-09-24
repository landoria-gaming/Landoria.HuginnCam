using UnityEngine;
using CameraOperator;

namespace Landoria.SagaCapture
{
    // Chooses named viewpoints and applies the director's environment rules.
    internal sealed class CameraPlacementDirector
    {
        private const int MaximumPlacementAttempts = 4;
        private const float MinimumViewAngle = 45f;
        private static readonly CameraPlacement[] NormalPlacements =
        {
            CameraPlacement.Front,
            CameraPlacement.Left,
            CameraPlacement.Right
        };
        private static readonly CameraPlacement[] OpenAreaPlacements =
        {
            CameraPlacement.Front,
            CameraPlacement.Left,
            CameraPlacement.Right,
            CameraPlacement.Front,
            CameraPlacement.Left,
            CameraPlacement.Right,
            CameraPlacement.FrontTop,
            CameraPlacement.LeftTop,
            CameraPlacement.RightTop
        };
        private CameraPlacement? _lastPlacement;

        // Finds a visible placement, excluding elevated views in forests.
        internal bool TryPlace(SagaCaptureRig rig)
        {
            CameraPlacement[] placements = rig.IsTargetInForest()
                ? NormalPlacements : OpenAreaPlacements;
            for (int attempt = 0;
                 attempt < MaximumPlacementAttempts; attempt++)
            {
                CameraPlacement placement = SelectPlacement(placements);
                if (!rig.TryCutViewpoint(
                    placement, MinimumViewAngle))
                {
                    continue;
                }
                _lastPlacement = placement;
                return true;
            }
            return false;
        }

        // Selects a weighted placement without repeating the previous view.
        private CameraPlacement SelectPlacement(CameraPlacement[] placements)
        {
            var candidates = new CameraPlacement[placements.Length];
            int count = 0;
            foreach (CameraPlacement placement in placements)
            {
                if (_lastPlacement.HasValue &&
                    placement == _lastPlacement.Value)
                {
                    continue;
                }
                candidates[count++] = placement;
            }
            return candidates[Random.Range(0, count)];
        }
    }
}
