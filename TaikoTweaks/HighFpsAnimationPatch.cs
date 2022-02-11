using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine;

namespace TaikoTweaks;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class HighFpsAnimationPatch
{
    #region SpriteAnimation

    private static Dictionary<int, float> _instanceSpeedDict = new();

    [HarmonyPatch(typeof(SpriteAnimation))]
    [HarmonyPatch("UpdateExp")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPostfix]
    public static void UpdateExp_Postfix(SpriteAnimation __instance)
    {
        if (!_instanceSpeedDict.TryGetValue(__instance.GetInstanceID(), out var speed))
            speed = 1.0f;

        // HACK: This always assumes that we can actually reach the FPS
        __instance.animationFrameRate = 60f / Application.targetFrameRate * speed;
    }

    [HarmonyPatch(typeof(SpriteAnimation))]
    [HarmonyPatch("UpdateAnimationSpeed")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPostfix]
    public static void UpdateAnimationSpeed_Postfix(SpriteAnimation __instance, float speed)
    {
        _instanceSpeedDict[__instance.GetInstanceID()] = speed;
    }

    #endregion

    #region OnpuJump

    /// <summary>
    /// We are basically reimplementing MatchingProcess here, seems like the cleanest way to go about it.
    /// </summary>
    [HarmonyPatch(typeof(OnpuJump), "JumpAnim")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPrefix]
    public static bool JumpAnim_Prefix(OnpuJump __instance, ref IEnumerator __result, OnpuJump.AnimeTypes type)
    {
	    IEnumerator NewJumpAnim()
	    {
		    __instance.SettingConfig(type);
		    yield return null;
		    for (var i = 0; i < 65; i++)
		    {
			    switch (type)
			    {
				    case OnpuJump.AnimeTypes.Geki:
				    {
					    if (i < 30)
					    {
						    Vector2 vector =  __instance.AdjustPos *  __instance.aspectAdjust_ +  __instance.jumpAncPos[i + 1];
						    __instance.trans[0].localPosition = new Vector3(vector.x, vector.y, 0f);
					    }

					    if (i == 29)
					    {
						    __instance.intoImage.color = new Color(1f, 1f, 1f, 1f);
						    __instance.intoImage.uvRect = new Rect(0f, 0f, 0.25f, 1f);
					    }

					    var b = i >= 30 && i < 32 ? 1f - (i - 29) * 0.5f : i < 32 ? 1f : 0f;
					    var g = i >= 30 && i < 45 ? 1f - (i - 29) * 0.06f : i < 45 ? 1f : 0.1f;
					    var r = i >= 33 && i < 45 ? 1f - 0.05f * ((i - 33) / 11f) :
						    i < 45 ? 1f : 0.95f - 0.01f * (i - 44);
					    var a = i >= 59 ? 0f : i >= 54 ? 1f - 0.2f * (i - 54) : 1f;
					    __instance.intoImage.color = new Color(r, g, b, a);
					    switch (i)
					    {
						    case 31:
							    __instance.intoImage.uvRect = new Rect(0.25f, 0f, 0.25f, 1f);
							    break;
						    case 33:
							    __instance.intoImage.uvRect = new Rect(0.5f, 0f, 0.25f, 1f);
							    break;
					    }

					    if (i >= 33)
					    {
						    var num = (i - 33) / 16f;
						    var num2 = 0.3f * num * num + 0.4f * num + 0.8f;
						    var z = -27f / 88f * (i - 33) * (i - 33) - 63f / 88f * (i - 33) + 45f;
						    __instance.rectTrans[1].rotation = Quaternion.Euler(0f, 0f, z);
						    __instance.rectTrans[1].localScale = new Vector3(num2, num2, 0f);
					    }

					    if (i == 59)  __instance.sprits[0].color = new Color(1f, 1f, 1f, 0f);
					    var b2 = 0f;
					    var a2 = i > 29 && i <= 59 ? (i - 29) / 30f : i <= 59 ? 0f : 1f - 0.2f * (i - 59);
					    if (i < 34)
						    b2 = 0f;
					    else if (i > 59)
						    b2 = 1f;
					    else if (i >= 34 && i <= 59) b2 = (i - 34) / 25f;
					    __instance.sprits[1].color = new Color(1f, 1f, b2, a2);
					    for (var k = 0; k <  __instance.particles.Count; k++)  __instance.ParticleAnimation(k, i);
					    if (i < 30)
					    {
						    var num3 = 66f - 116 * (i + 1) / 30f;
						    if ( __instance.rectTrans[4].localScale.y < 0f) num3 *= -1f;
						    __instance.rectTrans[3].rotation = Quaternion.Euler(0f, 0f, num3);
						    __instance.rectTrans[4].rotation = Quaternion.Euler(0f, 0f, 0f);
						    __instance.rectTrans[4].anchoredPosition =  __instance.rainInAncPos[i + 1];
					    }
					    else if (i < 39)
					    {
						    var num4 = -50f;
						    if ( __instance.rectTrans[4].localScale.y < 0f) num4 *= -1f;
						    __instance.rectTrans[3].rotation = Quaternion.Euler(0f, 0f, num4);
						    __instance.rectTrans[4].rotation = Quaternion.Euler(0f, 0f, 0f);
					    }
					    else if (i <= 59)
					    {
						    var num5 = (0f - 114 * (i - 39)) / 20f;
						    if ( __instance.rectTrans[4].localScale.y < 0f) num5 *= -1f;
						    __instance.rectTrans[4].rotation = Quaternion.Euler(0f, 0f, num5);
						    __instance.rectTrans[4].anchoredPosition =  __instance.rainOutAncPos[i - 39];
					    }

					    break;
				    }
				    case OnpuJump.AnimeTypes.Don:
				    case OnpuJump.AnimeTypes.Katsu:
				    case OnpuJump.AnimeTypes.DaiRendaDon:
				    case OnpuJump.AnimeTypes.DaiRendaKatsu:
					    if (i < 50)  __instance.JumpCommonAnimation(i);
					    if (i == 50)
					    {
						    __instance.state = OnpuJump.State.ParticleOff;
						    yield break;
					    }

					    break;
				    case OnpuJump.AnimeTypes.DaiDon:
				    case OnpuJump.AnimeTypes.DaiKatsu:
					    if (i < 50)
					    {
						    __instance.JumpCommonAnimation(i);
						    for (var j = 0; j < __instance.particles.Count; j++) __instance.ParticleAnimation(j, i);
					    }

					    if (i == 50) yield break;
					    break;
			    }

			    // NOTE: Instead of waiting one frame, we are waiting as many frames as needed to slow down the animation. All of the code in here is written with 60fps in mind.
			    for (var j = 0; j < Math.Floor(Application.targetFrameRate / 60.0f); j++)
			    {
				    yield return null;
			    }
		    }

		    for (var l = 0; l < __instance.particles.Count; l++)
			    __instance.particles[l].uvRect = new Rect(1f, 0f, 0.125f, 0.5f);

		    __instance.state = OnpuJump.State.ParticleOff;
	    }

        __result = NewJumpAnim();

        return false;
    }

    #endregion

    #region RelayScene

    [HarmonyPatch(typeof(RelayScene))]
    [HarmonyPatch("Update")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPrefix]
    private static bool RelayScene_Update_Prefix(RelayScene __instance)
    {
	    if (__instance.state == RelayScene.State.Wait)
	    {
		    var fixedWaitTime = (Application.targetFrameRate / 60.0f) * __instance.WaitTime;

		    __instance.stateTimer++;
		    if (__instance.stateTimer > fixedWaitTime)
		    {
			    __instance.ChangeState(__instance.DoUnloadResources ? RelayScene.State.Unload : RelayScene.State.GCCollect);
		    }
		    return false;
	    }

	    return true;
    }

    #endregion
}