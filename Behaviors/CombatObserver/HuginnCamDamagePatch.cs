using HarmonyLib;

namespace Landoria.HuginnCam
{
    // Detects damage actually applied by the local player after Valheim resolves it.
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class HuginnCamDamagePatch
    {
        // Captures target health before Valheim processes the incoming hit.
        private static void Prefix(Character __instance, out float __state)
        {
            __state = __instance.GetHealth();
        }

        // Reports only confirmed health loss attributed to the local player.
        private static void Postfix(
            Character __instance, HitData hit, float __state)
        {
            Player player = Player.m_localPlayer;
            if (player == null || hit == null ||
                __instance.GetHealth() >= __state - 0.01f)
            {
                return;
            }

            Character attacker = hit.GetAttacker();
            if (__instance == player && attacker != null && attacker != player)
            {
                HuginnCamCombatEvents.ReportDamageReceived(attacker);
            }
            else if (__instance != player && attacker == player)
            {
                HuginnCamCombatEvents.ReportDamageDealt(__instance);
            }
        }
    }
}
