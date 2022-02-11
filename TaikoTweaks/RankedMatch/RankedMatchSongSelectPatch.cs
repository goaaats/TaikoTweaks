using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using Microsoft.Xbox;
using OnlineManager;
using RankedMatch;
using UnityEngine;

namespace TaikoTweaks.RankedMatch;

public class RankedMatchSongSelectPatch
{
	public const int MARKER_HAS_TAIKOTWEAKS = -999999;

    public static ManualLogSource Log => Plugin.Log;

    private static RankedMatchSongSelect _rankedMatchSongSelect;
    private static bool _matchedHasTaikoTweaks = false;
    private static bool _isFriendMatching = false;

    [HarmonyPatch(typeof(RankedMatchSceneManager), "Start")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPostfix]
    public static void Start_Postfix(RankedMatchSceneManager __instance)
    {
	    var go = new GameObject("RankedMatchSongSelect");

	    _rankedMatchSongSelect = go.AddComponent<RankedMatchSongSelect>();
	    _rankedMatchSongSelect.SceneManager = __instance;

	    /*
	    _rankedMatchSongSelect.SetMusicChoices(TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData.musicInfoAccessers.OrderBy(info => info.UniqueId).ToList());
	    _rankedMatchSongSelect.Mode = RankedMatchSongSelect.SongSelectMode.Song;
	    _rankedMatchSongSelect.IsActive = true;
	    */
    }

