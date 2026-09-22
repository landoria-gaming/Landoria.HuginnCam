using UnityEngine;

namespace Landoria.SagaCapture
{
    // Presents the offscreen capture image without changing game resolution.
    internal sealed class SagaCapturePreviewPresenter : MonoBehaviour
    {
        private RenderTexture _texture;

        // Selects the same render texture consumed by the recorder.
        internal void Initialize(RenderTexture texture)
        {
            _texture = texture;
        }

        // Draws the capture image at its original aspect ratio.
        private void OnGUI()
        {
            if (_texture == null)
            {
                return;
            }
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.blackTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height),
                _texture, ScaleMode.ScaleToFit, false);
        }
    }
}
