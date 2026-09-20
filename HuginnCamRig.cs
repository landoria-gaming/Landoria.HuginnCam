using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.PostProcessing;

namespace Landoria.HuginnCam
{
    // Maintains the autonomous Huginn camera around the player.
    internal sealed class HuginnCamRig : MonoBehaviour
    {
        private const float HeadHeight = 1.7f;
        private const float DirectionTurnSpeed = 0.75f;
        private const float DestinationAdvanceDistance = 1f;
        private Camera _camera;
        private AudioListener _listener;
        private AudioListener _originalListener;
        private HuginnCamAudio _audio;
        private RenderTexture _offscreenTarget;
        private Vector3 _movementDirection;
        private bool _poseInitialized;
        private readonly HuginnCamSpeed _speed = new HuginnCamSpeed();
        private readonly HuginnCamLook _look = new HuginnCamLook();
        private readonly HuginnCamWander _wander = new HuginnCamWander();
        private readonly HuginnCamOverwatch _overwatch = new HuginnCamOverwatch();
        private readonly List<HuginnCamEffectMirror> _effectMirrors =
            new List<HuginnCamEffectMirror>();
        private static readonly Dictionary<Type, FieldInfo[]> SerializableFields =
            new Dictionary<Type, FieldInfo[]>();
        private static readonly HashSet<string> WarnedUnclassifiedComponents = new HashSet<string>();
        internal Camera Camera => _camera;
        internal AudioListener Listener => _listener;
        internal RenderTexture PreparedTarget => _offscreenTarget;
        // Clones the gameplay camera and optionally transfers audio listening to it.
        internal void Initialize(Camera sourceCamera, bool transferAudio = true)
        {
            GameObject cameraObject = new GameObject("HuginnCamCamera");
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _audio = cameraObject.AddComponent<HuginnCamAudio>();
            _audio.Initialize();
            _camera.CopyFrom(sourceCamera);
            _camera.depth = sourceCamera.depth + 1f;
            _camera.enabled = false;
            CopyVisualEffectStack(sourceCamera, cameraObject);
            if (transferAudio)
            {
                _originalListener = HuginnCamAudio.FindActiveListener(sourceCamera);
                if (_originalListener != null)
                {
                    _originalListener.enabled = false;
                }
                _listener = cameraObject.AddComponent<AudioListener>();
            }
            UpdatePose();
        }
        // Displays the Huginn camera directly on the player's screen.
        internal void BeginPreview()
        {
            EndOffscreenRendering();
            _camera.targetTexture = null;
            _camera.enabled = true;
        }
        // Recreates safe visual effects in their source order and mirrors their settings.
        private void CopyVisualEffectStack(Camera sourceCamera, GameObject target)
        {
            Component[] components = sourceCamera.gameObject.GetComponents<Component>();
            foreach (Component source in components)
            {
                if (source is FlareLayer)
                {
                    target.AddComponent<FlareLayer>();
                    continue;
                }
                if (source is PostProcessingBehaviour)
                {
                    CopyPostProcessing(sourceCamera, target);
                    continue;
                }
                string typeName = source.GetType().FullName;
                if (!IsMirroredVisualEffect(typeName))
                {
                    if (!IsExcludedCameraComponent(typeName) && WarnedUnclassifiedComponents.Add(typeName))
                    {
                        HuginnCamPlugin.Log.LogWarning(
                            $"Camera component '{typeName}' is neither mirrored nor explicitly excluded; " +
                            "HuginnCam left it off the secondary camera.");
                    }

                    continue;
                }

                Component destination = target.AddComponent(source.GetType());
                CopySerializedSettings(source, destination);
                if (source is Behaviour sourceBehaviour && destination is Behaviour destinationBehaviour)
                {
                    destinationBehaviour.enabled = sourceBehaviour.enabled;
                }

                _effectMirrors.Add(new HuginnCamEffectMirror(source, destination));
            }
        }
        // Identifies camera infrastructure that must not be duplicated on a secondary camera.
        private static bool IsExcludedCameraComponent(string typeName)
        {
            switch (typeName)
            {
                case "UnityEngine.Transform":
                case "UnityEngine.Camera":
                case "UnityEngine.AudioListener":
                case "GameCamera":
                case "CameraEffects":
                case "ShieldDomeImageEffect":
                case "UpscaledFrameBuffer":
                    return true;
                default:
                    return false;
            }
        }
        // Identifies image effects that are safe to instantiate on an independent camera.
        private static bool IsMirroredVisualEffect(string typeName)
        {
            switch (typeName)
            {
                case "GlobalBlueNoise":
                case "AmplifyOcclusionEffect":
                case "UnityStandardAssets.ImageEffects.SunShafts":
                case "UnityStandardAssets.ImageEffects.DepthOfField":
                case "HeatDistortImageEffect":
                case "DepthCopy":
                    return true;
                default:
                    return false;
            }
        }
        // Synchronizes environment-dependent serialized settings without sharing runtime resources.
        private void SynchronizeVisualEffects()
        {
            foreach (HuginnCamEffectMirror mirror in _effectMirrors)
            {
                if (mirror.Source == null || mirror.Destination == null)
                {
                    continue;
                }

                CopySerializedSettings(mirror.Source, mirror.Destination);
                if (mirror.Source is Behaviour sourceBehaviour &&
                    mirror.Destination is Behaviour destinationBehaviour)
                {
                    destinationBehaviour.enabled = sourceBehaviour.enabled;
                }
            }
        }
        // Copies only Unity-serialized fields and excludes private runtime state.
        private static void CopySerializedSettings(Component source, Component destination)
        {
            foreach (FieldInfo field in GetSerializableFields(source.GetType()))
            {
                field.SetValue(destination, field.GetValue(source));
            }
        }
        // Discovers and caches public or explicitly serialized fields for one effect type.
        private static FieldInfo[] GetSerializableFields(Type type)
        {
            if (SerializableFields.TryGetValue(type, out FieldInfo[] cached))
            {
                return cached;
            }

            var fields = new List<FieldInfo>();
            for (Type current = type; current != null && current != typeof(MonoBehaviour); current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    bool serialized = field.IsPublic || field.IsDefined(typeof(SerializeField), true);
                    if (serialized && !field.IsStatic && !field.IsInitOnly && !field.IsNotSerialized)
                    {
                        fields.Add(field);
                    }
                }
            }