    /// <summary>
    /// We are basically reimplementing MatchingProcess here, seems like the cleanest way to go about it.
    /// </summary>
    [HarmonyPatch(typeof(RankedMatchSceneManager), "MatchingProcess")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPrefix]
    public static bool MatchingProcess_Prefix(RankedMatchSceneManager __instance, ref IEnumerator __result)
    {
	    var status = Traverse.Create(__instance).Field("status").GetValue<RankedMatchStatus>();
	    var networkManager = Traverse.Create(__instance).Field("networkManager").GetValue<RankedMatchNetworkManager>();
	    var setting = Traverse.Create(__instance).Field("setting").GetValue<RankedMatchSetting>();
	    var inputManager = Traverse.Create(__instance).Field("inputManager").GetValue<RankedMatchInputManager>();
	    var songPlayer = Traverse.Create(__instance).Field("songPlayer").GetValue<RankedMatchSongPlayer>();
	    var voicePlayer = Traverse.Create(__instance).Field("voicePlayer").GetValue<RankedMatchSoundPlayer>();

	    IEnumerator NewMatchingProcess()
	    {
		    /* =========== MINE =============== */
		    _matchedHasTaikoTweaks = false;
		    _isFriendMatching = false;
		    /* ================================ */

		    /* ================================= */
		    /* ====== Set-up Networking ======== */
		    /* ================================= */

		    var playDataManager = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.PlayData;
		    var isDone = false;
		    var isSucceeded = false;
		    var errorMessage = "";
		    __instance.userInterfaceObjectReadyMessage = "";
		    status.CurrentMatchingState = MatchingState.Initializing;
		    status.MatchingSongUniqueId = -1;
		    if (!networkManager.IsAcceptedRematch())
		    {
			    isDone = false;
			    networkManager.StartCleaningUpPlayFab(delegate { isDone = true; });
			    yield return new WaitUntil(() => isDone);
			    isDone = false;
			    networkManager.StartInitializingOnlineManager(delegate(bool result)
			    {
				    isDone = true;
				    isSucceeded = result;
			    });
			    yield return new WaitUntil(() => isDone);
			    if (!isSucceeded)
			    {
				    status.CurrentMatchingErrorType = ErrorType.NetworkLight;
				    if (networkManager.IsReceivedInvitation()) networkManager.ResetReceivedInvitationFlag();
				    if (networkManager.IsAcceptedRematch()) networkManager.ResetAcceptedRematchFlag();
				    yield break;
			    }
		    }

		    isDone = false;
		    networkManager.StartRefleshNetworkTime(delegate(bool result)
		    {
			    isDone = true;
			    isSucceeded = result;
		    });
		    yield return new WaitUntil(() => isDone);
		    if (!isSucceeded)
		    {
			    status.CurrentMatchingErrorType = ErrorType.NetworkLight;
			    if (networkManager.IsReceivedInvitation()) networkManager.ResetReceivedInvitationFlag();
			    if (networkManager.IsAcceptedRematch()) networkManager.ResetAcceptedRematchFlag();
			    yield break;
		    }

		    /* ================================= */
		    /* ======== Find Player ============ */
		    /* ================================= */

		    DateTime currentTime = status.GetCurrentTime();
		    if (status.GetSeasonInfo(currentTime, out var info)) playDataManager.Rankmatch_SeasonId = info.SeasonId;
		    playDataManager.RankMatch_IsCoinUp = status.TimeEventRemainingSpan.TotalSeconds > 0.0;
		    networkManager.InitializeSeasonRankPoint(currentTime, shouldSync: true);
		    networkManager.GetSeasonPlayerInfo(0, out var playerData, out var _, out var _);
		    status.MatchingPlayer1Data = playerData;
		    networkManager.GetPlayMusicInfo(out var musicData);
		    status.MatchingPlayer1Music = musicData;
		    TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.PlayData.GetEnsoMode(out var dst);
		    status.MatchingPlayer1Setting =
			    new EnsoSetting(dst, EnsoData.EnsoLevelType.Easy, status.CurrentMatchingType);
		    status.CurrentMatchingState = MatchingState.SearchingPlayer;
		    var startTime = Time.time;
		    isDone = false;
		    errorMessage = "";

		    if (EnsoData.IsFriendMatch(status.CurrentMatchingType))
		    {
			    /* =========== MINE =============== */
			    _isFriendMatching = true;
			    /* ================================ */

			    if (networkManager.IsAcceptedRematch())
			    {
				    Log.LogInfo("[MatchingProcess] Is Accepted Rematch");

				    isDone = true;
				    isSucceeded = true;
				    networkManager.ResetAcceptedRematchFlag();
			    }
			    else if (networkManager.IsReceivedInvitation())
			    {
				    Log.LogInfo("[MatchingProcess] Start joining friend");

				    isDone = false;
				    networkManager.StartJoiningFriend(delegate(bool result)
				    {
					    isDone = true;
					    isSucceeded = result;
					    if (!isSucceeded) errorMessage = "not joined";
				    });
				    networkManager.ResetReceivedInvitationFlag();
			    }
			    else
			    {
				    Log.LogInfo("[MatchingProcess] Start searching friend");

				    isDone = false;
				    networkManager.StartInvitingFriend(delegate(bool result, string message)
				    {
					    isDone = true;
					    isSucceeded = result;
					    errorMessage = message;
				    });
			    }
		    }
		    else
		    {
			    yield return new WaitForSeconds(setting.matchingWaitingSec);
			    networkManager.StartSearchingPlayer(status.MatchingPlayer1Data.rankType, delegate(bool result)
			    {
				    isDone = true;
				    isSucceeded = result;
			    });
		    }

		    yield return new WaitUntil(() => isDone);
		    if (!isSucceeded)
		    {
			    if (errorMessage == "cancel")
				    inputManager.MoveToTopMenuFromMatching();
			    else if (errorMessage == "not joined")
				    status.CurrentMatchingErrorType = ErrorType.SessionHeavy;
			    else
				    status.CurrentMatchingErrorType = ErrorType.NetworkLight;
			    yield break;
		    }

		    /* ================================= */
		    /* ====== Transceive Account ======= */
		    /* ================================= */

		    Log.LogInfo("[MatchingProcess] Now transceive player info");

		    status.CurrentMatchingState = MatchingState.TransceivePlayerInfo;
		    isDone = false;
		    CustomStartTransceivePlayerInfo(networkManager, 1, delegate(bool result)
		    {
			    isDone = true;
			    isSucceeded = result;
		    });
		    yield return new WaitUntil(() => isDone);
		    if (!isSucceeded)
		    {
			    __instance.StartRematch();
			    yield break;
		    }

		    /* ================================= */
		    /* ==== Transceive Song & Diff ===== */
		    /* ================================= */

		    if (status.CurrentMatchingType == EnsoData.RankMatchType.RankMatch &&
		        status.MatchingPlayer2Setting.matchingType == EnsoData.RankMatchType.FriendInvited)
		    {
			    status.CurrentMatchingType = EnsoData.RankMatchType.FriendInviting;
			    status.CurrentTopMenuButtonState = TopMenuButtonState.MatchingFriend;
		    }

		    status.CurrentMatchingState = MatchingState.TransceiveSongInfo;
		    if (networkManager.IsMatchingHost())
		    {
			    /* =========== MINE =========== */
			    if (EnsoData.IsFriendMatch(status.CurrentMatchingType) && _matchedHasTaikoTweaks)
			    {
				    var musicInfoList = status.GetMusicInfoList();

				    for (int num = musicInfoList.Count - 1; num >= 0; num--)
				    {
					    var musicInfoAccesser = musicInfoList[num];

					    if (!musicInfoAccesser.IsDLC)
						    continue;

					    var needHasPlayerNum = 0;
					    needHasPlayerNum += (status.MatchingPlayer1Music.purchasedMusicList.Contains(musicInfoAccesser.UniqueId) ? 1 : 0);
					    needHasPlayerNum += (status.MatchingPlayer2Music.purchasedMusicList.Contains(musicInfoAccesser.UniqueId) ? 1 : 0);
					    if (needHasPlayerNum == 0 || needHasPlayerNum < musicInfoAccesser.RankmatchNeedHasPlayer)
					    {
						    musicInfoList.RemoveAt(num);
					    }
				    }

				    _rankedMatchSongSelect.Mode = RankedMatchSongSelect.SongSelectMode.Song;
				    _rankedMatchSongSelect.ResetChoices();
				    _rankedMatchSongSelect.SetMusicChoices(musicInfoList);
				    _rankedMatchSongSelect.IsActive = true;

				    Log.LogInfo("[MatchingProcess] Now waiting local song choice");

				    yield return new WaitUntil(() => _rankedMatchSongSelect.ChosenSong != null);

				    status.MatchingSongUniqueId = _rankedMatchSongSelect.ChosenSong.UniqueId;

				    isDone = false;
				    StartTransceiveSongPreviewInfo(__instance.networkManager, delegate(bool result)
				    {
					    isDone = true;
					    isSucceeded = result;
				    });
				    yield return new WaitUntil(() => isDone);
				    if (!isSucceeded)
				    {
					    __instance.StartRematch();
					    yield break;
				    }

				    Log.LogInfo("[MatchingProcess] Transceived local song choice");

				    Log.LogInfo("[MatchingProcess] Now waiting for other player difficulty");
				    isDone = false;
				    StartTransceiveDecideDifficultyInfo(__instance.networkManager, delegate(bool result)
				    {
					    isDone = true;
					    isSucceeded = result;
				    });
				    yield return new WaitUntil(() => isDone);
				    if (!isSucceeded)
				    {
					    __instance.StartRematch();
					    yield break;
				    }

				    yield return new WaitUntil(() => _rankedMatchSongSelect.ChosenDifficulty.HasValue);

				    var matchingPlayer1Setting = status.MatchingPlayer1Setting;
				    matchingPlayer1Setting.difficulty = _rankedMatchSongSelect.ChosenDifficulty.Value;
				    status.MatchingPlayer1Setting = matchingPlayer1Setting;
				    var matchingPlayer2Setting = status.MatchingPlayer2Setting;
				    matchingPlayer2Setting.difficulty = decideDifficulty;
				    status.MatchingPlayer2Setting = matchingPlayer2Setting;

				    Log.LogInfo("[MatchingProcess] Set up!");
			    }
			    /* ============================ */
			    else
			    {
				    status.MatchingSongUniqueId = __instance.GetMatchingSongUniqueId(status.MatchingPlayer1Music.playHistory,
					    status.MatchingPlayer1Music.purchasedMusicList, status.MatchingPlayer2Music.playHistory,
					    status.MatchingPlayer2Music.purchasedMusicList);
				    status.GetMusicInfo(status.MatchingSongUniqueId, out var info2);
				    __instance.GetDifficulty(info2, status.MatchingPlayer1Data.rankType, status.MatchingPlayer2Data.rankType,
					    out var level, out var level2);

				    EnsoSetting matchingPlayer1Setting = status.MatchingPlayer1Setting;
				    matchingPlayer1Setting.difficulty = level;
				    status.MatchingPlayer1Setting = matchingPlayer1Setting;
				    EnsoSetting matchingPlayer2Setting = status.MatchingPlayer2Setting;
				    matchingPlayer2Setting.difficulty = level2;
				    status.MatchingPlayer2Setting = matchingPlayer2Setting;
			    }
		    }
		    /* =========== MINE =========== */
		    else if (EnsoData.IsFriendMatch(status.CurrentMatchingType) && _matchedHasTaikoTweaks)
		    {
			    _rankedMatchSongSelect.ResetChoices();
			    _rankedMatchSongSelect.Mode = RankedMatchSongSelect.SongSelectMode.SongWaitHost;
			    _rankedMatchSongSelect.IsActive = true;

			    Log.LogInfo("[MatchingProcess] Now waiting for other player song choice");

			    isDone = false;
			    StartTransceiveSongPreviewInfo(__instance.networkManager, delegate(bool result)
			    {
				    isDone = true;
				    isSucceeded = result;
			    });
			    yield return new WaitUntil(() => isDone);
			    if (!isSucceeded)
			    {
				    __instance.StartRematch();
				    yield break;
			    }

			    Log.LogInfo($"[MatchingProcess] Got song choice from other player: {previewSongUniqueId}");

			    var chosenSongAccessor = status.GetMusicInfoList().First(x => x.UniqueId == previewSongUniqueId);

			    _rankedMatchSongSelect.ChosenSong = chosenSongAccessor;
			    _rankedMatchSongSelect.Mode = RankedMatchSongSelect.SongSelectMode.Difficulty;
			    _rankedMatchSongSelect.IsActive = true;
			    yield return new WaitUntil(() => _rankedMatchSongSelect.ChosenDifficulty.HasValue);

			    Log.LogInfo("[MatchingProcess] Now sending other player difficulty");
			    isDone = false;
			    StartTransceiveDecideDifficultyInfo(__instance.networkManager, delegate(bool result)
			    {
				    isDone = true;
				    isSucceeded = result;
			    });
			    yield return new WaitUntil(() => isDone);
			    if (!isSucceeded)
			    {
				    __instance.StartRematch();
				    yield break;
			    }
		    }
		    /* ============================ */

		    isDone = false;
		    networkManager.StartTransceiveSongInfo(2, delegate(bool result)
		    {
			    isDone = true;
			    isSucceeded = result;
		    });
		    yield return new WaitUntil(() => isDone);
		    if (!isSucceeded)
		    {
			    __instance.StartRematch();
			    yield break;
		    }

		    /* ================================= */
		    /* ====== Match & Game Set-up ====== */
		    /* ================================= */

		    if (status.CurrentMatchingType == EnsoData.RankMatchType.RankMatch)
		    {
			    var additionalRankPoint =
				    TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.RankmatchData
					    .GetAdditionalRankPoint(DataConst.MatchResultType.Lose, status.MatchingPlayer1Data.rankPoint,
						    status.MatchingPlayer2Data.rankPoint);
			    __instance.SavePenaltyParam(playDataManager.Rankmatch_SeasonId, flag: true, additionalRankPoint,
				    shouldSync: false);
		    }

		    __instance.SaveStatistics((int)((Time.time - startTime) * 100f));
		    __instance.SavePlayHistory(shouldSync: true);

		    /* ================================= */
		    /* ==== Player found animation ===== */
		    /* ================================= */

		    status.CurrentMatchingState = MatchingState.DisplayingDon;
		    yield return new WaitUntil(() => __instance.userInterfaceObjectReadyMessage == "don2");
		    yield return new WaitForSeconds(setting.matchingDisplaySec);

		    status.CurrentMatchingState = MatchingState.PlayingSong;
		    songPlayer.SetupSong(status.MatchingSongUniqueId);
		    yield return new WaitUntil(() => songPlayer.IsSetup());

		    __instance.StopBgm();
		    status.IsPlayedBgm = false;
		    songPlayer.PlaySong(
			    TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MySoundManager.GetVolume(SoundManager.SoundType
				    .OutGameSong));

		    yield return new WaitForSeconds(setting.matchingPlayingSec);
		    status.CurrentMatchingState = MatchingState.Greeting;
		    voicePlayer.PlaySound("v_rank_play_start",
			    TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MySoundManager.GetVolume(SoundManager.SoundType
				    .Voice));

		    yield return new WaitForSeconds(setting.matchingGreetingSec);

		    isDone = false;
		    networkManager.StartRefleshNetworkTime(delegate(bool result)
		    {
			    isDone = true;
			    isSucceeded = result;
		    });
		    yield return new WaitUntil(() => isDone);
		    if (!isSucceeded)
		    {
			    status.CurrentMatchingErrorType = ErrorType.NetworkHeavy;
			    yield break;
		    }

		    /* ================================= */
		    /* ========== Start game =========== */
		    /* ================================= */

		    __instance.SaveEnsoSetting();
		    __instance.SavePlayer1Info();
		    __instance.SavePlayer2Info();
		    networkManager.SyncSaveData();
		    __instance.MoveSceneRelayed("EnsoRankedMatch");
		    status.CurrentMatchingState = MatchingState.None;
	    }

        __result = NewMatchingProcess();

        return false;
    }

