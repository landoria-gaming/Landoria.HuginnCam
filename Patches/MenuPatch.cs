using HarmonyLib;
using UnityEngine;

namespace Landoria.SagaCapture
{
    // Prevents Escape from opening Valheim's menu while PreviewMode exits.
    [HarmonyPatch(typeof(Menu), "Update")]
    internal static class SagaCaptureMenuPatch
    {
        private static int _suppressedFrame = -1;

        // Marks Escape as consumed by PreviewMode for the current frame.
        internal static void SuppressThisFrame()
        {
            _suppressedFrame = Time.frameCount;
        }

        // Skips menu input when Escape belongs to PreviewMode.
        private static bool Prefix()
        {
            bool consumed = _suppressedFrame == Time.frameCount;
            bool activeEscape = SagaCapturePlugin.IsPreviewModeActive &&
                                ZInput.GetKeyDown(KeyCode.Escape);
            return !consumed && !activeEscape;
        }
    }

    // Stops recording when the player selects the quit menu item.
    [HarmonyPatch(typeof(Menu), nameof(Menu.OnQuit))]
    internal static class SagaCaptureQuitMenuPatch
    {
        // Starts recorder finalization before Valheim opens the confirmation dialog.
        private static void Prefix()
        {
            SagaCapturePlugin.StopRecordingForMenuExit();
        }
    }

    // Stops recording when the player selects the disconnect menu item.
    [HarmonyPatch(typeof(Menu), nameof(Menu.OnLogout))]
    internal static class SagaCaptureLogoutMenuPatch
    {
        // Starts recorder finalization before Valheim opens the confirmation dialog.
        private static void Prefix()
        {
            SagaCapturePlugin.StopRecordingForMenuExit();
        }
    }
}
