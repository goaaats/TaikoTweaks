using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using OpCodes = System.Reflection.Emit.OpCodes;

namespace TaikoTweaks.RankedMatch;

[HarmonyPatch(typeof(EnsoGameManager))]
[HarmonyPatch("SetResults")]
public class RankedMatchScoreSavePatch
{
    public static ManualLogSource Log => Plugin.Log;

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = new List<CodeInstruction>(instructions);
        var tEnsoGameManager = typeof(EnsoGameManager);
        var ensoParamField = tEnsoGameManager.GetField("ensoParam", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var tEnsoPlayingParameter = typeof(EnsoPlayingParameter);
        var isOnlineRankedMatchFunc = tEnsoPlayingParameter.GetMethod("get_IsOnlineRankedMatch", BindingFlags.Instance | BindingFlags.Public)!;

        for (var i = 0; i < code.Count; i++)
        {
            if (i >= code.Count - 4)
                throw new Exception("Reached end of method without finding code to patch!");

            if (!code[i].IsLdarg(0))
                continue;

            if (!code[i + 1].LoadsField(ensoParamField))
                continue;

            if (!code[i + 2].Calls(isOnlineRankedMatchFunc))
                continue;

            if (code[i + 3].opcode != OpCodes.Brtrue)
                continue;

            code[i].opcode = OpCodes.Nop; // IL_02e0: ldarg.0
            code[i + 1].opcode = OpCodes.Nop; // IL_02e1: ldfld class EnsoPlayingParameter EnsoGameManager::ensoParam
            code[i + 2].opcode = OpCodes.Nop; // IL_02e6: callvirt instance bool EnsoPlayingParameter::get_IsOnlineRankedMatch()
            code[i + 3].opcode = OpCodes.Nop; // IL_02eb: brtrue IL_03a7

            Log.LogInfo("[RankedMatchScoreSavePatch] Patched SetResults to save scores for online matches");

            return code.AsEnumerable();
        }

        throw new Exception("Could not find code to patch!");
    }
}