using System;
using System.Collections;
using UnityEngine;

namespace Landoria.SagaCapture
{
    // Switches between the gameplay and secondary cameras without blocking input.
    internal sealed class SagaCaptureController : MonoBehaviour
    {
        private const int MinimumWarmupFrames = 8;
        private const float MinimumWarmupSeconds = 0.5f;
        private SagaCaptureRig _cameraRig;
        private Camera _gameplayCamera;
        private bool _gameplayCameraWasEnabled;
        private RenderTexture _hiddenGameplayTarget;
        private RenderTexture _originalGameplayTarget;
        private Coroutine _previewWarmupRoutine;
        private readonly SagaCaptureReturnTransition _returnTransition =
            new SagaCaptureReturnTransition();
        private bool _returning;
        private readonly SagaCaptureInterfaceController _interface =
            new SagaCaptureInterfaceController();
        private readonly SagaCaptureFrameRateLimit _frameRateLimit =
            new SagaCaptureFrameRateLimit();

        internal bool IsActive => _cameraRig != null;

        // Keeps the interface hidden while camera mode is active.
        private void Update()
        {
            if (IsActive)
            {
                _interface.Hide();
            }
        }

        // Switches to or from the secondary camera.
        internal void ToggleCamera()
        {
            if (IsActive)
            {
                RequestStopPreview();
            }
            else
            {
                StartPreview();
            }
        }

        // Advances the smooth return after all normal camera updates.
        private void LateUpdate()
        {
            if (!_returning || _cameraRig?.Camera == null ||
                _gameplayCamera == null)
            {
                return;
            }

            if (_returnTransition.Step(
                    _cameraRig.Camera, _gameplayCamera.transform,
                    Time.deltaTime))
            {
                CompleteStopPreview();
            }
        }

        // Creates and displays the secondary camera.
        private void StartPreview()
        {
            try
            {
                _gameplayCamera = Camera.main;
                ValidateGameplayCamera();
                _frameRateLimit.Apply(
                    "PreviewMode", Preference.CameraMaximumFrameRate);
                _gameplayCameraWasEnabled = _gameplayCamera.enabled;
                GraphicsSettingsState settings =
                    SagaCaptureRendering.GetGraphicsSettings();
                SagaCaptureRendering.GetResolution(settings,
                    out int width, out int height, out FilterMode filterMode);
                _cameraRig = gameObject.AddComponent<SagaCaptureRig>();
                _cameraRig.Initialize(_gameplayCamera, false, settings);
                _cameraRig.BeginWarmup(width, height, 1, filterMode);
                SagaCaptureCameraLogger.LogEffectiveConfiguration(
                    "PreviewMode", _gameplayCamera, _cameraRig.Camera,
                    GraphicsSettingsManager.Instance.ActiveSettings, settings);
                _previewWarmupRoutine =
                    StartCoroutine(WarmupThenShowPreview());
            }
            catch (Exception exception)
            {
                SagaCapturePlugin.Log.LogError(exception);
                Notify($"Saga Capture failed: {exception.Message}");
                CompleteStopPreview();
            }
        }

        // Keeps gameplay visible until the preview camera has stabilized.
        private IEnumerator WarmupThenShowPreview()
        {
            float startedAt = Time.realtimeSinceStartup;
            int renderedFrames = 0;
            while (renderedFrames < MinimumWarmupFrames ||
                   Time.realtimeSinceStartup - startedAt < MinimumWarmupSeconds)
            {
                renderedFrames++;
                yield return new WaitForEndOfFrame();
            }

            _previewWarmupRoutine = null;
            try
            {
                _gameplayCamera.enabled = false;
                BeginHiddenGameplayRendering();
                _cameraRig.BeginPreview();
                _interface.Hide();
            }
            catch (Exception exception)
            {
                SagaCapturePlugin.Log.LogError(exception);
                Notify($"Saga Capture failed: {exception.Message}");
                CompleteStopPreview();
            }
        }

        // Keeps gameplay rendering offscreen to reproduce CaptureMode workload.
        private void BeginHiddenGameplayRendering()
        {
            _originalGameplayTarget = _gameplayCamera.targetTexture;
            _hiddenGameplayTarget = new RenderTexture(
                Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            _hiddenGameplayTarget.Create();
            _gameplayCamera.targetTexture = _hiddenGameplayTarget;
            _gameplayCamera.enabled = true;
        }

        // Restores the gameplay camera target and releases its hidden texture.
        private void EndHiddenGameplayRendering()
        {
            if (_gameplayCamera != null)
            {
                _gameplayCamera.enabled = false;
                _gameplayCamera.targetTexture = _originalGameplayTarget;
                _originalGameplayTarget = null;
            }
            if (_hiddenGameplayTarget != null)
            {
                _hiddenGameplayTarget.Release();
                Destroy(_hiddenGameplayTarget);
                _hiddenGameplayTarget = null;
            }
        }

        // Ensures the player camera exists before creating its clone.
        private void ValidateGameplayCamera()
        {
            if (Player.m_localPlayer == null || _gameplayCamera == null)
            {
                throw new InvalidOperationException(
                    "The local player camera is unavailable.");
            }
        }

        // Restores the original gameplay camera and listener.
        private void RequestStopPreview()
        {
            if (_returning)
            {
                return;
            }
            if (_previewWarmupRoutine != null || _cameraRig?.Camera == null)
            {
                CompleteStopPreview();
                return;
            }

            _cameraRig.PauseFlight();
            _returnTransition.Start(
                _cameraRig.Camera, Player.m_localPlayer);
            _returning = true;
        }

        // Restores gameplay and the interface after the return completes.
        private void CompleteStopPreview()
        {
            if (_previewWarmupRoutine != null)
            {
                StopCoroutine(_previewWarmupRoutine);
                _previewWarmupRoutine = null;
            }
            EndHiddenGameplayRendering();
            if (_cameraRig != null)
            {
                _cameraRig.Dispose();
                Destroy(_cameraRig);
                _cameraRig = null;
            }
            if (_gameplayCamera != null)
            {
                _gameplayCamera.enabled = _gameplayCameraWasEnabled;
                _gameplayCamera = null;
            }
            _returning = false;
            _frameRateLimit.Restore("PreviewMode");
            _interface.Restore();
        }

        // Restores the gameplay view before plugin unload.
        internal void Shutdown()
        {
            if (IsActive)
            {
                CompleteStopPreview();
            }
        }

        // Displays a local status message when a player is available.
        private static void Notify(string message)
        {
            Player.m_localPlayer?.Message(
                MessageHud.MessageType.TopLeft, message);
        }
    }
}
