using BepInEx.Unity.IL2CPP.Utils;
using BepInEx.Unity.IL2CPP;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using BepInEx.Configuration;
using NijiiroScoring.Plugins;
using UnityEngine;
using System.Collections;
using UniRx;
using BepInEx.Unity.IL2CPP.Utils.Collections;

namespace NijiiroScoring
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, ModName, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        public const string ModName = "NijiiroScoring";

        public static Plugin Instance;
        private Harmony _harmony = null;
        public new static ManualLogSource Log;


        public ConfigEntry<bool> ConfigEnabled;
        //public ConfigEntry<bool> ConfigAlwaysCalculate;
        public ConfigEntry<string> ConfigNijiroScoreDataPath;
        public ConfigEntry<string> ConfigTakoTakoPath;



        public override void Load()
        {
            Instance = this;

            Log = base.Log;

            SetupConfig();
            SetupHarmony();
        }

        private void SetupConfig()
        {
            var dataFolder = Path.Combine("BepInEx", "data", ModName);

            ConfigEnabled = Config.Bind("General",
                "Enabled",
                true,
                "Enables the mod.");

            // This config didn't work the way I had planned
            //ConfigAlwaysCalculate = Config.Bind("General",
            //    "AlwaysCalculate",
            //    false,
            //    "Set this to true to always calculate song's nijiro values if they are invalid. This can fix rare issues where songs aren't being calculated. ");

            ConfigNijiroScoreDataPath = Config.Bind("General",
                "NijiroScoreDataPath",
                Path.Combine("BepInEx", "data", "NijiiroScoring", "SongData.json"),
                "The file path to the json file containing any nijiiro scoring data. Data is auto generated, but can be adjusted manually.");

            ConfigTakoTakoPath = Config.Bind("General",
                "TakoTakoPath",
                Path.Combine("BepInEx", "data", "TakoTako", "customSongs"),
                "The folder path to your TakoTako customSongs directory from TDMX. " +
                "Leave blank if you don't want to import point values from TDMX.");
        }

        private void SetupHarmony()
        {
            // Patch methods
            _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

            if (ConfigEnabled.Value)
            {
                bool result = true;
                // If any PatchFile fails, result will become false
                result &= PatchFile(typeof(GetFumenDataHook));
                result &= PatchFile(typeof(NijiroScoringPatch));
                result &= PatchFile(typeof(NijiroScoreLoading));
                if (result)
                {
                    Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_NAME} is loaded!");
                }
                else
                {
                    Log.LogError($"Plugin {MyPluginInfo.PLUGIN_GUID} failed to load.");
                    // Unload this instance of Harmony
                    // I hope this works the way I think it does
                    _harmony.UnpatchSelf();
                }
            }
            else
            {
                Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_NAME} is disabled.");
            }
        }

        private bool PatchFile(Type type)
        {
            if (_harmony == null)
            {
                _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            }
            try
            {
                _harmony.PatchAll(type);
#if DEBUG
                Log.LogInfo("File patched: " + type.FullName);
#endif
                return true;
            }
            catch (Exception e)
            {
                Log.LogInfo("Failed to patch file: " + type.FullName);
                Log.LogInfo(e.Message);
                return false;
            }
        }

        public static MonoBehaviour GetMonoBehaviour() => TaikoSingletonMonoBehaviour<CommonObjects>.Instance;
        public Coroutine StartCoroutine(IEnumerator enumerator)
        {
            return GetMonoBehaviour().StartCoroutine(enumerator);
        }

        public void StartMicroCoroutine(IEnumerator enumerator)
        {
            MainThreadDispatcher.StartUpdateMicroCoroutine(enumerator.WrapToIl2Cpp());
        }
    }
}
