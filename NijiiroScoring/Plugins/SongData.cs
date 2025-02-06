using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using UnityEngine;

namespace NijiiroScoring.Plugins
{
    public class SongData
    {
        public string SongId { get; set; }
        public Dictionary<EnsoData.EnsoLevelType, SongDataPoints> Points { get; set; } = new Dictionary<EnsoData.EnsoLevelType, SongDataPoints>();
    }

    public class SongDataPoints
    {
        public int Points { get; set; }
        public int ScoreRank { get; set; }
    }

    public class SongDataManager
    {
        static string FilePath = Path.Combine("BepInEx", "data", "NijiiroScoring", "SongData.json");
        static bool IsInitialized = false;
        static bool IsInitializedFromTakoTako = false;

        static bool IsPauseExporting = false;

        static Dictionary<string, SongData> AllSongData = new Dictionary<string, SongData>();

        static void Initialize()
        {
            if (!IsInitialized)
            {
                // This will read in the json made for this mod
                if (File.Exists(FilePath))
                {
                    try
                    {
                        JsonArray node = JsonNode.Parse(File.ReadAllText(FilePath)).AsArray();
                        for (int i = 0; i < node.Count; i++)
                        {
                            SongData data = new SongData();
                            data.SongId = node[i]["SongId"].GetValue<string>();
                            for (EnsoData.EnsoLevelType j = 0; j < EnsoData.EnsoLevelType.Num; j++)
                            {
                                string diff = j.ToString();
                                if (j == EnsoData.EnsoLevelType.Mania)
                                {
                                    diff = "Oni";
                                }
                                SongDataPoints points = new SongDataPoints();
                                points.Points = node[i][diff]["Points"].GetValue<int>();
                                points.ScoreRank = node[i][diff]["ScoreRank"].GetValue<int>();
                                data.Points.Add(j, points);
                            }
                            AllSongData.Add(data.SongId, data);
                        }
                    }
                    catch
                    {

                    }
                }

                IsInitialized = true;
            }
        }

        static void InitializeFromTakoTako()
        {
            if (!IsInitializedFromTakoTako)
            {
                // This will read in all the files from TakoTako, then output the json made for this mod
                if (Directory.Exists(Plugin.Instance.ConfigTakoTakoPath.Value))
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(Plugin.Instance.ConfigTakoTakoPath.Value);
                    var files = dirInfo.GetFiles("data.json", SearchOption.AllDirectories).ToList();
                    for (int i = 0; i < files.Count; i++)
                    {
                        try
                        {
                            var node = JsonNode.Parse(File.ReadAllText(files[i].FullName));
                            SongData data = new SongData();
                            data.SongId = node["id"].GetValue<string>();
                            if (AllSongData.ContainsKey(data.SongId))
                            {
                                continue;
                            }
                            for (EnsoData.EnsoLevelType j = 0; j < EnsoData.EnsoLevelType.Num; j++)
                            {
                                string diff = j.ToString();
                                if (j == EnsoData.EnsoLevelType.Mania)
                                {
                                    diff = "Mania";
                                }
                                SongDataPoints points = new SongDataPoints();
                                points.Points = node["shinuti" + diff].GetValue<int>();
                                points.ScoreRank = node["score" + diff].GetValue<int>();
                                data.Points.Add(j, points);
                            }
                            AllSongData.Add(data.SongId, data);
                        }
                        catch
                        {
                            //Logger.Log("Error parsing TakoTako data.json file: " + files[i].Directory.Name, LogType.Error);
                        }
                    }
                    ExportSongData();
                }
                IsInitializedFromTakoTako = true;
            }
        }

        static IEnumerator CalculateSongPointValues(string songId, EnsoData.EnsoLevelType level)
        {
            MusicDataInterface.MusicInfoAccesser musicInfo = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData.GetInfoById(songId);
            return CalculateSongPointValues(musicInfo, level);
        }

