using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Landoria.SagaCapture
{
    // Loads the bundled drone mesh and its game-compatible materials.
    internal sealed class SagaCaptureDroneModel
    {
        private readonly GameObject _instance;
        private readonly List<Material> _materials;
        private readonly float _sourceDiameter;
        private readonly Vector3 _centerOffset;
        private readonly Vector3 _sourceScale;

        // Stores the imported mesh and its original bounds.
        private SagaCaptureDroneModel(
            GameObject instance, List<Material> materials,
            float sourceDiameter, Vector3 centerOffset,
            Vector3 sourceScale)
        {
            _instance = instance;
            _materials = materials;
            _sourceDiameter = sourceDiameter;
            _centerOffset = centerOffset;
            _sourceScale = sourceScale;
        }

        // Loads one prefab without keeping its Unity bundle open afterward.
        internal static SagaCaptureDroneModel Load(Transform parent, int layer)
        {
            string path = Preference.DroneModelBundlePath;
            if (!File.Exists(path))
            {
                SagaCapturePlugin.Log.LogWarning(
                    $"Drone model bundle not found: {path}");
                return null;
            }
            AssetBundle bundle = null;
            try
            {
                bundle = AssetBundle.LoadFromFile(path);
                GameObject prefab = bundle?.LoadAsset<GameObject>(
                    "assets/models/sagadrone.prefab");
                if (prefab == null)
                {
                    throw new InvalidDataException(
                        "The drone bundle does not contain SagaDrone.prefab.");
                }
                return Create(prefab, parent, layer);
            }
            catch (Exception error)
            {
                SagaCapturePlugin.Log.LogError(
                    $"Could not load drone model: {error}");
                return null;
            }
            finally
            {
                bundle?.Unload(false);
            }
        }

        // Instantiates the mesh and replaces imported shaders before rendering.
        private static SagaCaptureDroneModel Create(
            GameObject prefab, Transform parent, int layer)
        {
            GameObject instance = UnityEngine.Object.Instantiate(
                prefab, parent, false);
            instance.name = "SagaCaptureDroneModel";
            var materials = new List<Material>();
            try
            {
                Configure(instance, layer, materials);
                SagaCapturePlugin.Log.LogInfo(
                    $"Drone model loaded: {instance.GetComponentsInChildren<Renderer>().Length} " +
                    $"renderers, shader {(materials.Count > 0 ? materials[0].shader.name : "none")}, layer {layer}.");
                Bounds bounds = BoundsOf(instance);
                float diameter = Mathf.Max(bounds.size.x,
                    Mathf.Max(bounds.size.y, bounds.size.z));
                Vector3 offset = parent.InverseTransformPoint(bounds.center);
                instance.SetActive(false);
                return new SagaCaptureDroneModel(
                    instance, materials, diameter, -offset,
                    instance.transform.localScale);
            }
            catch
            {
                UnityEngine.Object.Destroy(instance);
                foreach (Material material in materials)
                {
                    UnityEngine.Object.Destroy(material);
                }
                throw;
            }
        }

        // Applies a camera-only layer and enables a two-sided drone shadow.
        private static void Configure(GameObject instance, int layer,
            List<Material> owned)
        {
            foreach (Transform child in instance.GetComponentsInChildren<Transform>())
            {
                child.gameObject.layer = layer;
            }
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.Destroy(collider);
            }
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
                renderer.receiveShadows = true;
                Material[] slots = renderer.sharedMaterials;
                for (int index = 0; index < slots.Length; index++)
                {
                    slots[index] = MakeMaterial(slots[index],
                        slots[index]?.name, owned);
                }
                renderer.sharedMaterials = slots;
            }
        }

        // Uses the bundled drone shader and recolors one model part.
        private static Material MakeMaterial(Material source, string name,
            List<Material> owned)
        {
            Color color = ColorFor(name);
            Material template = source?.shader != null &&
                source.shader.name == "SagaCapture/Drone" &&
                source.shader.isSupported ? source : FindGameMaterial();
            if (template == null)
            {
                throw new InvalidOperationException(
                    "Neither the bundled drone shader nor a fallback is available.");
            }
            Material material = new Material(template);
            material.color = color;
            if (template != source && material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", Texture2D.whiteTexture);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.55f);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.45f);
            }
            if (material.HasProperty("_EmissionColor") &&
                name != null && name.StartsWith("Light", StringComparison.OrdinalIgnoreCase))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color);
            }
            owned.Add(material);
            return material;
        }

        // Maps the source model's color slots to bronze and warm light.
        private static Color ColorFor(string name)
        {
            if (name != null && name.StartsWith("Light", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(1f, 0.58f, 0.2f);
            }
            if (name != null && name.StartsWith("edge", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(0.08f, 0.09f, 0.09f);
            }
            return new Color(0.46f, 0.48f, 0.5f);
        }

        // Finds a shader already used to draw the local player.
        private static Material FindGameMaterial()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return null;
            }
            foreach (Renderer renderer in
                     player.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && material.shader != null &&
                        material.shader.isSupported &&
                        material.HasProperty("_Color"))
                    {
                        return material;
                    }
                }
            }
            return null;
        }

        // Combines renderer bounds to measure the imported model once.
        private static Bounds BoundsOf(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                throw new InvalidDataException("The drone prefab has no mesh.");
            }
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        // Scales and centers the imported model inside the collision sphere.
        internal void SetRadius(float radius)
        {
            float scale = radius * 2f / _sourceDiameter;
            _instance.transform.localScale = _sourceScale * scale;
            _instance.transform.localPosition = _centerOffset * scale;
        }

        // Shows the model only in the caller's selected camera mode.
        internal void SetVisible(bool visible)
        {
            if (_instance.activeSelf != visible)
            {
                _instance.SetActive(visible);
            }
        }

        // Pulses only the model's originally luminous material slots.
        internal void SetGlow(float pulse)
        {
            foreach (Material material in _materials)
            {
                if (material.IsKeywordEnabled("_EMISSION"))
                {
                    material.SetColor("_EmissionColor",
                        material.color * pulse);
                }
            }
        }

        // Destroys the caller-owned instance and temporary material copies.
        internal void Dispose()
        {
            UnityEngine.Object.Destroy(_instance);
            foreach (Material material in _materials)
            {
                UnityEngine.Object.Destroy(material);
            }
        }
    }
}
