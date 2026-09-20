namespace Landoria.HuginnCam
{
    // Describes the speed envelope owned by one flight behavior.
    internal struct HuginnCamFlightProfile
    {
        internal readonly float MinimumCruiseSpeed;
        internal readonly float MaximumCruiseSpeed;
        internal readonly float MaximumSpeed;
        internal readonly float CatchUpDistance;
        internal readonly float CatchUpSpeedPerMeter;
        internal readonly float SpeedChangeRate;

        // Creates one immutable behavior-specific speed envelope.
        internal HuginnCamFlightProfile(
            float minimumCruiseSpeed, float maximumCruiseSpeed,
            float maximumSpeed, float catchUpDistance,
            float catchUpSpeedPerMeter, float speedChangeRate)
        {
            MinimumCruiseSpeed = minimumCruiseSpeed;
            MaximumCruiseSpeed = maximumCruiseSpeed;
            MaximumSpeed = maximumSpeed;
            CatchUpDistance = catchUpDistance;
            CatchUpSpeedPerMeter = catchUpSpeedPerMeter;
            SpeedChangeRate = speedChangeRate;
        }
    }
}
