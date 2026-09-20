using System;
using System.IO;
using UnityEngine;
using Recorder = UnityRuntimeCameraRecorder.UnityRuntimeCameraRecorder;

namespace Landoria.HuginnCam
{
    // Records the wandering Huginn camera while gameplay remains visible.
    internal sealed class RecordingController : MonoBehaviour
    {
        private const int AntiAliasingSamples = 1;
        private HuginnCamRig _cameraRig;
        private Recorder _recorder;
        private AudioListener _gameplayListener;
        private string _outputPath;

        internal bool IsActive => _recorder != null;

        // Starts a new Huginn camera recording when the recorder is idle.
        internal void StartRecording()
        {
            if (IsActive)
            {
                return;
            }

            try
            {
                Camera gameplayCamera = Camera.main;
                ValidateGameplayCamera(gameplayCamera);
                PrepareCamera(gameplayCamera);
                _outputPath = CreateOutputPath();
                _recorder = gameObject.AddComponent<Recorder>();
                SubscribeRecorder();
                _recorder.StartRecording(
                    CreateSequence(),
                    _gameplayListener,
                    CreateSettings(_outputPath));
                Notify("Huginn Cam is preparing the recording.");
            }
            catch (Exception exception)
            {
                HandleRecordingFailed(exception);
            }
        }

        // Stops capture and starts asynchronous MP4 finalization.
        internal void StopRecording()
        {
            if (_recorder?.IsCapturing != true)
            {
                Notify("Huginn Cam is finalizing the video.");
                return;
            }

            _recorder.StopRecording();
            ReleaseCamera();
        }

        // Validates the player camera and its active audio listener.
        private void ValidateGameplayCamera(Camera gameplayCamera)
        {
            if (Player.m_localPlayer == null || gameplayCamera == null)
            {
                throw new InvalidOperationException("The local player camera is unavailable.");
            }

            _gameplayListener = HuginnCamAudio.FindActiveListener(gameplayCamera);
            if (_gameplayListener == null)
            {
                throw new InvalidOperationException("The gameplay audio listener is unavailable.");
            }
        }

        // Creates an offscreen Huginn camera with copied visual effects.
        private void PrepareCamera(Camera gameplayCamera)
        {
            _cameraRig = gameObject.AddComponent<HuginnCamRig>();
            _cameraRig.Initialize(gameplayCamera, false);
            int width = Math.Max(2, Screen.width & ~1);
            int height = Math.Max(2, Screen.height & ~1);
            _cameraRig.BeginWarmup(width, height, AntiAliasingSamples);
        }

        // Creates a single-source sequence for the Huginn camera.
        private UnityRuntimeCameraRecorder.VideoSequenceSettings CreateSequence()
        {
            return new UnityRuntimeCameraRecorder.VideoSequenceSettings
            {
                Sources = new[]
                {
                    UnityRuntimeCameraRecorder.VideoSequenceSource.FromCamera(_cameraRig.Camera)
                }
            };
        }

        // Creates the encoder and output configuration for one recording.
        private static UnityRuntimeCameraRecorder.RecordingSettings CreateSettings(
            string outputPath)
        {
            return new UnityRuntimeCameraRecorder.RecordingSettings
            {
                FfmpegPath = ResolveFfmpegDirectory(),
                TemporaryContainerPath = outputPath + ".mkv.tmp",
                OutputPath = outputPath,
                Width = Math.Max(2, Screen.width & ~1),
                Height = Math.Max(2, Screen.height & ~1),
                MaximumFrameRate = Preference.MaximumFrameRate,
                SourceAntiAliasingSamples = AntiAliasingSamples,
                QualityPreset = Preference.RecordingQuality
            };
        }

        // Resolves the FFmpeg bin directory used by the sample recorder.
        private static string ResolveFfmpegDirectory()
        {
            string directory = Environment.GetEnvironmentVariable("FFMPEG_PATH");
            if (string.IsNullOrWhiteSpace(directory) ||
                !File.Exists(Path.Combine(directory, "ffmpeg.exe")))
            {
                throw new FileNotFoundException(
                    "FFMPEG_PATH must point to a directory containing ffmpeg.exe.");
            }

            return directory;
        }

        // Creates a unique MP4 path in the user's video directory.
        private static string CreateOutputPath()
        {
            string directory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            Directory.CreateDirectory(directory);
            string fileName = $"HuginnCam_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.mp4";
            return Path.Combine(directory, fileName);
        }

        // Connects the recorder lifecycle callbacks.
        private void SubscribeRecorder()
        {
            _recorder.CaptureStarted += HandleCaptureStarted;
            _recorder.FinalizationStarted += HandleFinalizationStarted;
            _recorder.RecordingCompleted += HandleRecordingCompleted;
            _recorder.RecordingFailed += HandleRecordingFailed;
        }

        // Disconnects the recorder lifecycle callbacks.
        private void UnsubscribeRecorder()
        {
            _recorder.CaptureStarted -= HandleCaptureStarted;
            _recorder.FinalizationStarted -= HandleFinalizationStarted;
            _recorder.RecordingCompleted -= HandleRecordingCompleted;
            _recorder.RecordingFailed -= HandleRecordingFailed;
        }

        // Reports that video and game-audio capture are active.
        private void HandleCaptureStarted()
        {
            Notify("Huginn Cam recording started.");
            HuginnCamPlugin.Log.LogInfo($"Recording started: {_outputPath}");
        }

        // Reports that FFmpeg is finalizing the MP4.
        private void HandleFinalizationStarted()
        {
            Notify("Huginn Cam is finalizing the video.");
        }

        // Reports the saved MP4 and releases the completed session.
        private void HandleRecordingCompleted()
        {
            Notify("Huginn Cam saved the video to My Videos.");
            HuginnCamPlugin.Log.LogInfo($"Recording saved: {_outputPath}");
            ReleaseSession();
        }

        // Reports a recorder failure and releases all session resources.
        private void HandleRecordingFailed(Exception exception)
        {
            HuginnCamPlugin.Log.LogError(exception);
            Notify($"Huginn Cam recording failed: {exception.Message}");
            ReleaseSession();
        }

        // Releases the recorder and its offscreen Huginn camera.
        private void ReleaseSession()
        {
            ReleaseCamera();
            if (_recorder != null)
            {
                UnsubscribeRecorder();
                Destroy(_recorder);
                _recorder = null;
            }

            _gameplayListener = null;
        }

        // Releases the offscreen Huginn camera.
        private void ReleaseCamera()
        {
            if (_cameraRig != null)
            {
                _cameraRig.Dispose();
                Destroy(_cameraRig);
                _cameraRig = null;
            }
        }

        // Stops and releases the recorder before plugin unload.
        internal void Shutdown()
        {
            if (_recorder?.IsCapturing == true)
            {
                _recorder.StopRecording();
            }

            ReleaseSession();
        }

        // Displays a local status message when a player is available.
        private static void Notify(string message)
        {
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, message);
        }
    }
}
