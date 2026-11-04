using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneSaber
{
    [CreateAssetMenu(menuName = "Beat and Blade/Beatmap")]
    public sealed class BeatmapController : ScriptableObject
    {
        public AudioClip Music;
        public float JumpOffset;
        public float BPM = 112, MusicOffset;
        public List<RhythmNote> Notes = new List<RhythmNote>();
        public static BeatmapController Demo(int difficulty)
        {
            var map = CreateInstance<BeatmapController>();
            float beat = 60/map.BPM;
            int[] magic = {0,1,3,2,0,3,2,1};
            int sword=3;
            for(int phrase=0;phrase<24;phrase++)
            {
                int lane=phrase%2;
                for(int n=0;n<4;n++)
                {
                    if(difficulty==0 && n%2!=0) continue;
                    map.Notes.Add(new RhythmNote{Timestamp=(8+phrase*8+n)*beat,Lane=lane,Direction=lane==0?magic[(phrase+n)%8]:sword});
                    if(lane==1) sword=sword==3?1:3;
                }
            }
            return map;
        }
    }
}
