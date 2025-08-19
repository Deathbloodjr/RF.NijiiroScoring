using Scripts.OutGame.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NijiiroScoring.Plugins
{
    internal class GetFumenDataUtility
    {
        public static async Task<byte[]> GetFumenDataAsync(string songId, EnsoData.EnsoLevelType level)
        {
            Logger.Log("GetFumenDataAsync for " + songId + ": " + level.ToString(), LogType.Debug);
            MusicDataInterface.MusicInfoAccesser infoByUniqueId = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData.GetInfoById(songId);

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

            if (infoByUniqueId.InPackage != MusicDataInterface.InPackageType.HasSongAndFumen)
            {
                if (PackedSongUtility.CheckSongFileExists(infoByUniqueId.UniqueId))
                {
                    await Plugin.Instance.WaitForCoroutine(playerData.ReadLocalStorageDataCoroutine(infoByUniqueId.UniqueId, fumenPath));
                }
                else
                {
                    playerData.Dispose();
                    Logger.Log("GetFumenDataAsync for " + songId + ": " + level.ToString() + " failed", LogType.Debug);
                    return Array.Empty<byte>();
                }
            }
            else
            {
                string filePath = Path.Combine(Application.streamingAssetsPath, "fumen", fumenPath);
                await Plugin.Instance.WaitForCoroutine(playerData.ReadCoroutine(filePath));
            }



            if (!playerData.isReadEnd ||
                !playerData.isReadSucceed)
            {
                playerData.Dispose();
                Logger.Log("GetFumenDataAsync for " + songId + ": " + level.ToString() + " failed", LogType.Debug);
                return Array.Empty<byte>();
            }

            byte[] fumenData = new byte[playerData.fumenSize];

            unsafe
            {
                var ptr = playerData.Pointer;
                var fumenDataPtr = playerData.Pointer + (8 * 3);
                var result = (void**)fumenDataPtr;
                var result2 = *result;

                Marshal.Copy((IntPtr)result2, fumenData, 0, playerData.fumenSize);
            }

            playerData.Dispose();

            //File.WriteAllBytes(@"C:\Other\Taiko\Code\Repositories\TDMX\zTmp\" + fumenPath, fumenData);

            Logger.Log("GetFumenDataAsync for " + songId + ": " + level.ToString() + " success", LogType.Debug);
            return fumenData;
        }
    }
}
