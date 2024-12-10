using System;
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
                            Logger.Log("Error parsing TakoTako data.json file: " + files[i].Directory.Name, LogType.Error);
                        }
                    }
                }
                ExportSongData();
                IsInitializedFromTakoTako = true;
            }
        }

        static void CalculateSongPointValues(string songId)
        {
            for (EnsoData.EnsoLevelType i = 0; i < EnsoData.EnsoLevelType.Num; i++)
            {
                var bytes = GetFumenData(songId, i).ToArray();
                // Do parsing stuff here, probably through my ChartConverter.dll
            }
            ExportSongData();
        }

        public static SongDataPoints GetSongDataPoints(string songId, EnsoData.EnsoLevelType level)
        {
            Initialize();
            
            if (!AllSongData.ContainsKey(songId))
            {
                InitializeFromTakoTako();
            }

            if (!AllSongData.ContainsKey(songId))
            {
                CalculateSongPointValues(songId);
            }

            if (!AllSongData.ContainsKey(songId))
            {
                // It shouldn't really ever get here I think
                Logger.Log("Error parsing song: " + songId, LogType.Error);
                return null;
            }

            return AllSongData[songId].Points[level];
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

        /// <summary>
        /// I don't really know how well this function works in RF. It was made for TDMX, and slightly tested in RF's demo
        /// </summary>
        static unsafe List<byte> GetFumenData(string songId, EnsoData.EnsoLevelType level)
        {
            string[] array =
            [
                "_e",
                "_n",
                "_h",
                "_m",
                "_x"
            ];

            var filePath = Path.Combine(Application.streamingAssetsPath, string.Concat(
            [
                "fumen/",
                songId,
                array[(int)level],
                ".bin"
            ]));

            try
            {
                //FumenLoader fumenLoader = new FumenLoader();
                ////fumenLoader.settings.Reset();
                //fumenLoader.settings.musicuid = songId;
                //EnsoData.PlayerSettings[] playerSettings = new EnsoData.PlayerSettings[5];
                //playerSettings[0] = new EnsoData.PlayerSettings();
                //playerSettings[0].courseType = level;
                //fumenLoader.settings.ensoPlayerSettings = playerSettings;
                //fumenLoader.LoadStart();
                //while (!fumenLoader.IsLoadingCompleted())
                //{
                //    Thread.Sleep(10);
                //}
                //byte[] fumenData = new byte[fumenLoader.GetFumenSize(0)];
                //IntPtr ptr = new IntPtr(fumenLoader.GetFumenData(0));
                //Marshal.Copy(ptr, fumenData, 0, fumenData.Length);
                //for (int i = 0; i < fumenData.Length; i++)
                //{
                //    fumenData[i] = ((byte)fumenLoader.GetFumenData(0))
                //}
                //return fumenData.ToList();


                //var task = Cryptgraphy.ReadAllAesAndGZipBytesAsync(filePath, Cryptgraphy.AesKeyType.Type2);
                //while (task.Status != Cysharp.Threading.Tasks.UniTaskStatus.Succeeded)
                //{
                //    Thread.Sleep(10);
                //}

                //return task.result.ToList();


                var result = Cryptgraphy.ReadAllAesAndGZipBytes(filePath, Cryptgraphy.AesKeyType.Type2);
                return result.ToList();


                //return Cryptgraphy.ReadAllAesAndGZipBytes(filePath, Cryptgraphy.AesKeyType.Type2).ToList();
            }
            catch (Exception)
            {
                // I'm upset at TakoTako for making me do this, rather than just ReadAllAesAndGZipBytes to get the data
                //var customPath = FumenReading.GetCustomFumenPath(songId, level);
                //var bytes = File.ReadAllBytes(customPath).ToList();

                //bool gzipped = true;
                //List<byte> gzippedFileHeader = new List<byte>() { 0x1F, 0x8B, 0x08 };
                //for (int i = 0; i < gzippedFileHeader.Count; i++)
                //{
                //    if (bytes[i] != gzippedFileHeader[i])
                //    {
                //        gzipped = false;
                //        break;
                //    }
                //}
                //if (!gzipped)
                //{
                //    return bytes;
                //}
                //using (FileStream fs = new FileStream(customPath, FileMode.Open))
                //{
                //    MemoryStream memoryStream = new MemoryStream();
                //    using (GZipStream gzipStream = new GZipStream(fs, CompressionMode.Decompress))
                //    {
                //        gzipStream.CopyTo(memoryStream);
                //    }
                //    return memoryStream.ToArray().ToList();
                //}
            }

            return new List<byte>();
        }
    }
}
