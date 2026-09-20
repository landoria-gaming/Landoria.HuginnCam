using UnityEngine;

namespace Landoria.HuginnCam
{
    // Produces gradual random cruise speeds with optional catch-up acceleration.
    internal sealed class HuginnCamSpeed
    {
        private float _current;
        private float _targetCruise;
        private float _nextChangeTime;

        internal float Current => _current;

        // Chooses the first cruise speed without an artificial startup ramp.
        internal void Initialize(HuginnCamFlightProfile profile)
        {
            SelectCruiseSpeed(profile);
            _current = _targetCruise;
        }

        // Returns a smoothly changing speed for the current target distance.
        internal float Update(
            float targetDistance, HuginnCamFlightProfile profile)
        {
            if (Time.time >= _nextChangeTime)
            {
                SelectCruiseSpeed(profile);
            }

            _targetCruise = Mathf.Clamp(
                _targetCruise,
                profile.MinimumCruiseSpeed,
                profile.MaximumCruiseSpeed);
            float catchUp = Mathf.Max(
                0f, targetDistance - profile.CatchUpDistance);
            float desired = Mathf.Min(
                profile.MaximumSpeed,
                _targetCruise + catchUp * profile.CatchUpSpeedPerMeter);
            _current = Mathf.MoveTowards(
                _current, desired, profile.SpeedChangeRate * Time.deltaTime);
            return _current;
        }

        // Synchronizes cruise state with an externally smoothed movement.
        internal void MatchCurrent(float speed)
        {
            _current = Mathf.Max(0f, speed);
        }

        // Chooses a new cruise speed and holds it for several seconds.
        private void SelectCruiseSpeed(HuginnCamFlightProfile profile)
        {
            _targetCruise = Random.Range(
                profile.MinimumCruiseSpeed, profile.MaximumCruiseSpeed);
            _nextChangeTime = Time.time + Random.Range(4f, 8f);
        }
    }
}
