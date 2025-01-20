using Cysharp.Threading.Tasks;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime.Startup;
using Il2CppSystem;
using Il2CppSystem.Runtime.Serialization;
using Scripts.GameSystem;
using Scripts.OutGame.Help;
using Scripts.OutGame.Title;
using Scripts.UserData;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
using WebSocketSharp;
using static DataConst;
using static MusicDataInterface;
using static TaikoCoreTypes;

namespace NijiiroScoring.Plugins
{
    public class NijiroScoringPatch
    {
        static Queue<int> ScoreIncreaseQueue = new Queue<int>();
        static int CurrentScore;

        public static bool IsEnabled = false;

        static int Points = 1000;
        static int PointsOks = 500;

        static int UpdateFrameResults = -1;

        static bool IsResults = false;

        static List<OnpuTypes> NoteTypes = new List<OnpuTypes>()
        {
            OnpuTypes.Don,
            OnpuTypes.Do,
            OnpuTypes.Ko,
            OnpuTypes.Katsu,
            OnpuTypes.Ka,
            OnpuTypes.DaiDon,
            OnpuTypes.DaiKatsu,

            OnpuTypes.WDon,
            OnpuTypes.WKatsu,
            OnpuTypes.KyodaiDon,
            OnpuTypes.KyodaiKatsu,
            OnpuTypes.SeparateRyoDon,
            OnpuTypes.SeparateRyoDo,
            OnpuTypes.SeparateRyoKo,
            OnpuTypes.SeparateRyoKatsu,
            OnpuTypes.SeparateRyoKa,
            OnpuTypes.SeparateKaDon,
            OnpuTypes.SeparateKaDo,
            OnpuTypes.SeparateKaKo,
            OnpuTypes.SeparateKaKatsu,
            OnpuTypes.SeparateKaKa,
        };
        static List<OnpuTypes> RendaTypes = new List<OnpuTypes>()
        {
            OnpuTypes.Renda,
            OnpuTypes.DaiRenda,
            OnpuTypes.GekiRenda,
            OnpuTypes.Imo,
        };

        static List<string> ParsedSongIds = new List<string>();
        static Dictionary<string, (MusicInfoAccesser musicInfo, bool isParsed)> ParsedMusicInfos = new Dictionary<string, (MusicInfoAccesser musicInfo, bool isParsed)>();

        // This was used previously
        [HarmonyPatch(typeof(EnsoGameManager))]
        [HarmonyPatch(nameof(EnsoGameManager.ProcExecMain))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPrefix]
        public static void EnsoGameManager_ProcExecMain_Prefix(EnsoGameManager __instance)
        {
            if (IsEnabled)
            {
                // GetFrameResults ends up getting called 8 or so times per frame
                // After the 4th time it's called, we want to take action on the result of it
                UpdateFrameResults = 4;
            }
        }

        //[HarmonyPatch(typeof(EnsoInput))]
        //[HarmonyPatch(nameof(EnsoInput.UpdateController))]
        //[HarmonyPatch(MethodType.Normal)]
        //[HarmonyPostfix]
        static void EnsoInput_UpdateController_Postfix(EnsoInput __instance, EnsoInput.EnsoInputFlag __result)
        {
            if (IsEnabled)
            {
                if (__result != EnsoInput.EnsoInputFlag.None)
                {
                    UpdateFrameResults = 5;
                }
            }
        }
        //[HarmonyPatch(typeof(EnsoSound))]
        //[HarmonyPatch(nameof(EnsoSound.KeyOnTone))]
        //[HarmonyPatch(MethodType.Normal)]
        //[HarmonyPrefix]
        //static void EnsoSound_KeyOnTone_Prefix(EnsoSound __instance, EnsoInput input)
        //{
        //    if (IsEnabled)
        //    {
        //        if (input.playerNum == 0)
        //        {

        //            UpdateFrameResults = 1;
        //        }
        //    }
        //}

