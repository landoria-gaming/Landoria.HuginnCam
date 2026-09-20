using HarmonyLib;

namespace Landoria.HuginnCam
{
    // Suppresses only the local player's footstep effects in visible Huginn mode.
    [HarmonyPatch(typeof(FootStep), "RPC_Step")]
    internal static class HuginnCamFootstepPatch
    {
        internal static bool Muted { get; set; }

        // Skips local footstep playback while leaving every other character untouched.
        private static bool Prefix(FootStep __instance)
        {
            if (!Muted || Player.m_localPlayer == null)
            {
                return true;
            }

            Character character = __instance.GetComponent<Character>();
            return character != Player.m_localPlayer;
        }
    }
}
