using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using SongSelect;

namespace TaikoTweaks.SongSelect;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class MissingDifficultiesPatch
{
    public static ManualLogSource Log => Plugin.Log;

    [HarmonyPatch(typeof(SongSelectKanban))]
    [HarmonyPatch("UpdateDisplay")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPostfix]
    public static void UpdateDisplay_Postfix(SongSelectKanban __instance, in SongSelectManager.Song song)
    {
        // Kanban => Contents => DiffCourse
        var diffCourse = __instance.transform.GetChild(0).GetChild(16);

        if (diffCourse.name != "DiffCourse")
        {
            Log.LogError("DiffCourse not found! This might mean that this plugin is outdated or incompatible with your current game version.");
            return;
        }

        // We can ignore ura here, the game checks for it
        var easy = diffCourse.GetChild(0);
        var normal = diffCourse.GetChild(1);
        var hard = diffCourse.GetChild(2);
        var mania = diffCourse.GetChild(3);

        var isExistEasy = song.Stars[0] > 0;
        var isExistNormal = song.Stars[1] > 0;
        var isExistHard = song.Stars[2] > 0;
        var isExistMania = song.Stars[3] > 0;

        easy.gameObject.SetActive(isExistEasy);
        normal.gameObject.SetActive(isExistNormal);
        hard.gameObject.SetActive(isExistHard);
        mania.gameObject.SetActive(isExistMania);
    }

    [HarmonyPatch(typeof(CourseSelect))]
    [HarmonyPatch("SelectCourse")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPrefix]
    public static bool SelectCourse_Prefix(CourseSelect __instance, int course)
    {
        // We have to grab the song here because star info isn't copied onto the object on CourseSelect
        var song = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData.musicInfoAccessers
            .First(x => x.Id == __instance.selectedSongInfo.Id);

        var isExistEasy = song.Stars[0] > 0;
        var isExistNormal = song.Stars[1] > 0;
        var isExistHard = song.Stars[2] > 0;
        var isExistMania = song.Stars[3] > 0;

        if (!isExistEasy && course == 0)
            return false;

        if (!isExistNormal && course == 1)
            return false;

        if (!isExistHard && course == 2)
            return false;

        if (!isExistMania && course == 3)
            return false;

        return true;
    }

    [HarmonyPatch(typeof(CourseSelect))]
    [HarmonyPatch("SetInfo")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPostfix]
    public static void SetInfo_Postfix(CourseSelect __instance, MusicDataInterface.MusicInfoAccesser song, bool isRandomSelect, bool isDailyBonus)
    {
        var diffCourse = __instance.transform.GetChild(0).GetChild(0).GetChild(6);

        if (diffCourse.name != "DiffCourse")
        {
            Log.LogError("DiffCourse not found! This might mean that this plugin is outdated or incompatible with your current game version.");
            return;
        }

        var easy = diffCourse.GetChild(0);
        var normal = diffCourse.GetChild(1);
        var hard = diffCourse.GetChild(2);
        var mania = diffCourse.GetChild(3);

        var isExistEasy = song.Stars[0] > 0;
        var isExistNormal = song.Stars[1] > 0;
        var isExistHard = song.Stars[2] > 0;
        var isExistMania = song.Stars[3] > 0;

        easy.gameObject.SetActive(isExistEasy);
        normal.gameObject.SetActive(isExistNormal);
        hard.gameObject.SetActive(isExistHard);
        mania.gameObject.SetActive(isExistMania);

        if (song.Stars[__instance.selectedCourse] == 0)
        {
            if (isExistMania)
            {
                __instance.selectedCourse = 3;
                __instance.selectedCourse2P = 3;
                __instance.courseIcon.PlayCourseAnim(3);
                __instance.courseIcon2P.PlayCourseAnim(3);
            }
            else if (isExistHard)
            {
                __instance.selectedCourse = 2;
                __instance.selectedCourse2P = 2;
                __instance.courseIcon.PlayCourseAnim(2);
                __instance.courseIcon2P.PlayCourseAnim(2);
            }
            else if (isExistNormal)
            {
                __instance.selectedCourse = 1;
                __instance.selectedCourse2P = 1;
                __instance.courseIcon.PlayCourseAnim(1);
                __instance.courseIcon2P.PlayCourseAnim(1);
            }
            else if (isExistEasy)
            {
                __instance.selectedCourse = 0;
                __instance.selectedCourse2P = 0;
                __instance.courseIcon.PlayCourseAnim(0);
                __instance.courseIcon2P.PlayCourseAnim(0);
            }
        }

        __instance.isRequestedCourseViewUpdate = true;
        __instance.isCursorPositionUpdated1P = true;
        __instance.isCursorPositionUpdated2P = true;
    }
}