    private static int previewSongUniqueId = 0;
    private static EnsoData.EnsoLevelType decideDifficulty = EnsoData.EnsoLevelType.Easy;

    private static void CustomStartTransceivePlayerInfo(RankedMatchNetworkManager networkManager, int id, BoolDelegate callback)
    {
	    networkManager.isMatchingErrorCalled = false;
		AccountInfo accountInfo1 = default(AccountInfo);
		networkManager.status.MatchingPlayer1Data.ApplyToAccountInfo(ref accountInfo1);
		networkManager.status.MatchingPlayer1Music.ApplyToAccountInfo(ref accountInfo1);
		networkManager.status.MatchingPlayer1Setting.ApplyToAccountInfo(ref accountInfo1);

		/* =========== MINE =============== */
		if (_isFriendMatching)
		{
			accountInfo1.PurchasedDlc = accountInfo1.PurchasedDlc.AddItem(MARKER_HAS_TAIKOTWEAKS).ToArray();
			Log.LogInfo("[CustomStartTransceivePlayerInfo] Added marker!");
		}
		/* ================================ */

		if (networkManager.status.CurrentMatchingType == EnsoData.RankMatchType.RankMatch)
		{
			if (networkManager.waitingCoroutine != null)
			{
				networkManager.StopCoroutine(networkManager.waitingCoroutine);
			}
			networkManager.waitingCoroutine = CoroutineWait(callback);
			networkManager.StartCoroutine(networkManager.waitingCoroutine);
		}
		if (networkManager.sendingCoroutine != null)
		{
			networkManager.StopCoroutine(networkManager.sendingCoroutine);
		}
		networkManager.sendingCoroutine = CoroutineSend();
		networkManager.StartCoroutine(networkManager.sendingCoroutine);
		if (networkManager.matchingCoroutine != null)
		{
			networkManager.StopCoroutine(networkManager.matchingCoroutine);
		}
		networkManager.matchingCoroutine = CoroutineRecieve(callback);
		networkManager.StartCoroutine(networkManager.matchingCoroutine);
		IEnumerator CoroutineRecieve(BoolDelegate callback)
		{
			yield return new WaitUntil(() => TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.CheckACK(id));
			if (networkManager.sendingCoroutine != null)
			{
				networkManager.StopCoroutine(networkManager.sendingCoroutine);
			}
			AccountInfo accountInfo2 = default(AccountInfo);
			yield return new WaitUntil(() => TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.GetLastRecieveData(ReceiveDataType.AccountInfo, ref accountInfo2));
			if (networkManager.waitingCoroutine != null)
			{
				networkManager.StopCoroutine(networkManager.waitingCoroutine);
			}

			/* =========== MINE =============== */
			if (_isFriendMatching && accountInfo2.PurchasedDlc.Contains(MARKER_HAS_TAIKOTWEAKS))
			{
				_matchedHasTaikoTweaks = true;
				Log.LogInfo("[CustomStartTransceivePlayerInfo] Found marker!");
			}
			else
			{
				_matchedHasTaikoTweaks = false;
			}
			/* ================================ */

			networkManager.status.MatchingPlayer2Data = new PlayerData(accountInfo2);
			networkManager.status.MatchingPlayer2Music = new MusicData(accountInfo2);
			networkManager.status.MatchingPlayer2Setting = new EnsoSetting(accountInfo2);

			callback(flag: true);
		}
		IEnumerator CoroutineSend()
		{
			while (!TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.CheckACK(id))
			{
				TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.SendACK(id);
				TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.SendData(ReceiveDataType.AccountInfo, accountInfo1);
				yield return null;
			}
		}
		IEnumerator CoroutineWait(BoolDelegate callback)
		{
			yield return new WaitForSeconds(networkManager.setting.matchingTransceivingSec);
			if (networkManager.sendingCoroutine != null)
			{
				networkManager.StopCoroutine(networkManager.sendingCoroutine);
			}
			if (networkManager.matchingCoroutine != null)
			{
				networkManager.StopCoroutine(networkManager.matchingCoroutine);
			}
			callback(flag: false);
		}
    }

