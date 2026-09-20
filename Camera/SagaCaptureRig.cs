using UnityEngine;

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
        private readonly SagaCapturePositionLogger _positionLogger =
            new SagaCapturePositionLogger();
        private readonly DroneEnvironment _environment = new DroneEnvironment();
        private readonly DroneFlightController _flight =
            new DroneFlightController();
        private readonly DroneTrajectoryPlanner _trajectory =
            new DroneTrajectoryPlanner();
        private readonly DroneMotion _motion = new DroneMotion();
        private readonly DroneLook _look = new DroneLook();
        private readonly SagaCaptureEffects _effects = new SagaCaptureEffects();
        private bool _flightInitialized;

        internal Camera Camera => _camera;

        // Clones the gameplay camera and optionally transfers audio listening.
        internal void Initialize(Camera sourceCamera, bool transferAudio = true)
        {
            _sourceCamera = sourceCamera;
            _mainCamera.Initialize(sourceCamera);
            GameObject cameraObject = new GameObject("SagaCaptureCamera");
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.CopyFrom(sourceCamera);
            _camera.depth = sourceCamera.depth + 1f;
            _camera.enabled = false;
            _effects.Initialize(sourceCamera, cameraObject);
            SynchronizePose();
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

            if (!_flightInitialized)
            {
                InitializeFlight(player);
            }

            UpdateFlight(player);
            _positionLogger.Update(player, _camera.transform.position);
        }

        // Starts continuous motion from the cloned gameplay camera pose.
        private void InitializeFlight(Player player)
        {
            _motion.Initialize(player.GetVelocity());
            _look.Initialize(_camera.transform, GetPlayerFocus(player));
            _flightInitialized = true;
        }

        // Advances environment, planning, movement, and framing policies.
        private void UpdateFlight(Player player)
        {
            Vector3 position = _camera.transform.position;
            _environment.Update(position);
            Vector3 desired = _flight.Update(
                player, position, _environment, out float targetSpeed);
            desired = _trajectory.Plan(position, desired, _motion.Speed);
            Vector3 next = _motion.Step(position, desired, targetSpeed);
            _camera.transform.position = KeepAboveTerrain(next);
            _look.Update(_camera.transform, GetPlayerFocus(player));
        }

        // Returns a stable point near the player's upper body.
        private static Vector3 GetPlayerFocus(Player player)
        {
            return player.transform.position + Vector3.up * 1.25f;
        }

        // Enforces the absolute two-meter terrain clearance safety rule.
        private static Vector3 KeepAboveTerrain(Vector3 position)
        {
            if (ZoneSystem.instance != null &&
                ZoneSystem.instance.GetGroundHeight(position, out float ground))
            {
                position.y = Mathf.Max(position.y, ground + 2f);
            }

            return position;
        }

        // Restores listeners and destroys the secondary camera.
        internal void Dispose()
        {
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
