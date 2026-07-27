using System;
using System.Collections.Generic;

namespace ShinyMinds.Core.Save
{
    [Serializable]
    public class MissionProgress
    {
        public string missionId;
        public bool completed;
        public string bestEndingId;
        public int bestStars;
        public int attempts;
        public string lastPlayedUtc;
    }

    [Serializable]
    public class SaveFile
    {
        public int version = 1;
        public List<MissionProgress> missions = new List<MissionProgress>();
    }
}