    private static void StartTransceiveSongPreviewInfo(RankedMatchNetworkManager networkManager, BoolDelegate callback)
    {
	    const int ackId = 14;

	    networkManager.isMatchingErrorCalled = false;
	    if (networkManager.status.CurrentMatchingType == EnsoData.RankMatchType.RankMatch)
	    {
		    if (networkManager.waitingCoroutine != null) networkManager.StopCoroutine(networkManager.waitingCoroutine);
		    networkManager.waitingCoroutine = CoroutineWait(callback);
		    networkManager.StartCoroutine(networkManager.waitingCoroutine);
	    }

	    SongPreviewInfo musicInfo;
	    if (TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.IsHost)
	    {
		    musicInfo = default;
		    musicInfo.SongUniqueId = networkManager.status.MatchingSongUniqueId;
		    if (networkManager.sendingCoroutine != null) networkManager.StopCoroutine(networkManager.sendingCoroutine);
		    networkManager.sendingCoroutine = HostCoroutineSend();
		    networkManager.StartCoroutine(networkManager.sendingCoroutine);
		    if (networkManager.matchingCoroutine != null) networkManager.StopCoroutine(networkManager.matchingCoroutine);
		    networkManager.matchingCoroutine = HostCoroutineRecieve(callback);
		    networkManager.StartCoroutine(networkManager.matchingCoroutine);
	    }
	    else
	    {
		    if (networkManager.matchingCoroutine != null) networkManager.StopCoroutine(networkManager.matchingCoroutine);
		    networkManager.matchingCoroutine = ClientCoroutine(callback);
		    networkManager.StartCoroutine(networkManager.matchingCoroutine);
	    }

	    IEnumerator ClientCoroutine(BoolDelegate callback)
	    {
		    var previewInfo = default(SongPreviewInfo);
		    yield return new WaitUntil(() =>
			    TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.GetLastRecieveData(
				    (ReceiveDataType) CustomReceiveDataType.SongPreviewInfo, ref previewInfo));
		    if (networkManager.waitingCoroutine != null) networkManager.StopCoroutine(networkManager.waitingCoroutine);
		    previewSongUniqueId = previewInfo.SongUniqueId;
		    callback(true);
	    }

	    IEnumerator CoroutineWait(BoolDelegate callback)
	    {
		    yield return new WaitForSeconds(networkManager.setting.matchingTransceivingSec);
		    if (networkManager.sendingCoroutine != null) networkManager.StopCoroutine(networkManager.sendingCoroutine);
		    if (networkManager.matchingCoroutine != null) networkManager.StopCoroutine(networkManager.matchingCoroutine);
		    callback(false);
	    }

	    IEnumerator HostCoroutineRecieve(BoolDelegate callback)
	    {
		    yield return new WaitUntil(() => TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.CheckACK(ackId));
		    if (networkManager.sendingCoroutine != null) networkManager.StopCoroutine(networkManager.sendingCoroutine);
		    if (networkManager.waitingCoroutine != null) networkManager.StopCoroutine(networkManager.waitingCoroutine);
		    callback(true);
	    }

	    IEnumerator HostCoroutineSend()
	    {
		    while (!TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.CheckACK(ackId))
		    {
			    TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.SendACK(ackId);
			    TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.SendData((ReceiveDataType) CustomReceiveDataType.SongPreviewInfo,
				    musicInfo);
			    yield return null;
		    }
	    }
    }

