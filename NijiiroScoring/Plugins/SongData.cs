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
}
