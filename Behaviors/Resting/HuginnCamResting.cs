using UnityEngine;

namespace Landoria.HuginnCam
{
    // Keeps Huginn grounded while it calmly observes its surroundings.
    internal sealed class HuginnCamResting
    {
        private const float MinimumDuration = 5f;
        private const float MaximumDuration = 10f;
        private const float LookCycleSpeed = 0.12f;
        private const float MaximumLookAngle = 100f;
        private const float MinimumSpeed = 0f;
        private const float MaximumSpeed = 0f;
        private float _endTime;
        private float _lookSeed;

        internal bool IsActive { get; private set; }
        internal HuginnCamFlightProfile Profile => new HuginnCamFlightProfile(
            MinimumSpeed, MaximumSpeed, MaximumSpeed,
            float.MaxValue, 0f, 0.4f);

        // Starts one quiet grounded observation session.
        internal void Begin()
        {
            IsActive = true;
            _endTime = Time.time + Random.Range(MinimumDuration, MaximumDuration);
            _lookSeed = Random.Range(0f, 1000f);
        }

        // Ends naturally and reports whether normal flight should resume.
        internal bool Update()
        {
            if (!IsActive || Time.time < _endTime)
            {
                return false;
            }

            IsActive = false;
            return true;
        }

        // Interrupts rest immediately only when combat danger starts.
        internal bool CancelForDanger(bool danger)
        {
            if (!IsActive || !danger)
            {
                return false;
            }

            IsActive = false;
            return true;
        }

        // Looks downhill on slopes or scans gently around on level ground.
        internal void UpdateLook(
            HuginnCamLook look, Transform cameraTransform, Vector3 playerFocus)
        {
            if (HuginnCamTerrain.TryGetDownhillDirection(
                cameraTransform.position, out Vector3 downhill))
            {
                look.UpdateHorizon(cameraTransform, downhill);
                return;
            }

            Vector3 towardPlayer = playerFocus - cameraTransform.position;
            towardPlayer.y = 0f;
            float sample = Mathf.PerlinNoise(
                _lookSeed, Time.time * LookCycleSpeed) * 2f - 1f;
            Vector3 direction = Quaternion.Euler(
                0f, sample * MaximumLookAngle, 0f) * towardPlayer.normalized;
            look.UpdateFree(cameraTransform, direction);
        }
    }
}