        static IEnumerator CalculateSongPointValues(MusicDataInterface.MusicInfoAccesser musicInfo, EnsoData.EnsoLevelType level)
        {
            string songId = musicInfo.Id;
            SongDataPoints points = new SongDataPoints();
            points.Points = 0;
            points.ScoreRank = 0;
            if (musicInfo.Stars[(int)level] != 0)
            {
                //Logger.Log("CalculateSongPointValues(" + songId + ", " + level.ToString() + ") (Single)", LogType.Debug);
                yield return GetFumenDataHook.GetFumenData(musicInfo, level);
                var bytes = GetFumenDataHook.GetFumenDataResult(songId, level);
                if (bytes.Length > 0)
                {
                    // Do parsing stuff here, probably through my ChartConverter.dll
                    var chart = ChartConverterLib.Fumen.ReadFumen(bytes, false);
                    var chartPoints = chart.GetPointsAndScore();
                    points.Points = chartPoints.points;
                    points.ScoreRank = chartPoints.score;
                }
            }

            if (AllSongData.ContainsKey(songId))
            {
                if (AllSongData[songId].Points.ContainsKey(level))
                {
                    AllSongData[songId].Points[level] = points;
                }
                else
                {
                    AllSongData[songId].Points.Add(level, points);
                }
            }
            else
            {
                SongData data = new SongData();
                data.SongId = songId;
                data.Points.Add(level, points);
                AllSongData.Add(songId, data);
            }
        }

        public static IEnumerator VerifySongDataPoints(int uniqueId)
        {
            MusicDataInterface.MusicInfoAccesser musicInfo = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData.GetInfoByUniqueId(uniqueId);
            List<Coroutine> coroutines = new List<Coroutine>();
            for (EnsoData.EnsoLevelType i = 0; i < EnsoData.EnsoLevelType.Num; i++)
            {
                if (SongDataRequiresCalculating(musicInfo.Id, i))
                {
                    coroutines.Add(Plugin.Instance.StartCoroutine(VerifySongDataPoints2(musicInfo.Id, i)));
                }
                //VerifySongDataPoints(musicInfo.Id, i, calculate);
            }
            for (int i = 0; i < coroutines.Count; i++)
            {
                yield return coroutines[i];
            }
        }
        public static IEnumerator VerifySongDataPoints(string songId)
        {
            MusicDataInterface.MusicInfoAccesser musicInfo = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData.GetInfoById(songId);

            List<Coroutine> coroutines = new List<Coroutine>();
            for (EnsoData.EnsoLevelType i = 0; i < EnsoData.EnsoLevelType.Num; i++)
            {
                if (SongDataRequiresCalculating(songId, i))
                {
                    coroutines.Add(Plugin.Instance.StartCoroutine(VerifySongDataPoints2(songId, i)));
                }
                //VerifySongDataPoints(songId, i, calculate);
            }
            for (int i = 0; i < coroutines.Count; i++)
            {
                yield return coroutines[i];
            }
        }

        public static IEnumerator VerifySongDataPoints(MusicDataInterface.MusicInfoAccesser musicInfo)
        {
            List<Coroutine> coroutines = new List<Coroutine>();
            for (EnsoData.EnsoLevelType i = 0; i < EnsoData.EnsoLevelType.Num; i++)
            {
                if (SongDataRequiresCalculating(musicInfo.Id, i))
                {
                    coroutines.Add(Plugin.Instance.StartCoroutine(VerifySongDataPoints2(musicInfo, i)));
                }
                //VerifySongDataPoints(songId, i, calculate);
            }
            for (int i = 0; i < coroutines.Count; i++)
            {
                yield return coroutines[i];
            }
        }
        public static IEnumerator VerifySongDataPoints(string songId, EnsoData.EnsoLevelType level)
        {
            MusicDataInterface.MusicInfoAccesser musicInfo = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData.GetInfoById(songId);
            return VerifySongDataPoints(musicInfo, level);
        }
        public static IEnumerator VerifySongDataPoints(MusicDataInterface.MusicInfoAccesser musicInfo, EnsoData.EnsoLevelType level)
        {
            Initialize();

            if (!AllSongData.ContainsKey(musicInfo.Id))
            {
                InitializeFromTakoTako();
                if (!AllSongData.ContainsKey(musicInfo.Id))
                {
                    yield return CalculateSongPointValues(musicInfo.Id, level);
                    if (!AllSongData.ContainsKey(musicInfo.Id))
                    {
                        // It shouldn't really ever get here I think
                        Logger.Log("Error parsing song: " + musicInfo.Id, LogType.Error);
                    }
                }
            }
            var data = AllSongData[musicInfo.Id];
            if (!data.Points.ContainsKey(level))
            {
                yield return CalculateSongPointValues(musicInfo.Id, level);
            }
            else
            {
                var result = AllSongData[musicInfo.Id].Points[level];
                if (!IsValidPoints(result.Points, result.ScoreRank))
                {
                    yield return CalculateSongPointValues(musicInfo.Id, level);
                }
            }
        }

