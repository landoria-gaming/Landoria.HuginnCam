using UnityEngine;
using CameraOperator;

namespace Landoria.SagaCapture
{
    // Owns the secondary camera used for recording.
    internal sealed class SagaCaptureRig : MonoBehaviour
    {
        private Camera _camera;
        private Camera _sourceCamera;
        private RenderTexture _offscreenTarget;
        private CameraOperatorController _cameraOperator;
        private readonly SagaCaptureEffects _effects = new SagaCaptureEffects();
        private bool _movementEnabled;
        private const int ViewpointCandidateCount = 4;

        internal Camera Camera => _camera;
        internal RenderTexture OutputTexture => _offscreenTarget;
        internal bool IsMoving => _movementEnabled;

        // Clones the gameplay camera for the recording pipeline.
        internal void Initialize(Camera sourceCamera,
            GraphicsSettingsState? captureSettings = null)
        {
            _sourceCamera = sourceCamera;
            GameObject cameraObject = new GameObject("SagaCaptureCamera");
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.CopyFrom(sourceCamera);
            _camera.fieldOfView = sourceCamera.fieldOfView;
            _camera.depth = sourceCamera.depth + 1f;
            _camera.enabled = false;
            _effects.Initialize(sourceCamera, cameraObject, captureSettings);
            if (captureSettings.HasValue)
            {
                cameraObject.AddComponent<SagaCaptureQualityOverride>()
                    .Initialize(captureSettings.Value);
            }
            SynchronizePose();
        }

        // Allows autonomous movement after camera preparation is complete.
        internal void BeginMovement()
        {
            SynchronizePose();
            _camera.gameObject.AddComponent<SagaCaptureFrameRateDisplay>();
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                throw new System.InvalidOperationException(
                    "The local player is unavailable.");
            }
            var adapter = new ValheimCameraAdapter();
            _cameraOperator = new CameraOperatorController(
                _camera, player.gameObject, Preference.CameraOperatorConfigPath,
                adapter.CreateWorld(), adapter.CreateProfiles());
            _movementEnabled = true;
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

        // Updates autonomous camera movement after player movement completes.
        private void LateUpdate()
        {
            _effects.Synchronize();
            Player player = Player.m_localPlayer;
            if (player == null || _camera == null)
            {
                return;
            }
            _camera.fieldOfView = _sourceCamera.fieldOfView;
            if (!_movementEnabled)
            {
                SynchronizePose();
                return;
            }

            _cameraOperator?.Update(player.GetVelocity(), player.m_runSpeed);
        }

        // Returns whether the current camera has an unobstructed player view.
        internal bool HasTargetVisibility()
        {
            Player player = Player.m_localPlayer;
            if (!_movementEnabled || player == null)
            {
                return false;
            }
            Vector3 focus = PlayerFocus(player);
            Vector3 viewport = _camera.WorldToViewportPoint(focus);
            return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f &&
                   viewport.y >= 0f && viewport.y <= 1f &&
                   IsTargetVisible(_camera.transform.position, player);
        }

        // Cuts to a visible random viewpoint, optionally rendering it now.
        internal bool TryCutViewpoint(bool renderImmediately)
        {
            Player player = Player.m_localPlayer;
            if (!_movementEnabled || player == null)
            {
                return false;
            }
            Vector3 focus = PlayerFocus(player);
            for (int index = 0; index < ViewpointCandidateCount; index++)
            {
                Vector3 position = CreateViewpoint(
                    focus, player);
                if (!IsTargetVisible(position, player))
                {
                    continue;
                }
                _cameraOperator?.Reposition(position, player.GetVelocity());
                if (renderImmediately)
                {
                    _camera.Render();
                }
                return true;
            }
            return false;
        }

        // Generates one substantially different position around the player.
        private Vector3 CreateViewpoint(
            Vector3 focus, Player player)
        {
            string zone = SelectHorizontalZone();
            Vector3 direction = CreateViewDirection(player, zone);
            float radius = Random.Range(
                PlacementSetting("minimum_distance"),
                PlacementSetting("maximum_distance"));
            Vector3 position = focus + direction * radius;
            float ground = ValheimCameraAdapter.GroundHeight(position) ??
                           player.transform.position.y;
            float amount = Mathf.InverseLerp(
                PlacementSetting("minimum_distance"),
                PlacementSetting("maximum_distance"), radius);
            float maximumHeight = Mathf.Min(
                PlacementSetting("maximum_height"),
                _cameraOperator.MaximumHeight);
            position.y = Mathf.Max(focus.y, ground + Mathf.Lerp(
                PlacementSetting("minimum_height"),
                maximumHeight, amount));
            return position;
        }

        // Reads one placement limit from CameraOperator's live configuration.
        private float PlacementSetting(string path)
        {
            return _cameraOperator.Settings.Number("positioning." + path);
        }

        // Returns the operator-configured point of interest on the player.
        private Vector3 PlayerFocus(Player player)
        {
            return player.transform.position +
                Vector3.up * _cameraOperator.TargetHeight;
        }

        // Selects a front or lateral position around the player.
        private static string SelectHorizontalZone()
        {
            return Random.value < 0.5f ? "front" : "lateral";
        }

        // Chooses a direction inside one player-relative cut zone.
        private Vector3 CreateViewDirection(Player player, string zone)
        {
            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }
            float angle = CreateViewAngle(zone);
            return Quaternion.Euler(0f, angle, 0f) * forward.normalized;
        }

        // Generates an angle for front or lateral framing.
        private float CreateViewAngle(string zone)
        {
            float variation = PlacementSetting("angle_variation");
            if (zone == "front")
            {
                return Random.Range(-variation, variation);
            }
            float side = Random.value < 0.5f ? -1f : 1f;
            return side * (90f + Random.Range(-variation, variation));
        }

        // Tests the frame and physical line of sight from one camera position.
        private bool IsTargetVisible(Vector3 position, Player player)
        {
            Vector3 focus = PlayerFocus(player);
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

        // Releases rendering resources and destroys the secondary camera.
        internal void Dispose()
        {
            _cameraOperator?.Dispose();
            _cameraOperator = null;
            EndOffscreenRendering();
            if (_camera != null)
            {
                Destroy(_camera.gameObject);
                _camera = null;
                _effects.Clear();
            }
        }

        // Copies the gameplay camera pose without introducing movement behavior.
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
