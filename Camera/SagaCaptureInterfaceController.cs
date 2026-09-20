namespace Landoria.SagaCapture
{
    // Hides the Valheim interface and restores its previous visibility state.
    internal sealed class SagaCaptureInterfaceController
    {
        private bool _previousHiddenState;
        private bool _stateCaptured;

        // Hides the interface while preserving its previous state.
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

        // Restores the interface state captured before camera mode.
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
