using HarmonyLib;
using Scripts.GameSystem;
using Scripts.UserData;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MusicDataInterface;

namespace NijiiroScoring.Plugins
{
    internal class NijiroScoreLoading
    {
        static List<string> ParsedSongIds = new List<string>();
        static Dictionary<string, (MusicInfoAccesser musicInfo, bool isParsed)> ParsedMusicInfos = new Dictionary<string, (MusicInfoAccesser musicInfo, bool isParsed)>();

        static bool IsUserDataLoaded = false;
        [HarmonyPatch(typeof(UserData))]
        [HarmonyPatch(nameof(UserData.OnLoaded))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPostfix]
        static void UserData_OnLoaded_Postfix()
        {
            //Logger.Log("UserData_OnLoaded_Postfix");
            // This part's basically no longer needed, as SetMusicInfo can grab every song
            // And this part was just used to get anything missed from AddMusicInfo
            MusicDataInterface musicInfos = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData;
            for (int i = 0; i < musicInfos.MusicInfoAccesserList.Count; i++)
            {
                var musicInfo = musicInfos.MusicInfoAccesserList[i];
                if (musicInfo is null)
                {
                    continue;
                }

                bool found = false;
                foreach (var item in ParsedMusicInfos)
                {
                    if (ParsedMusicInfos.ContainsKey(item.Value.musicInfo.Id))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    //Logger.Log("Adding MusicInfo in OnLoaded: " + musicInfo.Id);
                    ParsedMusicInfos.Add(musicInfo.Id, (musicInfo, false));
                }
            }
            

            IsUserDataLoaded = true;
            foreach (var item in ParsedMusicInfos.Values)
            {
                if (!item.isParsed)
                {
                    ParsedMusicInfos[item.musicInfo.Id] = (item.musicInfo, true);
                    Plugin.Instance.StartCoroutine(LoadSongPointsByMusicInfo(item.musicInfo));
                }
            }
        }

        [HarmonyPatch(typeof(MusicInfoAccesser))]
        [HarmonyPatch(nameof(MusicInfoAccesser.SetMusicInfo))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPostfix]
        public static void MusicInfoAccesser_SetMusicInfo_Postfix(MusicInfoAccesser __instance, MusicDataInterface.MusicInfo musicinfo)
        {
            //Logger.Log("MusicDataInterface_AddMusicInfo_Postfix: __instance.Id: " + musicinfo.Id);
            if (!ParsedMusicInfos.ContainsKey(musicinfo.Id))
            {
                //Logger.Log("Adding MusicInfo in MusicDataInterface: " + musicinfo.Id);
                if (IsUserDataLoaded)
                {
                    ParsedMusicInfos.Add(musicinfo.Id, (__instance, true));
                    Plugin.Instance.StartCoroutine(LoadSongPointsByMusicInfo(__instance));
                }
                else
                {
                    ParsedMusicInfos.Add(musicinfo.Id, (__instance, false));
                }
            }
        }

        //[HarmonyPatch(typeof(MusicDataInterface))]
        //[HarmonyPatch(nameof(MusicDataInterface.AddMusicInfo))]
        //[HarmonyPatch(MethodType.Normal)]
        //[HarmonyPostfix]
        //public static void MusicDataInterface_AddMusicInfo_Postfix(MusicDataInterface __instance, MusicDataInterface.MusicInfo musicinfo)
        //{
        //    //Logger.Log("MusicDataInterface_AddMusicInfo_Postfix: __instance.Id: " + musicinfo.Id);
        //    if (!ParsedMusicInfos.ContainsKey(musicinfo.Id))
        //    {
        //        Logger.Log("Adding MusicInfo in MusicDataInterface: " + musicinfo.Id);
        //        if (IsUserDataLoaded)
        //        {
        //            ParsedMusicInfos.Add(musicinfo.Id, (__instance.GetInfoById(musicinfo.Id), true));
        //            Plugin.Instance.StartCoroutine(LoadSongPointsByMusicInfo(__instance.GetInfoById(musicinfo.Id)));
        //        }
        //        else
        //        {
        //            ParsedMusicInfos.Add(musicinfo.Id, (__instance.GetInfoById(musicinfo.Id), false));
        //        }
        //    }
        //}

