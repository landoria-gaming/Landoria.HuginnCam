using UnityEngine;

namespace Landoria.SagaCapture
{
    // Displays measured cinematic-camera FPS on the player screen.
    internal sealed class SagaCaptureFrameRateDisplay : MonoBehaviour
    {
        private GUIStyle _style;
        private int _renderedFrames;
        private float _measurementStartedAt;
        private float _framesPerSecond;

        // Starts a fresh measurement window.
        private void Awake()
        {
            _measurementStartedAt = Time.realtimeSinceStartup;
        }

        // Measures frames rendered by the attached cinematic camera.
        private void OnPostRender()
        {
            _renderedFrames++;
            float elapsed = Time.realtimeSinceStartup - _measurementStartedAt;
            if (elapsed < 0.5f)
            {
                return;
            }
            _framesPerSecond = _renderedFrames / elapsed;
            _renderedFrames = 0;
            _measurementStartedAt = Time.realtimeSinceStartup;
        }

        // Draws the measurement on the local player screen.
        private void OnGUI()
        {
            if (_style == null)
            {
                int fontSize = Mathf.Clamp(
                    Mathf.RoundToInt(Screen.height / 60f), 24, 42);
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    normal = { textColor = Color.white }
                };
            }
            GUI.Label(new Rect(8f, 6f, 300f, 55f),
                $"Saga {_framesPerSecond:F1} FPS", _style);
        }
    }
}
