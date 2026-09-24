using UnityEngine;
using CameraOperator;

namespace Landoria.SagaCapture
{
    // Chooses named viewpoints and applies the director's environment rules.
    internal sealed class CameraPlacementDirector
    {
        private const int MaximumPlacementAttempts = 4;
        private CameraPlacement? _lastPlacement;

        // Finds a visible placement, excluding elevated views in forests.
        internal bool TryPlace(SagaCaptureRig rig, bool renderImmediately)
        {
            int placementCount = rig.IsTargetInForest() ? 3 : 6;
            for (int attempt = 0;
                 attempt < MaximumPlacementAttempts; attempt++)
            {
                CameraPlacement placement = SelectPlacement(placementCount);
                if (!rig.TryCutViewpoint(placement, renderImmediately))
                {
                    continue;
                }
                _lastPlacement = placement;
                return true;
            }
            return false;
        }

        // Selects every allowed placement equally while avoiding repetition.
        private CameraPlacement SelectPlacement(int placementCount)
        {
            int index = !_lastPlacement.HasValue ||
                (int)_lastPlacement.Value >= placementCount
                    ? Random.Range(0, placementCount)
                    : Random.Range(0, placementCount - 1);
            if (_lastPlacement.HasValue &&
                (int)_lastPlacement.Value < placementCount &&
                index >= (int)_lastPlacement.Value)
            {
                index++;
            }
            return (CameraPlacement)index;
        }
    }
}
