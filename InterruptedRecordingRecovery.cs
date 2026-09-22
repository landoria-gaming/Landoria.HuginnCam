using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Recorder = UnityRuntimeCameraRecorder.UnityRuntimeCameraRecorder;

namespace Landoria.SagaCapture
{
    // Finalizes Saga Capture containers left behind by interrupted game sessions.
    internal sealed class InterruptedRecordingRecovery : MonoBehaviour
    {
        private const string TemporarySuffix = ".mkv.tmp";
        private readonly Queue<string> _pending = new Queue<string>();
        private Recorder _recorder;
        private string _temporaryPath;
        private string _outputPath;

        // Discovers interrupted recordings once plugin initialization is complete.
        private void Start()
        {
            try
            {
                EnqueueInterruptedRecordings();
                StartNextRecovery();
            }
            catch (Exception exception)
            {
                SagaCapturePlugin.Log.LogError(exception);
            }
        }

        // Adds recoverable Saga Capture containers in chronological name order.
        private void EnqueueInterruptedRecordings()
        {
            string directory = Environment.GetFolderPath(
                Environment.SpecialFolder.MyVideos);
            if (!Directory.Exists(directory))
            {
                return;
            }

            string[] paths = Directory.GetFiles(
                directory, "SagaCapture_*" + TemporarySuffix);
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                string outputPath = GetOutputPath(path);
                if (!File.Exists(outputPath))
                {
                    _pending.Enqueue(path);
                }
                else
                {
                    SagaCapturePlugin.Log.LogWarning(
                        $"Skipped interrupted recording because output exists: {path}");
                }
            }
        }

        // Starts recovery of the next queued temporary container.
        private void StartNextRecovery()
        {
            if (_pending.Count == 0 || _recorder != null)
            {
                return;
            }

            _temporaryPath = _pending.Dequeue();
            _outputPath = GetOutputPath(_temporaryPath);
            try
            {
                _recorder = gameObject.AddComponent<Recorder>();
                SubscribeRecorder();
                SagaCapturePlugin.Log.LogWarning(
                    $"Recovering interrupted recording: {_temporaryPath}");
                _recorder.RecoverRecording(CreateSettings());
            }
            catch (Exception exception)
            {
                HandleRecoveryFailed(exception);
            }
        }

        // Creates the settings needed to finalize an existing container.
        private UnityRuntimeCameraRecorder.RecordingSettings CreateSettings()
        {
            return new UnityRuntimeCameraRecorder.RecordingSettings
            {
                FfmpegPath = RecordingController.ResolveFfmpegDirectory(),
                TemporaryContainerPath = _temporaryPath,
                OutputPath = _outputPath,
                QualityPreset = Preference.RecordingQuality
            };
        }

        // Converts a temporary container name back to its intended output name.
        private static string GetOutputPath(string temporaryPath)
        {
            return temporaryPath.Substring(
                0, temporaryPath.Length - TemporarySuffix.Length);
        }

        // Connects recovery completion and failure callbacks.
        private void SubscribeRecorder()
        {
            _recorder.RecordingCompleted += HandleRecoveryCompleted;
            _recorder.RecordingFailed += HandleRecoveryFailed;
        }

        // Reports successful recovery and advances the queue.
        private void HandleRecoveryCompleted()
        {
            SagaCapturePlugin.Log.LogInfo(
                $"Recovered interrupted recording: {_outputPath}");
            ReleaseRecorder();
            StartNextRecovery();
        }

        // Reports failed recovery, preserves the container and advances the queue.
        private void HandleRecoveryFailed(Exception exception)
        {
            SagaCapturePlugin.Log.LogError(
                $"Could not recover interrupted recording {_temporaryPath}: {exception}");
            ReleaseRecorder();
            StartNextRecovery();
        }

        // Disconnects and destroys the active recovery recorder.
        private void ReleaseRecorder()
        {
            if (_recorder == null)
            {
                return;
            }
            _recorder.RecordingCompleted -= HandleRecoveryCompleted;
            _recorder.RecordingFailed -= HandleRecoveryFailed;
            Destroy(_recorder);
            _recorder = null;
        }

        // Stops any background recovery during plugin unload.
        internal void Shutdown()
        {
            _pending.Clear();
            ReleaseRecorder();
        }
    }
}
