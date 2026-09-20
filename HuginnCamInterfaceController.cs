namespace Landoria.HuginnCam
{
    // Hides the Valheim interface and restores its previous visibility state.
    internal sealed class HuginnCamInterfaceController
    {
        private bool _previousHiddenState;
        private bool _stateCaptured;

        // Hides the interface while preserving its state before Huginn camera mode.
        internal void Hide()
        {
            if (!Hud.instance)
            {
                return;
            }

            if (!_stateCaptured)
            {
                _previousHiddenState = Hud.IsUserHidden();
                _stateCaptured = true;
            }

            Hud.instance.m_userHidden = true;
        }

        // Restores the interface state captured before Huginn camera mode.
        internal void Restore()
        {
            if (_stateCaptured && Hud.instance)
            {
                Hud.instance.m_userHidden = _previousHiddenState;
            }

            _stateCaptured = false;
        }
    }
}
