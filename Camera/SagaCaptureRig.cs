using UnityEngine;
using DronePilot;

namespace Landoria.SagaCapture
{
    // Owns the secondary camera used for preview and recording.
    internal sealed class SagaCaptureRig : MonoBehaviour
    {
        private Camera _camera;
        private Camera _sourceCamera;
        private AudioListener _listener;
        private AudioListener _originalListener;
        private RenderTexture _offscreenTarget;
        private readonly SagaCaptureMainCamera _mainCamera =
            new SagaCaptureMainCamera();
        private DronePilotController _pilot;
        private readonly SagaCaptureEffects _effects = new SagaCaptureEffects();
        private bool _flightEnabled;
        private bool _debugSnapshots;
        private bool _synchronizeSourcePose = true;

        internal Camera Camera => _camera;

        // Clones the gameplay camera and optionally transfers audio listening.
        internal void Initialize(Camera sourceCamera, bool transferAudio = true)
        {
            _sourceCamera = sourceCamera;
            _debugSnapshots = Preference.CreateTelemetry(transferAudio).Enabled;
            _mainCamera.Initialize(sourceCamera);
            GameObject cameraObject = new GameObject("SagaCaptureCamera");
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.CopyFrom(sourceCamera);
            _camera.fieldOfView = Preference.SagaCameraFOV;
            _camera.depth = sourceCamera.depth + 1f;
            _camera.enabled = false;
            _effects.Initialize(sourceCamera, cameraObject);
            SynchronizePose();
            SagaCaptureCameraLogger.LogSnapshot(
                "created", _sourceCamera, _camera, _debugSnapshots);
            if (transferAudio)
            {
                TransferAudio(sourceCamera, cameraObject);
            }
        }

        // Displays the secondary camera directly on the player's screen.
        internal void BeginPreview()
        {
            EndOffscreenRendering();
            _mainCamera.Take(_camera);
            _camera.targetTexture = null;
            _camera.enabled = true;
            BeginFlight(true);
        }

        // Allows autonomous movement after camera preparation is complete.
        internal void BeginFlight(bool preview = false)
        {
            SynchronizePose();
            SagaCaptureCameraLogger.LogSnapshot(
                "flight start", _sourceCamera, _camera, _debugSnapshots);
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                throw new System.InvalidOperationException(
                    "The local player is unavailable.");
            }
            var adapter = new ValheimDroneAdapter();
            _pilot = new DronePilotController(
                _camera, player.gameObject, Preference.DroneConfigPath,
                adapter.CreateWorld(), adapter.CreateProfiles(),
                Vector3.up * 1.25f, Preference.CreateTelemetry(preview));
            _flightEnabled = true;
        }

        // Stops autonomous flight without snapping back to the source pose.
        internal void PauseFlight()
        {
            _flightEnabled = false;
            _synchronizeSourcePose = false;
            _pilot?.Dispose();
            _pilot = null;
        }

        // Starts invisible rendering for the recording pipeline.
        internal void BeginWarmup(
            int requestedWidth, int requestedHeight,
            int antiAliasingSamples)
        {
            int width = Mathf.Max(2, requestedWidth & ~1);
            int height = Mathf.Max(2, requestedHeight & ~1);
            _offscreenTarget = new RenderTexture(
                width, height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            _offscreenTarget.antiAliasing = antiAliasingSamples;
            _offscreenTarget.Create();
            _camera.targetTexture = _offscreenTarget;
            _camera.enabled = true;
        }

        // Updates autonomous drone flight after player movement completes.
        private void LateUpdate()
        {
            _effects.Synchronize();
            Player player = Player.m_localPlayer;
            if (player == null || _camera == null)
            {
                return;
            }
            _camera.fieldOfView = Preference.SagaCameraFOV;
            if (!_flightEnabled)
            {
                if (_synchronizeSourcePose)
                {
                    SynchronizePose();
                }
                return;
            }

            _pilot?.Update(player.GetVelocity(), player.m_runSpeed);
        }

        // Restores listeners and destroys the secondary camera.
        internal void Dispose()
        {
            _pilot?.Dispose();
            _pilot = null;
            EndOffscreenRendering();
            _mainCamera.Restore(_camera);
            if (_originalListener != null)
            {
                _originalListener.enabled = true;
                _originalListener = null;
            }
            if (_camera != null)
            {
                Destroy(_camera.gameObject);
                _camera = null;
                _listener = null;
                _effects.Clear();
            }
        }

        // Copies the gameplay camera pose without introducing flight behavior.
        private void SynchronizePose()
        {
            if (_sourceCamera == null || _camera == null)
            {
                return;
            }
            _camera.transform.SetPositionAndRotation(
                _sourceCamera.transform.position,
                _sourceCamera.transform.rotation);
        }

        // Moves active listening to the preview camera.
        private void TransferAudio(Camera sourceCamera, GameObject target)
        {
            _originalListener = SagaCaptureAudioListener.FindActive(sourceCamera);
            if (_originalListener != null)
            {
                _originalListener.enabled = false;
            }
            _listener = target.AddComponent<AudioListener>();
        }

        // Stops offscreen rendering and releases its texture.
        private void EndOffscreenRendering()
        {
            if (_camera != null)
            {
                _camera.enabled = false;
                _camera.targetTexture = null;
            }
            if (_offscreenTarget != null)
            {
                _offscreenTarget.Release();
                Destroy(_offscreenTarget);
                _offscreenTarget = null;
            }
        }
    }
}
