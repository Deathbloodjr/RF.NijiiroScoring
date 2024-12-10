using System;
using System.Collections;
using System.Collections.Generic;
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
                }
                ExportSongData();
                IsInitializedFromTakoTako = true;
            }
        }

        static IEnumerator CalculateSongPointValues(string songId)
        {
            //Logger.Log("CalculateSongPointValues(" + songId + ")");
            MusicDataInterface.MusicInfoAccesser musicInfo = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData.GetInfoById(songId);
            SongData data = new SongData();
            data.SongId = songId;
            for (EnsoData.EnsoLevelType i = 0; i < EnsoData.EnsoLevelType.Num; i++)
            {
                if (musicInfo.Stars[(int)i] == 0)
                {
                    SongDataPoints points = new SongDataPoints();
                    points.Points = 0;
                    points.ScoreRank = 0;
                    data.Points.Add(i, points);
                    continue;
                }
                yield return GetFumenDataHook.GetFumenData(songId, i);
                var bytes = GetFumenDataHook.GetFumenDataResult(songId, i);
                if (bytes.Length > 0)
                {
                    // Do parsing stuff here, probably through my ChartConverter.dll
                    var chart = ChartConverterLib.Fumen.ReadFumen(bytes, false);
                    var chartPoints = chart.GetPointsAndScore();
                    SongDataPoints points = new SongDataPoints();
                    points.Points = chartPoints.points;
                    points.ScoreRank = chartPoints.score;
                    data.Points.Add(i, points);
                }
                else
                {
                    SongDataPoints points = new SongDataPoints();
                    points.Points = 0;
                    points.ScoreRank = 0;
                    data.Points.Add(i, points);
                }
            }
            if (AllSongData.ContainsKey(songId))
            {
                AllSongData[songId] = data;
            }
            else
            {
                AllSongData.Add(songId, data);
            }
            ExportSongData();
        }

        public static IEnumerator VerifySongDataPoints(string songId, EnsoData.EnsoLevelType level, bool calculate = true)
        {
            Initialize();

            if (!AllSongData.ContainsKey(songId))
            {
                InitializeFromTakoTako();
            }

            if (!AllSongData.ContainsKey(songId) && calculate)
            {
                yield return CalculateSongPointValues(songId);
            }

            if (!AllSongData.ContainsKey(songId))
            {
                // It shouldn't really ever get here I think
                Logger.Log("Error parsing song: " + songId, LogType.Error);
            }
            else
            {
                var result = AllSongData[songId].Points[level];
                if (result.Points == 0 ||
                    result.ScoreRank == 0 ||
                    (result.Points == 1000 &&
                    result.ScoreRank == 1000000
                    ))
                {
                    if (calculate)
                    {
                        yield return CalculateSongPointValues(songId);
                    }
                }
            }
        }

        public static SongDataPoints GetSongDataPoints(string songId, EnsoData.EnsoLevelType level)
        {
            var result = AllSongData[songId].Points[level];
            return result;
        }

        static void ExportSongData()
        {
            if (!Directory.Exists(Path.GetDirectoryName(FilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            }

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

        public static int GetOkPoints(int points)
        {
            if (points / 10 % 2 != 0)
            {
                return points / 2 - 5;
            }
            return points / 2;
        }
    }
}
