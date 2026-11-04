using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneSaber
{
    [Serializable] public class RhythmNote
    {
        public float Timestamp;
        public int Lane; // 0 magic, 1 saber
        public int Direction;
        public float TravelSpeed = 9;
        public float HitWindow = .16f;
    }


    public sealed class MusicConductor : MonoBehaviour
    {
        public double SongTime => ((paused ? pauseDsp : AudioSettings.dspTime)-startDsp)*rate + from + offset;
        public float Beat => (float)(SongTime * bpm / 60);
        public bool Playing { get; private set; }
        public bool Paused => paused;
        public float Length => source && source.clip ? source.clip.length : 0;
        AudioSource source; double startDsp, pauseDsp, from; float bpm, offset, rate=1; bool paused;
        void Load(BeatmapController map, AudioClip clip)
        {
            if (!source) { source = gameObject.AddComponent<AudioSource>(); source.volume = .8f; }
            source.Stop(); source.clip = map.Music ? map.Music : clip;
            bpm = map.BPM; offset = map.MusicOffset; Playing = true;
        }
        public void Begin(BeatmapController map, AudioClip generated, double start=0)
        { Load(map,generated); PlayFrom(start); }
        // Editor entry: the clip is loaded but holds at a time until PlayFrom.
        public void Prepare(BeatmapController map, AudioClip clip) { Load(map,clip); SeekPaused(0); }
        public void PlayFrom(double time, float speed=1)
        {
            source.Stop(); rate = speed; source.pitch = speed; from = time-offset;
            source.time = Mathf.Clamp((float)from,0,Mathf.Max(0,source.clip.length-.05f));
            startDsp = AudioSettings.dspTime + .12; paused = false;
            source.PlayScheduled(startDsp);
        }
        // The DSP clock time at which a given song time is heard.
        public double DspAt(double songTime) => startDsp + (songTime-from-offset)/rate;
        public void Hold(double time) { if(source) source.Pause(); SeekPaused(time); }
        void SeekPaused(double time) { pauseDsp = startDsp = AudioSettings.dspTime; from = time-offset; rate = 1; paused = true; }
        public void TogglePause()
        {
            if (!Playing) return;
            if (!paused) { pauseDsp = AudioSettings.dspTime; source.Pause(); paused=true; }
            else { startDsp += AudioSettings.dspTime-pauseDsp; source.UnPause(); paused=false; }
        }
        public void Spectrum(float[] bins)
        {
            if(source && Playing && !Paused) source.GetSpectrumData(bins,0,FFTWindow.BlackmanHarris);
            else System.Array.Clear(bins,0,bins.Length);
        }
        public void Stop() { if (source) source.Stop(); Playing=false; paused=false; }

        // Original 112 BPM instrumental. Constructed once, played by the DSP clock.
        public static AudioClip Synthesize(float bpm, int beats = 200)
        {
            const int rate = 24000;
            float beat = 60/bpm; int length = Mathf.CeilToInt((beats*beat+2)*rate);
            float[] pcm = new float[length];
            int[] roots = {0,0,-3,-5}; int[] melody = {0,7,12,10,7,3,5,7};
            for (int i=0;i<length;i++)
            {
                double t = (double)i/rate; int n = (int)(t/beat);
                double phase=t%beat, half=t%(beat*.5), bar=t%(beat*4);
                int root=roots[(n/16)%4];
                double bassHz=55*Math.Pow(2,root/12.0);
                double kick = Math.Sin(2*Math.PI*(47*phase+5*(1-Math.Exp(-phase*35))))*Math.Exp(-phase*18)*.48;
                double bass=Math.Sin(2*Math.PI*bassHz*t)*Math.Exp(-phase*5)*.16;
                // deterministic noise; no random allocations in the synthesis loop
                double noise = ((i*1103515245L+12345)&65535)/32768.0-1;
                double hat=noise*Math.Exp(-half*100)*.045;
                double snare = n%2==1 ? noise*Math.Exp(-phase*30)*.12 : 0;
                double hz=220*Math.Pow(2,(root+melody[(n/2)%8])/12.0);
                double lead=(Math.Sin(2*Math.PI*hz*t)+.3*Math.Sin(4*Math.PI*hz*t))*Math.Exp(-(t%(beat*2))*3)*.06;
                double pad=(Math.Sin(2*Math.PI*bassHz*4*t)+Math.Sin(2*Math.PI*bassHz*6*t))*.035*(.6+.4*Math.Sin(bar/beat*Math.PI/4));
                pcm[i]=(float)Math.Tanh((kick+bass+hat+snare+lead+pad)*.9)*Mathf.Clamp01((float)t*2);
            }
            var clip=AudioClip.Create("Arcane • original procedural session",length,1,rate,false);
            clip.SetData(pcm,0); return clip;
        }
    }

    public sealed class ScoreManager
    {
        public int Score, Combo, BestCombo, Hits, Misses, Total;
        public float AccuracySum;
        public string Judgment = "FIND YOUR FLOW";
        public float LastJudgment;
        public float Accuracy => Total == 0 ? 100 : 100*AccuracySum/Total;
        public void Hit(float error, float window)
        {
            float quality = Mathf.Clamp01(1-Mathf.Abs(error)/window);
            Combo++; Hits++; Total++; BestCombo=Mathf.Max(BestCombo,Combo);
            AccuracySum+=quality;
            Score+=Mathf.RoundToInt((70+30*quality)*(1+Mathf.Min(Combo/8,7)));
            Judgment=Mathf.Abs(error)<.055f ? "PERFECT" : Mathf.Abs(error)<.11f ? "GREAT" : "GOOD";
            LastJudgment=Time.unscaledTime;
        }
        public void Miss(string reason = "MISS") { Combo=0; Misses++; Total++; Judgment=reason; LastJudgment=Time.unscaledTime; }
    }
}
