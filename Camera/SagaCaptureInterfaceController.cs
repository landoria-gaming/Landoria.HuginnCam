using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Landoria.SagaCapture
{
    // Hides the Valheim interface and restores its previous visibility state.
    internal sealed class SagaCaptureInterfaceController
    {
        private bool _previousHiddenState;
        private bool _stateCaptured;
        private readonly Dictionary<RawImage, bool> _scaledFrames =
            new Dictionary<RawImage, bool>();

        // Hides the interface while preserving its previous state.
        internal void Hide()
        {
            if (!Hud.instance)
            {
                return;
            }

            if (!_stateCaptured)
            {
                _previousHiddenState = Hud.IsUserHidden();
                _stateCaptured = true;
                HideScaledFrames();
            }
            Hud.instance.m_userHidden = true;
        }

        // Hides Valheim's upscaled gameplay image above the preview camera.
        private void HideScaledFrames()
        {
            foreach (FrameBufferScaler scaler in
                     Object.FindObjectsByType<FrameBufferScaler>(
                         FindObjectsSortMode.None))
            {
                RawImage image = scaler.GetComponent<RawImage>();
                if (image == null)
                {
                    continue;
                }
                _scaledFrames[image] = image.enabled;
                image.enabled = false;
            }
        }

        // Restores the interface state captured before camera mode.
        internal void Restore()
        {
            if (_stateCaptured && Hud.instance)
            {
                Hud.instance.m_userHidden = _previousHiddenState;
            }
            foreach (KeyValuePair<RawImage, bool> frame in _scaledFrames)
            {
                if (frame.Key != null)
                {
                    frame.Key.enabled = frame.Value;
                }
            }
            _scaledFrames.Clear();
            _stateCaptured = false;
        }
    }
}
