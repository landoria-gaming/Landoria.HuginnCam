namespace Landoria.HuginnCam
{
    // Temporarily accelerates Huginn after it falls too far behind its target.
    internal sealed class HuginnCamCatchUpFlight
    {
        private const float ActivationDistance = 5f;
        private const float ReleaseDistance = 2.5f;
        private const float MinimumSpeed = 1f;
        private const float MaximumCruiseSpeed = 2f;
        private const float MaximumSpeed = 6f;
        private const float SpeedPerMeter = 0.75f;
        private const float AccelerationRate = 1.5f;

        internal bool IsActive { get; private set; }
        internal HuginnCamFlightProfile Profile => new HuginnCamFlightProfile(
            MinimumSpeed, MaximumCruiseSpeed, MaximumSpeed,
            ActivationDistance, SpeedPerMeter, AccelerationRate);

        // Enters with a large delay and exits only after most delay is recovered.
        internal void Update(float targetDistance, bool allowed)
        {
            if (!allowed)
            {
                IsActive = false;
                return;
            }

            if (IsActive)
            {
                IsActive = targetDistance > ReleaseDistance;
            }
            else
            {
                IsActive = targetDistance > ActivationDistance;
            }
        }
    }
}
