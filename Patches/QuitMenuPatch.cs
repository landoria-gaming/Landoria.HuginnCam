using HarmonyLib;

namespace Landoria.SagaCapture
{
    // Stops recording when the player selects the quit menu item.
    [HarmonyPatch(typeof(Menu), nameof(Menu.OnQuit))]
    internal static class SagaCaptureQuitMenuPatch
    {
        // Starts recorder finalization before Valheim handles the quit action.
        private static void Prefix()
        {
            SagaCapturePlugin.StopRecordingForMenuExit();
        }
    }
}
