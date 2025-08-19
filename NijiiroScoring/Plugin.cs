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
using SaveProfileManager.Plugins;
using System.Reflection;

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

            SetupConfig(Config, Path.Combine("BepInEx", "data", ModName));
            SetupHarmony();

            var isSaveManagerLoaded = IsSaveManagerLoaded();
            if (isSaveManagerLoaded)
            {
                AddToSaveManager();
            }
        }

        private void SetupConfig(ConfigFile config, string saveFolder, bool isSaveManager = false)
        {
            var dataFolder = Path.Combine("BepInEx", "data", ModName);

            if (!isSaveManager)
            {
                ConfigEnabled = config.Bind("General",
                   "Enabled",
                   true,
                   "Enables the mod.");
            }

            // This config didn't work the way I had planned
            //ConfigAlwaysCalculate = Config.Bind("General",
            //    "AlwaysCalculate",
            //    false,
            //    "Set this to true to always calculate song's nijiro values if they are invalid. This can fix rare issues where songs aren't being calculated. ");

            ConfigNijiroScoreDataPath = Config.Bind("General",
                "NijiroScoreDataPath",
                Path.Combine(dataFolder, "SongData.json"),
                "The file path to the json file containing any nijiiro scoring data. Data is auto generated, but can be adjusted manually.");

            ConfigTakoTakoPath = Config.Bind("General",
                "TakoTakoPath",
                "null",
                "The folder path to your TakoTako customSongs directory from TDMX. " + 
                "When this data is parsed, it will set this value back to null to avoid reparsing it again. " +
                "Ignore if you don't want to import point values from TDMX.");
        }

        private void SetupHarmony()
        {
            // Patch methods
            _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

            LoadPlugin(ConfigEnabled.Value);
        }

        public static void LoadPlugin(bool enabled)
        {
            if (enabled)
            {
                bool result = true;
                // If any PatchFile fails, result will become false
                //result &= Instance.PatchFile(typeof(GetFumenDataHook));
                result &= Instance.PatchFile(typeof(NijiroScoringPatch));
                result &= Instance.PatchFile(typeof(NijiroScoreLoading));
                result &= Instance.PatchFile(typeof(GetFrameResultsHook));
                if (result)
                {
                    Logger.Log($"Plugin {MyPluginInfo.PLUGIN_NAME} is loaded!");
                }
                else
                {
                    Logger.Log($"Plugin {MyPluginInfo.PLUGIN_GUID} failed to load.", LogType.Error);
                    // Unload this instance of Harmony
                    // I hope this works the way I think it does
                    Instance._harmony.UnpatchSelf();
                }
            }
            else
            {
                Logger.Log($"Plugin {MyPluginInfo.PLUGIN_NAME} is disabled.");
            }
        }

        internal bool PatchFile(Type type)
        {
            if (_harmony == null)
            {
                _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            }
            try
            {
                _harmony.PatchAll(type);
#if DEBUG
                Logger.Log("File patched: " + type.FullName);
#endif
                return true;
            }
            catch (Exception e)
            {
                Logger.Log("Failed to patch file: " + type.FullName);
                Logger.Log(e.Message);
                return false;
            }
        }

        internal static void UnpatchMethod(string methodName)
        {
            if (Instance._harmony is null)
            {
                return;
            }

            var methods = Instance._harmony.GetPatchedMethods().ToList();
            for (int i = 0; i < methods.Count; i++)
            {
                if (methods[i].Name == methodName)
                {
                    Instance._harmony.Unpatch(methods[i], HarmonyPatchType.All, Instance._harmony.Id);
                }
            }
        }

        public static void UnloadPlugin()
        {
            Instance._harmony.UnpatchSelf();
            Logger.Log($"Plugin {MyPluginInfo.PLUGIN_NAME} has been unpatched.");
        }

        public static void ReloadPlugin()
        {
            // Reloading will always be completely different per mod
            // You'll want to reload any config file or save data that may be specific per profile
            // If there's nothing to reload, don't put anything here, and keep it commented in AddToSaveManager
            //SwapSongLanguagesPatch.InitializeOverrideLanguages();
            //TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData.Reload();
        }

        public void AddToSaveManager()
        {
            // Add SaveDataManager dll path to your csproj.user file
            // https://github.com/Deathbloodjr/RF.SaveProfileManager
            var plugin = new PluginSaveDataInterface(MyPluginInfo.PLUGIN_GUID);
            plugin.AssignLoadFunction(LoadPlugin);
            plugin.AssignUnloadFunction(UnloadPlugin);
            //plugin.AssignReloadSaveFunction(ReloadPlugin);
            plugin.AssignConfigSetupFunction(SetupConfig);
            plugin.AddToManager(ConfigEnabled.Value);
        }

        private bool IsSaveManagerLoaded()
        {
            try
            {
                Assembly loadedAssembly = Assembly.Load("com.DB.RF.SaveProfileManager");
                return loadedAssembly != null;
            }
            catch
            {
                return false;
            }
        }

        public static MonoBehaviour GetMonoBehaviour() => TaikoSingletonMonoBehaviour<CommonObjects>.Instance;
        public void StartCoroutine(IEnumerator enumerator)
        {
            GetMonoBehaviour().StartCoroutine(enumerator);
        }
        public void StartCoroutine(Il2CppSystem.Collections.IEnumerator enumerator)
        {
            GetMonoBehaviour().StartCoroutine(enumerator);
        }

        public Task WaitForCoroutine(IEnumerator coroutine)
        {
            Logger.Log("WaitForCoroutine 1");
            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(RunCoroutineAndCompleteTask(coroutine, tcs));
            Logger.Log("WaitForCoroutine After");
            return tcs.Task;
        }

        public Task WaitForCoroutine(Il2CppSystem.Collections.IEnumerator coroutine)
        {
            Logger.Log("WaitForCoroutine 2");
            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(RunCoroutineAndCompleteTask(coroutine, tcs));
            Logger.Log("WaitForCoroutine After");
            return tcs.Task;
        }

        private IEnumerator RunCoroutineAndCompleteTask(IEnumerator coroutine, TaskCompletionSource<bool> tcs)
        {
            yield return GetMonoBehaviour().StartCoroutine(coroutine);
            tcs.SetResult(true); // Complete the task when the coroutine is done
        }

        private IEnumerator RunCoroutineAndCompleteTask(Il2CppSystem.Collections.IEnumerator coroutine, TaskCompletionSource<bool> tcs)
        {
            yield return GetMonoBehaviour().StartCoroutine(coroutine);
            tcs.SetResult(true); // Complete the task when the coroutine is done
        }
    }
}
