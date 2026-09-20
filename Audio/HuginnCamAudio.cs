using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace Landoria.HuginnCam
{
    // Loads and plays embedded raven calls requested by Huginn behaviors.
    internal sealed class HuginnCamAudio : MonoBehaviour
    {
        private static readonly int[] LoadedCalls = { 2, 3, 4, 6 };
        private readonly Dictionary<int, AudioClip> _clips =
            new Dictionary<int, AudioClip>();
        private AudioSource _source;
        private int _pendingCall;
        private bool _wasEnabled;
        internal float LastPlayedTime { get; private set; } = -1f;

        // Finds Valheim's active listener even when it is outside Camera.main.
        internal static AudioListener FindActiveListener(Camera gameplayCamera)
        {
            AudioListener listener = gameplayCamera.GetComponent<AudioListener>() ??
                                     gameplayCamera.GetComponentInParent<AudioListener>();
            if (listener != null)
            {
                return listener;
            }

            AudioListener[] listeners = FindObjectsByType<AudioListener>(
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

        // Creates the non-spatial source and begins loading all embedded calls.
        internal void Initialize()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.volume = 0.35f;
            _wasEnabled = Preference.RavenCallsEnabled;
            StartCoroutine(LoadClips());
        }

        // Applies runtime audio configuration without choosing any call.
        private void Update()
        {
            HandleEnabledStateChange();
        }

        // Plays one numbered call without deciding why or when it is needed.
        internal void PlayCall(int number)
        {
            if (!Preference.RavenCallsEnabled)
            {
                _pendingCall = 0;
                return;
            }

            if (_source == null || !_clips.TryGetValue(number, out AudioClip clip))
            {
                _pendingCall = number;
                return;
            }

            _pendingCall = 0;
            _source.Stop();
            _source.clip = clip;
            _source.Play();
            LastPlayedTime = Time.time;
        }

        // Stops immediately when disabled and resets the timer when re-enabled.
        private void HandleEnabledStateChange()
        {
            bool enabled = Preference.RavenCallsEnabled;
            if (enabled == _wasEnabled)
            {
                return;
            }

            _wasEnabled = enabled;
            _pendingCall = 0;
            if (!enabled)
            {
                _source?.Stop();
            }
        }

        // Extracts embedded OGG files and lets Unity decode them asynchronously.
        private IEnumerator LoadClips()
        {
            foreach (int number in LoadedCalls)
            {
                string path = ExtractClip(number);
                using (UnityWebRequest request =
                       UnityWebRequestMultimedia.GetAudioClip("file:///" + path, AudioType.OGGVORBIS))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        HuginnCamPlugin.Log.LogWarning(
                            $"Could not load craw{number}.ogg: {request.error}");
                        continue;
                    }

                    AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                    clip.name = $"HuginnCam_craw{number}";
                    _clips[number] = clip;
                    if (_pendingCall == number)
                    {
                        PlayCall(number);
                    }
                }
            }
        }

        // Writes one embedded call to the temporary cache used by Unity's decoder.
        private static string ExtractClip(int number)
        {
            Assembly assembly = typeof(HuginnCamAudio).Assembly;
            string suffix = $".craw{number}.ogg";
            string resourceName = assembly.GetManifestResourceNames()
                .Single(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            string directory = Path.Combine(Path.GetTempPath(), "Landoria.HuginnCam");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"craw{number}.ogg");
            using (Stream input = assembly.GetManifestResourceStream(resourceName))
            using (FileStream output = File.Create(path))
            {
                input.CopyTo(output);
            }

            return path.Replace('\\', '/');
        }

        // Releases decoded clips when the camera rig is destroyed.
        private void OnDestroy()
        {
            foreach (AudioClip clip in _clips.Values)
            {
                Destroy(clip);
            }

            _clips.Clear();
        }
    }
}
