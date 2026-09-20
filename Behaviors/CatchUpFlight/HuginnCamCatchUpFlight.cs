using UnityEngine;

namespace Landoria.HuginnCam
{
    // Temporarily accelerates Huginn after it falls too far behind its target.
    internal sealed class HuginnCamCatchUpFlight
    {
        private const float ActivationDistance = 5f;
        private const float ReleaseDistance = 2.5f;
        private const float MinimumSpeed = 1f;
        private const float AccelerationRate = 1.5f;
        private const float TargetRefreshInterval = 0.5f;
        private const float TargetSmoothTime = 0.6f;
        private const float MaximumTargetSpeed = 12f;
        private Vector3 _sampledTarget;
        private Vector3 _smoothedTarget;
        private Vector3 _targetVelocity;
        private float _nextTargetTime;
        private float _maximumSpeed = MinimumSpeed;
        private bool _targetInitialized;

        internal bool IsActive { get; private set; }
        internal HuginnCamFlightProfile Profile => new HuginnCamFlightProfile(
            _maximumSpeed, _maximumSpeed, _maximumSpeed,
            float.MaxValue, 0f, AccelerationRate);

        // Enters with a large delay and exits only after most delay is recovered.
        internal void Update(
            float targetDistance, bool allowed, float maximumSpeed)
        {
            _maximumSpeed = Mathf.Max(MinimumSpeed, maximumSpeed);
            if (!allowed)
            {
                IsActive = false;
                return;
            }

            if (IsActive)
            {
                IsActive = targetDistance > ReleaseDistance;
            }
            else
            {
                IsActive = targetDistance > ActivationDistance;
            }
        }

        // Samples the moving destination twice per second and follows it smoothly.
        internal Vector3 TrackTarget(Vector3 target)
        {
            if (!IsActive)
            {
                _targetInitialized = false;
                return target;
            }

            if (!_targetInitialized)
            {
                _sampledTarget = target;
                _smoothedTarget = target;
                _nextTargetTime = Time.time + TargetRefreshInterval;
                _targetInitialized = true;
            }
            else if (Time.time >= _nextTargetTime)
            {
                _sampledTarget = target;
                _nextTargetTime = Time.time + TargetRefreshInterval;
            }

            _smoothedTarget = Vector3.SmoothDamp(
                _smoothedTarget, _sampledTarget, ref _targetVelocity,
                TargetSmoothTime, MaximumTargetSpeed);
            return _smoothedTarget;
        }
    }
}
