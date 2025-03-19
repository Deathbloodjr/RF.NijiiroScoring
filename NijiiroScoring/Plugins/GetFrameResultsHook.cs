using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NijiiroScoring.Plugins
{
    internal class GetFrameResultsHook
    {
        [HarmonyPatch(typeof(EnsoPlayingParameter))]
        [HarmonyPatch(nameof(EnsoPlayingParameter.GetFrameResults))]
        [HarmonyPatch(MethodType.Normal)]
        [HarmonyPostfix]
        static void EnsoPlayingParameter_GetFrameResults_Postfix(EnsoPlayingParameter __instance, ref TaikoCoreFrameResults __result)
        {
            var eachPlayer = __result.eachPlayer[0];
            eachPlayer.score = eachPlayer.countRyo * (uint)NijiroScoringPatch.Points;
            eachPlayer.score += eachPlayer.countKa * (uint)NijiroScoringPatch.PointsOks;
            eachPlayer.score += eachPlayer.countRenda * 100;

            __result.eachPlayer[0] = eachPlayer;
            Console.WriteLine("EnsoPlayingParameter_GetFrameResults_Postfix");
            Plugin.UnpatchMethod(nameof(EnsoPlayingParameter.GetFrameResults));
        }
    }
}
