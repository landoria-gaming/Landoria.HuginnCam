using UnityEngine;

namespace Landoria.SagaCapture
{
    // Displays the supplied drone model outside the recording camera.
    [DefaultExecutionOrder(10000)]
    internal sealed class SagaCaptureDroneVisual : MonoBehaviour
    {
        private Camera _sourceCamera;
        private SagaCaptureDroneModel _model;
        private int _sourceMask;
        private bool _flightActive;
        private float _radius;
        private int _layerMask;
        private int _remainingDiagnostics;
        private float _nextDiagnosticAt;

        // Loads the model on a layer visible to the player but not the drone.
        internal void Initialize(Camera droneCamera, Camera sourceCamera)
        {
            int layer = FindUnusedLayer();
            if (layer < 0)
            {
                SagaCapturePlugin.Log.LogWarning(
                    "No unused Unity layer is available for the drone model.");
                return;
            }
            _model = SagaCaptureDroneModel.Load(transform, layer);
            if (_model == null)
            {
                return;
            }
            _sourceCamera = sourceCamera;
            _sourceMask = sourceCamera.cullingMask;
            _layerMask = 1 << layer;
            sourceCamera.cullingMask |= _layerMask;
            droneCamera.cullingMask &= ~_layerMask;
            Refresh();
        }

        // Activates the model only while the gameplay view records the drone.
        internal void SetFlightActive(bool active)
        {
            _flightActive = active;
            Refresh();
            if (active)
            {
                _remainingDiagnostics = 3;
                _nextDiagnosticAt = Time.unscaledTime + 0.5f;
                Renderer[] renderers = GetComponentsInChildren<Renderer>();
                SagaCapturePlugin.Log.LogInfo(
                    $"Drone visual enabled: model={_model != null}, " +
                    $"renderers={renderers.Length}, position={transform.position}, " +
                    $"sourceMask={_sourceCamera?.cullingMask}, " +
                    $"show={Preference.ShowDroneVisual}.");
            }
        }

        // Keeps the model visible if Valheim resets the camera mask each frame.
        private void LateUpdate()
        {
            if (_sourceCamera != null && _flightActive)
            {
                _sourceCamera.cullingMask |= _layerMask;
                LogVisibilityDiagnostic();
            }
        }

        // Records the model's real position and screen projection after flight moves.
        private void LogVisibilityDiagnostic()
        {
            if (_remainingDiagnostics <= 0 ||
                Time.unscaledTime < _nextDiagnosticAt)
            {
                return;
            }
            _remainingDiagnostics--;
            _nextDiagnosticAt = Time.unscaledTime + 1f;
            Renderer renderer = GetComponentInChildren<Renderer>(true);
            if (renderer == null)
            {
                return;
            }
            Vector3 view = _sourceCamera.WorldToViewportPoint(renderer.bounds.center);
            SagaCapturePlugin.Log.LogInfo(
                $"Drone visibility: world={renderer.bounds.center}, " +
                $"size={renderer.bounds.size}, viewport={view}, " +
                $"active={renderer.gameObject.activeInHierarchy}, " +
                $"renderer={renderer.enabled}, visible={renderer.isVisible}, " +
                $"shader={renderer.sharedMaterial?.shader?.name}, " +
                $"camera={_sourceCamera.transform.position}, " +
                $"cameraEnabled={_sourceCamera.enabled}, mask={_sourceCamera.cullingMask}.");
        }

        // Matches the model's physical size to the pilot's collision radius.
        internal void SetRadius(float radius)
        {
            if (_model == null || Mathf.Abs(_radius - radius) < 0.0001f)
            {
                return;
            }
            _radius = radius;
            _model.SetRadius(radius);
        }

        // Applies the live visibility option and subtle nightly light pulse.
        internal void Refresh()
        {
            if (_model == null)
            {
                return;
            }
            bool visible = _flightActive && Preference.ShowDroneVisual;
            _model.SetVisible(visible);
            if (visible)
            {
                float pulse = 1.15f + 0.2f * Mathf.Sin(Time.time * 1.8f);
                _model.SetGlow(pulse);
            }
        }

        // Restores gameplay rendering and releases model resources.
        internal void Dispose()
        {
            if (_sourceCamera != null)
            {
                _sourceCamera.cullingMask = _sourceMask;
                _sourceCamera = null;
            }
            _model?.Dispose();
            _model = null;
        }

        // Finds an unnamed layer for the model's camera exclusion.
        private static int FindUnusedLayer()
        {
            for (int layer = 31; layer >= 8; layer--)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(layer)))
                {
                    return layer;
                }
            }
            return -1;
        }
    }
}
