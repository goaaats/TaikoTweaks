using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace TaikoTweaks.SongSelect;

/// <summary>
/// This patch adds a "quick scroll" mode to the song select that skips 10 songs at a time by double tapping up or down,
/// akin to the newer arcade titles.
/// </summary>
[HarmonyPatch(typeof(SongSelectManager))]
[HarmonyPatch("UpdateSongSelect")]
public class FastScrollPatch
{
    private static DateTimeOffset _lastOk;
    private static DateTimeOffset _lastTrigger;

    private static bool _scrollArmed;
    private static bool _scrollOk;

    // ReSharper disable once InconsistentNaming
    public static bool Prefix(SongSelectManager __instance)
    {
        if (__instance.CurrentState != SongSelectManager.State.SongSelect || __instance.SongList.Count <= 0)
            return true;

        var dir = TaikoSingletonMonoBehaviour<ControllerManager>.Instance.GetDirectionButton(ControllerManager.ControllerPlayerNo.Player1, ControllerManager.Prio.None);

        var isRelevantButton = dir is ControllerManager.Dir.Up or ControllerManager.Dir.Down;

        if (isRelevantButton && !_scrollArmed)
        {
            _scrollArmed = true;
        }

        if (!isRelevantButton && _scrollArmed)
        {
            _scrollOk = true;
            _scrollArmed = false;
            _lastOk = DateTimeOffset.Now;
        }

        const int delay = 120;
        var inDelayPeriod = (DateTimeOffset.Now - _lastOk).TotalMilliseconds < delay;
        if (isRelevantButton && _scrollOk && inDelayPeriod)
        {
            var songIndex = (dir == ControllerManager.Dir.Up ? __instance.SelectedSongIndex - 10 + __instance.SongList.Count : __instance.SelectedSongIndex + 10) % __instance.SongList.Count;

            __instance.SelectedSongIndex = songIndex;

            __instance.UpdateCenterKanbanSurface(true);
            __instance.kanbans[0].RootAnim.Play("SelectOn", 0, 1f);
            __instance.kanbans[0].EffectBonusL.InitAnim();
            __instance.sortBarView.ShowView();
            __instance.UpdateSortBarSurface();
            __instance.Score1PObject.FadeIn();
            if (__instance.status.Is2PActive)
            {
                __instance.Score2PObject.FadeIn();
            }
            __instance.UpdateScoreDisplay();
            __instance.SongSelectBg.SetGenre((EnsoData.SongGenre)__instance.SongList[__instance.SelectedSongIndex].SongGenre);

            __instance.UpdateKanbanSurface();
            __instance.isKanbanMoving = false;
            __instance.kanbanMoveCount = 0;

            __instance.PlayKanbanMoveAnim(SongSelectManager.KanbanMoveType.MoveEnded);

            _lastTrigger = DateTimeOffset.Now;

            TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MySoundManager.CommonSePlay("fast", false);

            _scrollArmed = false;
            _scrollOk = false;

            __instance.songPlayer.Stop(isImmediate: true);
            __instance.isSongPlaying = false;
            __instance.isSongLoadRequested = true;
        }
        else if (_scrollOk && !inDelayPeriod)
        {
            _scrollArmed = false;
            _scrollOk = false;
        }

        return !((DateTimeOffset.Now - _lastTrigger).TotalMilliseconds < 500);
    }
}