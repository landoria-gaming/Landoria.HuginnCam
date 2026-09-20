using System;
using UnityEngine;

namespace Landoria.HuginnCam
{
    // Switches between the gameplay and Huginn cameras without blocking input.
    internal sealed class HuginnCamController : MonoBehaviour
    {
        private HuginnCamRig _cameraRig;
        private Camera _gameplayCamera;
        private bool _gameplayCameraWasEnabled;
        private readonly HuginnCamInterfaceController _interface =
            new HuginnCamInterfaceController();

        internal bool IsActive => _cameraRig != null;

        // Keeps the interface hidden while Huginn camera mode is active.
        private void Update()
        {
            if (IsActive)
            {
                _interface.Hide();
            }
        }

        // Switches to or from the Huginn camera.
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

        // Creates and displays the Huginn camera.
        private void StartPreview()
        {
            try
            {
                _gameplayCamera = Camera.main;
                if (Player.m_localPlayer == null || _gameplayCamera == null)
                {
                    throw new InvalidOperationException("The local player camera is unavailable.");
                }

                _gameplayCameraWasEnabled = _gameplayCamera.enabled;
                _cameraRig = gameObject.AddComponent<HuginnCamRig>();
                _cameraRig.Initialize(_gameplayCamera);
                _gameplayCamera.enabled = false;
                _cameraRig.BeginPreview();
                _cameraRig.GetComponentInChildren<HuginnCamAudio>()
                    .PlayActivationCall();
                HuginnCamFootstepPatch.Muted = true;
                _interface.Hide();
                Notify("Huginn Cam enabled.");
            }
            catch (Exception exception)
            {
                HuginnCamPlugin.Log.LogError(exception);
                Notify($"Huginn Cam failed: {exception.Message}");
                StopPreview();
            }
        }

        // Restores the original gameplay camera and audio listener.
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
            HuginnCamFootstepPatch.Muted = false;
            Notify("Huginn Cam disabled.");
        }

        // Restores the gameplay view before the plugin is unloaded.
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
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, message);
        }
    }
}
