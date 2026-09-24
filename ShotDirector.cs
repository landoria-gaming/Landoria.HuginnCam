using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Recorder = UnityRuntimeCameraRecorder.UnityRuntimeCameraRecorder;

namespace Landoria.SagaCapture
{
    // Directs shot selection and owns the recording lifecycle.
    internal sealed class ShotDirector : MonoBehaviour
    {
        private const int AntiAliasingSamples = 1;
        private const int MinimumWarmupFrames = 8;
        private const float MinimumWarmupSeconds = 0.5f;
        private const float MinimumShotDurationSeconds = 5f;
        private const float MaximumShotDurationSeconds = 10f;
        private const float TargetLossGraceSeconds = 1f;
        private SagaCaptureRig _cameraRig;
        private Recorder _recorder;
        private Coroutine _warmupRoutine;
        private AudioListener _gameplayListener;
        private SagaCaptureGameplayCapture _gameplayCapture;
        private SagaCaptureVideoSource _videoSource;
        private string _outputPath;
        private float _targetLostSince = -1f;
        private float _nextShotAt;
        private readonly SagaCaptureFrameRateLimit _frameRateLimit =
            new SagaCaptureFrameRateLimit();

        internal bool IsActive => _recorder != null || _warmupRoutine != null;
        internal bool IsCameraActive => _cameraRig?.IsMoving == true;
        internal bool IsCinematicImageActive =>
            _recorder?.IsCapturing == true &&
            _videoSource?.IsCinematic == true;

        // Applies mod-owned cuts and protects recordings from lost framing.
        private void Update()
        {
            if (_recorder?.IsCapturing != true)
            {
                return;
            }
            if (Time.time >= _nextShotAt)
            {
                CutToNextSource();
            }
            CheckCinematicVisibility();
        }

        // Forces gameplay after the cinematic camera loses the player.
        private void CheckCinematicVisibility()
        {
            if (_videoSource?.IsCinematic != true)
            {
                _targetLostSince = -1f;
                return;
            }
            if (_cameraRig?.HasTargetVisibility() == true)
            {
                _targetLostSince = -1f;
                return;
            }
            if (_targetLostSince < 0f)
            {
                _targetLostSince = Time.time;
            }
            else if (Time.time - _targetLostSince >= TargetLossGraceSeconds)
            {
                ActivateGameplay("Cinematic target occluded; cut to gameplay.");
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
                _targetLostSince = -1f;
                SubscribeRecorder();
                _cameraRig.BeginMovement();
                if (!_cameraRig.TryCutViewpoint(false))
                {
                    SagaCapturePlugin.Log.LogWarning(
                        "Initial cinematic cut has no visible viewpoint.");
                }
                _videoSource.SetCinematic(true);
                ScheduleNextCut();
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
            _cameraRig.Initialize(gameplayCamera, graphicsSettings);
            _cameraRig.BeginWarmup(
                renderWidth, renderHeight, AntiAliasingSamples, filterMode);
            _videoSource = gameObject.AddComponent<SagaCaptureVideoSource>();
            _videoSource.Initialize(_cameraRig.Camera,
                _cameraRig.OutputTexture, Preference.GameplayIncludeUi);
            _gameplayCapture = gameplayCamera.gameObject
                .AddComponent<SagaCaptureGameplayCapture>();
            _gameplayCapture.Initialize(_videoSource);
            SagaCaptureCameraLogger.LogEffectiveConfiguration(
                "CaptureMode", gameplayCamera, _cameraRig.Camera,
                GraphicsSettingsManager.Instance.ActiveSettings,
                graphicsSettings);
        }

        // Exposes one stable texture; SagaCapture performs all source cuts.
        private UnityRuntimeCameraRecorder.VideoSequenceSettings CreateSequence()
        {
            return new UnityRuntimeCameraRecorder.VideoSequenceSettings
            {
                Sources = new[] { UnityRuntimeCameraRecorder
                    .VideoSequenceSource.FromTexture(_videoSource.Texture) }
            };
        }

        // Alternates the image written to the recorder's single texture.
        private void CutToNextSource()
        {
            if (_videoSource.IsCinematic)
            {
                ActivateGameplay("Gameplay shot activated.");
                return;
            }
            bool visible = _cameraRig?.TryCutViewpoint(true) == true;
            if (!visible)
            {
                SagaCapturePlugin.Log.LogInfo(
                    "Cinematic cut postponed: no visible target viewpoint.");
            }
            else
            {
                _videoSource.SetCinematic(true);
                _targetLostSince = -1f;
                SagaCapturePlugin.Log.LogInfo(
                    "Cinematic shot activated with a new viewpoint.");
            }
            ScheduleNextCut();
        }

        // Selects gameplay immediately and schedules the next mod-owned cut.
        private void ActivateGameplay(string logMessage)
        {
            _videoSource.SetCinematic(false);
            _targetLostSince = -1f;
            ScheduleNextCut();
            SagaCapturePlugin.Log.LogInfo(logMessage);
        }

        // Chooses the duration of the current shot independently of the recorder.
        private void ScheduleNextCut()
        {
            _nextShotAt = Time.time + UnityEngine.Random.Range(
                MinimumShotDurationSeconds, MaximumShotDurationSeconds);
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
            return Path.Combine(
                ResolveOutputDirectory(),
                $"SagaCapture_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.mp4");
        }

        // Returns the dedicated output directory and creates it when missing.
        internal static string ResolveOutputDirectory()
        {
            string directory = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.MyVideos), "SagaCapture");
            Directory.CreateDirectory(directory);
            return directory;
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
            Notify("Saga Capture saved the video to Videos/SagaCapture.");
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
            _targetLostSince = -1f;
        }

        // Releases the offscreen camera.
        private void ReleaseCamera()
        {
            if (_videoSource != null)
            {
                _videoSource.Dispose();
                Destroy(_videoSource);
                _videoSource = null;
            }
            if (_cameraRig != null)
            {
                _cameraRig.Dispose();
                Destroy(_cameraRig);
                _cameraRig = null;
            }
            if (_gameplayCapture != null)
            {
                _gameplayCapture.Dispose();
                Destroy(_gameplayCapture);
                _gameplayCapture = null;
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
