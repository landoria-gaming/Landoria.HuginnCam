using System.Collections;
using UnityEngine;

namespace Landoria.SagaCapture
{
    // Owns the single texture source exposed to the video recorder.
    internal sealed class SagaCaptureVideoSource : MonoBehaviour
    {
        private Camera _cinematicCamera;
        private RenderTexture _output;
        private bool _includeGameplayUi;
        private Coroutine _screenCaptureRoutine;

        internal Texture Texture => _output;
        internal bool IsCinematic { get; private set; }

        // Connects both mod-controlled views to one persistent output texture.
        internal void Initialize(Camera cinematicCamera, RenderTexture output,
            bool includeGameplayUi)
        {
            _cinematicCamera = cinematicCamera;
            _output = output;
            _includeGameplayUi = includeGameplayUi;
            SetCinematic(true);
            if (includeGameplayUi)
            {
                _screenCaptureRoutine = StartCoroutine(CaptureScreenFrames());
            }
        }

        // Performs a hard cut without changing the recorder's source.
        internal void SetCinematic(bool enabled)
        {
            IsCinematic = enabled;
            if (_cinematicCamera != null)
            {
                _cinematicCamera.enabled = enabled;
                if (enabled)
                {
                    _cinematicCamera.Render();
                }
            }
        }

        // Copies the pre-interface gameplay frame when that view is selected.
        internal void CopyGameplay(RenderTexture source)
        {
            if (!IsCinematic && !_includeGameplayUi && _output != null)
            {
                Graphics.Blit(source, _output);
            }
        }

        // Copies the completed screen when gameplay UI inclusion is enabled.
        private IEnumerator CaptureScreenFrames()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();
                if (!IsCinematic && _output != null)
                {
                    CopyScreenWithVerticalCorrection();
                }
            }
        }

        // Converts the screen texture from display-space to recorder orientation.
        private void CopyScreenWithVerticalCorrection()
        {
            RenderTexture screen = RenderTexture.GetTemporary(
                _output.width, _output.height, 0, _output.format,
                RenderTextureReadWrite.sRGB);
            try
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(screen);
                Graphics.Blit(screen, _output,
                    new Vector2(1f, -1f), new Vector2(0f, 1f));
            }
            finally
            {
                RenderTexture.ReleaseTemporary(screen);
            }
        }

        // Stops copying frames without releasing the rig-owned output texture.
        internal void Dispose()
        {
            if (_screenCaptureRoutine != null)
            {
                StopCoroutine(_screenCaptureRoutine);
                _screenCaptureRoutine = null;
            }
            _cinematicCamera = null;
            _output = null;
        }
    }
}
