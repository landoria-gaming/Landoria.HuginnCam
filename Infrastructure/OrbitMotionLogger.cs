using UnityEngine;

namespace Landoria.SagaCapture
{
    // Diagnoses forward and backward motion along the planned orbit.
    internal sealed class OrbitMotionLogger
    {
        private const float LogInterval = 0.5f;
        private const float DirectionThreshold = 0.05f;
        private float _nextLogTime;

        internal void Update(
            Vector3 playerPosition, Vector3 dronePosition,
            Vector3 destination, Vector3 probeTarget,
            Vector3 velocity, Vector3 acceleration)
        {
            if (!Preference.DebugLogs || Time.time < _nextLogTime)
            {
                return;
            }

            _nextLogTime = Time.time + LogInterval;
            Vector3 tangent = Flatten(probeTarget - destination).normalized;
            Vector3 radial = Flatten(dronePosition - playerPosition).normalized;
            Vector3 targetOffset = Flatten(destination - dronePosition);
            float tangentialSpeed = Vector3.Dot(Flatten(velocity), tangent);
            float radialSpeed = Vector3.Dot(Flatten(velocity), radial);
            float tangentialAcceleration =
                Vector3.Dot(Flatten(acceleration), tangent);
            string direction = GetDirection(tangentialSpeed);
            string reason = GetReason(
                tangentialSpeed, tangentialAcceleration,
                velocity, targetOffset);
            SagaCapturePlugin.Log.LogInfo(
                $"Orbit motion: direction={direction}, reason={reason}, " +
                $"tangentSpeed={tangentialSpeed:F2}m/s, " +
                $"radialSpeed={radialSpeed:F2}m/s, " +
                $"tangentAcceleration={tangentialAcceleration:F2}m/s2, " +
                $"targetDistance={targetOffset.magnitude:F2}m.");
        }

        private static string GetDirection(float tangentialSpeed)
        {
            if (tangentialSpeed > DirectionThreshold)
            {
                return "forward";
            }
            return tangentialSpeed < -DirectionThreshold
                ? "backward"
                : "stationary";
        }

        private static string GetReason(
            float tangentialSpeed, float tangentialAcceleration,
            Vector3 velocity, Vector3 targetOffset)
        {
            if (tangentialSpeed >= -DirectionThreshold)
            {
                return Mathf.Abs(Vector3.Dot(Flatten(velocity),
                           targetOffset.normalized)) < DirectionThreshold
                    ? "radial correction"
                    : "following orbit target";
            }
            if (Vector3.Dot(Flatten(velocity), targetOffset) < 0f)
            {
                return "target overshoot";
            }
            return tangentialAcceleration > 0f
                ? "reverse inertia while braking"
                : "controller steering toward target";
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
