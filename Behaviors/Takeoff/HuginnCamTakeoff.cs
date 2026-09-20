using UnityEngine;

namespace Landoria.HuginnCam
{
    // Launches Huginn toward the clearest nearby air corridor.
    internal sealed class HuginnCamTakeoff
    {
        private const float HorizontalDistance = 5f;
        private const float SafeHeight = 3f;
        private const float CompletionDistance = 0.75f;
        private const float MinimumSpeed = 0.4f;
        private const float MaximumCruiseSpeed = 0.8f;
        private const float MaximumSpeed = 1.5f;
        private Vector3 _target;

        internal bool IsActive { get; private set; }
        internal Vector3 Direction { get; private set; }
        internal HuginnCamFlightProfile Profile => new HuginnCamFlightProfile(
            MinimumSpeed, MaximumCruiseSpeed, MaximumSpeed,
            float.MaxValue, 0f, 0.6f);

        // Selects a clear rising path from the current grounded position.
        internal void Begin(Vector3 position, Vector3 preferredDirection)
        {
            Direction = HuginnCamTreeAvoidance.ChooseTakeoffDirection(
                position, preferredDirection, HorizontalDistance);
            _target = position + Direction * HorizontalDistance +
                      Vector3.up * SafeHeight;
            IsActive = true;
        }

        // Returns the fixed takeoff target and completes near safe open air.
        internal Vector3 Update(Vector3 position)
        {
            if (IsActive && Vector3.Distance(position, _target) <=
                CompletionDistance)
            {
                IsActive = false;
            }

            return _target;
        }
    }
}
