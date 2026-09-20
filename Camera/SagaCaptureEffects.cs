using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.PostProcessing;

namespace Landoria.SagaCapture
{
    // Recreates and synchronizes safe gameplay-camera visual effects.
    internal sealed class SagaCaptureEffects
    {
        private readonly List<SagaCaptureEffectMirror> _mirrors =
            new List<SagaCaptureEffectMirror>();
        private static readonly Dictionary<Type, FieldInfo[]> SerializableFields =
            new Dictionary<Type, FieldInfo[]>();
        private static readonly HashSet<string> WarnedComponents =
            new HashSet<string>();

        // Recreates safe visual effects in their original component order.
        internal void Initialize(Camera sourceCamera, GameObject target)
        {
            foreach (Component source in
                     sourceCamera.gameObject.GetComponents<Component>())
            {
                CopyComponent(sourceCamera, target, source);
            }
        }

        // Synchronizes runtime settings without sharing effect instances.
        internal void Synchronize()
        {
            foreach (SagaCaptureEffectMirror mirror in _mirrors)
            {
                if (mirror.Source == null || mirror.Destination == null)
                {
                    continue;
                }

                CopySerializedSettings(mirror.Source, mirror.Destination);
                SynchronizeEnabledState(mirror.Source, mirror.Destination);
            }
        }

        // Releases references when the secondary camera is destroyed.
        internal void Clear()
        {
            _mirrors.Clear();
        }

        // Copies one supported component and ignores camera infrastructure.
        private void CopyComponent(
            Camera sourceCamera, GameObject target, Component source)
        {
            if (source is FlareLayer)
            {
                target.AddComponent<FlareLayer>();
                return;
            }
            if (source is PostProcessingBehaviour)
            {
                CopyPostProcessing(sourceCamera, target);
                return;
            }

            string typeName = source.GetType().FullName;
            if (!IsMirroredVisualEffect(typeName))
            {
                WarnWhenUnclassified(typeName);
                return;
            }

            Component destination = target.AddComponent(source.GetType());
            CopySerializedSettings(source, destination);
            SynchronizeEnabledState(source, destination);
            _mirrors.Add(new SagaCaptureEffectMirror(source, destination));
        }

        // Warns once when a camera component has no explicit policy.
        private static void WarnWhenUnclassified(string typeName)
        {
            if (IsExcludedCameraComponent(typeName) ||
                !WarnedComponents.Add(typeName))
            {
                return;
            }

            SagaCapturePlugin.Log.LogWarning(
                $"Camera component '{typeName}' is not mirrored or excluded; " +
                "SagaCapture left it off the secondary camera.");
        }

        // Identifies infrastructure that must not be copied.
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

        // Identifies image effects safe for an independent camera instance.
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

        // Copies only fields Unity serializes for the effect component.
        private static void CopySerializedSettings(
            Component source, Component destination)
        {
            foreach (FieldInfo field in GetSerializableFields(source.GetType()))
            {
                field.SetValue(destination, field.GetValue(source));
            }
        }

        // Discovers and caches serialized fields for one component type.
        private static FieldInfo[] GetSerializableFields(Type type)
        {
            if (SerializableFields.TryGetValue(type, out FieldInfo[] cached))
            {
                return cached;
            }

            var fields = new List<FieldInfo>();
            for (Type current = type;
                 current != null && current != typeof(MonoBehaviour);
                 current = current.BaseType)
            {
                AddSerializableFields(current, fields);
            }
            cached = fields.ToArray();
            SerializableFields[type] = cached;
            return cached;
        }

        // Adds serialized instance fields declared by one type.
        private static void AddSerializableFields(
            Type type, List<FieldInfo> fields)
        {
            foreach (FieldInfo field in type.GetFields(
                         BindingFlags.Instance | BindingFlags.Public |
                         BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                bool serialized = field.IsPublic ||
                                  field.IsDefined(typeof(SerializeField), true);
                if (serialized && !field.IsStatic && !field.IsInitOnly &&
                    !field.IsNotSerialized)
                {
                    fields.Add(field);
                }
            }
        }

        // Mirrors an effect's enabled state when it is a Behaviour.
        private static void SynchronizeEnabledState(
            Component source, Component destination)
        {
            if (source is Behaviour sourceBehaviour &&
                destination is Behaviour destinationBehaviour)
            {
                destinationBehaviour.enabled = sourceBehaviour.enabled;
            }
        }

        // Shares the source color-grading and exposure profile.
        private static void CopyPostProcessing(
            Camera sourceCamera, GameObject target)
        {
            PostProcessingBehaviour source =
                sourceCamera.GetComponent<PostProcessingBehaviour>();
            if (source == null || source.profile == null)
            {
                return;
            }

            PostProcessingBehaviour destination =
                target.AddComponent<PostProcessingBehaviour>();
            destination.profile = source.profile;
            destination.enabled = source.enabled;
        }
    }
}
