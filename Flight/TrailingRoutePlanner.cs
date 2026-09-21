using UnityEngine;

namespace Landoria.SagaCapture
{
    // Preplans short curved trailing routes from drone and player motion.
    internal sealed class TrailingRoutePlanner
    {
        private const float RefreshInterval = 0.5f;
        private const float MinimumHorizon = 0.8f;
        private const float MaximumHorizon = 2.5f;
        private const float WaypointProgress = 0.4f;
        private const float MinimumPredictedBehindDistance = 3.5f;
        private const int RouteSamples = 8;
        private static readonly float[] LateralOffsets =
        {
            0f, -1f, 1f, -2f, 2f, -3f, 3f
        };
        private Vector3 _waypoint;
        private float _nextRefreshTime;
        private float _preferredSide;
        private bool _initialized;
        private bool _directRoute;

        // Returns a stable near-term waypoint on the clearest predicted route.
        internal Vector3 Plan(
            Vector3 origin, Vector3 desired, Vector3 droneVelocity,
            Vector3 playerPosition, Vector3 playerVelocity)
        {
            float speed = Mathf.Max(
                droneVelocity.magnitude, playerVelocity.magnitude);
            float horizon = Mathf.Clamp(
                0.8f + speed * 0.25f, MinimumHorizon, MaximumHorizon);
            Vector3 endpoint = desired + Flatten(playerVelocity) * horizon;
            endpoint = KeepBehindPlayer(
                endpoint, playerPosition, playerVelocity);
            if (_initialized && Time.time < _nextRefreshTime)
            {
                return _directRoute ? endpoint : _waypoint;
            }
            Vector3 tangent = GetInitialTangent(
                origin, endpoint, droneVelocity, horizon);
            _waypoint = SelectRoute(origin, endpoint, tangent);
            _nextRefreshTime = Time.time + RefreshInterval;
            _initialized = true;
            return _waypoint;
        }

        // Prevents prediction from putting the trailing target ahead.
        private static Vector3 KeepBehindPlayer(
            Vector3 endpoint, Vector3 playerPosition,
            Vector3 playerVelocity)
        {
            Vector3 travel = Flatten(playerVelocity);
            if (travel.sqrMagnitude < 0.04f)
            {
                return endpoint;
            }
            Vector3 direction = travel.normalized;
            float behind = Vector3.Dot(
                Flatten(playerPosition - endpoint), direction);
            if (behind < MinimumPredictedBehindDistance)
            {
                endpoint -= direction *
                    (MinimumPredictedBehindDistance - behind);
            }
            return endpoint;
        }

        internal void Reset()
        {
            _initialized = false;
            _preferredSide = 0f;
            _directRoute = false;
        }

        // Preserves current momentum while still bending toward the player.
        private static Vector3 GetInitialTangent(
            Vector3 origin, Vector3 endpoint, Vector3 velocity, float horizon)
        {
            if (velocity.sqrMagnitude > 0.04f)
            {
                return origin + velocity * horizon * 0.5f;
            }

            return Vector3.Lerp(origin, endpoint, 0.5f);
        }

        // Chooses a clear quadratic path, preferring the previous side.
        private Vector3 SelectRoute(
            Vector3 origin, Vector3 endpoint, Vector3 tangent)
        {
            Vector3 direction = Flatten(endpoint - origin);
            Vector3 right = direction.sqrMagnitude > 0.001f
                ? Vector3.Cross(Vector3.up, direction.normalized)
                : Vector3.right;
            foreach (float offset in OrderedOffsets())
            {
                Vector3 control = tangent + right * offset;
                if (IsClear(origin, control, endpoint))
                {
                    if (Mathf.Abs(offset) < 0.01f)
                    {
                        _preferredSide = 0f;
                        _directRoute = true;
                        return endpoint;
                    }
                    _directRoute = false;
                    if (Mathf.Abs(offset) > 0.01f)
                    {
                        _preferredSide = Mathf.Sign(offset);
                    }
                    return Bezier(origin, control, endpoint, WaypointProgress);
                }
            }

            _directRoute = false;
            return endpoint;
        }

        // Tests the previous avoidance side first to prevent oscillation.
        private float[] OrderedOffsets()
        {
            if (_preferredSide == 0f)
            {
                return LateralOffsets;
            }

            return _preferredSide < 0f
                ? new[] { 0f, -1f, -2f, -3f, 1f, 2f, 3f }
                : new[] { 0f, 1f, 2f, 3f, -1f, -2f, -3f };
        }

        // Samples the complete curve with the camera collision radius.
        private static bool IsClear(
            Vector3 origin, Vector3 control, Vector3 endpoint)
        {
            Vector3 previous = origin;
            for (int index = 1; index <= RouteSamples; index++)
            {
                float amount = index / (float)RouteSamples;
                Vector3 point = Bezier(origin, control, endpoint, amount);
                if (DroneTrajectoryPlanner.IsRouteBlocked(previous, point))
                {
                    return false;
                }
                previous = point;
            }

            return true;
        }

        private static Vector3 Bezier(
            Vector3 start, Vector3 control, Vector3 end, float amount)
        {
            float inverse = 1f - amount;
            return inverse * inverse * start +
                   2f * inverse * amount * control +
                   amount * amount * end;
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