    private static void StartTransceiveDecideDifficultyInfo(RankedMatchNetworkManager networkManager, BoolDelegate callback)
    {
	    const int ackId = 15;

	    networkManager.isMatchingErrorCalled = false;
	    if (networkManager.status.CurrentMatchingType == EnsoData.RankMatchType.RankMatch)
	    {
		    if (networkManager.waitingCoroutine != null) networkManager.StopCoroutine(networkManager.waitingCoroutine);
		    networkManager.waitingCoroutine = CoroutineWait(callback);
		    networkManager.StartCoroutine(networkManager.waitingCoroutine);
	    }

	    DecideDifficultyInfo decideInfo;
	    if (!TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.IsHost)
	    {
		    decideInfo = default;
		    decideInfo.HasDecision = true;
		    decideInfo.LevelType = _rankedMatchSongSelect.ChosenDifficulty.Value;

		    if (networkManager.sendingCoroutine != null) networkManager.StopCoroutine(networkManager.sendingCoroutine);
		    networkManager.sendingCoroutine = HostCoroutineSend();
		    networkManager.StartCoroutine(networkManager.sendingCoroutine);
		    if (networkManager.matchingCoroutine != null) networkManager.StopCoroutine(networkManager.matchingCoroutine);
		    networkManager.matchingCoroutine = HostCoroutineRecieve(callback);
		    networkManager.StartCoroutine(networkManager.matchingCoroutine);
	    }
	    else
	    {
		    if (networkManager.matchingCoroutine != null) networkManager.StopCoroutine(networkManager.matchingCoroutine);
		    networkManager.matchingCoroutine = ClientCoroutine(callback);
		    networkManager.StartCoroutine(networkManager.matchingCoroutine);
	    }

	    IEnumerator ClientCoroutine(BoolDelegate callback)
	    {
		    var decideDifficultyInfo = default(DecideDifficultyInfo);
		    yield return new WaitUntil(() =>
			    TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.GetLastRecieveData(
				    (ReceiveDataType) CustomReceiveDataType.DecideNonHostDifficulty, ref decideDifficultyInfo));
		    if (networkManager.waitingCoroutine != null) networkManager.StopCoroutine(networkManager.waitingCoroutine);
		    decideDifficulty = decideDifficultyInfo.LevelType;
		    callback(true);
	    }

	    IEnumerator CoroutineWait(BoolDelegate callback)
	    {
		    yield return new WaitForSeconds(networkManager.setting.matchingTransceivingSec);
		    if (networkManager.sendingCoroutine != null) networkManager.StopCoroutine(networkManager.sendingCoroutine);
		    if (networkManager.matchingCoroutine != null) networkManager.StopCoroutine(networkManager.matchingCoroutine);
		    callback(false);
	    }

	    IEnumerator HostCoroutineRecieve(BoolDelegate callback)
	    {
		    yield return new WaitUntil(() => TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.CheckACK(ackId));
		    if (networkManager.sendingCoroutine != null) networkManager.StopCoroutine(networkManager.sendingCoroutine);
		    if (networkManager.waitingCoroutine != null) networkManager.StopCoroutine(networkManager.waitingCoroutine);
		    callback(true);
	    }

	    IEnumerator HostCoroutineSend()
	    {
		    while (!TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.CheckACK(ackId))
		    {
			    TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.SendACK(ackId);
			    TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.SendData((ReceiveDataType) CustomReceiveDataType.DecideNonHostDifficulty,
				    decideInfo);
			    yield return null;
		    }
	    }
    }

