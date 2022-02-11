using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TaikoTweaks.RankedMatch;
using TaikoTweaks.SongSelect;

namespace TaikoTweaks
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("com.fluto.takotako", "2.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public ConfigEntry<bool> ConfigRandomSongSelectSkip;
        public ConfigEntry<bool> ConfigSongSelectKanbanCrown;

        public static Plugin Instance;
        private Harmony _harmony;
        public static ManualLogSource Log;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            SetupConfig();

            SetupHarmony();
        }

        private void SetupConfig()
        {
            ConfigRandomSongSelectSkip = Config.Bind("General",
                "RandomSongSelectSkip",
                true,
                "When true, the game will not proceed to the song screen when selecting a random song, instead letting you re-roll");

            ConfigSongSelectKanbanCrown = Config.Bind("General",
                "SongSelectKanbanCrown",
                true,
                "When true, the song select will show the highest achieved crown on the highest achieved difficulty for each song");
        }

        private void SetupHarmony()
        {
            if (typeof(EnsoData).Assembly.GetName().Version < Version.Parse("1.0.2.23102"))
            {
                Logger.LogError("Please make sure that your game is up to date!");
                return;
            }

            // Patch methods
            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);

            if (ConfigRandomSongSelectSkip.Value)
                _harmony.PatchAll(typeof(RandomRepeatPatch));

            if (ConfigSongSelectKanbanCrown.Value)
                _harmony.PatchAll(typeof(KanbanRankIconPatch));

            _harmony.PatchAll(typeof(FastScrollPatch));

            _harmony.PatchAll(typeof(RankedMatchScoreSavePatch));
            _harmony.PatchAll(typeof(RankedMatchSongSelectPatch));
            _harmony.PatchAll(typeof(RankedMatchNetworkDlcPatch));

            _harmony.PatchAll(typeof(MissingDifficultiesPatch));
            _harmony.PatchAll(typeof(HighFpsAnimationPatch));

            //this._harmony.PatchAll(typeof(ThemePatches));
        }

        public void StartCustomCoroutine(IEnumerator enumerator)
        {
            StartCoroutine(enumerator);
        }
    }
}