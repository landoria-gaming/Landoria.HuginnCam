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
        private bool _previewFlight;
        private bool _debugSnapshots;
        private bool _synchronizeSourcePose = true;
        private float _nextViewpointCut;
        private const float NearViewpointRadius = 3f;
        private const float DistantViewpointRadius = 8f;
        private const float MinimumCutInterval = 8f;
        private const float MaximumCutInterval = 15f;
        private const int ViewpointCandidateCount = 12;

        internal Camera Camera => _camera;
        internal bool IsFlying => _flightEnabled;

        // Clones the gameplay camera and optionally transfers audio listening.
        internal void Initialize(Camera sourceCamera, bool transferAudio = true,
            GraphicsSettingsState? captureSettings = null)
        {
            _sourceCamera = sourceCamera;
            _debugSnapshots = Preference.CreateTelemetry(transferAudio).Enabled;
            _mainCamera.Initialize(sourceCamera);
            GameObject cameraObject = new GameObject("SagaCaptureCamera");
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.CopyFrom(sourceCamera);
            _camera.fieldOfView = Preference.GetDroneCameraFov(sourceCamera);
            _camera.depth = sourceCamera.depth + 1f;
            _camera.enabled = false;
            _effects.Initialize(sourceCamera, cameraObject, captureSettings);
            if (captureSettings.HasValue)
            {
                cameraObject.AddComponent<SagaCaptureQualityOverride>()
                    .Initialize(captureSettings.Value);
            }
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
            _camera.gameObject.AddComponent<SagaCapturePreviewPresenter>()
                .Initialize(_offscreenTarget);
            _camera.enabled = true;
            BeginFlight(true);
        }

        // Allows autonomous movement after camera preparation is complete.
        internal void BeginFlight(bool preview = false)
        {
            if (preview)
            {
                _mainCamera.Take(_camera);
            }
            _camera.gameObject.AddComponent<SagaCaptureFrameRateDisplay>();
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
            _previewFlight = preview;
            _pilot = new DronePilotController(
                _camera, player.gameObject, Preference.DroneConfigPath,
                adapter.CreateWorld(), adapter.CreateProfiles(),
                Vector3.up * 1.60f, Preference.CreateTelemetry(preview),
                droneVisual: new DroneVisualOptions
                {
                    ViewerCamera = _sourceCamera,
                    Visible = !preview && Preference.ShowDroneVisual,
                    Color = Preference.ShellColor,
                    LogInfo = message => SagaCapturePlugin.Log.LogInfo(message),
                    LogWarning = message => SagaCapturePlugin.Log.LogWarning(message),
                    LogError = message => SagaCapturePlugin.Log.LogError(message)
                });
            _flightEnabled = true;
            ScheduleViewpointCut();
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
            int antiAliasingSamples, FilterMode filterMode)
        {
            int width = Mathf.Max(2, requestedWidth & ~1);
            int height = Mathf.Max(2, requestedHeight & ~1);
            _offscreenTarget = new RenderTexture(
                width, height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            _offscreenTarget.antiAliasing = antiAliasingSamples;
            _offscreenTarget.filterMode = filterMode;
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
            _camera.fieldOfView = Preference.GetDroneCameraFov(_sourceCamera);
            if (!_flightEnabled)
            {
                if (_synchronizeSourcePose)
                {
                    SynchronizePose();
                }
                return;
            }

            if ((_previewFlight ||
                 Preference.Content == OutputContent.DroneOnly) &&
                Time.time >= _nextViewpointCut)
            {
                TryCutViewpoint(false);
            }
            _pilot?.Update(player.GetVelocity(), player.m_runSpeed);
            _pilot?.SetVisual(!_previewFlight && Preference.ShowDroneVisual,
                Preference.ShellColor);
        }

        // Returns whether the current camera has an unobstructed player view.
        internal bool HasTargetVisibility()
        {
            Player player = Player.m_localPlayer;
            if (!_flightEnabled || player == null)
            {
                return false;
            }
            Vector3 focus = player.transform.position + Vector3.up * 1.60f;
            Vector3 viewport = _camera.WorldToViewportPoint(focus);
            return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f &&
                   viewport.y >= 0f && viewport.y <= 1f &&
                   IsTargetVisible(_camera.transform.position, player);
        }

        // Cuts to a visible random viewpoint, optionally rendering it now.
        internal bool TryCutViewpoint(bool renderImmediately)
        {
            Player player = Player.m_localPlayer;
            if (!_flightEnabled || player == null)
            {
                return false;
            }
            Vector3 focus = player.transform.position + Vector3.up * 1.60f;
            Vector3 radial = _camera.transform.position - focus;
            radial.y = 0f;
            if (radial.sqrMagnitude < 0.01f)
            {
                radial = -player.transform.forward;
            }
            for (int index = 0; index < ViewpointCandidateCount; index++)
            {
                Vector3 position = CreateViewpoint(
                    focus, radial.normalized, player);
                if (!IsTargetVisible(position, player))
                {
                    continue;
                }
                _pilot?.Reposition(position);
                if (renderImmediately)
                {
                    _camera.Render();
                }
                ScheduleViewpointCut();
                return true;
            }
            ScheduleViewpointCut();
            return false;
        }

        // Generates one substantially different position around the player.
        private static Vector3 CreateViewpoint(
            Vector3 focus, Vector3 radial, Player player)
        {
            float angle = Random.Range(110f, 250f);
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * radial;
            float radius = Random.Range(
                NearViewpointRadius, DistantViewpointRadius);
            Vector3 position = focus + direction * radius;
            float ground = ValheimDroneAdapter.GroundHeight(position) ??
                           player.transform.position.y;
            float amount = Mathf.InverseLerp(
                NearViewpointRadius, DistantViewpointRadius, radius);
            position.y = Mathf.Max(focus.y, ground + Mathf.Lerp(2f, 4f, amount));
            return position;
        }

        // Tests the frame and physical line of sight from one camera position.
        private bool IsTargetVisible(Vector3 position, Player player)
        {
            Vector3 focus = player.transform.position + Vector3.up * 1.60f;
            Vector3 direction = focus - position;
            RaycastHit[] hits = Physics.RaycastAll(position,
                direction.normalized, direction.magnitude,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,
                (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                return hit.collider.GetComponentInParent<Player>() == player;
            }
            return true;
        }

        // Chooses the next unsynchronized viewpoint-cut time.
        private void ScheduleViewpointCut()
        {
            _nextViewpointCut = Time.time +
                Random.Range(MinimumCutInterval, MaximumCutInterval);
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
