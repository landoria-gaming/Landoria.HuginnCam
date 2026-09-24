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
        private ValheimCameraAdapter _adapter;
        private readonly SagaCaptureEffects _effects = new SagaCaptureEffects();
        private bool _movementEnabled;

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
            _adapter = new ValheimCameraAdapter();
            _cameraOperator = new CameraOperatorController(
                _camera, player.gameObject, Preference.CameraOperatorConfigPath,
                _adapter.CreateWorld());
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

        // Tries one director-selected viewpoint and optionally renders it now.
        internal bool TryCutViewpoint(
            CameraPlacement placement, float minimumViewAngle,
            bool renderImmediately)
        {
            Player player = Player.m_localPlayer;
            if (!_movementEnabled || player == null)
            {
                return false;
            }
            Vector3 position = CreateViewpoint(PlayerFocus(player), player,
                placement);
            if (!HasDistinctDirection(position, player, minimumViewAngle) ||
                !IsTargetVisible(position, player))
            {
                return false;
            }
            _cameraOperator?.Reposition(position, player.GetVelocity());
            if (renderImmediately)
            {
                _camera.Render();
            }
            return true;
        }

        // Rejects a view whose real horizontal angle resembles the current one.
        private bool HasDistinctDirection(
            Vector3 position, Player player, float minimumViewAngle)
        {
            Vector3 current = _cameraOperator.DirectionFromTarget;
            Vector3 candidate = position - player.transform.position;
            candidate.y = 0f;
            return current.sqrMagnitude == 0f ||
                   candidate.sqrMagnitude == 0f ||
                   Vector3.Angle(current, candidate) >= minimumViewAngle;
        }

        // Reports whether the player currently occupies a forested area.
        internal bool IsTargetInForest()
        {
            Player player = Player.m_localPlayer;
            return player != null && _adapter?.IsForest(
                player.transform.position) == true;
        }

        // Generates one substantially different position around the player.
        private Vector3 CreateViewpoint(
            Vector3 focus, Player player, CameraPlacement placement)
        {
            Vector3 direction = CreateViewDirection(player, placement);
            float radius = Random.Range(
                PlacementSetting("minimum_distance"),
                PlacementSetting("maximum_distance"));
            Vector3 position = focus + direction * radius;
            position.y = focus.y + (IsTop(placement)
                ? PlacementSetting("top_height") : 0f);
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

        // Chooses a direction around the player's current orientation.
        private Vector3 CreateViewDirection(
            Player player, CameraPlacement placement)
        {
            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }
            float angle = CreateViewAngle(placement);
            return Quaternion.Euler(0f, angle, 0f) * forward.normalized;
        }

        // Generates a varied front, left, or right framing angle.
        private float CreateViewAngle(CameraPlacement placement)
        {
            float variation = PlacementSetting("angle_variation");
            float baseAngle = PlacementAngle(placement);
            return baseAngle + Random.Range(-variation, variation);
        }

        // Maps a named camera placement to its angle around the player.
        private static float PlacementAngle(CameraPlacement placement)
        {
            switch (placement)
            {
                case CameraPlacement.Left:
                case CameraPlacement.LeftTop:
                    return -90f;
                case CameraPlacement.Right:
                case CameraPlacement.RightTop:
                    return 90f;
                default:
                    return 0f;
            }
        }

        // Returns whether a placement requests additional elevation.
        private static bool IsTop(CameraPlacement placement)
        {
            return placement == CameraPlacement.FrontTop ||
                   placement == CameraPlacement.LeftTop ||
                   placement == CameraPlacement.RightTop;
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