    [HarmonyPatch(typeof(RankedMatchSceneManager), "GetDifficulty")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPrefix]
    public static bool GetDifficulty_Prefix(RankedMatchSceneManager __instance, MusicDataInterface.MusicInfoAccesser info, DataConst.RankType rank1, DataConst.RankType rank2, ref EnsoData.EnsoLevelType level1, ref EnsoData.EnsoLevelType level2)
    {
        var rankedMatchStatus = Traverse.Create(__instance).Field("status").GetValue() as RankedMatchStatus;
        if (rankedMatchStatus == null)
        {
            throw new Exception("RankedMatchStatus was null");
        }

        // Always return the rank-selected difficulties for the ranked match
        if (!EnsoData.IsFriendMatch(rankedMatchStatus.CurrentMatchingType))
            return true;

        /*
        var isUraExist = info.Stars[4] > 0;

        Log.LogInfo("[RankedMatchSongSelectPatch] Now choosing difficulty for friend match");
        var friendLevelType = DecideDifficulty(isUraExist);
        if (friendLevelType == null)
	        return true;

        level1 = level2 = friendLevelType.Value;

        return false;
        */

        return true;
    }

    [HarmonyPatch(typeof(RankedMatchSceneManager), "OnMatchingNetworkError")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPostfix]
    public static void OnMatchingNetworkError_Postfix()
    {
	    Log.LogInfo("[OnMatchingNetworkError] Called!");
	    _rankedMatchSongSelect.IsActive = false;
    }

