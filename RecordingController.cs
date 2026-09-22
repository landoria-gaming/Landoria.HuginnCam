using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Recorder = UnityRuntimeCameraRecorder.UnityRuntimeCameraRecorder;

namespace Landoria.SagaCapture
{
    // Records the secondary camera while gameplay remains visible.
    internal sealed class RecordingController : MonoBehaviour
    {
        private const int AntiAliasingSamples = 1;
        private const int MinimumWarmupFrames = 8;
        private const float MinimumWarmupSeconds = 0.5f;
        private const float MinimumShotDurationSeconds = 5f;
        private const float MaximumShotDurationSeconds = 10f;
        private SagaCaptureRig _cameraRig;
        private Recorder _recorder;
        private Coroutine _warmupRoutine;
        private AudioListener _gameplayListener;
        private string _outputPath;
        private int _lastVideoSourceIndex = -1;
        private readonly SagaCaptureFrameRateLimit _frameRateLimit =
            new SagaCaptureFrameRateLimit();

        internal bool IsActive => _recorder != null || _warmupRoutine != null;
        internal bool IsCameraActive => _cameraRig?.IsFlying == true;
        internal bool IsDroneImageActive =>
            _recorder?.IsCapturing == true &&
            _recorder.ActiveVideoSourceIndex == 0;

        // Reframes the drone whenever a mixed recording returns from gameplay.
        private void Update()
        {
            if (_recorder?.IsCapturing != true ||
                Preference.Content != OutputContent.DroneAndGameplay)
            {
                return;
            }
            int sourceIndex = _recorder.ActiveVideoSourceIndex;
            if (_lastVideoSourceIndex == 1 && sourceIndex == 0)
            {
                _cameraRig?.CutViewpoint();
            }
            if (sourceIndex >= 0)
            {
                _lastVideoSourceIndex = sourceIndex;
            }
        }

