using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneSaber
{
    public static class MusicChartBuilder
    {
        // Energy novelty peaks retain their real audio timestamps; rests stay empty.
        public static BeatmapController Analyze(AudioClip clip,int difficulty)
        {
            if(clip.loadState!=AudioDataLoadState.Loaded) clip.LoadAudioData();
            if(clip.length>600) throw new InvalidOperationException("Use a music track shorter than ten minutes.");
            float[] pcm=new float[clip.samples*clip.channels];
            if(!clip.GetData(pcm,0)) throw new InvalidOperationException("Music must use Decompress On Load. Use Beat and Blade > Import Music.");
            int hop=Mathf.Max(1,clip.frequency/100),count=clip.samples/hop;
            float[] energy=new float[count],novelty=new float[count];
            float previous=0;
            for(int f=0;f<count;f++)
            {
                double sum=0;
                for(int i=0;i<hop;i++) for(int c=0;c<clip.channels;c++) { float x=pcm[(f*hop+i)*clip.channels+c]; sum+=x*x; }
                energy[f]=Mathf.Sqrt((float)(sum/(hop*clip.channels)));
                novelty[f]=Mathf.Max(0,energy[f]-previous); previous=energy[f];
            }
            var map=ScriptableObject.CreateInstance<BeatmapController>(); map.Music=clip;
            float last=-10; int phrase=0,within=0,saber=3; int[] keys={0,1,3,2,3,0,2,1};
            float gap=difficulty==0?.58f:difficulty==1?.43f:.34f;
            for(int f=2;f<count-2;f++)
            {
                float t=(float)(f*hop)/clip.frequency;
                if(t<3 || t>clip.length-.4f || t-last<(within==0?.8f:gap)) continue;
                float mean=0; int n=0;
                for(int k=Mathf.Max(0,f-20);k<f;k++) { mean+=novelty[k]; n++; }
                if(energy[f]<.015f || novelty[f]<Mathf.Max(.003f,mean/Mathf.Max(1,n)*1.55f) || novelty[f]<novelty[f-1] || novelty[f]<=novelty[f+1]) continue;
                int lane=phrase%2;
                map.Notes.Add(new RhythmNote{Timestamp=t,Lane=lane,Direction=lane==0?keys[(phrase+within)%keys.Length]:saber});
                if(lane==1) saber=saber==3?1:3;
                last=t; within++; if(within==4) { within=0; phrase++; }
            }
            // Estimate a useful lighting pulse period; note timing remains unsnapped.
            if(map.Notes.Count>3)
            {
                var gaps=new List<float>(); for(int i=1;i<map.Notes.Count;i++) { float d=map.Notes[i].Timestamp-map.Notes[i-1].Timestamp; if(d<1) gaps.Add(d); }
                gaps.Sort(); if(gaps.Count>0) map.BPM=Mathf.Clamp(60/gaps[gaps.Count/2],70,180);
            }
            return map;
        }
        public static void HorizontalFlow(BeatmapController map)
        {
            map.Notes.Sort((a,b)=>a.Timestamp.CompareTo(b.Timestamp));
            float last=-10; int lane=-1,direction=3;
            for(int i=0;i<map.Notes.Count;)
            {
                var note=map.Notes[i];
                if(note.Timestamp-last<(lane!=note.Lane?.65f:.32f)) { map.Notes.RemoveAt(i); continue; }
                if(note.Lane==1) { note.Direction=direction; direction=direction==3?1:3; }
                last=note.Timestamp; lane=note.Lane; i++;
            }
        }
    }
}