        [HarmonyPatch(typeof(EnsoGameManager))]
        [HarmonyPatch(nameof(EnsoGameManager.SetResults))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPrefix]
        public static void EnsoGameManager_SetResults_Prefix(EnsoGameManager __instance)
        {
            if (IsEnabled)
            {
                IsResults = true;
            }
        }

        // This works for setting the score directly
        [HarmonyPatch(typeof(TaikoCorePlayer))]
        [HarmonyPatch(nameof(TaikoCorePlayer.GetFrameResults))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPostfix]
        static void TaikoCorePlayer_GetFrameResults_Postfix(TaikoCorePlayer __instance, ref TaikoCoreFrameResults __result)
        {
            // Plugin.Log.LogInfo("TaikoCorePlayer_GetFrameResults_Postfix");

            if (IsEnabled)
            {

                

                if (!IsResults)
                {
                    UpdateFrameResults--;
                    if (UpdateFrameResults != 0 && !IsResults)
                    {
                        return;
                    }


                    int numCounted = 0;
                    for (int i = 0; i < __result.hitResultInfoNum; i++)
                    {
                        var hitResult = __result.hitResultInfo[i];
                        if (hitResult.player != 0)
                        {
                            continue;
                        }
                        var type = (OnpuTypes)hitResult.onpuType;
                        var result = (HitResultTypes)hitResult.hitResult;
                        if (type == OnpuTypes.None || result == HitResultTypes.None)
                        {
                            continue;
                        }
                        numCounted++;
                        //hitResult.addBonusScore = 0;
                        //Logger.Log(type.ToString());
                        //Logger.Log(result.ToString());
                        if (NoteTypes.Contains(type))
                        {
                            if (result == HitResultTypes.Ryo)
                            {
                                //hitResult.addScore = Points;
                                ScoreIncreaseQueue.Enqueue(Points);
                            }
                            else if (result == HitResultTypes.Ka)
                            {
                                //hitResult.addScore = PointsOks;
                                ScoreIncreaseQueue.Enqueue(PointsOks);
                            }
                        }
                        else if (RendaTypes.Contains(type))
                        {
                            if (result == HitResultTypes.Ryo)
                            {
                                //hitResult.addScore = 100;
                                ScoreIncreaseQueue.Enqueue(100);
                            }
                        }
                        //__result.hitResultInfo[i] = hitResult;
                    }
                    // This causes the score to be incorrect for a frame
                    //if (numCounted == 0)
                    //{
                    //    return;
                    //}
                    //for (int i = 0; i < Mathf.Min(1, __result.eachPlayer.Length); i++)
                    //{
                    //    var eachPlayer = __result.eachPlayer[i];

                    //    var numGoods = __result.eachPlayer[i].countRyo;
                    //    var numOk = __result.eachPlayer[i].countKa;
                    //    var numRenda = __result.eachPlayer[i].countRenda;

                    //    uint points = (uint)Points;
                    //    uint pointsOks = (uint)PointsOks;

                    //    uint newScore = (numGoods * points) + (numOk * pointsOks) + (numRenda * 100);
                    //    //Plugin.Log.LogInfo("newScore: " + newScore);
                    //    //Plugin.Log.LogInfo("__result.eachPlayer[" + i + "].score: " + __result.eachPlayer[i].score);
                    //    if (newScore != CurrentScore)
                    //    {
                    //        //ScoreIncreaseQueue.Enqueue((int)(newScore - CurrentScore));
                    //        CurrentScore = (int)newScore;
                    //    }

                    //    eachPlayer.score = newScore;
                    //    __result.eachPlayer[i] = eachPlayer;
                    //    //Plugin.Log.LogInfo("__result.eachPlayer[" + i + "].score: " + __result.eachPlayer[i].score);

                    //}
                }
                else
                {



                    for (int i = 0; i < Mathf.Min(1, __result.eachPlayer.Length); i++)
                    {
                        var eachPlayer = __result.eachPlayer[i];

                        var numGoods = __result.eachPlayer[i].countRyo;
                        var numOk = __result.eachPlayer[i].countKa;
                        var numRenda = __result.eachPlayer[i].countRenda;

                        uint points = (uint)Points;
                        uint pointsOks = (uint)PointsOks;

                        uint newScore = (numGoods * points) + (numOk * pointsOks) + (numRenda * 100);
                        //Plugin.Log.LogInfo("newScore: " + newScore);
                        //Plugin.Log.LogInfo("__result.eachPlayer[" + i + "].score: " + __result.eachPlayer[i].score);
                        if (newScore != CurrentScore)
                        {
                            //ScoreIncreaseQueue.Enqueue((int)(newScore - CurrentScore));
                            CurrentScore = (int)newScore;
                        }

                        eachPlayer.score = newScore;
                        __result.eachPlayer[i] = eachPlayer;
                        //Plugin.Log.LogInfo("__result.eachPlayer[" + i + "].score: " + __result.eachPlayer[i].score);

                    }
                }
            }
        }

