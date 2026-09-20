namespace Landoria.HuginnCam
{
    // Logs only transitions between the effective prioritized behaviors.
    internal sealed class HuginnCamBehaviorLogger
    {
        private string _current;

        // Resolves and logs the behavior that currently controls Huginn.
        internal void Update(
            bool avoidingObstacle, bool freedomFlight, bool catchUpFlight,
            HuginnCamBehaviorController controller)
        {
            string next = avoidingObstacle
                ? "ObstacleAvoidance"
                : freedomFlight
                    ? "FreedomFlight"
                    : catchUpFlight
                        ? "CatchUpFlight"
                        : controller.ActiveBehaviorName;
            if (next == _current)
            {
                return;
            }

            string previous = _current ?? "None";
            _current = next;
            HuginnCamPlugin.Log.LogInfo(
                $"Huginn behavior changed: {previous} -> {next}");
        }
    }
}
