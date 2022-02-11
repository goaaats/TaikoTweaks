using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using RankedMatch;

namespace TaikoTweaks.RankedMatch;

public class RankedMatchNetworkDlcPatch
{
    public static ManualLogSource Log => Plugin.Log;

    [HarmonyPatch(typeof(RankedMatchNetworkManager))]
    [HarmonyPatch("GetPlayMusicInfo")]
    [HarmonyPrefix]
    public static bool Prefix(RankedMatchNetworkManager __instance, ref bool __result, out MusicData musicData)
    {
        var rankedMatchStatus = Traverse.Create(__instance).Field("status").GetValue() as RankedMatchStatus;
        if (rankedMatchStatus == null)
        {
            throw new Exception("RankedMatchStatus was null");
        }

        Log.LogInfo($"[RankedMatchNetworkDlcPatch] CALLED: {rankedMatchStatus.CurrentMatchingType}");

        // Always return the original method for the ranked match
        if (!EnsoData.IsFriendMatch(rankedMatchStatus.CurrentMatchingType))
        {
            musicData = default;
            return true;
        }

        TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.PlayData.GetRankmatchPlayHistory(0, out var dst);
        var list = new List<int>();

        foreach (var musicInfo in rankedMatchStatus.GetMusicInfoList())
        {
            if (musicInfo.IsDLC) // Note: We should probably also check if the DLC is owned, in the future? There doesn't seem to be a way to do this yet.
                list.Add(musicInfo.UniqueId);
        }

        Log.LogInfo($"[RankedMatchNetworkDlcPatch] Networking DLC songs: {list.Count}");

        musicData = new MusicData(dst, list.ToArray());
        __result = true;
        return false;
    }
}