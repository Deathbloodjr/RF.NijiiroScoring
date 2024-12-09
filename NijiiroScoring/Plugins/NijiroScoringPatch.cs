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

                uint points = 1000;

                uint newScore = (numGoods * points) + (numOk * (points / 2)) + (numRenda * 100);
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
            }
            else
            {
                IsEnabled = false;
            }
        }

        // Can't touch this function without it breaking
        //[HarmonyPatch(typeof(MusicDataUtility))]
        //[HarmonyPatch(nameof(MusicDataUtility.GetNormalRecordInfo))]
        //[HarmonyPatch(new Type[] { typeof(int), typeof(int), typeof(EnsoData.EnsoLevelType), typeof(EnsoRecordInfo) },
        //              new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })]
        //[HarmonyPatch(MethodType.Normal)]
        //[HarmonyPrefix]
        //public static void MusicDataUtility_GetNormalRecordInfo_Prefix(int playerNo, int songUid, EnsoData.EnsoLevelType lv)
        //{
        //    Logger.Log("MusicDataUtility_GetNormalRecordInfo_Prefix");
        //    if (playerNo == 0)
        //    {
        //        UserData data = SingletonMonoBehaviour<CommonObjects>.Instance.SaveData.data; ;
        //        var musicInfoEx = data.MusicsData.Datas[songUid];
        //        //musicInfoEx.normalRecordInfo[0][(int)lv].normalHiScore.score;
        //    }

        //for (int j = 0; j < __instance.MusicsData.Datas.Count; j++)
        //{
        //    var data = __instance.MusicsData.Datas[j];
        //    if (data != null)
        //    {
        //        for (EnsoData.EnsoLevelType i = 0; i < EnsoData.EnsoLevelType.Num; i++)
        //        {
        //            MusicDataUtility.GetNormalRecordInfo(0, j, i, out var result);

        //            var numGoods = result.normalHiScore.excellent;
        //            var numOk = result.normalHiScore.good;
        //            var numRenda = result.normalHiScore.renda;

        //            int points = 1000;

        //            int newScore = (numGoods * points) + (numOk * (points / 2)) + (numRenda * 100);
        //            result.normalHiScore.score = newScore;

        //            MusicsData musicData = SingletonMonoBehaviour<CommonObjects>.Instance.MusicData;
        //            musicData.UpdateNormalRecordInfo(j, i, false, ref result.normalHiScore, result.crown);
        //        }
        //    }
        //}


        //}


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

            
            //var hiScore = dst.normalHiScore;
            //hiScore.score = 956720;
            //musicData.UpdateNormalRecordInfo(115, EnsoData.EnsoLevelType.Mania, false, ref hiScore, DataConst.CrownType.Rainbow);

            // This only works for changing Easy scores for some reason
            var datas = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.SaveData.Data.MusicsData.Datas;
            int i = 137;


            //for (int i = 0; i < datas.Length; i++)
            {
                //Logger.Log("i: " + i);
                //var data = datas[115];
                var data = datas[i];
                //Il2CppReferenceArray<Il2CppStructArray<EnsoRecordInfo>> normalRecordInfo = new Il2CppReferenceArray<Il2CppStructArray<EnsoRecordInfo>>(1);

                for (EnsoData.EnsoLevelType j = 0; j < EnsoData.EnsoLevelType.Num; j++)
                {
                    // Proof that the bytes are not lining up properly throughout the array
                    // Easy showed 10-20-30-40-50-60
                    // Normal showed 10000000-30-40-50-60-xx

                    int value = 1000000 + (int)j;
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

                    //EnsoRecordInfo hiScore = new EnsoRecordInfo();
                    //hiScore.normalHiScore.score = 10 + (int)j;
                    //hiScore.normalHiScore.excellent = (short)(20 + (short)j);
                    //hiScore.normalHiScore.good = (short)(30 + (short)j);
                    //hiScore.normalHiScore.bad = (short)(40 + (short)j);
                    //hiScore.normalHiScore.combo = (short)(50 + (short)j);
                    //hiScore.normalHiScore.renda = (short)(60 + (short)j);

                    //hiScore.shinuchiHiScore.score = 70 + (int)j;
                    //hiScore.shinuchiHiScore.excellent = (short)(80 + (short)j);
                    //hiScore.shinuchiHiScore.good = (short)(90 + (short)j);
                    //hiScore.shinuchiHiScore.bad = (short)(100 + (short)j);
                    //hiScore.shinuchiHiScore.combo = (short)(110 + (short)j);
                    //hiScore.shinuchiHiScore.renda = (short)(120 + (short)j);

                    data.normalRecordInfo[0][(int)j] = hiScore;
                }







                //int sizeData = Marshal.SizeOf(data);
                //int sizeData = 1000;
                //byte[] arrData = new byte[sizeData];

                //IntPtr ptr2 = IntPtr.Zero;
                //try
                //{
                //    ptr2 = Marshal.AllocHGlobal(sizeData);
                //    Marshal.StructureToPtr(data, ptr2, false);
                //    Marshal.Copy(new IntPtr(), arrData, 0, sizeData);
                //}
                //finally
                //{
                //    Marshal.FreeHGlobal(ptr2);
                //}



                //File.WriteAllBytes(@"C:\Other\Tako\RhythmFestival\Workspace\testdata.bin", byteArray);
















                //if (data != null)
                //{
                //    for (int j = 0; j < data.normalRecordInfo.Length; j++)
                //    {
                //        //Logger.Log("j: " + j);
                //        for (int k = 0; k < data.normalRecordInfo[j].Length; k++)
                //        {
                //            //Logger.Log("k: " + k);
                //            var highScoreData = data.normalRecordInfo[j][k];
                //            highScoreData.normalHiScore.score = 956720;
                //            data.UpdateNormalRecordInfo((EnsoData.EnsoLevelType)j, false, ref highScoreData.normalHiScore, highScoreData.crown);
                //            //data.normalRecordInfo[j][k] = highScoreData;
                //        }
                //    }
                //}
            }

        }

        //[HarmonyPatch(typeof(TitleSceneUiController))]
        //[HarmonyPatch(nameof(TitleSceneUiController.StartAsync))]
        //[HarmonyPatch(MethodType.Normal)]
        //[HarmonyPrefix]
        //public static bool TitleSceneUiController_StartAsync_Prefix(TitleSceneUiController __instance)
        //{
        //    Logger.Log("TitleSceneUiController_StartAsync_Prefix");
        //    var datas = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MusicData.Datas;
        //    //for (int i = 0; i < datas.Length; i++)
        //    {
        //        //Logger.Log("i: " + i);
        //        var data = datas[115];
        //        if (data != null)
        //        {
        //            for (int j = 0; j < data.normalRecordInfo.Length; j++)
        //            {
        //                //Logger.Log("j: " + j);
        //                for (int k = 0; k < data.normalRecordInfo[j].Length; k++)
        //                {
        //                    //Logger.Log("k: " + k);
        //                    var highScoreData = data.normalRecordInfo[j][k];
        //                    highScoreData.normalHiScore.score = 1003;
        //                    data.normalRecordInfo[j][k] = highScoreData;
        //                }
        //            }
        //        }
        //    }
        //    return true;
        //}

    }
}
