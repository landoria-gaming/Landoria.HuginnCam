using UnityEngine;

namespace Landoria.HuginnCam
{
    // Schedules ordinary raven calls independently from flight behaviors.
    internal sealed class HuginnCamAmbientCallScheduler
    {
        private const float MinimumCallDelay = 10f;
        private const float MaximumCallDelay = 20f;
        private readonly int[] _calls = { 2, 3, 4, 6 };
        private float _nextCallTime;
        private float _observedCallTime = -1f;

        // Plays a random ambient call after a quiet interval.
        internal void Update(HuginnCamAudio audio)
        {
            if (!Preference.RavenCallsEnabled)
            {
                _nextCallTime = 0f;
                return;
            }

            if (audio.LastPlayedTime != _observedCallTime)
            {
                _observedCallTime = audio.LastPlayedTime;
                ScheduleNextCall();
                return;
            }

            if (_nextCallTime <= 0f)
            {
                ScheduleNextCall();
            }
            else if (Time.time >= _nextCallTime)
            {
                audio.PlayCall(_calls[Random.Range(0, _calls.Length)]);
                ScheduleNextCall();
            }
        }

        // Chooses the next ambient call time after a guaranteed quiet period.
        private void ScheduleNextCall()
        {
            _nextCallTime = Time.time +
                            Random.Range(MinimumCallDelay, MaximumCallDelay);
        }
    }
}
