using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TaikoTweaks.RankedMatch;

public class RankedMatchSongSelect : MonoBehaviour
{
    private string _songNameFilterText;
    private int _genreFilterIndex = 0;
    private Vector2 _scrollViewVector;
    private Dictionary<int, (string Title, string Artist)> _songNameDict = new();
    private bool _songListenIsCued = false;
    private List<MusicDataInterface.MusicInfoAccesser> _songListFiltered = null;
    private bool _isListening = false;

    private bool _internalIsActive = false;
    public bool IsActive
    {
        get => _internalIsActive;
        set
        {
            _internalIsActive = value;

            if (_internalIsActive)
            {
                TaikoSingletonMonoBehaviour<ControllerManager>.Instance.SetForcedMouseVisibleOff(false);
                TaikoSingletonMonoBehaviour<ControllerManager>.Instance.SetMouseVisible(true);
            }
        }
    }

    public void Start()
    {
        var wordDataMgr = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.WordDataMgr;

        _songNameDict.Clear();
        foreach (var infoAccesser in TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData.musicInfoAccessers)
        {
            var wordTitle = wordDataMgr.GetWordListInfo($"song_{infoAccesser.Id}");
            var wordArtist = wordDataMgr.GetWordListInfo($"song_sub_{infoAccesser.Id}");
            _songNameDict.Add(infoAccesser.UniqueId, (wordTitle.Text, wordArtist.Text));
        }
    }

    public SongSelectMode Mode { get; set; }

    public enum SongSelectMode
    {
        SongWaitHost,
        Song,
        Difficulty,
    }

    private List<MusicDataInterface.MusicInfoAccesser> MusicChoices => _songListFiltered ?? MusicAvailable;

    public RankedMatchSceneManager SceneManager { get; set; }

    public MusicDataInterface.MusicInfoAccesser ChosenSong { get; set; }

    public EnsoData.EnsoLevelType? ChosenDifficulty { get; private set; }

    public List<MusicDataInterface.MusicInfoAccesser> MusicAvailable { get; private set; }

    public void ResetChoices()
    {
        _songNameFilterText = string.Empty;
        ChosenSong = null;
        ChosenDifficulty = null;
        MusicAvailable = null;
        _songListFiltered = null;
        _genreFilterIndex = 0;
        _isListening = false;
    }

    public void SetMusicChoices(List<MusicDataInterface.MusicInfoAccesser> choices)
    {
        this.MusicAvailable = choices;
        _songNameFilterText = string.Empty;
        _genreFilterIndex = 0;
        _songListFiltered = null;
    }

