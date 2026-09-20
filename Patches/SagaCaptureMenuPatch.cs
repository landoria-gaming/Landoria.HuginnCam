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
}