        static IEnumerator LoadSongPointsByMusicInfo(MusicInfoAccesser musicInfo)
        {
            //Logger.Log("Loading Nijiro Point Values");
            MusicsData musicData = SingletonMonoBehaviour<CommonObjects>.Instance.MusicData;

            var datas = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.SaveData.Data.MusicsData.Datas;

            SongDataManager.PauseExporting(true);


            if (musicInfo != null && !musicInfo.Debug && musicInfo.Session == "")
            {
                var data = datas[musicInfo.UniqueId];
                //Logger.Log("LoadSongPointsByMusicInfo start for song: " + musicInfo.Id);

                yield return SongDataManager.VerifySongDataPoints(musicInfo);
                for (EnsoData.EnsoLevelType j = 0; j < EnsoData.EnsoLevelType.Num; j++)
                {
                    if (musicInfo.Stars[(int)j] == 0)
                    {
                        continue;
                    }

                    musicData.GetNormalRecordInfo(musicInfo.UniqueId, j, out var result);

                    int numGoods = result.normalHiScore.excellent;
                    int numOks = result.normalHiScore.good;
                    int numRenda = result.normalHiScore.renda;


                    var points = SongDataManager.GetSongDataPoints(musicInfo.Id, j);

                    if (points == null)
                    {
                        Logger.Log("Couldn't get points for song: " + musicInfo.Id);
                        break;
                    }
                    if (!SongDataManager.IsValidPoints(points.Points, points.ScoreRank))
                    {
                        break;
                    }

                    // This part's fucked
                    // Basically, the struct wasn't lining up properly when assigning values to different difficulties
                    // Assigning values to OKs on easy were just fine, but assigning a value to OKs on normal would assign it to Goods
                    // Assigning values to OKs on hard would assign it to Score, etc
                    int value = (numGoods * points.Points) + (numOks * SongDataManager.GetOkPoints(points.Points)) + (numRenda * 100);

                    //Logger.Log("Song " + musicInfo.Id + " calculated score of " + value + " for difficulty " + j.ToString());

                    // Split the 4 byte value into two 2 byte values
                    short topHalf = (short)(value << 16 >> 16);
                    short botHalf = (short)(value >> 16);

                    var hiScore = data.normalRecordInfo[0][(int)j];
                    switch (j)
                    {
                        case EnsoData.EnsoLevelType.Easy:
                            hiScore.normalHiScore.score = value;
                            break;
                        case EnsoData.EnsoLevelType.Normal:
                            // This should assign the value to only the 2 bytes of the score that we want to change
                            // While not overwriting any of the 2 bytes of score that were there previously
                            // I don't even know what data would be there, but I don't want to overwrite it to be safe
                            hiScore.normalHiScore.score = (((hiScore.normalHiScore.score << 16) >> 16) | topHalf << 16);
                            hiScore.normalHiScore.excellent = botHalf;
                            break;
                        case EnsoData.EnsoLevelType.Hard:
                            hiScore.normalHiScore.excellent = topHalf;
                            hiScore.normalHiScore.good = botHalf;
                            break;
                        case EnsoData.EnsoLevelType.Mania:
                            hiScore.normalHiScore.good = topHalf;
                            hiScore.normalHiScore.bad = botHalf;
                            break;
                        case EnsoData.EnsoLevelType.Ura:
                            hiScore.normalHiScore.bad = topHalf;
                            hiScore.normalHiScore.combo = botHalf;
                            break;
                    }

                    data.normalRecordInfo[0][(int)j] = hiScore;
                    Logger.Log("data.normalRecordInfo set for song: " + musicInfo.Id, LogType.Debug);
                }
            }
            SongDataManager.PauseExporting(false);
            SongDataManager.ExportSongData();
        }


        [HarmonyPatch(typeof(MusicsData))]
        [HarmonyPatch(nameof(MusicsData.AddDownloadedSong))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPostfix]
        static void MusicsData_AddDownloadedSong_Postfix(MusicsData __instance, int songUid)
        {
            Logger.Log("New song downloaded", LogType.Debug);
            Plugin.Instance.StartCoroutine(UpdateNewlyDownloadedSong(songUid));

        }

        static IEnumerator UpdateNewlyDownloadedSong(int songUid)
        {
            yield return SongDataManager.VerifySongDataPoints(songUid);
            MusicDataInterface.MusicInfoAccesser musicInfo = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData.GetInfoByUniqueId(songUid);
            yield return LoadSongPointsByMusicInfo(musicInfo);
            Logger.Log("Nijiro points calculated for newly downloaded song");
        }
    }
}