        [HarmonyPatch(typeof(ScorePlayer))]
        [HarmonyPatch(nameof(ScorePlayer.SetAddScorePool))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPrefix]
        public static void ScorePlayer_SetAddScorePool_Prefix(ScorePlayer __instance, int index, ref int score)
        {
            if (IsEnabled)
            {
                if (ScoreIncreaseQueue.Count != 0)
                {
                    score = ScoreIncreaseQueue.Dequeue();
                    CurrentScore += score;
                }
            }
        }

        [HarmonyPatch(typeof(ScorePlayer))]
        [HarmonyPatch(nameof(ScorePlayer.SetScore))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPrefix]
        public static void ScorePlayer_SetScore_Prefix(ScorePlayer __instance, ref int score, ref bool enableGreenLight, ref bool enableHighScoreBg, ref bool enableHighScoreEffect)
        {
            if (IsEnabled)
            {
                enableGreenLight = false;
                enableHighScoreBg = false;
                enableHighScoreEffect = false;

                score = CurrentScore;
                if (score >= __instance.m_iHighScore)
                {
                    if (__instance.m_iReachScore < __instance.m_iHighScore)
                    {
                        enableHighScoreEffect = true;
                    }
                    else
                    {
                        enableHighScoreBg = true;
                    }
                }
                else if (score >= __instance.m_iReachScore)
                {
                    enableGreenLight = true;
                }
                List<string> output = new List<string>()
                {
                    "__instance.m_iHighScore: " + __instance.m_iHighScore,
                    "__instance.m_iPrevScore: " + __instance.m_iPrevScore,
                    "__instance.m_iReachScore: " + __instance.m_iReachScore,
                };
                Logger.Log(output);
            }
        }

        // I can't test if this function works properly
        [HarmonyPatch(typeof(EnsoGameManager))]
        [HarmonyPatch(nameof(EnsoGameManager.ProcLoading))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPrefix]
        static void EnsoGameManager_ProcLoading_Prefix(EnsoGameManager __instance)
        {
            if (__instance.ensoParam.IsOnlineMode == false)
            {
                if (__instance.settings.ensoPlayerSettings[0].shinuchi == OptionOnOff.On)
                {
                    IsEnabled = false;
                }
                else
                {
                    IsEnabled = true;
                    var musicInfo = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData.GetInfoByUniqueId(__instance.settings.musicUniqueId);
                    var points = SongDataManager.GetSongDataPoints(musicInfo.Id, __instance.settings.ensoPlayerSettings[0].courseType);
                    CurrentScore = 0;
                    IsResults = false;
                    if (points != null)
                    {
                        Points = points.Points;
                        PointsOks = SongDataManager.GetOkPoints(Points);
                    }
                    else
                    {
                        IsEnabled = false;
                    }
                }
            }
            else
            {
                IsEnabled = false;
            }
        }


