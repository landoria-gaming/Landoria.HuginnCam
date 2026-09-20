using UnityEngine;

namespace Landoria.HuginnCam
{
    // Selects smooth orbit destinations while the player remains nearby.
    internal sealed class HuginnCamOrbitFlight
    {
        private const float MinimumDistance = 3f;
        private const float MaximumDistance = 10f;
        private const float MinimumHeight = 1f;
        private const float MaximumHeight = 3f;
        private const float MinimumHorizontalDistance = 2f;
        private const float MinimumSpeed = 0.2f;
        private const float MaximumCruiseSpeed = 1f;
        private const float MaximumSpeed = 1f;
        private float _lateral;
        private float _longitudinal;
        private bool _initialized;
        private bool _changeLateralNext;

        internal HuginnCamFlightProfile Profile => new HuginnCamFlightProfile(
            MinimumSpeed, MaximumCruiseSpeed, MaximumSpeed,
            float.MaxValue, 0f, 0.4f);

        // Chooses a height inside the idle flight zone.
        internal float SelectHeight()
        {
            return Random.Range(MinimumHeight, MaximumHeight);
        }

        // Forgets the old orbit axes after a movement-state transition.
        internal void Reset()
        {
            _initialized = false;
        }

        // Selects a new destination without crossing both axes at once.
        internal void SelectTarget()
        {
            if (!_initialized)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(MinimumDistance, MaximumDistance);
                SetTarget(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius);
                return;
            }

            if (_changeLateralNext)
            {
                _lateral = SelectCoordinate(_longitudinal, _lateral);
            }
            else
            {
                _longitudinal = SelectCoordinate(_lateral, _longitudinal);
            }

            _changeLateralNext = !_changeLateralNext;
        }

        // Installs an explicit orbit point, including a landing candidate.
        internal void SetTarget(float lateral, float longitudinal)
        {
            _lateral = lateral;
            _longitudinal = longitudinal;
            _initialized = true;
            _changeLateralNext = Random.value >= 0.5f;
        }

        // Resolves the selected point on stable axes around the player.
        internal Vector3 GetPosition(Vector3 origin, Vector3 forward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return origin + right * _lateral + forward * _longitudinal;
        }

        // Selects one axis while keeping the point inside the idle annulus.
        private static float SelectCoordinate(float fixedCoordinate, float current)
        {
            float fixedSquared = fixedCoordinate * fixedCoordinate;
            float maximum = Mathf.Sqrt(Mathf.Max(
                0f, MaximumDistance * MaximumDistance - fixedSquared));
            float minimum = fixedSquared >= MinimumDistance * MinimumDistance
                ? 0f
                : Mathf.Sqrt(MinimumDistance * MinimumDistance - fixedSquared);
            float magnitude = Random.Range(minimum, maximum);
            bool preserveSide = Mathf.Abs(fixedCoordinate) < MinimumHorizontalDistance;
            bool positive = preserveSide ? current >= 0f : Random.value >= 0.5f;
            return positive ? magnitude : -magnitude;
        }
    }
}