    [HarmonyPatch(typeof(RankedMatchSceneManager), "OnMatchingSessionError")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPostfix]
    public static void OnMatchingSessionError_Postfix()
    {
	    _rankedMatchSongSelect.IsActive = false;
    }

    [HarmonyPatch(typeof(XboxLiveOnlineManager), "ClearAllRecieveData")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPrefix]
    public static bool ClearAllReceiveData_Prefix(XboxLiveOnlineManager __instance)
    {
	    Log.LogInfo("[ClearAllReceiveData] Cleared!");

	    foreach (Queue<object> receiveDatum in __instance.receiveData)
	    {
		    receiveDatum.Clear();
		    receiveDatum.TrimExcess();
	    }
	    __instance.receiveData.Clear();
	    for (var i = 0; i < 99; i++) // HACK: The game only allocates slots for its own message types. We need to allocate more to allow for our network types to be awaited.
	    {
		    __instance.receiveData.Add(new Queue<object>());
	    }

	    return false;
    }

    [HarmonyPatch(typeof(XboxLiveOnlineManager), "SetObject")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPrefix]
    public static bool SetObject_Prefix(XboxLiveOnlineManager __instance, ref byte[] objData, ReceiveDataType type)
    {
	    Log.LogInfo($"[SetObject] type: {type}");

	    switch (type)
	    {
		    case (ReceiveDataType)CustomReceiveDataType.DecideNonHostDifficulty:
			    __instance.Enqueue<DecideDifficultyInfo>(ref objData, type);
			    break;
		    case (ReceiveDataType)CustomReceiveDataType.SongPreviewInfo:
			    Log.LogInfo($"[SetObject] Enqueue ok");
			    __instance.Enqueue<SongPreviewInfo>(ref objData, type);
			    break;
	    }

	    return true;
    }

