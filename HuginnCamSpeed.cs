using UnityEngine;

namespace Landoria.HuginnCam
{
    // Produces gradual random cruise speeds with optional catch-up acceleration.
    internal sealed class HuginnCamSpeed
    {
        private const float MinimumCruiseSpeed = 0f;
        private const float MaximumCruiseSpeed = 1f;
        private const float MaximumCatchUpSpeed = 6f;
        private const float CatchUpDistance = 5f;
        private const float CatchUpSpeedPerMeter = 0.75f;
        private const float SpeedChangeRate = 0.4f;
        private const float AvoidanceSpeed = 4f;
        private const float AvoidanceSpeedChangeRate = 4f;
        private float _current;
        private float _targetCruise;
        private float _nextChangeTime;

        internal float Current => _current;

        // Chooses the first cruise speed without an artificial startup ramp.
        internal void Initialize()
        {
            SelectCruiseSpeed();
            _current = _targetCruise;
        }

        // Returns a smoothly changing speed for the current target distance.
        internal float Update(float targetDistance, bool avoidingObstacle)
        {
            if (Time.time >= _nextChangeTime)
            {
                SelectCruiseSpeed();
            }

            float catchUp = Mathf.Max(0f, targetDistance - CatchUpDistance);
            float desired = Mathf.Min(
                MaximumCatchUpSpeed,
                _targetCruise + catchUp * CatchUpSpeedPerMeter);
            if (avoidingObstacle)
            {
                desired = Mathf.Max(desired, AvoidanceSpeed);
            }

            float changeRate = avoidingObstacle
                ? AvoidanceSpeedChangeRate
                : SpeedChangeRate;
            _current = Mathf.MoveTowards(
                _current, desired, changeRate * Time.deltaTime);
            return _current;
        }

        // Synchronizes cruise state with an externally smoothed movement.
        internal void MatchCurrent(float speed)
        {
            _current = Mathf.Max(0f, speed);
        }

        // Chooses a new cruise speed and holds it for several seconds.
        private void SelectCruiseSpeed()
        {
            _targetCruise = Random.Range(MinimumCruiseSpeed, MaximumCruiseSpeed);
            _nextChangeTime = Time.time + Random.Range(4f, 8f);
        }
    }
}
