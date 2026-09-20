using UnityEngine;

namespace Landoria.HuginnCam
{
    // Transfers Unity's MainCamera identity while Huginn owns the visible view.
    internal sealed class HuginnCamMainCamera
    {
        private Camera _source;
        private string _sourceTag;
        private bool _ownsTag;

        // Remembers the gameplay camera without affecting offscreen recording.
        internal void Initialize(Camera source)
        {
            _source = source;
        }

        // Makes Valheim evaluate spatial systems from Huginn's position.
        internal void Take(Camera huginn)
        {
            if (_source == null || huginn == null ||
                !_source.CompareTag("MainCamera"))
            {
                return;
            }

            _sourceTag = _source.tag;
            _source.tag = "Untagged";
            huginn.tag = "MainCamera";
            _ownsTag = true;
        }

        // Restores the gameplay camera before Huginn's camera is destroyed.
        internal void Restore(Camera huginn)
        {
            if (!_ownsTag)
            {
                return;
            }

            if (huginn != null)
            {
                huginn.tag = "Untagged";
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
