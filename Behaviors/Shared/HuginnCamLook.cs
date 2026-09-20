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
        private const float MaximumHeadTurnAngle = 150f;
        private const float BackwardLookThreshold = 90f;
        private const float MinimumBackwardGlanceDuration = 1.5f;
        private const float MaximumBackwardGlanceDuration = 3f;
        private const float MinimumForwardLookDuration = 2f;
        private const float MaximumForwardLookDuration = 5f;
        private float _yawVelocity;
        private float _pitchVelocity;
        private float _rollVelocity;
        private float _nextBackwardLookChange;
        private bool _backwardGlanceInitialized;
        private bool _lookingBackward;

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

        // Alternates natural backward glances with attention to the flight path.
        internal void UpdateFlightAware(
            Transform cameraTransform, Vector3 focus,
            Vector3 bodyDirection)
        {
            Vector3 desired = (focus - cameraTransform.position).normalized;
            UpdateDirection(cameraTransform,
                SelectHeadDirection(desired, bodyDirection));
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
        internal void UpdateLanding(
            Transform cameraTransform, Vector3 playerFocus,
            Vector3 bodyDirection)
        {
            if (HuginnCamTerrain.TryGetDownhillDirection(
                cameraTransform.position, out Vector3 downhill))
            {
                UpdateDirection(cameraTransform,
                    SelectHeadDirection(downhill, bodyDirection));
                return;
            }

            UpdateFlightAware(
                cameraTransform, playerFocus, bodyDirection);
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

        // Smoothly looks freely within the forward flight hemisphere.
        internal void UpdateFreeFlightAware(
            Transform cameraTransform, Vector3 direction,
            Vector3 bodyDirection)
        {
            if (direction.sqrMagnitude > 0.001f)
            {
                UpdateDirection(
                    cameraTransform,
                    ConstrainToBody(direction.normalized, bodyDirection),
                    FreeFlightRotationSmoothTime,
                    MaximumFreeFlightRotationSpeed);
            }
        }

        // Chooses whether Huginn watches a subject currently behind its body.
        private Vector3 SelectHeadDirection(
            Vector3 desired, Vector3 bodyDirection)
        {
            if (bodyDirection.sqrMagnitude < 0.001f)
            {
                return desired;
            }

            float angle = Vector3.Angle(bodyDirection, desired);
            if (angle <= BackwardLookThreshold)
            {
                _backwardGlanceInitialized = false;
                _lookingBackward = false;
                return ConstrainToBody(desired, bodyDirection);
            }

            UpdateBackwardGlance();
            return _lookingBackward
                ? ConstrainToBody(desired, bodyDirection)
                : bodyDirection.normalized;
        }

        // Alternates short looks behind with longer forward-looking intervals.
        private void UpdateBackwardGlance()
        {
            if (!_backwardGlanceInitialized)
            {
                _backwardGlanceInitialized = true;
                _lookingBackward = true;
                ScheduleBackwardLookChange();
            }
            else if (Time.time >= _nextBackwardLookChange)
            {
                _lookingBackward = !_lookingBackward;
                ScheduleBackwardLookChange();
            }
        }

        // Schedules the next smooth change of head attention.
        private void ScheduleBackwardLookChange()
        {
            float duration = _lookingBackward
                ? Random.Range(
                    MinimumBackwardGlanceDuration,
                    MaximumBackwardGlanceDuration)
                : Random.Range(
                    MinimumForwardLookDuration,
                    MaximumForwardLookDuration);
            _nextBackwardLookChange = Time.time + duration;
        }

        // Limits independent head rotation relative to the invisible body.
        private static Vector3 ConstrainToBody(
            Vector3 desired, Vector3 bodyDirection)
        {
            if (bodyDirection.sqrMagnitude < 0.001f)
            {
                return desired;
            }

            return Vector3.RotateTowards(
                bodyDirection.normalized, desired,
                MaximumHeadTurnAngle * Mathf.Deg2Rad, 0f).normalized;
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
