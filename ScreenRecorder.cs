using System;
using System.Collections;
using System.IO;
using UnityMediaRecorder;
using UnityEngine;
using MediaRecorder = UnityMediaRecorder.UnityMediaRecorder;

namespace Landoria.HuginnCam
{
    // Connects HuginnCam input, camera behavior and status UI to the reusable recorder.
    internal sealed class ScreenRecorder : MonoBehaviour
    {
        private const int FrameRate = 60;
        private const float CameraWarmupSeconds = 3f;
        private readonly RecordingStatusDisplay _statusDisplay = new RecordingStatusDisplay();
        private CinematicCameraRig _cameraRig;
        private MediaRecorder _recorder;
        private string _archivePath;
        private string _gameplayPreviewPath;
        private string _outputPath;
        private string _previewPath;
        private string _temporaryContainerPath;
        private Coroutine _warmupRoutine;

        // Handles the recording hotkey without knowing the media pipeline internals.
        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.F8))
            {
                return;
            }

            if (_warmupRoutine != null)
            {
                CancelWarmup();
            }
            else if (_recorder?.IsFinalizing == true)
            {
                Notify("HuginnCam is still finalizing the previous recording");
            }
            else if (_recorder?.IsCapturing == true)
            {
                StopRecording();
            }
            else
            {
                StartRecording();
            }
        }

        // Stops active work and releases camera and display resources on destruction.
        private void OnDestroy()
        {
            if (_recorder != null)
            {
                UnsubscribeRecorder();
                _recorder.StopRecording();
                Destroy(_recorder);
                _recorder = null;
            }

            ReleaseCameraRig();
            _statusDisplay.Dispose();
        }

        // Creates the cinematic camera and starts the reusable Unity recorder.
        private void StartRecording()
        {
            try
            {
                Camera gameplayCamera = Camera.main;
                if (gameplayCamera == null)
                {
                    throw new InvalidOperationException("Valheim's gameplay camera was not found.");
                }

                CreateOutputPaths();
                _cameraRig = gameObject.AddComponent<CinematicCameraRig>();
                _cameraRig.Initialize(gameplayCamera);
                int width = Math.Max(2, Screen.width & ~1);
                int height = Math.Max(2, Screen.height & ~1);
                _cameraRig.BeginWarmup(width, height, HuginnCamPreference.AntiAliasingSamples);
                _statusDisplay.Show("Preparing camera...");
                _warmupRoutine = StartCoroutine(StartRecorderAfterWarmup());
            }
            catch (Exception exception)
            {
                HandleRecordingFailed(exception);
            }
        }

        // Waits for independent auto-exposure to settle before opening the media streams.
        private IEnumerator StartRecorderAfterWarmup()
        {
            float deadline = Time.realtimeSinceStartup + CameraWarmupSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            _warmupRoutine = null;
            try
            {
                _recorder = gameObject.AddComponent<MediaRecorder>();
                SubscribeRecorder();
                _recorder.StartRecording(
                    _cameraRig.Camera,
                    _cameraRig.Listener,
                    CreateSettings(),
                    _cameraRig.PreparedTarget);
            }
            catch (Exception exception)
            {
                HandleRecordingFailed(exception);
            }
        }

        // Cancels camera preparation before any output process has been started.
        private void CancelWarmup()
        {
            StopCoroutine(_warmupRoutine);
            _warmupRoutine = null;
            _statusDisplay.Hide();
            ReleaseCameraRig();
            Notify("HuginnCam recording cancelled");
        }

        // Stops capture while allowing the recorder to create the final files.
        private void StopRecording()
        {
            _recorder?.StopRecording();
            ReleaseCameraRig();
        }

        // Builds generic recording settings from HuginnCam configuration and paths.
        private RecordingSettings CreateSettings()
        {
            return new RecordingSettings
            {
                FfmpegPath = HuginnCamPreference.FfmpegPath,
                TemporaryContainerPath = _temporaryContainerPath,
                ArchivePath = _archivePath,
                KeepIntermediateFile = HuginnCamPreference.KeepIntermediateFile,
                GeneratePreviewImage = HuginnCamPreference.GeneratePreviewImage,
                PreviewImagePath = _previewPath,
                OutputPath = _outputPath,
                Width = Math.Max(2, Screen.width & ~1),
                Height = Math.Max(2, Screen.height & ~1),
                MaximumFrameRate = FrameRate,
                AntiAliasingSamples = HuginnCamPreference.AntiAliasingSamples,
                FlipVertically = true
            };
        }

        // Creates timestamped HuginnCam output paths on the Desktop.
        private void CreateOutputPaths()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string baseName = $"HuginnCam_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}";
            _temporaryContainerPath = Path.Combine(desktop, $"{baseName}.mkv.tmp");
            _archivePath = Path.Combine(desktop, $"{baseName}.mkv");
            _gameplayPreviewPath = Path.Combine(desktop, $"{baseName}_Gameplay.png");
            _outputPath = Path.Combine(desktop, $"{baseName}.mp4");
            _previewPath = Path.Combine(desktop, $"{baseName}.png");
        }

        // Connects recorder lifecycle events to HuginnCam presentation behavior.
        private void SubscribeRecorder()
        {
            _recorder.CaptureStarting += HandleCaptureStarting;
            _recorder.CaptureStarted += HandleCaptureStarted;
            _recorder.FinalizationStarted += HandleFinalizationStarted;
            _recorder.RecordingCompleted += HandleRecordingCompleted;
            _recorder.RecordingFailed += HandleRecordingFailed;
        }

        // Disconnects recorder lifecycle events before destroying the component.
        private void UnsubscribeRecorder()
        {
            _recorder.CaptureStarting -= HandleCaptureStarting;
            _recorder.CaptureStarted -= HandleCaptureStarted;
            _recorder.FinalizationStarted -= HandleFinalizationStarted;
            _recorder.RecordingCompleted -= HandleRecordingCompleted;
            _recorder.RecordingFailed -= HandleRecordingFailed;
        }

        // Saves the cinematic frame and player screen immediately before video capture starts.
        private void HandleCaptureStarting()
        {
            if (!HuginnCamPreference.GeneratePreviewImage)
            {
                return;
            }

            Texture2D gameplayImage = null;
            try
            {
                gameplayImage = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(_gameplayPreviewPath, gameplayImage.EncodeToPNG());
                HuginnCamPlugin.Log.LogInfo($"Gameplay preview saved: {_gameplayPreviewPath}");
            }
            finally
            {
                if (gameplayImage != null)
                {
                    Destroy(gameplayImage);
                }
            }
        }

        // Displays the active recording state after the media pipes connect.
        private void HandleCaptureStarted()
        {
            _statusDisplay.Show("Recording...");
            Notify($"HuginnCam recording started at {Screen.width}x{Screen.height}, {FrameRate} FPS");
            HuginnCamPlugin.Log.LogInfo($"Recording started: {_outputPath}");
        }

        // Displays the background MP4 finalization state after capture stops.
        private void HandleFinalizationStarted()
        {
            _statusDisplay.Show("Finalizing...");
            Notify("HuginnCam is finalizing the recording");
        }

        // Reports successful output creation and releases the recorder façade.
        private void HandleRecordingCompleted()
        {
            _statusDisplay.Hide();
            HuginnCamPlugin.Log.LogInfo($"Recording saved: {_outputPath}");
            if (HuginnCamPreference.KeepIntermediateFile)
            {
                HuginnCamPlugin.Log.LogInfo($"Archive recording saved: {_archivePath}");
            }
            Notify(HuginnCamPreference.KeepIntermediateFile
                ? "HuginnCam saved the MP4 and archive recording to Desktop"
                : "HuginnCam saved the MP4 to Desktop");
            ReleaseRecorder();
        }

        // Reports a recorder failure while preserving any recoverable temporary file.
        private void HandleRecordingFailed(Exception exception)
        {
            _statusDisplay.Hide();
            HuginnCamPlugin.Log.LogError(exception);
            Notify($"HuginnCam recording failed: {exception.Message}");
            ReleaseCameraRig();
            ReleaseRecorder();
        }

        // Restores the gameplay audio listener and destroys the cinematic camera.
        private void ReleaseCameraRig()
        {
            if (_warmupRoutine != null)
            {
                StopCoroutine(_warmupRoutine);
                _warmupRoutine = null;
            }

            if (_cameraRig == null)
            {
                return;
            }

            _cameraRig.Dispose();
            Destroy(_cameraRig);
            _cameraRig = null;
        }

        // Disconnects and destroys the completed recorder component.
        private void ReleaseRecorder()
        {
            if (_recorder == null)
            {
                return;
            }

            UnsubscribeRecorder();
            Destroy(_recorder);
            _recorder = null;
        }

        // Displays a short HuginnCam notification through Valheim's message HUD.
        private static void Notify(string message)
        {
            MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, message);
        }
    }
}
