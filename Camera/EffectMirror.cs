using UnityEngine;

namespace Landoria.SagaCapture
{
    // Associates one source image effect with its independent camera copy.
    internal sealed class SagaCaptureEffectMirror
    {
        internal readonly Component Source;
        internal readonly Component Destination;

        // Stores the two components participating in a settings mirror.
        internal SagaCaptureEffectMirror(
            Component source, Component destination)
        {
            Source = source;
            Destination = destination;
        }
    }
}