            cached = fields.ToArray();
            SerializableFields[type] = cached;
            return cached;
        }
        // Starts invisible rendering so this camera can establish its own automatic exposure.
        internal void BeginWarmup(
            int requestedWidth = 0,
            int requestedHeight = 0,
            int antiAliasingSamples = 1)
        {
            int width = requestedWidth > 0
                ? Mathf.Max(2, requestedWidth & ~1)
                : Mathf.Max(2, Mathf.Min(Screen.width, 1920) & ~1);
            float scale = (float)width / Mathf.Max(1, Screen.width);
            int height = requestedHeight > 0
                ? Mathf.Max(2, requestedHeight & ~1)
                : Mathf.Max(2, Mathf.RoundToInt(Screen.height * scale) & ~1);
            _offscreenTarget = new RenderTexture(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            _offscreenTarget.antiAliasing = antiAliasingSamples;
            _offscreenTarget.Create();
            _camera.targetTexture = _offscreenTarget;
            _camera.enabled = true;
        }
        // Stops invisible warmup rendering while preserving the camera's exposure history.
        internal void EndWarmup()
        {
            EndOffscreenRendering();
        }
        // Stops offscreen rendering and releases its render texture.
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
        // Applies the gameplay camera's color grading and exposure profile to the Huginn camera.
        private static void CopyPostProcessing(Camera sourceCamera, GameObject target)
        {
            PostProcessingBehaviour source = sourceCamera.GetComponent<PostProcessingBehaviour>();
            if (source == null || source.profile == null)
            {
                return;
            }

            PostProcessingBehaviour destination = target.AddComponent<PostProcessingBehaviour>();
            destination.profile = source.profile;
        }

        // Updates the camera after the player has completed movement for the frame.
        private void LateUpdate()
        {
            SynchronizeVisualEffects();
            UpdatePose();
        }

        // Restores the gameplay listener and destroys the Huginn camera.
        internal void Dispose()
        {
            EndWarmup();
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
                _effectMirrors.Clear();
            }
        }