        public static bool SongDataRequiresCalculating(string songId, EnsoData.EnsoLevelType level)
        {
            Initialize();

            if (!AllSongData.ContainsKey(songId))
            {
                InitializeFromTakoTako();
                if (!AllSongData.ContainsKey(songId))
                {
                    return true;
                }
            }
            var data = AllSongData[songId];
            if (!data.Points.ContainsKey(level))
            {
                return true;
            }
            else
            {
                var result = AllSongData[songId].Points[level];
                if (!IsValidPoints(result.Points, result.ScoreRank))
                {
                    return true;
                }
            }
            return false;
        }

        public static IEnumerator VerifySongDataPoints2(string songId, EnsoData.EnsoLevelType level)
        {
            MusicDataInterface.MusicInfoAccesser musicInfo = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData.GetInfoById(songId);
            return VerifySongDataPoints2(musicInfo, level);
        }

        public static IEnumerator VerifySongDataPoints2(MusicDataInterface.MusicInfoAccesser musicInfo, EnsoData.EnsoLevelType level)
        {
            if (AllSongData.ContainsKey(musicInfo.Id))
            {
                var songData = AllSongData[musicInfo.Id];
                if (songData.Points.ContainsKey(level))
                {
                    var points = songData.Points[level];
                    if (!IsValidPoints(points.Points, points.ScoreRank))
                    {
                        yield return CalculateSongPointValues(musicInfo, level);
                    }
                }
            }
            else
            {
                Initialize();
                if (!AllSongData.ContainsKey(musicInfo.Id))
                {
                    InitializeFromTakoTako();
                    if (!AllSongData.ContainsKey(musicInfo.Id))
                    {
                        yield return CalculateSongPointValues(musicInfo, level);
                        if (!AllSongData.ContainsKey(musicInfo.Id))
                        {
                            // It shouldn't really ever get here I think
                            Logger.Log("Error parsing song: " + musicInfo.Id, LogType.Error);
                        }
                    }
                }
            }
        }

        public static SongDataPoints GetSongDataPoints(string songId, EnsoData.EnsoLevelType level)
        {
            var result = AllSongData[songId].Points[level];
            return result;
        }

        public static void ExportSongData()
        {
            if (!Directory.Exists(Path.GetDirectoryName(FilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            }

            if (!IsPauseExporting)
            {
                JsonArray array = new JsonArray();

                foreach (var song in AllSongData.Values)
                {
                    JsonObject obj = new JsonObject()
                    {
                        ["SongId"] = song.SongId,
                        ["Easy"] = new JsonObject()
                        {
                            ["Points"] = song.Points[EnsoData.EnsoLevelType.Easy].Points,
                            ["ScoreRank"] = song.Points[EnsoData.EnsoLevelType.Easy].ScoreRank,
                        },
                        ["Normal"] = new JsonObject()
                        {
                            ["Points"] = song.Points[EnsoData.EnsoLevelType.Normal].Points,
                            ["ScoreRank"] = song.Points[EnsoData.EnsoLevelType.Normal].ScoreRank,
                        },
                        ["Hard"] = new JsonObject()
                        {
                            ["Points"] = song.Points[EnsoData.EnsoLevelType.Hard].Points,
                            ["ScoreRank"] = song.Points[EnsoData.EnsoLevelType.Hard].ScoreRank,
                        },
                        ["Oni"] = new JsonObject()
                        {
                            ["Points"] = song.Points[EnsoData.EnsoLevelType.Mania].Points,
                            ["ScoreRank"] = song.Points[EnsoData.EnsoLevelType.Mania].ScoreRank,
                        },
                        ["Ura"] = new JsonObject()
                        {
                            ["Points"] = song.Points[EnsoData.EnsoLevelType.Ura].Points,
                            ["ScoreRank"] = song.Points[EnsoData.EnsoLevelType.Ura].ScoreRank,
                        }
                    };
                    array.Add(obj);
                }

                File.WriteAllText(FilePath, array.ToJsonString());
            }
        }

        public static int GetOkPoints(int points)
        {
            if (points / 10 % 2 != 0)
            {
                return points / 2 - 5;
            }
            return points / 2;
        }

        public static void PauseExporting(bool pause)
        {
            IsPauseExporting = pause;
        }

        public static bool IsValidPoints(int points, int scoreRank)
        {
            // I'm doing this stupidly at first, I'll make it better later when I can better define invalid points
            if (points == 0 || scoreRank == 0)
            {
                return false;
            }
            if (scoreRank < 900000)
            {
                return false;
            }
            if (points == 1000 && scoreRank == 1000000)
            {
                return false;
            }

            return true;
        }
    }
}
