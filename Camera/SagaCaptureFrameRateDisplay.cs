using UnityEngine;

namespace Landoria.SagaCapture
{
    // Displays measured Saga-camera FPS on the player screen only.
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

        // Measures frames actually rendered by the attached Saga camera.
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

        // Draws the diagnostic after camera rendering, outside the recording.
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
            string text = $"Saga {_framesPerSecond:F1} FPS";
            GUI.Label(new Rect(8f, 6f, 300f, 55f), text, _style);
        }
    }
}