        // Starts a recording when the recorder is idle.
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
                _frameRateLimit.Apply(
                    "CaptureMode", Preference.CameraMaximumFrameRate);
                PrepareCamera(gameplayCamera);
                _outputPath = CreateOutputPath();
                _warmupRoutine = StartCoroutine(WarmupThenStart());
                Notify("Saga Capture is preparing CaptureMode.");
            }
            catch (Exception exception)
            {
                HandleRecordingFailed(exception);
            }
        }

        // Stops capture and starts asynchronous MP4 finalization.
        internal void StopRecording()
        {
            if (_warmupRoutine != null)
            {
                StopCoroutine(_warmupRoutine);
                _warmupRoutine = null;
                ReleaseCamera();
                Notify("Saga Capture CaptureMode cancelled during warmup.");
                return;
            }

            if (_recorder?.IsCapturing != true)
            {
                Notify("Saga Capture is finalizing the video.");
                return;
            }
            _recorder.StopRecording();
            ReleaseCamera();
        }

        // Renders several offscreen frames before starting the recorder.
        private IEnumerator WarmupThenStart()
        {
            float startedAt = Time.realtimeSinceStartup;
            int renderedFrames = 0;
            while (renderedFrames < MinimumWarmupFrames ||
                   Time.realtimeSinceStartup - startedAt < MinimumWarmupSeconds)
            {
                renderedFrames++;
                yield return new WaitForEndOfFrame();
            }

            _warmupRoutine = null;
            StartPreparedRecording();
        }

        // Starts capture after the secondary camera exposure has stabilized.
        private void StartPreparedRecording()
        {
            try
            {
                _recorder = gameObject.AddComponent<Recorder>();
                _lastVideoSourceIndex = -1;
                SubscribeRecorder();
                _cameraRig.BeginFlight();
                _recorder.StartRecording(
                    CreateSequence(), _gameplayListener,
                    CreateSettings(_outputPath));
            }
            catch (Exception exception)
            {
                HandleRecordingFailed(exception);
            }
        }

        // Validates the player camera and active audio listener.
        private void ValidateGameplayCamera(Camera gameplayCamera)
        {
            if (Player.m_localPlayer == null || gameplayCamera == null)
            {
                throw new InvalidOperationException(
                    "The local player camera is unavailable.");
            }
            _gameplayListener = SagaCaptureAudioListener.FindActive(gameplayCamera);
            if (_gameplayListener == null)
            {
                throw new InvalidOperationException(
                    "The gameplay audio listener is unavailable.");
            }
        }

        // Creates an offscreen camera for recording.
        private void PrepareCamera(Camera gameplayCamera)
        {
            GraphicsSettingsState graphicsSettings =
                SagaCaptureRendering.GetGraphicsSettings();
            SagaCaptureRendering.GetResolution(graphicsSettings,
                out int renderWidth, out int renderHeight,
                out FilterMode filterMode);
            _cameraRig = gameObject.AddComponent<SagaCaptureRig>();
            _cameraRig.Initialize(gameplayCamera, false, graphicsSettings);
            _cameraRig.BeginWarmup(
                renderWidth, renderHeight, AntiAliasingSamples, filterMode);
            SagaCaptureCameraLogger.LogEffectiveConfiguration(
                "CaptureMode", gameplayCamera, _cameraRig.Camera,
                GraphicsSettingsManager.Instance.ActiveSettings,
                graphicsSettings);
        }

        // Creates the configured drone-only or alternating camera sequence.
        private UnityRuntimeCameraRecorder.VideoSequenceSettings CreateSequence()
        {
            var drone = UnityRuntimeCameraRecorder.VideoSequenceSource
                .FromCamera(_cameraRig.Camera);
            if (Preference.Content == OutputContent.DroneOnly)
            {
                return new UnityRuntimeCameraRecorder.VideoSequenceSettings
                {
                    Sources = new[] { drone }
                };
            }

            return new UnityRuntimeCameraRecorder.VideoSequenceSettings
            {
                Sources = new[]
                {
                    drone,
                    UnityRuntimeCameraRecorder.VideoSequenceSource
                        .FromScreen(false)
                },
                Order = UnityRuntimeCameraRecorder.VideoSequenceOrder.Sequential,
                MinimumShotDurationSeconds = MinimumShotDurationSeconds,
                MaximumShotDurationSeconds = MaximumShotDurationSeconds,
                Transitions = new[]
                {
                    UnityRuntimeCameraRecorder.VideoSequenceTransition.NoTransition
                }
            };
        }

        // Creates the encoder and output configuration.
        private UnityRuntimeCameraRecorder.RecordingSettings CreateSettings(
            string outputPath)
        {
            RenderTexture cameraTarget = _cameraRig.Camera.targetTexture;
            return new UnityRuntimeCameraRecorder.RecordingSettings
            {
                FfmpegPath = ResolveFfmpegDirectory(),
                TemporaryContainerPath = outputPath + ".mkv.tmp",
                OutputPath = outputPath,
                Width = cameraTarget.width,
                Height = cameraTarget.height,
                MaximumFrameRate = Preference.CameraMaximumFrameRate,
                SourceAntiAliasingSamples = AntiAliasingSamples,
                QualityPreset = Preference.RecordingQuality
            };
        }

        // Resolves the FFmpeg directory used by the recorder.
        internal static string ResolveFfmpegDirectory()
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
            string directory = Environment.GetFolderPath(
                Environment.SpecialFolder.MyVideos);
            Directory.CreateDirectory(directory);
            return Path.Combine(
                directory,
                $"SagaCapture_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.mp4");
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

        // Reports that capture has started.
        private void HandleCaptureStarted()
        {
            Notify("Saga Capture CaptureMode started.");
            SagaCapturePlugin.Log.LogInfo($"Recording started: {_outputPath}");
        }

        // Reports that MP4 finalization has started.
        private void HandleFinalizationStarted()
        {
            Notify("Saga Capture is finalizing the video.");
        }

        // Reports the completed file and releases the session.
        private void HandleRecordingCompleted()
        {
            Notify("Saga Capture saved the video to My Videos.");
            SagaCapturePlugin.Log.LogInfo($"Recording saved: {_outputPath}");
            ReleaseSession();
        }

        // Reports a recorder failure and releases the session.
        private void HandleRecordingFailed(Exception exception)
        {
            SagaCapturePlugin.Log.LogError(exception);
            Notify($"Saga Capture recording failed: {exception.Message}");
            ReleaseSession();
        }

        // Releases the recorder and offscreen camera.
        private void ReleaseSession()
        {
            if (_warmupRoutine != null)
            {
                StopCoroutine(_warmupRoutine);
                _warmupRoutine = null;
            }
            ReleaseCamera();
            if (_recorder != null)
            {
                UnsubscribeRecorder();
                Destroy(_recorder);
                _recorder = null;
            }
            _gameplayListener = null;
            _lastVideoSourceIndex = -1;
        }

        // Releases the offscreen camera.
        private void ReleaseCamera()
        {
            if (_cameraRig != null)
            {
                _cameraRig.Dispose();
                Destroy(_cameraRig);
                _cameraRig = null;
            }
            _frameRateLimit.Restore("CaptureMode");
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
            Player.m_localPlayer?.Message(
                MessageHud.MessageType.TopLeft, message);
        }
    }
}
