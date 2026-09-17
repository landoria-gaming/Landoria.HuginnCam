using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.PostProcessing;

namespace Landoria.HuginnCam
{
    // Maintains a collision-aware cinematic camera behind the player.
    internal sealed class CinematicCameraRig : MonoBehaviour
    {
        // Associates a source image effect with its independent camera copy.
        private sealed class EffectMirror
        {
            internal readonly Component Source;
            internal readonly Component Destination;

            // Stores the two components participating in a settings mirror.
            internal EffectMirror(Component source, Component destination)
            {
                Source = source;
                Destination = destination;
            }
        }

        private const float HeadHeight = 1.7f;
        private const float HeightAboveHead = 2f;
        private const float DistanceBehind = 5f;
        private Camera _camera;
        private AudioListener _listener;
        private AudioListener _originalListener;
        private RenderTexture _offscreenTarget;
        private readonly List<EffectMirror> _effectMirrors = new List<EffectMirror>();
        private static readonly Dictionary<Type, FieldInfo[]> SerializableFields =
            new Dictionary<Type, FieldInfo[]>();
        private static readonly HashSet<string> WarnedUnclassifiedComponents = new HashSet<string>();

        internal Camera Camera => _camera;
        internal AudioListener Listener => _listener;
        internal RenderTexture PreparedTarget => _offscreenTarget;

        // Clones the gameplay camera and optionally transfers audio listening to it.
        internal void Initialize(Camera sourceCamera, bool transferAudio = true)
        {
            GameObject cameraObject = new GameObject("HuginnCamCinematicCamera");
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.CopyFrom(sourceCamera);
            _camera.depth = sourceCamera.depth + 1f;
            _camera.enabled = false;
            CopyVisualEffectStack(sourceCamera, cameraObject);
            if (transferAudio)
            {
                _originalListener = sourceCamera.GetComponent<AudioListener>();
                if (_originalListener != null)
                {
                    _originalListener.enabled = false;
                }

                _listener = cameraObject.AddComponent<AudioListener>();
            }

            UpdatePose();
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

                _effectMirrors.Add(new EffectMirror(source, destination));
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
            foreach (EffectMirror mirror in _effectMirrors)
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

        // Applies the gameplay camera's color grading and exposure profile to the cinematic camera.
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

        // Restores the gameplay listener and destroys the cinematic camera.
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

        // Places the camera behind the player and keeps a clear line of sight.
        private void UpdatePose()
        {
            Player player = Player.m_localPlayer;
            if (player == null || _camera == null)
            {
                return;
            }

            Vector3 head = player.transform.position + Vector3.up * HeadHeight;
            Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            Vector3 desired = head + Vector3.up * HeightAboveHead - forward * DistanceBehind;
            _camera.transform.position = ResolveCollision(head, desired);
            _camera.transform.rotation = CreateLevelRotation(head - _camera.transform.position);
        }

        // Creates a look rotation with an explicit zero roll so the horizon remains level.
        private static Quaternion CreateLevelRotation(Vector3 direction)
        {
            Vector3 normalized = direction.normalized;
            float yaw = Mathf.Atan2(normalized.x, normalized.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            return Quaternion.Euler(pitch, yaw, 0f);
        }

        // Pulls the camera in front of the nearest obstacle along the sight line.
        private static Vector3 ResolveCollision(Vector3 focus, Vector3 desired)
        {
            Vector3 direction = desired - focus;
            float distance = direction.magnitude;
            if (Physics.SphereCast(focus, 0.2f, direction.normalized, out RaycastHit hit, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return focus + direction.normalized * Mathf.Max(0.3f, hit.distance - 0.25f);
            }

            return desired;
        }
    }
}
