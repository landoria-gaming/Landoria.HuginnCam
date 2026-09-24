using HarmonyLib;

namespace Landoria.SagaCapture
{
    // Reduces spawned footstep and landing sounds without affecting other SFX.
    [HarmonyPatch(typeof(FootStep), "SetEffectCreator")]
    internal static class SagaCaptureFootstepVolumePatch
    {
        private const float VolumeMultiplier = 0.6f;

        // Applies the reduction after Valheim has initialized the step effect.
        private static void Postfix(UnityEngine.GameObject go)
        {
            if (!SagaCapturePlugin.IsCinematicImageActive)
            {
                return;
            }
            foreach (ZSFX sound in go.GetComponentsInChildren<ZSFX>())
            {
                sound.SetVolumeModifier(
                    sound.GetVolumeModifier() * VolumeMultiplier);
            }
        }
    }
}