        static bool IsUserDataLoaded = false;
        [HarmonyPatch(typeof(UserData))]
        [HarmonyPatch(nameof(UserData.OnLoaded))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPostfix]
        static void UserData_OnLoaded_Postfix()
        {
            //Logger.Log("UserData_OnLoaded_Postfix");
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


        [HarmonyPatch(typeof(MusicDataInterface))]
        [HarmonyPatch(nameof(MusicDataInterface.AddMusicInfo))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPostfix]
        public static void MusicDataInterface_AddMusicInfo_Postfix(MusicDataInterface __instance, MusicDataInterface.MusicInfo musicinfo)
        {
            //Logger.Log("MusicDataInterface_AddMusicInfo_Postfix: __instance.Id: " + musicinfo.Id);
            if (!ParsedMusicInfos.ContainsKey(musicinfo.Id))
            {
                if (IsUserDataLoaded)
                {
                    ParsedMusicInfos.Add(musicinfo.Id, (__instance.GetInfoById(musicinfo.Id), true));
                    Plugin.Instance.StartCoroutine(LoadSongPointsByMusicInfo(__instance.GetInfoById(musicinfo.Id)));
                }
                else
                {
                    ParsedMusicInfos.Add(musicinfo.Id, (__instance.GetInfoById(musicinfo.Id), false));
                }
            }
        }

        static IEnumerator LoadSongPoints()
        {
            Logger.Log("Loading Nijiro Point Values");
            MusicsData musicData = SingletonMonoBehaviour<CommonObjects>.Instance.MusicData;
            MusicDataInterface musicDataInterface = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData;

            var datas = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.SaveData.Data.MusicsData.Datas;

            SongDataManager.PauseExporting(true);

            for (int i = 0; i < datas.Length; i++)
            {
                if (i % 100 == 0)
                {
                    Logger.Log(i + "/" + datas.Length);
                }
                var data = datas[i];
                var musicInfo = musicDataInterface.GetInfoByUniqueId(i);
                //if (!data.IsDownloaded)
                //{
                //    continue;
                //}
                // Skip songs that don't have MusicInfo
                if (musicInfo == null)
                {
                    continue;
                }
                Logger.Log("SongId: " + musicInfo.Id);
                // Skip debug songs (tmap4, kakunin, etc)
                if (musicInfo.Debug)
                {
                    continue;
                }
                // Skip session songs from Taiko Band mode
                if (musicInfo.Session != "")
                {
                    continue;
                }

#if DEBUG
                Stopwatch sw = Stopwatch.StartNew();
#endif

                yield return SongDataManager.VerifySongDataPoints(musicInfo.Id);
                for (EnsoData.EnsoLevelType j = 0; j < EnsoData.EnsoLevelType.Num; j++)
                {
                    if (musicInfo.Stars[(int)j] == 0)
                    {
                        continue;
                    }

                    musicData.GetNormalRecordInfo(i, j, out var result);

                    int numGoods = result.normalHiScore.excellent;
                    int numOks = result.normalHiScore.good;
                    int numRenda = result.normalHiScore.renda;


                    //yield return SongDataManager.VerifySongDataPoints(musicInfo.Id, j, calculate);
                    var points = SongDataManager.GetSongDataPoints(musicInfo.Id, j);

                    if (points == null)
                    {
                        Logger.Log("Couldn't get points for song: " + musicInfo.Id);
                        break;
                    }

                    // This part's fucked
                    // Basically, the struct wasn't lining up properly when assigning values to different difficulties
                    // Assigning values to OKs on easy were just fine, but assigning a value to OKs on normal would assign it to Goods
                    // Assigning values to OKs on hard would assign it to Score, etc

                    int value = (numGoods * points.Points) + (numOks * SongDataManager.GetOkPoints(points.Points)) + (numRenda * 100);
                    Logger.Log("New value for songId: " + musicInfo.Id + " Difficulty: " + j.ToString() + ": " + value);

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
                }
#if DEBUG
                sw.Stop();
                Logger.Log("Song number " + i + " took " + sw.ElapsedMilliseconds + "ms");
#endif
            }
            SongDataManager.PauseExporting(false);
            SongDataManager.ExportSongData();
            Logger.Log("Nijiro Point Values Loaded");
        }

        static IEnumerator LoadSongPointsByMusicInfo()
        {
            Logger.Log("Loading Nijiro Point Values");
            MusicsData musicData = SingletonMonoBehaviour<CommonObjects>.Instance.MusicData;
            MusicDataInterface musicDataInterface = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData;

            var datas = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.SaveData.Data.MusicsData.Datas;

            SongDataManager.PauseExporting(true);

            for (int i = 0; i < musicDataInterface.MusicInfoAccesserList.Count; i++)
            {
                var musicInfo = musicDataInterface.MusicInfoAccesserList[i];

                if (musicInfo == null)
                {
                    continue;
                }
                Logger.Log("SongId: " + musicInfo.Id);
                // Skip debug songs (tmap4, kakunin, etc)
                if (musicInfo.Debug)
                {
                    continue;
                }
                // Skip session songs from Taiko Band mode
                if (musicInfo.Session != "")
                {
                    continue;
                }

                var data = datas[musicInfo.UniqueId];
#if DEBUG
                Stopwatch sw = Stopwatch.StartNew();
#endif

                yield return SongDataManager.VerifySongDataPoints(musicInfo.Id);
                for (EnsoData.EnsoLevelType j = 0; j < EnsoData.EnsoLevelType.Num; j++)
                {
                    if (musicInfo.Stars[(int)j] == 0)
                    {
                        continue;
                    }

                    musicData.GetNormalRecordInfo(i, j, out var result);

                    int numGoods = result.normalHiScore.excellent;
                    int numOks = result.normalHiScore.good;
                    int numRenda = result.normalHiScore.renda;


                    //yield return SongDataManager.VerifySongDataPoints(musicInfo.Id, j, calculate);
                    var points = SongDataManager.GetSongDataPoints(musicInfo.Id, j);

                    if (points == null)
                    {
                        Logger.Log("Couldn't get points for song: " + musicInfo.Id);
                        break;
                    }

                    // This part's fucked
                    // Basically, the struct wasn't lining up properly when assigning values to different difficulties
                    // Assigning values to OKs on easy were just fine, but assigning a value to OKs on normal would assign it to Goods
                    // Assigning values to OKs on hard would assign it to Score, etc

                    int value = (numGoods * points.Points) + (numOks * SongDataManager.GetOkPoints(points.Points)) + (numRenda * 100);
                    Logger.Log("New value for songId: " + musicInfo.Id + " Difficulty: " + j.ToString() + ": " + value);

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
                }
#if DEBUG
                sw.Stop();
                Logger.Log("Song number " + i + " took " + sw.ElapsedMilliseconds + "ms");
#endif
            }

            SongDataManager.PauseExporting(false);
            SongDataManager.ExportSongData();
            Logger.Log("Nijiro Point Values Loaded");
        }

        static IEnumerator LoadSongPointsByMusicInfo(MusicInfoAccesser musicInfo)
        {
            //Logger.Log("Loading Nijiro Point Values");
            MusicsData musicData = SingletonMonoBehaviour<CommonObjects>.Instance.MusicData;

            var datas = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.SaveData.Data.MusicsData.Datas;

            SongDataManager.PauseExporting(true);


            if (musicInfo != null && !musicInfo.Debug && musicInfo.Session == "")
            {
                var data = datas[musicInfo.UniqueId];

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

                    // This part's fucked
                    // Basically, the struct wasn't lining up properly when assigning values to different difficulties
                    // Assigning values to OKs on easy were just fine, but assigning a value to OKs on normal would assign it to Goods
                    // Assigning values to OKs on hard would assign it to Score, etc
                    int value = (numGoods * points.Points) + (numOks * SongDataManager.GetOkPoints(points.Points)) + (numRenda * 100);

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
            Logger.Log("New song downloaded");
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
