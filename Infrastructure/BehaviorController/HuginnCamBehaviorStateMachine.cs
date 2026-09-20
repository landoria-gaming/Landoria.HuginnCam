namespace Landoria.HuginnCam
{
    // Resolves one primary behavior plus the obstacle-avoidance interruption.
    internal sealed class HuginnCamBehaviorStateMachine
    {
        private HuginnCamBehaviorState _state;
        private string _effectiveName;

        internal HuginnCamBehaviorState State => _state;

        // Selects the single primary state and logs effective transitions.
        internal void Update(
            HuginnCamBehaviorController controller,
            HuginnCamFreedomFlight freedomFlight,
            HuginnCamCatchUpFlight catchUpFlight,
            HuginnCamObstacleAvoidance obstacleAvoidance)
        {
            _state = freedomFlight.IsActive
                ? HuginnCamBehaviorState.FreedomFlight
                : controller.IsDangerActive
                    ? HuginnCamBehaviorState.CombatObserver
                    : controller.IsResting
                        ? HuginnCamBehaviorState.Resting
                        : controller.IsLanding
                            ? HuginnCamBehaviorState.Landing
                            : catchUpFlight.IsActive
                                ? HuginnCamBehaviorState.CatchUpFlight
                                : controller.IsPlayerMoving
                                    ? HuginnCamBehaviorState.TrailingFlight
                                    : HuginnCamBehaviorState.OrbitFlight;
            string effectiveName = obstacleAvoidance.IsActive
                ? "ObstacleAvoidance"
                : _state.ToString();
            LogTransition(effectiveName);
        }

        // Returns whether one primary behavior currently owns the camera.
        internal bool Is(HuginnCamBehaviorState state)
        {
            return _state == state;
        }

        // Selects the profile of the interruption or active primary state.
        internal HuginnCamFlightProfile GetProfile(
            HuginnCamBehaviorController controller,
            HuginnCamFreedomFlight freedomFlight,
            HuginnCamCatchUpFlight catchUpFlight,
            HuginnCamObstacleAvoidance obstacleAvoidance)
        {
            if (obstacleAvoidance.IsActive)
            {
                return obstacleAvoidance.Profile;
            }
            if (_state == HuginnCamBehaviorState.FreedomFlight)
            {
                return freedomFlight.Profile;
            }
            if (_state == HuginnCamBehaviorState.CatchUpFlight)
            {
                return catchUpFlight.Profile;
            }
            return controller.Profile;
        }

        // Writes one line only when the effective behavior changes.
        private void LogTransition(string next)
        {
            if (next == _effectiveName)
            {
                return;
            }

            string previous = _effectiveName ?? "None";
            _effectiveName = next;
            HuginnCamPlugin.Log.LogInfo(
                $"Huginn behavior changed: {previous} -> {next}");
        }
    }
}