        // Smoothly follows the player while keeping a clear line of sight.
        private void UpdatePose()
        {
            Player player = Player.m_localPlayer;
            if (player == null || _camera == null)
            {
                return;
            }

            Vector3 head = player.transform.position + Vector3.up * HeadHeight;
            Vector3 desired = _wander.GetTarget(player);
            _overwatch.Update(
                player, _wander.IsPlayerMoving,
                _wander.TravelDirection,
                _camera.transform.position,
                _movementDirection * _speed.Current,
                ref desired);
            _audio.HandleOverwatch(_overwatch);
            if (_overwatch.TryTakeExitVelocity(out Vector3 exitVelocity) &&
                exitVelocity.sqrMagnitude > 0.001f)
            {
                _movementDirection = exitVelocity.normalized;
                _speed.MatchCurrent(exitVelocity.magnitude);
            }
            bool avoidingObstacle =
                !HuginnCamVisibility.HasClearSight(player, head, desired) ||
                !HuginnCamVisibility.HasClearSight(
                    player, head, _camera.transform.position);
            if (avoidingObstacle)
            {
                _wander.RetrySoon();
            }

            if (!_poseInitialized)
            {
                _camera.transform.position = desired;
                _look.Snap(_camera.transform, head);
                _movementDirection = player.transform.forward.normalized;
                _speed.Initialize();
                _poseInitialized = true;
                return;
            }

            bool perched = _wander.UpdatePerch(
                _camera.transform.position, desired);
            if (_overwatch.IsActive)
            {
                Vector3 next = _overwatch.Move(
                    _camera.transform.position, desired);
                _camera.transform.position = HuginnCamTerrain.ResolveMovement(
                    _camera.transform.position, next, false);
                if (_overwatch.FollowVelocity.sqrMagnitude > 0.001f)
                {
                    _movementDirection = _overwatch.FollowVelocity.normalized;
                    _speed.MatchCurrent(_overwatch.FollowVelocity.magnitude);
                }
            }
            else if (!perched)
            {
                MoveCamera(
                    player, ref desired, avoidingObstacle, _overwatch.IsActive);
            }
            if (!HuginnCamVisibility.HasClearSight(
                player, head, _camera.transform.position))
            {
                _wander.RetrySoon();
            }

            if (_overwatch.IsActive)
            {
                _look.UpdateHorizon(
                    _camera.transform, player.transform.forward);
            }
            else if (_wander.IsLanding)
            {
                _look.UpdateLanding(_camera.transform, _movementDirection);
            }
            else
            {
                _look.Update(_camera.transform, head);
            }
        }

        // Moves continuously at cruise speed and catches up only when far behind.
        private void MoveCamera(
            Player player, ref Vector3 desired, bool avoidingObstacle,
            bool preserveDestination)
        {
            bool avoidingTerrain = !_wander.IsLanding &&
                HuginnCamTerrain.NeedsAvoidance(
                    _camera.transform.position, desired);
            if (avoidingTerrain)
            {
                desired.y = Mathf.Max(
                    desired.y, _camera.transform.position.y + 2f);
            }

            Vector3 offset = desired - _camera.transform.position;
            if (!preserveDestination &&
                offset.magnitude <= DestinationAdvanceDistance)
            {
                _wander.AdvanceNow();
                desired = _wander.GetTarget(player);
                offset = desired - _camera.transform.position;
            }

            Vector3 targetDirection = offset.sqrMagnitude > 0.001f
                ? offset.normalized
                : _movementDirection;
            _movementDirection = Vector3.RotateTowards(
                _movementDirection,
                targetDirection,
                DirectionTurnSpeed * Time.deltaTime,
                0f).normalized;
            float speed = _speed.Update(
                offset.magnitude, avoidingObstacle || avoidingTerrain);
            Vector3 nextPosition = _camera.transform.position +
                                   _movementDirection * speed * Time.deltaTime;
            if (!preserveDestination)
            {
                nextPosition = _wander.KeepOutsideMinimumRadius(
                    player, nextPosition);
            }
            _camera.transform.position = HuginnCamTerrain.ResolveMovement(
                _camera.transform.position, nextPosition, _wander.IsLanding);
        }
    }
}
