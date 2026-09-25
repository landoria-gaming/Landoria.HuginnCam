using HarmonyLib;

namespace Landoria.SagaCapture
{
    // Stops recording when the player selects the logout menu item.
    [HarmonyPatch(typeof(Menu), nameof(Menu.OnLogout))]
    internal static class SagaCaptureLogoutMenuPatch
    {
        // Starts recorder finalization before Valheim handles the logout action.
        private static void Prefix()
        {
            SagaCapturePlugin.StopRecordingForMenuExit();
        }
    }
}
