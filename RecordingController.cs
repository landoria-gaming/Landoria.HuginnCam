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
        private SagaCaptureRig _cameraRig;
        private Recorder _recorder;
        private Coroutine _warmupRoutine;
        private AudioListener _gameplayListener;
        private string _outputPath;

        internal bool IsActive => _recorder != null || _warmupRoutine != null;
        internal bool IsCameraActive => _cameraRig?.IsFlying == true;

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
                GetCaptureGraphicsSettings();
            GetRenderResolution(graphicsSettings,
                out int renderWidth, out int renderHeight,
                out FilterMode filterMode);
            _cameraRig = gameObject.AddComponent<SagaCaptureRig>();
            _cameraRig.Initialize(gameplayCamera, false, graphicsSettings);
            _cameraRig.BeginWarmup(
                renderWidth, renderHeight, AntiAliasingSamples, filterMode);
        }

        // Creates a sequence containing the secondary camera.
        private UnityRuntimeCameraRecorder.VideoSequenceSettings CreateSequence()
        {
            return new UnityRuntimeCameraRecorder.VideoSequenceSettings
            {
                Sources = new[]
                {
                    UnityRuntimeCameraRecorder.VideoSequenceSource.FromCamera(
                        _cameraRig.Camera)
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

        // Resolves the active Valheim 3D rendering resolution and filter.
        private static void GetRenderResolution(GraphicsSettingsState settings,
            out int width, out int height, out FilterMode filterMode)
        {
            GetConfiguredRenderResolution(settings.m_target3DResolutionVertical,
                out width, out height);
            filterMode = settings.m_upscalingAlgorithm ==
                         UpscalingAlgorithm.NearestNeighbor
                ? FilterMode.Point
                : FilterMode.Bilinear;
        }

        // Resolves the configured camera-rendering height.
        private static void GetConfiguredRenderResolution(int presetHeight,
            out int width, out int height)
        {
            switch (Preference.CameraRenderResolution)
            {
                case CameraRenderResolutionPreset.SameAsGame:
                    width = Screen.width;
                    height = Screen.height;
                    return;
                case CameraRenderResolutionPreset.HD720:
                    width = 1280;
                    height = 720;
                    return;
                case CameraRenderResolutionPreset.FullHD1080:
                    width = 1920;
                    height = 1080;
                    return;
                case CameraRenderResolutionPreset.QHD1440:
                    width = 2560;
                    height = 1440;
                    return;
                case CameraRenderResolutionPreset.UHD2160:
                    width = 3840;
                    height = 2160;
                    return;
            }
            if (presetHeight <= 0)
            {
                presetHeight = Screen.dpi <= 96f
                    ? Screen.height
                    : Mathf.RoundToInt(Screen.height * 96f / Screen.dpi);
            }
            else if (presetHeight == int.MaxValue)
            {
                presetHeight = Screen.height;
            }
            height = Math.Max(2, presetHeight & ~1);
            width = Math.Max(2, (height * Screen.width / Screen.height) & ~1);
        }

        // Builds the graphics state selected for the capture camera.
        private static GraphicsSettingsState GetCaptureGraphicsSettings()
        {
            GraphicsSettingsManager manager = GraphicsSettingsManager.Instance;
            GraphicsSettingsState state = manager.ActiveSettings;
            if (Preference.RecordingGraphicsPreset !=
                CaptureGraphicsPreset.SameAsGame)
            {
                ApplyCapturePreset(manager, ref state);
            }
            Preference.ApplyCameraEffectOverrides(ref state);
            return state;
        }

        // Applies the selected Valheim preset to a capture graphics state.
        private static void ApplyCapturePreset(GraphicsSettingsManager manager,
            ref GraphicsSettingsState state)
        {
            GraphicsModeConfiguration config =
                manager.GetCurrentGraphicsModeConfiguration();
            int presetId = Preference.RecordingGraphicsPreset switch
            {
                CaptureGraphicsPreset.VeryLow => 4,
                CaptureGraphicsPreset.Low => 0,
                CaptureGraphicsPreset.Medium => 1,
                _ => 2
            };
            GraphicsSettingsPreset preset = config.GetPresetByID(presetId);
            manager.SetGraphicsSettingsFromPreset(
                config, ref state, preset, false);
        }

        // Resolves the FFmpeg directory used by the recorder.
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
