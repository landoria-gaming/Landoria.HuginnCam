using UnityEngine;

namespace Landoria.HuginnCam
{
    // Shares confirmed local-player damage events with CombatObserver.
    internal static class HuginnCamCombatEvents
    {
        internal static float LastDamageDealtTime { get; private set; } = -1f;
        internal static Character LastDamagedOpponent { get; private set; }
        internal static float LastDamageReceivedTime { get; private set; } = -1f;
        internal static Character LastAttacker { get; private set; }

        // Records one confirmed health loss caused by the local player.
        internal static void ReportDamageDealt(Character opponent)
        {
            LastDamageDealtTime = Time.time;
            LastDamagedOpponent = opponent;
        }

        // Records one confirmed health loss caused by another character.
        internal static void ReportDamageReceived(Character attacker)
        {
            LastDamageReceivedTime = Time.time;
            LastAttacker = attacker;
        }
    }
}
