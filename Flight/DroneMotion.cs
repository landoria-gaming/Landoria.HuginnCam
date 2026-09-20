using UnityEngine;

namespace Landoria.SagaCapture
{
    // Moves the drone with bounded acceleration and gradual force changes.
    internal sealed class DroneMotion
    {
        private const float MaximumAcceleration = 1f;
        private const float MaximumJerk = 1.5f;
        private const float MaximumVerticalSpeed = 0.4f;
        private const float VerticalSmoothTime = 1.25f;
        private const float HorizontalArrivalTime = 1f;
        private const float OrbitRadialDeadZone = 0.2f;
        private const float OrbitRadialCorrectionTime = 2.5f;
        private const float MaximumOrbitRadialSpeed = 0.15f;
        private Vector3 _velocity;
        private Vector3 _acceleration;
        private float _verticalVelocity;

        internal Vector3 Velocity => new Vector3(
            _velocity.x, _verticalVelocity, _velocity.z);
        internal Vector3 Acceleration => _acceleration;
        internal float Speed => Velocity.magnitude;

        // Initializes continuous motion from the gameplay camera velocity.
        internal void Initialize(Vector3 velocity)
        {
            _velocity = velocity;
            _velocity.y = 0f;
            _verticalVelocity = 0f;
            _acceleration = Vector3.zero;
        }

        // Advances one jerk-limited movement step toward the destination.
        internal Vector3 Step(
            Vector3 position, Vector3 destination, float targetSpeed)
        {
            Vector3 offset = destination - position;
            offset.y = 0f;
            float arrivalSpeed = offset.magnitude / HorizontalArrivalTime;
            float desiredSpeed = Mathf.Min(targetSpeed, arrivalSpeed);
            Vector3 desiredVelocity = offset.sqrMagnitude > 0.0001f
                ? offset.normalized * desiredSpeed
                : Vector3.zero;
            return Advance(position, destination.y, desiredVelocity);
        }

        // Flies continuously along an orbit with a separate radial correction.
        internal Vector3 StepOrbit(
            Vector3 position, Vector3 destination, Vector3 tangent,
            float targetSpeed)
        {
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.001f)
            {
                return Step(position, destination, targetSpeed);
            }

            tangent.Normalize();
            Vector3 offset = destination - position;
            offset.y = 0f;
            Vector3 radial = offset - tangent * Vector3.Dot(offset, tangent);
            float radialError = radial.magnitude;
            float radialSpeed = radialError > OrbitRadialDeadZone
                ? Mathf.Min(
                    MaximumOrbitRadialSpeed,
                    (radialError - OrbitRadialDeadZone) /
                    OrbitRadialCorrectionTime)
                : 0f;
            Vector3 desiredVelocity = tangent * targetSpeed;
            if (radialError > 0.001f)
            {
                desiredVelocity += radial.normalized * radialSpeed;
            }
            return Advance(position, destination.y, desiredVelocity);
        }

        // Applies jerk-limited acceleration and the shared vertical smoothing.
        private Vector3 Advance(
            Vector3 position, float destinationHeight,
            Vector3 desiredVelocity)
        {
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 desiredAcceleration = Vector3.ClampMagnitude(
                (desiredVelocity - _velocity) / deltaTime,
                MaximumAcceleration);
            desiredAcceleration.y = 0f;
            _acceleration = Vector3.MoveTowards(
                _acceleration, desiredAcceleration,
                MaximumJerk * deltaTime);
            _velocity += _acceleration * deltaTime;
            _velocity.y = 0f;
            Vector3 next = position + _velocity * deltaTime;
            next.y = Mathf.SmoothDamp(
                position.y, destinationHeight, ref _verticalVelocity,
                VerticalSmoothTime, MaximumVerticalSpeed, deltaTime);
            return next;
        }
    }
}