    [HarmonyPatch(typeof(XboxLiveOnlineManager), "ClearMatchingInfo")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPrefix]
    public static bool ClearMatchingInfo_Prefix(XboxLiveOnlineManager __instance)
	{
		Log.LogInfo($"[ClearMatchingInfo] Cleared!");
	    __instance.ClearEachRecieveData((ReceiveDataType) CustomReceiveDataType.DecideNonHostDifficulty);
	    __instance.ClearEachRecieveData((ReceiveDataType) CustomReceiveDataType.SongPreviewInfo);

	    return true;
	}

    /*
    private static EnsoData.EnsoLevelType? DecideDifficulty(bool isUraExist)
    {
	    var hasConfigDefault = Plugin.Instance.ConfigFriendMatchingDefaultDifficulty.Value != 0;
	    var configDefault = (EnsoData.EnsoLevelType)Plugin.Instance.ConfigFriendMatchingDefaultDifficulty.Value - 1;

	    EnsoData.EnsoLevelType? friendLevelType;
	    if (Input.GetKey(KeyCode.C))
	    {
		    friendLevelType = EnsoData.EnsoLevelType.Easy;
	    }
	    else if (Input.GetKey(KeyCode.V))
	    {
		    friendLevelType = EnsoData.EnsoLevelType.Normal;
	    }
	    else if (Input.GetKey(KeyCode.B))
	    {
		    friendLevelType = EnsoData.EnsoLevelType.Hard;
	    }
	    else if (Input.GetKey(KeyCode.N))
	    {
		    friendLevelType = EnsoData.EnsoLevelType.Mania;
	    }
	    else if(Input.GetKey(KeyCode.M))
	    {
		    friendLevelType = isUraExist ? EnsoData.EnsoLevelType.Ura : EnsoData.EnsoLevelType.Mania;
	    }
	    else if(hasConfigDefault)
	    {
		    Log.LogInfo($"[DecideDifficulty] No key pressed, using config default: {configDefault}");
		    if (configDefault == EnsoData.EnsoLevelType.Ura && !isUraExist)
		    {
			    friendLevelType = EnsoData.EnsoLevelType.Mania;
		    }
		    else
		    {
			    friendLevelType = configDefault;
		    }
	    }
	    else
	    {
		    Log.LogInfo("[DecideDifficulty] No key pressed, choosing random difficulty");
		    friendLevelType = null;
	    }

	    return friendLevelType;
    }
    */
}