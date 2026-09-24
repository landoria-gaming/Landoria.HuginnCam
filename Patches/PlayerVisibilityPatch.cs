using HarmonyLib;

namespace Landoria.SagaCapture
{
    // Keeps the local player's model visible to the secondary camera.
    [HarmonyPatch(typeof(Character), "SetVisible")]
    internal static class SagaCapturePlayerVisibilityPatch
    {
        // Ignores Valheim's close-camera hiding while the cinematic camera is active.
        private static bool Prefix(Character __instance, bool visible)
        {
            return visible || __instance != Player.m_localPlayer ||
                   !SagaCapturePlugin.IsCinematicCameraActive;
        }
    }
}