    private void OnGUI()
    {
        if (!IsActive)
            return;

        if (_songListenIsCued && SceneManager.songPlayer.IsSetup())
        {
            SceneManager.songPlayer.PlaySong(TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MySoundManager.GetVolume(SoundManager.SoundType.OutGameSong));
            _songListenIsCued = false;
        }

        switch (Mode)
        {
            case SongSelectMode.SongWaitHost:
            {
                GUI.Box(new Rect(15, 15, 600, 40), string.Empty);
                GUI.Box(new Rect(15, 15, 600, 40), string.Empty);
                GUI.Box(new Rect(15, 15, 600, 40), string.Empty);

                GUI.Label(new Rect (25, 25, 600, 30), "Waiting for host to pick a song...");
            }
                break;

            case SongSelectMode.Song:
            {
                const int songLineHeight = 30;

                // Fuck changing the bg color of these, just putting more so it's darker
                GUI.Box(new Rect(0, 0, 620, 390), string.Empty);
                GUI.Box(new Rect(0, 0, 620, 390), string.Empty);
                GUI.Box(new Rect(0, 0, 620, 390), string.Empty);

                this._scrollViewVector = GUI.BeginScrollView(new Rect(10, 10, 600, 300), this._scrollViewVector, new Rect(0, 0, 500, songLineHeight * this.MusicChoices.Count));

                var runningY = 0;
                foreach (var infoAccesser in this.MusicChoices)
                {
                    var words = _songNameDict[infoAccesser.UniqueId];

                    var songLabel = words.Title;
                    if (!string.IsNullOrEmpty(words.Artist))
                        songLabel += $" - {words.Artist}";

                    GUI.Label(new Rect(0, runningY, 600, 100), songLabel);

                    if (GUI.Button(new Rect(400, runningY, 70, songLineHeight), "Select"))
                    {
                        ChosenSong = infoAccesser;

                        if (_isListening)
                        {
                            _isListening = false;
                        }

                        Mode = SongSelectMode.Difficulty;
                    }

                    if (GUI.Button(new Rect(480, runningY, 70, songLineHeight), "Listen"))
                    {
                        SceneManager.StopBgm();
                        SceneManager.songPlayer.SetupSong(infoAccesser.UniqueId);
                        _songListenIsCued = true;
                        _isListening = true;
                    }

                    runningY += songLineHeight;
                }

                GUI.EndScrollView();

                this._songNameFilterText = GUI.TextField(new Rect(10, 310, 300, 30), this._songNameFilterText);
                GUI.Label(new Rect(315, 315, 500, 100), "Filter by song name");

                if (GUI.Button(new Rect(500, 315, 90, 30), "Random"))
                {
                    var rng = new System.Random();
                    var index = rng.Next(0, this.MusicChoices.Count);
                    ChosenSong = this.MusicChoices[index];

                    if (_isListening)
                    {
                        SceneManager.songPlayer.StopSong();
                        SceneManager.PlayBgm();
                        _isListening = false;
                    }

                    Mode = SongSelectMode.Difficulty;
                }

                var enumGenres = Enum.GetValues(typeof(EnsoData.SongGenre)).Cast<EnsoData.SongGenre>().Where(x => x != EnsoData.SongGenre.Children).Select(x => x.ToString()).Reverse().Skip(1).Reverse();
                var genreList = new [] { "All" }.Concat(enumGenres).ToArray();

                this._genreFilterIndex = GUI.Toolbar(new Rect(10, 350, 600, 30), _genreFilterIndex, genreList);

                if (GUI.changed)
                {
                    var searchTerm = this._songNameFilterText.ToLower();
                    var hasSearchTerm = !string.IsNullOrWhiteSpace(searchTerm);
                    this._songListFiltered = MusicAvailable.Where(x =>
                    {
                        var termMatches = true;
                        if (hasSearchTerm)
                        {
                            var words = _songNameDict[x.UniqueId];
                            termMatches = words.Title.ToLower().Contains(searchTerm) || words.Artist.ToLower().Contains(searchTerm) || x.Id.Contains(searchTerm);
                        }

                        var genreMatches = true;
                        if (_genreFilterIndex != 0)
                        {
                            if (this._genreFilterIndex <= (int)EnsoData.SongGenre.Children)
                            {
                                genreMatches = x.GenreNo == _genreFilterIndex - 1;
                            }
                            else
                            {
                                genreMatches = x.GenreNo == _genreFilterIndex;
                            }
                        }

                        return termMatches && genreMatches;
                    }).ToList();
                }
            }
                break;

            case SongSelectMode.Difficulty:
            {
                if (!_isListening)
                {
                    UnityEngine.Debug.Log("Stop BGM");
                    SceneManager.StopBgm();
                    SceneManager.songPlayer.SetupSong(this.ChosenSong.UniqueId);
                    _songListenIsCued = true;
                    _isListening = true;
                }

                void StopPlaying()
                {
                    SceneManager.songPlayer.StopSong();
                    SceneManager.PlayBgm();
                }

                var isExistEasy = this.ChosenSong.Stars[0] > 0;
                var isExistNormal = this.ChosenSong.Stars[1] > 0;
                var isExistHard = this.ChosenSong.Stars[2] > 0;
                var isExistMania = this.ChosenSong.Stars[3] > 0;
                var isExistUra = this.ChosenSong.Stars[4] > 0;

                var width = 10;

                if (isExistEasy)
                    width += 100;
                if (isExistNormal)
                    width += 100;
                if (isExistHard)
                    width += 100;
                if (isExistMania)
                    width += 100;
                if (isExistUra)
                    width += 100;

                GUI.Box(new Rect(0, 0, width, 140), string.Empty);
                GUI.Box(new Rect(0, 0, width, 140), string.Empty);
                GUI.Box(new Rect(0, 0, width, 140), string.Empty);

                if (isExistEasy)
                {
                    if (GUI.Button(new Rect(10, 10, 90, 90), $"Easy\n{ChosenSong.Stars[0]}★"))
                    {
                        ChosenDifficulty = EnsoData.EnsoLevelType.Easy;
                        IsActive = false;
                        StopPlaying();
                    }
                }

                if (isExistNormal)
                {
                    if (GUI.Button(new Rect(110, 10, 90, 90), $"Normal\n{ChosenSong.Stars[1]}★"))
                    {
                        ChosenDifficulty = EnsoData.EnsoLevelType.Normal;
                        IsActive = false;
                        StopPlaying();
                    }
                }

                if (isExistHard)
                {
                    if (GUI.Button(new Rect(210, 10, 90, 90), $"Hard\n{ChosenSong.Stars[2]}★"))
                    {
                        ChosenDifficulty = EnsoData.EnsoLevelType.Hard;
                        IsActive = false;
                        StopPlaying();
                    }
                }

                if (isExistMania)
                {
                    if (GUI.Button(new Rect(310, 10, 90, 90), $"Oni\n{ChosenSong.Stars[3]}★"))
                    {
                        ChosenDifficulty = EnsoData.EnsoLevelType.Mania;
                        IsActive = false;
                        StopPlaying();
                    }
                }

                if (isExistUra)
                {
                    if (GUI.Button(new Rect(410, 10, 90, 90), $"Ura\n{ChosenSong.Stars[4]}★"))
                    {
                        ChosenDifficulty = EnsoData.EnsoLevelType.Ura;
                        IsActive = false;
                        StopPlaying();
                    }
                }

                var words = _songNameDict[ChosenSong.UniqueId];

                var songLabel = words.Title;
                if (!string.IsNullOrEmpty(words.Artist))
                    songLabel += $" - {words.Artist}";

                GUI.Label(new Rect(10, 110, 1000, 30), songLabel);
            }
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}