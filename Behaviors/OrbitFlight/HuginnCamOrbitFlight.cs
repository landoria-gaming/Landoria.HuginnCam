using UnityEngine;

namespace Landoria.HuginnCam
{
    // Circles the stationary player on a slow, continuously varying spiral.
    internal sealed class HuginnCamOrbitFlight
    {
        private const float MinimumDistance = 3f;
        private const float MaximumDistance = 10f;
        private const float MinimumHeight = 1f;
        private const float MaximumHeight = 3f;
        private const float MinimumAngularSpeed = 2f;
        private const float MaximumAngularSpeed = 5f;
        private const float HorizontalCompositionAngle = 12f;
        private const float VerticalCompositionAngle = 6f;
        private const float MaximumHorizontalLookOffset = 2.5f;
        private const float MaximumVerticalLookOffset = 1f;
        private const float MinimumSpeed = 0.2f;
        private const float MaximumCruiseSpeed = 1f;
        private const float MaximumSpeed = 1f;
        private float _angle;
        private float _angularSpeed;
        private float _seed;
        private float _lateral;
        private float _longitudinal;
        private float _height;
        private bool _initialized;
        private bool _pinned;

        internal float Height => _height;
        internal HuginnCamFlightProfile Profile => new HuginnCamFlightProfile(
            MinimumSpeed, MaximumCruiseSpeed, MaximumSpeed,
            float.MaxValue, 0f, 0.4f);

        // Forgets the previous orbit after a movement-state transition.
        internal void Reset()
        {
            _initialized = false;
            _pinned = false;
        }

        // Starts or resumes a free continuous orbit.
        internal void SelectTarget()
        {
            _pinned = false;
            if (_initialized)
            {
                return;
            }

            _angle = Random.Range(0f, 360f);
            _angularSpeed = Random.Range(
                MinimumAngularSpeed, MaximumAngularSpeed) *
                (Random.value < 0.5f ? -1f : 1f);
            _seed = Random.Range(0f, 1000f);
            _height = Random.Range(MinimumHeight, MaximumHeight);
            _initialized = true;
        }

        // Pins an explicit orbit-relative point for a landing approach.
        internal void SetTarget(float lateral, float longitudinal)
        {
            _lateral = lateral;
            _longitudinal = longitudinal;
            _pinned = true;
            _initialized = true;
        }

        // Resolves the current spiral or pinned landing point around the player.
        internal Vector3 GetPosition(Vector3 origin, Vector3 forward)
        {
            if (!_pinned)
            {
                UpdateSpiral();
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return origin + right * _lateral + forward * _longitudinal;
        }

        // Keeps the player in a direction-dependent lower image corner.
        internal void UpdateLook(
            HuginnCamLook look, Transform cameraTransform, Vector3 playerFocus)
        {
            Vector3 towardPlayer = playerFocus - cameraTransform.position;
            Vector3 horizontal = towardPlayer;
            horizontal.y = 0f;
            if (horizontal.sqrMagnitude < 0.001f)
            {
                look.Update(cameraTransform, playerFocus);
                return;
            }

            float distance = horizontal.magnitude;
            Vector3 screenRight = Vector3.Cross(
                Vector3.up, horizontal.normalized);
            float horizontalOffset = Mathf.Min(
                MaximumHorizontalLookOffset,
                distance * Mathf.Tan(HorizontalCompositionAngle * Mathf.Deg2Rad));
            float verticalOffset = Mathf.Min(
                MaximumVerticalLookOffset,
                distance * Mathf.Tan(VerticalCompositionAngle * Mathf.Deg2Rad));
            float side = _angularSpeed >= 0f ? 1f : -1f;
            Vector3 compositionFocus = playerFocus +
                screenRight * horizontalOffset * side +
                Vector3.up * verticalOffset;
            look.Update(cameraTransform, compositionFocus);
        }

        // Advances angle, radius, and height without discrete destination jumps.
        private void UpdateSpiral()
        {
            _angle = Mathf.Repeat(
                _angle + _angularSpeed * Time.deltaTime, 360f);
            float time = Time.time * 0.035f;
            float radius = Mathf.Lerp(
                MinimumDistance, MaximumDistance,
                Mathf.PerlinNoise(_seed, time));
            _height = Mathf.Lerp(
                MinimumHeight, MaximumHeight,
                Mathf.PerlinNoise(_seed + 37f, time * 1.3f));
            float radians = _angle * Mathf.Deg2Rad;
            _lateral = Mathf.Sin(radians) * radius;
            _longitudinal = Mathf.Cos(radians) * radius;
        }
    }
}
