using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime.Startup;
using Il2CppSystem;
using Il2CppSystem.Runtime.Serialization;
using Scripts.GameSystem;
using Scripts.OutGame.Title;
using Scripts.UserData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
using WebSocketSharp;
using static DataConst;
using static MusicDataInterface;

namespace NijiiroScoring.Plugins
{
    internal class NijiroScoringPatch
    {
        static Queue<int> ScoreIncreaseQueue = new Queue<int>();
        static uint CurrentScore;

        static bool IsEnabled = false;

        static int Points = 1000;

        // This works for setting the score directly
        [HarmonyPatch(typeof(TaikoCorePlayer))]
        [HarmonyPatch(nameof(TaikoCorePlayer.GetFrameResults))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPostfix]
        public static void TaikoCorePlayer_GetFrameResults_Postfix(TaikoCorePlayer __instance, ref TaikoCoreFrameResults __result)
        {
            //Plugin.Log.LogInfo("TaikoCorePlayer_GetFrameResults_Postfix");

            for (int i = 0; i < __result.eachPlayer.Length; i++)
            {
                var eachPlayer = __result.eachPlayer[i];

                var numGoods = __result.eachPlayer[i].countRyo;
                var numOk = __result.eachPlayer[i].countKa;
                var numRenda = __result.eachPlayer[i].countRenda;

                uint points = (uint)Points;

                uint newScore = (numGoods * points) + (numOk * (uint)SongDataManager.GetOkPoints(Points)) + (numRenda * 100);
                //Plugin.Log.LogInfo("newScore: " + newScore);
                //Plugin.Log.LogInfo("__result.eachPlayer[" + i + "].score: " + __result.eachPlayer[i].score);
                if (newScore != CurrentScore)
                {
                    ScoreIncreaseQueue.Enqueue((int)(newScore - CurrentScore));
                    CurrentScore = newScore;
                }
                eachPlayer.score = newScore;
                __result.eachPlayer[i] = eachPlayer;
                //Plugin.Log.LogInfo("__result.eachPlayer[" + i + "].score: " + __result.eachPlayer[i].score);

            }

        }

        // This works properly for adjusting the score change amount
        [HarmonyPatch(typeof(ScorePlayer))]
        [HarmonyPatch(nameof(ScorePlayer.SetAddScorePool))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPrefix]
        public static void ScorePlayer_SetAddScorePool_Prefix(ScorePlayer __instance, int index, ref int score)
        {
            if (ScoreIncreaseQueue.Count != 0)
            {
                score = ScoreIncreaseQueue.Dequeue();
            }
        }

        // I can't test if this function works properly
        [HarmonyPatch(typeof(EnsoGameManager))]
        [HarmonyPatch(nameof(EnsoGameManager.ProcLoading))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPrefix]
        public static void EnsoGameManager_ProcLoading_Prefix(EnsoGameManager __instance)
        {
            if (__instance.ensoParam.IsOnlineMode == false)
            {
                IsEnabled = true;
                Logger.Log("__instance.settings.musicUniqueId: " + __instance.settings.musicUniqueId);
                var musicInfo = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData.GetInfoByUniqueId(__instance.settings.musicUniqueId);
                var points = SongDataManager.GetSongDataPoints(musicInfo.Id, __instance.settings.ensoPlayerSettings[0].courseType);
                if (points != null)
                {
                    Points = points.Points;
                }
                else
                {

                }
            }
            else
            {
                IsEnabled = false;
            }
        }

       

        //[HarmonyPatch(typeof(UserData))]
        //[HarmonyPatch(nameof(UserData.OnLoaded))]
        //[HarmonyPatch(MethodType.Normal)]
        //[HarmonyPostfix]
        [HarmonyPatch(typeof(TitleSceneUiController))]
        [HarmonyPatch(nameof(TitleSceneUiController.StartAsync))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPostfix]
        public static void TitleSceneUiController_StartAsync_Postfix()
        {
            Logger.Log("TitleSceneUiController_StartAsync_Postfix");
            MusicsData musicData = SingletonMonoBehaviour<CommonObjects>.Instance.MusicData;
            MusicDataInterface musicDataInterface = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData;

            var datas = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.SaveData.Data.MusicsData.Datas;

            for (int i = 0; i < datas.Length; i++)
            {
                var data = datas[i];

                for (EnsoData.EnsoLevelType j = 0; j < EnsoData.EnsoLevelType.Num; j++)
                {
                    musicData.GetNormalRecordInfo(i, j, out var result);

                    int numGoods = result.normalHiScore.excellent;
                    int numOks = result.normalHiScore.good;
                    int numRenda = result.normalHiScore.renda;

                    var musicInfo = musicDataInterface.GetInfoByUniqueId(i);
                    if (musicInfo == null)
                    {
                        continue;
                    }

                    if (musicInfo.Debug)
                    {
                        continue;
                    }

                    if (musicInfo.Session != "")
                    {
                        continue;
                    }



                    var points = SongDataManager.GetSongDataPoints(musicInfo.Id, j);

                    if (points == null)
                    {
                        Logger.Log("Couldn't get points for song: " + musicInfo.Id);
                        continue;
                    }

                    // For numOks, it isn't quite Points / 2
                    // It gets rounded in a certain way, I wanna make sure I get it right
                    int value = (numGoods * points.Points) + (numOks * SongDataManager.GetOkPoints(points.Points)) + (numRenda * 100);

                    short topHalf = (short)(value << 16 >> 16);
                    short botHalf = (short)(value >> 16);


                    var hiScore = data.normalRecordInfo[0][(int)j];
                    switch (j)
                    {
                        case EnsoData.EnsoLevelType.Easy: hiScore.normalHiScore.score = value; break;
                        case EnsoData.EnsoLevelType.Normal: hiScore.normalHiScore.score = (((hiScore.normalHiScore.score << 16) >> 16) | topHalf << 16); hiScore.normalHiScore.excellent = botHalf; break;
                        case EnsoData.EnsoLevelType.Hard: hiScore.normalHiScore.excellent = topHalf; hiScore.normalHiScore.good = botHalf; break;
                        case EnsoData.EnsoLevelType.Mania: hiScore.normalHiScore.good = topHalf; hiScore.normalHiScore.bad = botHalf; break;
                        case EnsoData.EnsoLevelType.Ura: hiScore.normalHiScore.bad = topHalf; hiScore.normalHiScore.combo = botHalf; break;
                    }

                    data.normalRecordInfo[0][(int)j] = hiScore;
                }
            }
        }
    }
}
