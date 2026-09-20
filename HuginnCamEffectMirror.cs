using UnityEngine;

namespace Landoria.HuginnCam
{
    // Associates a source image effect with its independent Huginn camera copy.
    internal sealed class HuginnCamEffectMirror
    {
        internal readonly Component Source;
        internal readonly Component Destination;

        // Stores the two components participating in a settings mirror.
        internal HuginnCamEffectMirror(Component source, Component destination)
        {
            Source = source;
            Destination = destination;
        }
    }
}
