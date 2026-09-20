using UnityEngine;

namespace Landoria.SagaCapture
{
    // Locates the listener used by Valheim's gameplay audio mix.
    internal static class SagaCaptureAudioListener
    {
        // Finds Valheim's active listener even when it is outside Camera.main.
        internal static AudioListener FindActive(Camera gameplayCamera)
        {
            AudioListener listener = gameplayCamera.GetComponent<AudioListener>() ??
                                     gameplayCamera.GetComponentInParent<AudioListener>();
            if (listener != null)
            {
                return listener;
            }

            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(
                FindObjectsSortMode.None);
            foreach (AudioListener candidate in listeners)
            {
                if (candidate.enabled && candidate.gameObject.activeInHierarchy)
                {
                    return candidate;
                }
            }

            return listeners.Length > 0 ? listeners[0] : null;
        }
    }
}
