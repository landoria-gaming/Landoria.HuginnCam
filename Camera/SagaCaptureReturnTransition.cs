using UnityEngine;

namespace Landoria.SagaCapture
{
    // Reproduces FreeFly's smooth three-stage return to gameplay.
    internal sealed class SagaCaptureReturnTransition
    {
        private const float Duration = 3f;
        private const float InitialTurnDuration = 1.5f;
        private const float FinalTurnDuration = 1f;
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Vector3 _lookTarget;
        private float _elapsed;

        // Captures the visible camera pose and player look target.
        internal void Start(Camera camera, Player player)
        {
            _startPosition = camera.transform.position;
            _startRotation = camera.transform.rotation;
            _lookTarget = player != null
                ? player.m_eye.position
                : _startPosition + camera.transform.forward;
            _elapsed = 0f;
        }

        // Advances the return and reports when gameplay can take over.
        internal bool Step(
            Camera camera, Transform gameplayCamera, float deltaTime)
        {
            _elapsed = Mathf.Min(_elapsed + deltaTime, Duration);
            float progress = _elapsed / Duration;
            Vector3 position = Vector3.Lerp(
                _startPosition, gameplayCamera.position, Ease(progress));
            Quaternion facing = LookAt(position);
            float initialTurn = Ease(Mathf.Clamp01(
                _elapsed / InitialTurnDuration));
            Quaternion playerFacing = Quaternion.Slerp(
                _startRotation, facing, initialTurn);
            float finalTurn = Ease(Mathf.Clamp01(
                (_elapsed - (Duration - FinalTurnDuration)) /
                FinalTurnDuration));
            Quaternion rotation = Quaternion.Slerp(
                playerFacing, gameplayCamera.rotation, finalTurn);
            camera.transform.SetPositionAndRotation(position, rotation);
            return _elapsed >= Duration;
        }

        // Returns a rotation facing the player during the approach.
        private Quaternion LookAt(Vector3 position)
        {
            Vector3 direction = _lookTarget - position;
            return direction.sqrMagnitude > Mathf.Epsilon
                ? Quaternion.LookRotation(direction, Vector3.up)
                : _startRotation;
        }

        // Smooths acceleration and deceleration like Landoria.FreeFly.
        private static float Ease(float progress)
        {
            return progress * progress * progress *
                   (progress * (progress * 6f - 15f) + 10f);
        }
    }
}
