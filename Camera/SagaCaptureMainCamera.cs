using UnityEngine;

namespace Landoria.SagaCapture
{
    // Transfers Unity's MainCamera identity to the recorder camera.
    internal sealed class SagaCaptureMainCamera
    {
        private Camera _source;
        private string _sourceTag;
        private bool _ownsTag;

        // Remembers the gameplay camera without affecting offscreen recording.
        internal void Initialize(Camera source)
        {
            _source = source;
        }

        // Makes Valheim evaluate spatial systems from the recorder camera.
        internal void Take(Camera recorderCamera)
        {
            if (_source == null || recorderCamera == null ||
                !_source.CompareTag("MainCamera"))
            {
                return;
            }

            _sourceTag = _source.tag;
            _source.tag = "Untagged";
            recorderCamera.tag = "MainCamera";
            _ownsTag = true;
        }

        // Restores the gameplay camera before the recorder camera is destroyed.
        internal void Restore(Camera recorderCamera)
        {
            if (!_ownsTag)
            {
                return;
            }

            if (recorderCamera != null)
            {
                recorderCamera.tag = "Untagged";
            }
            if (_source != null)
            {
                _source.tag = _sourceTag;
            }
            _ownsTag = false;
            _source = null;
        }
    }
}
