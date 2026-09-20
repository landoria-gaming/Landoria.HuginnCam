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
        private Vector3 _velocity;
        private Vector3 _acceleration;
        private float _verticalVelocity;

        internal Vector3 Velocity => new Vector3(
            _velocity.x, _verticalVelocity, _velocity.z);
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
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 offset = destination - position;
            offset.y = 0f;
            float arrivalSpeed = offset.magnitude / HorizontalArrivalTime;
            float desiredSpeed = Mathf.Min(targetSpeed, arrivalSpeed);
            Vector3 desiredVelocity = offset.sqrMagnitude > 0.0001f
                ? offset.normalized * desiredSpeed
                : Vector3.zero;
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
                position.y, destination.y, ref _verticalVelocity,
                VerticalSmoothTime, MaximumVerticalSpeed, deltaTime);
            return next;
        }
    }
}
