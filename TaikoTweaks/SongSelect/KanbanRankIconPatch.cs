using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using DG.Tweening;
using HarmonyLib;
using MessagePack.Formatters;
using PlayFab.Internal;
using SongSelect;
using UnityEngine;
using UnityEngine.SceneManagement;
using Image = UnityEngine.UI.Image;
using Object = UnityEngine.Object;

namespace TaikoTweaks.SongSelect;

/// <summary>
/// This patch prevents the game from advancing to the course select after rolling a random song
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
public class KanbanRankIconPatch
{
    private static readonly Dictionary<string, Animator> CrownIcons = new();
    private static readonly Dictionary<EnsoData.EnsoLevelType, Sprite> LevelIcons = new();

    private static AssetBundle _iconsAssetBundle;

    [HarmonyPatch(typeof(SongSelectManager))]
    [HarmonyPatch("Start")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPostfix]
    public static void Start_Postfix(SongSelectManager __instance)
    {
        CrownIcons.Clear();

        if (_iconsAssetBundle == null)
        {
            var assembly = typeof(KanbanRankIconPatch).Assembly;
            using var stream = assembly.GetManifestResourceStream("TaikoTweaks.Resources.difficons.assets");
            _iconsAssetBundle = AssetBundle.LoadFromStream(stream);

            for (var i = 1; i < 6; i++)
            {
                var sprite = _iconsAssetBundle.LoadAsset<Sprite>($"icon_result_00{i}");
                if (sprite == null)
                    throw new Exception("Could not load difficulty icons.");

                LevelIcons.Add((EnsoData.EnsoLevelType) i - 1, sprite);
            }
        }
    }

    [HarmonyPatch(typeof(SongSelectManager))]
    [HarmonyPatch("OnDestroy")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPostfix]
    public static void Destroy_Postfix(SongSelectManager __instance)
    {
        CrownIcons.Clear();
    }

    private class ScaleWithParent : MonoBehaviour
    {
        private void Update()
        {
            var kanban = transform.parent.parent.parent.GetComponent<SongSelectKanban>();
            var state = kanban.RootAnim.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("SelectOn"))
            {
                transform.localScale = new Vector3(0, 0, 0);
            }
            else
            {
                transform.localScale = transform.parent.localScale - new Vector3(0.2f, 0.2f, 0.2f);
            }
        }
    }

    [HarmonyPatch(typeof(SongSelectKanban))]
    [HarmonyPatch("UpdateDisplay")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPostfix]
    public static void UpdateDisplay_Postfix(SongSelectKanban __instance, in SongSelectManager.Song song)
    {
        if (!CrownIcons.TryGetValue(__instance.name, out var animator))
        {
            var crownImageObj = __instance.transform.GetChild(0).GetChild(16).GetChild(0).GetChild(2);
            var clonedCrownObj = Object.Instantiate(crownImageObj.gameObject, __instance.iconFavorite1P.transform, true);
            clonedCrownObj.AddComponent<ScaleWithParent>();
            clonedCrownObj.name = $"CrownIcon for {__instance.name}";

            var diffIconObj = new GameObject($"DiffIcon for {__instance.name}");
            diffIconObj.transform.parent = clonedCrownObj.transform;
            diffIconObj.transform.localPosition = new Vector3(13.3f ,-16f, 0);
            diffIconObj.transform.localScale = new Vector3(0.45f, 0.35f, 0.45f);

            var diffIconImage = diffIconObj.AddComponent<Image>();
            diffIconImage.sprite = LevelIcons[EnsoData.EnsoLevelType.Ura];
            diffIconImage.enabled = false;

            clonedCrownObj.transform.localPosition = new Vector3(1.48f, -31, 0);
            clonedCrownObj.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

            var rankImage = clonedCrownObj.GetComponent<Image>();
            animator = clonedCrownObj.GetComponent<Animator>();
            animator.Play("None");

            rankImage.color = Color.white;

            CrownIcons.Add(__instance.name, animator);
        }

        var diffIcon = animator.gameObject.transform.GetChild(0).GetComponent<Image>();

        if (song.HighScores != null)
        {
            var didAny = false;
            for (var i = 0; i < 5; i++)
            {
                if (song.HighScores[i].crown is DataConst.CrownType.None or DataConst.CrownType.Off)
                {
                    continue;
                }

                switch (song.HighScores[i].crown)
                {
                    case DataConst.CrownType.Silver:
                        animator.Play("Silver");
                        break;
                    case DataConst.CrownType.Gold:
                        animator.Play("Gold");
                        break;
                    case DataConst.CrownType.Rainbow:
                        animator.Play("Rainbow");
                        break;
                    default:
                        animator.Play("None");
                        break;
                }

                diffIcon.sprite = LevelIcons[(EnsoData.EnsoLevelType)i];
                diffIcon.enabled = true;
                didAny = true;
            }

            if (!didAny)
            {
                animator.Play("None");
                diffIcon.enabled = false;
            }
        }
        else
        {
            animator.Play("None");
            diffIcon.enabled = false;
        }
    }
}