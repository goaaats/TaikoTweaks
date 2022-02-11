using HarmonyLib;

namespace TaikoTweaks.SongSelect;

/// <summary>
/// This patch prevents the game from advancing to the course select after rolling a random song
/// </summary>
[HarmonyPatch(typeof(SongSelectManager))]
[HarmonyPatch("UpdateRandomSelect")]
public class RandomRepeatPatch
{
    // ReSharper disable once InconsistentNaming
    private static bool Prefix(SongSelectManager __instance)
    {
        if (__instance.currentRandomSelectState == SongSelectManager.RandomSelectState.DecideSong)
        {
            __instance.currentRandomSelectState = SongSelectManager.RandomSelectState.Prepare;
            __instance.ChangeState(SongSelectManager.State.SongSelect);
            __instance.isSongLoadRequested = true;

            return false; // Don't call original method
        }

        return true;
    }
}