using UnityEngine;

namespace Landoria.SagaCapture
{
    // Temporarily aligns the whole game loop with the Saga camera frame rate.
    internal sealed class SagaCaptureFrameRateLimit
    {
        private int _originalTargetFrameRate;
        private int _originalVSyncCount;
        private bool _applied;

        // Applies the configured camera limit and disables conflicting VSync.
        internal void Apply(string mode, int maximumFrameRate)
        {
            if (_applied)
            {
                return;
            }
            _originalTargetFrameRate = Application.targetFrameRate;
            _originalVSyncCount = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = maximumFrameRate;
            _applied = true;
            SagaCapturePlugin.Log.LogInfo(
                $"{mode} frame-rate limit applied: " +
                $"{maximumFrameRate} FPS " +
                $"(previous target={_originalTargetFrameRate}, " +
                $"vSyncCount={_originalVSyncCount}).");
        }

        // Restores the exact frame-rate state used before entering Saga mode.
        internal void Restore(string mode)
        {
            if (!_applied)
            {
                return;
            }
            Application.targetFrameRate = _originalTargetFrameRate;
            QualitySettings.vSyncCount = _originalVSyncCount;
            _applied = false;
            SagaCapturePlugin.Log.LogInfo(
                $"{mode} restored game frame-rate state: " +
                $"target={_originalTargetFrameRate}, " +
                $"vSyncCount={_originalVSyncCount}.");
        }
    }
}
