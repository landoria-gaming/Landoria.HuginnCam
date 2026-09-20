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
    // Loads and schedules the embedded raven calls used by Huginn.
    internal sealed class HuginnCamAudio : MonoBehaviour
    {
        private const float MinimumCallDelay = 10f;
        private const float MaximumCallDelay = 20f;
        private readonly Dictionary<int, AudioClip> _clips =
            new Dictionary<int, AudioClip>();
        private AudioSource _source;
        private float _nextCallTime;
        private int _pendingCall;
        private bool _wasEnabled;

        // Creates the non-spatial source and begins loading all embedded calls.
        internal void Initialize()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _wasEnabled = Preference.RavenCallsEnabled;
            ScheduleRegularCall();
            StartCoroutine(LoadClips());
        }

        // Plays a normal call whenever its randomized interval expires.
        private void Update()
        {
            if (HandleEnabledStateChange() || !Preference.RavenCallsEnabled)
            {
                return;
            }

            if (Time.time < _nextCallTime || _clips.Count < 6)
            {
                return;
            }

            PlayRegularCall();
        }

        // Plays the longer call that announces an overhead flight.
        internal void PlayOverheadDeparture()
        {
            Play(UnityEngine.Random.value < 0.5f ? 1 : 5);
        }

        // Plays one normal call after Huginn reaches the overhead position.
        internal void PlayOverheadArrival()
        {
            PlayRegularCall();
        }

        // Plays the short call used when visible Huginn mode starts.
        internal void PlayActivationCall()
        {
            Play(6);
        }

        // Plays the call associated with an overhead state transition.
        internal void HandleOverwatch(HuginnCamOverwatch overwatch)
        {
            if (overwatch.StartedThisFrame)
            {
                PlayOverheadDeparture();
            }

            if (overwatch.ArrivedThisFrame)
            {
                PlayOverheadArrival();
            }
        }

        // Chooses uniformly from calls 2, 3, 4, and 6.
        private void PlayRegularCall()
        {
            int[] regularCalls = { 2, 3, 4, 6 };
            Play(regularCalls[UnityEngine.Random.Range(0, regularCalls.Length)]);
        }

        // Plays one loaded clip and restarts the regular-call interval.
        private void Play(int number)
        {
            if (!Preference.RavenCallsEnabled)
            {
                _pendingCall = 0;
                return;
            }

            if (_source == null || !_clips.TryGetValue(number, out AudioClip clip))
            {
                _pendingCall = number;
                ScheduleRegularCall();
                return;
            }

            _pendingCall = 0;
            _source.Stop();
            _source.clip = clip;
            _source.Play();
            ScheduleRegularCall();
        }

        // Stops immediately when disabled and resets the timer when re-enabled.
        private bool HandleEnabledStateChange()
        {
            bool enabled = Preference.RavenCallsEnabled;
            if (enabled == _wasEnabled)
            {
                return false;
            }

            _wasEnabled = enabled;
            _pendingCall = 0;
            if (!enabled)
            {
                _source?.Stop();
            }
            else
            {
                ScheduleRegularCall();
            }

            return true;
        }

        // Chooses the next normal-call time between ten and twenty seconds.
        private void ScheduleRegularCall()
        {
            _nextCallTime = Time.time +
                            UnityEngine.Random.Range(MinimumCallDelay, MaximumCallDelay);
        }

        // Extracts embedded OGG files and lets Unity decode them asynchronously.
        private IEnumerator LoadClips()
        {
            for (int number = 1; number <= 6; number++)
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
                        Play(number);
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
