using Blittables;
using HarmonyLib;
using Scripts.UserData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DataConst;

namespace NijiiroScoring.Plugins
{
    internal class NijiroScoringPatch
    {
        public static bool IsEnabled = false;

        static Queue<int> ScoreIncreaseQueue = new Queue<int>();

        static int CurrentScore;
        static int PreviousScore;

        static int Points = 1000;
        static int PointsOks = 500;

        static int UpdateFrameResults = -1;

        static int goods = 0;
        static int oks = 0;
        static int drumroll = 0;

        [HarmonyPatch(typeof(EnsoGameManager))]
        [HarmonyPatch(nameof(EnsoGameManager.ProcExecMain))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPostfix]
        public static void EnsoGameManager_ProcExecMain_Postfix(EnsoGameManager __instance)
        {
            if (IsEnabled)
            {
                UpdateFrameResults--;
                if (UpdateFrameResults != 0)
                {
                    return;
                }
                // GetFrameResults ends up getting called 8 or so times per frame
                // After the 4th time it's called, we want to take action on the result of it
                var frameResult = __instance.ensoParam.GetFrameResults();
                var player = frameResult.eachPlayer[0];
                if (player.countRyo != goods)
                {
                    goods = (int)player.countRyo;
                    ScoreIncreaseQueue.Enqueue(Points);
                }
                else if (player.countKa != oks)
                {
                    oks = (int)player.countKa;
                    ScoreIncreaseQueue.Enqueue(PointsOks);
                }
                else if (player.countRenda != drumroll)
                {
                    drumroll = (int)player.countRenda;
                    ScoreIncreaseQueue.Enqueue(100);
                }
            }
        }

        [HarmonyPatch(typeof(EnsoInput))]
        [HarmonyPatch(nameof(EnsoInput.UpdateController))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPostfix]
        static void EnsoInput_UpdateController_Postfix(EnsoInput __instance, EnsoInput.EnsoInputFlag __result)
        {
            if (IsEnabled)
            {
                if (__result != EnsoInput.EnsoInputFlag.None)
                {
                    UpdateFrameResults = 1;
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
        public static bool ScorePlayer_SetScore_Prefix(ScorePlayer __instance, ref int score, ref bool enableGreenLight, ref bool enableHighScoreBG, ref bool enableHighScoreEffect)
        {
            if (IsEnabled)
            {
                if (PreviousScore != CurrentScore)
                {


                    enableGreenLight = false;
                    enableHighScoreBG = false;
                    enableHighScoreEffect = false;

                    score = CurrentScore;
                    PreviousScore = score;
                    if (__instance.m_iHighScore != 0)
                    {
                        if (score >= __instance.m_iHighScore)
                        {
                            if (__instance.m_iReachScore < __instance.m_iHighScore)
                            {
                                enableHighScoreEffect = true;
                            }
                            else
                            {
                                enableHighScoreBG = true;
                            }
                        }
                        else if (score >= __instance.m_iReachScore)
                        {
                            enableGreenLight = true;
                        }
                    }
                    List<string> output = new List<string>()
                    {
                        "__instance.m_iHighScore: " + __instance.m_iHighScore,
                        "__instance.m_iPrevScore: " + __instance.m_iPrevScore,
                        "__instance.m_iReachScore: " + __instance.m_iReachScore,
                    };
                    Logger.Log(output);
                    return true;
                }
                return false;
            }
            Logger.Log("Set Score Return True");
            return true;
        }


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
                    PreviousScore = -1;
                    IsResult = false;
                    if (points != null)
                    {
                        Points = points.Points;
                        PointsOks = SongDataManager.GetOkPoints(Points);
                        goods = 0;
                        oks = 0;
                        drumroll = 0;
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

        static bool IsResult = false;
        [HarmonyPatch(typeof(EnsoGameManager))]
        [HarmonyPatch(nameof(EnsoGameManager.SetResults))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPrefix]
        static void EnsoGameManager_SetResults_Prefix(EnsoGameManager __instance)
        {
            IsResult = true;
        }

        [HarmonyPatch(typeof(EnsoPlayingParameter))]
        [HarmonyPatch(nameof(EnsoPlayingParameter.GetFrameResults))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPostfix]
        static void TaikoCoreFrameResults_GetEachPlayer_Postfix(EnsoPlayingParameter __instance, ref TaikoCoreFrameResults __result)
        {
            if (IsResult)
            {
                var eachPlayer = __result.eachPlayer[0];
                eachPlayer.score = (uint)CurrentScore;
                __result.eachPlayer[0] = eachPlayer;
            }
        }
    }
}
