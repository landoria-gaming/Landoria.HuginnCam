using UnityEngine;

namespace Landoria.SagaCapture
{
    // Copies the final gameplay camera image before overlay UI is rendered.
    internal sealed class SagaCaptureGameplayCapture : MonoBehaviour
    {
        private SagaCaptureVideoSource _videoSource;

        // Connects gameplay rendering to the mod-owned recording source.
        internal void Initialize(SagaCaptureVideoSource videoSource)
        {
            _videoSource = videoSource;
        }

        // Preserves normal display while copying the pre-UI camera result.
        private void OnRenderImage(
            RenderTexture source, RenderTexture destination)
        {
            _videoSource?.CopyGameplay(source);
            Graphics.Blit(source, destination);
        }

        // Releases the offscreen gameplay image.
        internal void Dispose()
        {
            _videoSource = null;
        }
    }
}
