using UnityEngine;

namespace Landoria.HuginnCam
{
    // Smoothly aims the Huginn camera at the player with a level horizon.
    internal sealed class HuginnCamLook
    {
        private const float RotationSmoothTime = 0.45f;
        private const float MaximumRotationSpeed = 120f;
        private float _yawVelocity;
        private float _pitchVelocity;

        // Aims immediately when the Huginn camera is first created.
        internal void Snap(Transform cameraTransform, Vector3 focus)
        {
            cameraTransform.rotation = CreateLevelRotation(
                focus - cameraTransform.position);
        }

        // Smoothly turns the camera toward the current player position.
        internal void Update(Transform cameraTransform, Vector3 focus)
        {
            Vector3 direction = (focus - cameraTransform.position).normalized;
            UpdateDirection(cameraTransform, direction);
        }

        // Smoothly faces the level horizon in the supplied direction.
        internal void UpdateHorizon(Transform cameraTransform, Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                UpdateDirection(cameraTransform, direction.normalized);
            }
        }

        // Smoothly applies one normalized look direction.
        private void UpdateDirection(Transform cameraTransform, Vector3 direction)
        {
            float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float targetPitch = -Mathf.Asin(
                Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 current = cameraTransform.rotation.eulerAngles;
            float yaw = Mathf.SmoothDampAngle(
                current.y, targetYaw, ref _yawVelocity,
                RotationSmoothTime, MaximumRotationSpeed);
            float pitch = Mathf.SmoothDampAngle(
                current.x, targetPitch, ref _pitchVelocity,
                RotationSmoothTime, MaximumRotationSpeed);
            cameraTransform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // Creates an exact look rotation without camera roll.
        private static Quaternion CreateLevelRotation(Vector3 direction)
        {
            Vector3 normalized = direction.normalized;
            float yaw = Mathf.Atan2(normalized.x, normalized.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(
                Mathf.Clamp(normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            return Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
