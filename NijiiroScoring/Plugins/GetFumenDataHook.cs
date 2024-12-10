using BepInEx.Unity.IL2CPP.Utils;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NijiiroScoring.Plugins
{
    internal class GetFumenDataHook
    {
        public static List<FumenData> ReadFumenData = new List<FumenData>();

        public static byte[] GetFumenDataResult(string songId, EnsoData.EnsoLevelType level)
        {
            var fumenData = ReadFumenData.Find((x) => x.songId == songId && x.level == level);
            if (fumenData != null && fumenData.data != null)
            {
                return fumenData.data;
            }
            return Array.Empty<byte>();
        }

        public static IEnumerator GetFumenData(string songId, EnsoData.EnsoLevelType level)
        {
            var fumenData = ReadFumenData.Find((x) => x.songId == songId && x.level == level);
            if (fumenData == null)
            {
                MusicDataInterface.MusicInfoAccesser musicInfo = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData.GetInfoById(songId);

                string fumenPath = songId + "_";
                switch (level)
                {
                    case EnsoData.EnsoLevelType.Easy: fumenPath += "e"; break;
                    case EnsoData.EnsoLevelType.Normal: fumenPath += "n"; break;
                    case EnsoData.EnsoLevelType.Hard: fumenPath += "h"; break;
                    case EnsoData.EnsoLevelType.Mania: fumenPath += "m"; break;
                    case EnsoData.EnsoLevelType.Ura: fumenPath += "x"; break;
                }
                fumenPath += ".bin";

                FumenLoader.PlayerData playerData = new FumenLoader.PlayerData();
                if (musicInfo.InPackage == MusicDataInterface.InPackageType.None)
                {
                    FumenData data = new FumenData();
                    data.path = fumenPath;
                    data.songId = songId;
                    data.level = level;
                    ReadFumenData.Add(data);
                    yield return TaikoSingletonMonoBehaviour<CommonObjects>.Instance.StartCoroutine(playerData.ReadLocalStorageDataCoroutine(musicInfo.UniqueId, fumenPath));
                }
                else if (musicInfo.InPackage == MusicDataInterface.InPackageType.HasSongAndFumen)
                {
                    string filePath = Path.Combine(Application.streamingAssetsPath, "fumen", fumenPath);
                    FumenData data = new FumenData();
                    data.path = filePath;
                    data.songId = songId;
                    data.level = level;
                    ReadFumenData.Add(data);
                    yield return TaikoSingletonMonoBehaviour<CommonObjects>.Instance.StartCoroutine(playerData.ReadCoroutine(filePath));
                }
            }
        }

        [HarmonyPatch(typeof(FumenLoader.PlayerData))]
        [HarmonyPatch(nameof(FumenLoader.PlayerData.WriteFumenBuffer))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPrefix]
        public static void FumenLoader_PlayerData_WriteFumenBuffer_Prefix(FumenLoader.PlayerData __instance, Il2CppStructArray<byte> data)
        {
            //Logger.Log("FumenLoader_PlayerData_WriteFumenBuffer_Prefix", LogType.Debug);
            //Logger.Log("__instance.fumenPath: " + __instance.fumenPath, LogType.Debug);
            //Logger.Log("data.Length: " + data.Length, LogType.Debug);

            var fumenData = ReadFumenData.Find((x) => x.path == __instance.fumenPath);
            if (fumenData != null)
            {
                List<byte> newBytes = new List<byte>();
                for (int i = 0; i < data.Length; i++)
                {
                    newBytes.Add(data[i]);
                }
                fumenData.data = newBytes.ToArray();
            }
        }
    }

    internal class FumenData
    {
        public string path { get; set; }
        public string songId { get; set; }
        public EnsoData.EnsoLevelType level { get; set; }
        public byte[] data { get; set; } = new byte[0];
    }
}
