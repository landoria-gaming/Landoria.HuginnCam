using System;
using UnityEngine;

namespace Landoria.SagaCapture
{
    // Switches between the gameplay and secondary cameras without blocking input.
    internal sealed class SagaCaptureController : MonoBehaviour
    {
        private SagaCaptureRig _cameraRig;
        private Camera _gameplayCamera;
        private bool _gameplayCameraWasEnabled;
        private readonly SagaCaptureInterfaceController _interface =
            new SagaCaptureInterfaceController();

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
                StopPreview();
            }
            else
            {
                StartPreview();
            }
        }

        // Creates and displays the secondary camera.
        private void StartPreview()
        {
            try
            {
                _gameplayCamera = Camera.main;
                ValidateGameplayCamera();
                _gameplayCameraWasEnabled = _gameplayCamera.enabled;
                _cameraRig = gameObject.AddComponent<SagaCaptureRig>();
                _cameraRig.Initialize(_gameplayCamera);
                _gameplayCamera.enabled = false;
                _cameraRig.BeginPreview();
                _interface.Hide();
            }
            catch (Exception exception)
            {
                SagaCapturePlugin.Log.LogError(exception);
                Notify($"Saga Capture failed: {exception.Message}");
                StopPreview();
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
        private void StopPreview()
        {
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
            _interface.Restore();
        }

        // Restores the gameplay view before plugin unload.
        internal void Shutdown()
        {
            if (IsActive)
            {
                StopPreview();
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
