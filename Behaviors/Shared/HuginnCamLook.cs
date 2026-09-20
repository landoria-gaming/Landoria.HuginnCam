using UnityEngine;

namespace Landoria.HuginnCam
{
    // Smoothly aims the Huginn camera at the player with a level horizon.
    internal sealed class HuginnCamLook
    {
        private const float RotationSmoothTime = 1.2f;
        private const float MaximumRotationSpeed = 35f;
        private const float FreeFlightRotationSmoothTime = 1.2f;
        private const float MaximumFreeFlightRotationSpeed = 35f;
        private const float MaximumBankAngle = 7f;
        private const float BankSmoothTime = 0.8f;
        private const float MaximumBankSpeed = 15f;
        private float _yawVelocity;
        private float _pitchVelocity;
        private float _rollVelocity;

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

        // Faces downhill on slopes and the player on nearly level ground.
        internal void UpdateLanding(Transform cameraTransform, Vector3 playerFocus)
        {
            if (HuginnCamTerrain.TryGetDownhillDirection(
                cameraTransform.position, out Vector3 downhill))
            {
                UpdateHorizon(cameraTransform, downhill);
                return;
            }

            Update(cameraTransform, playerFocus);
        }

        // Smoothly faces an unrestricted direction during free flight.
        internal void UpdateFree(Transform cameraTransform, Vector3 direction)
        {
            if (direction.sqrMagnitude > 0.001f)
            {
                UpdateDirection(
                    cameraTransform, direction.normalized,
                    FreeFlightRotationSmoothTime,
                    MaximumFreeFlightRotationSpeed);
            }
        }

        // Smoothly applies one normalized look direction.
        private void UpdateDirection(Transform cameraTransform, Vector3 direction)
        {
            UpdateDirection(
                cameraTransform, direction,
                RotationSmoothTime, MaximumRotationSpeed);
        }

        // Applies one normalized direction with explicit damping limits.
        private void UpdateDirection(
            Transform cameraTransform, Vector3 direction,
            float smoothTime, float maximumSpeed)
        {
            float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float targetPitch = -Mathf.Asin(
                Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 current = cameraTransform.rotation.eulerAngles;
            float yawDelta = Mathf.DeltaAngle(current.y, targetYaw);
            float targetRoll = Mathf.Clamp(
                -yawDelta * 0.15f, -MaximumBankAngle, MaximumBankAngle);
            float yaw = Mathf.SmoothDampAngle(
                current.y, targetYaw, ref _yawVelocity,
                smoothTime, maximumSpeed);
            float pitch = Mathf.SmoothDampAngle(
                current.x, targetPitch, ref _pitchVelocity,
                smoothTime, maximumSpeed);
            float roll = Mathf.SmoothDampAngle(
                current.z, targetRoll, ref _rollVelocity,
                BankSmoothTime, MaximumBankSpeed);
            cameraTransform.rotation = Quaternion.Euler(pitch, yaw, roll);
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
