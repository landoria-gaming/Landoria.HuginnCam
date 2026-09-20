using UnityEngine;

namespace Landoria.SagaCapture
{
    // Moves the drone with bounded acceleration and gradual force changes.
    internal sealed class DroneMotion
    {
        private const float MaximumAcceleration = 1f;
        private const float MaximumJerk = 1.5f;
        private Vector3 _velocity;
        private Vector3 _acceleration;

        internal Vector3 Velocity => _velocity;
        internal float Speed => _velocity.magnitude;

        // Initializes continuous motion from the gameplay camera velocity.
        internal void Initialize(Vector3 velocity)
        {
            _velocity = velocity;
            _acceleration = Vector3.zero;
        }

        // Advances one jerk-limited movement step toward the destination.
        internal Vector3 Step(
            Vector3 position, Vector3 destination, float targetSpeed)
        {
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 offset = destination - position;
            Vector3 desiredVelocity = offset.sqrMagnitude > 0.0001f
                ? offset.normalized * targetSpeed
                : Vector3.zero;
            Vector3 desiredAcceleration = Vector3.ClampMagnitude(
                (desiredVelocity - _velocity) / deltaTime,
                MaximumAcceleration);
            _acceleration = Vector3.MoveTowards(
                _acceleration, desiredAcceleration,
                MaximumJerk * deltaTime);
            _velocity += _acceleration * deltaTime;
            return position + _velocity * deltaTime;
        }
    }
}
