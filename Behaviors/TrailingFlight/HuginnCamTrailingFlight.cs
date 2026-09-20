using UnityEngine;

namespace Landoria.HuginnCam
{
    // Selects destinations while the player is travelling.
    internal sealed class HuginnCamTrailingFlight
    {
        private const float MaximumDistance = 6f;
        private const float MaximumLateralDistance = 5f;
        private const float MinimumHeight = 1f;
        private const float MaximumHeight = 8f;
        private const float MinimumSpeed = 0.2f;
        private const float MaximumCruiseSpeed = 1f;
        private const float MaximumSpeed = 1f;
        private float _distance;
        private float _lateral;

        internal HuginnCamFlightProfile Profile => new HuginnCamFlightProfile(
            MinimumSpeed, MaximumCruiseSpeed, MaximumSpeed,
            float.MaxValue, 0f, 0.4f);

        // Chooses a height inside the travelling flight box.
        internal float SelectHeight()
        {
            return Random.Range(MinimumHeight, MaximumHeight);
        }

        // Chooses a point in the moving flight box.
        internal void SelectTarget()
        {
            _distance = Random.Range(0f, MaximumDistance);
            _lateral = Random.Range(-MaximumLateralDistance, MaximumLateralDistance);
        }

        // Resolves the selected point relative to the player's travel direction.
        internal Vector3 GetPosition(Vector3 origin, Vector3 direction)
        {
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            return origin - direction * _distance + right * _lateral;
        }
    }
}
