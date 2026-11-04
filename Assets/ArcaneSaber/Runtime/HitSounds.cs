using UnityEngine;

namespace ArcaneSaber
{
    // Synthesized cut sounds. Like Beat Saber, an early cut is scheduled onto the note's own
    // beat, so the sound always lands in time with the music rather than with the swing.
    public sealed class HitSounds : MonoBehaviour
    {
        public GameFeelSettings Settings;
        const int Rate=48000;
        AudioSource[] voices; AudioClip[] cuts,spells; AudioClip tick; int next;
        public void Initialize(GameFeelSettings settings)
        {
            Settings=settings; voices=new AudioSource[24];
            for(int i=0;i<voices.Length;i++) { voices[i]=gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake=false; voices[i].spatialBlend=0; }
            // Recorded sounds in Resources/Sounds win (CutLong* before Cut*, and Spell*); synthesis is the fallback.
            cuts=Recorded("CutLong"); if(cuts.Length==0) cuts=Recorded("Cut"); spells=Recorded("Spell");
            if(cuts.Length==0) { cuts=new AudioClip[4]; for(int i=0;i<cuts.Length;i++) cuts[i]=Cut(i); }
            if(spells.Length==0) { spells=new AudioClip[3]; for(int i=0;i<spells.Length;i++) spells[i]=Spell(i); }
            tick=Cut(9);
            Debug.Log($"Hit sounds: {cuts.Length} cut, {spells.Length} spell");
        }
        static AudioClip[] Recorded(string prefix)
        {
            var found=new System.Collections.Generic.List<AudioClip>();
            foreach(var clip in Resources.LoadAll<AudioClip>("Sounds"))
                if(clip.name.StartsWith(prefix))
                {
                    // Long cuts keep their ring-out; short ones are clipped so fast streams stay clean.
                    bool full=clip.name.StartsWith("CutLong") || clip.name.StartsWith("Spell");
                    var trimmed=Trim(clip,full?2f:.45f,full?.3f:.12f); if(trimmed) found.Add(trimmed);
                }
            return found.ToArray();
        }
        // Generated effects often start with silence and ring on; a hit must start at its first
        // sample to land on the beat, and stay short so fast streams do not smear together.
        static AudioClip Trim(AudioClip clip,float maximum,float fadeOut)
        {
            if(clip.loadState!=AudioDataLoadState.Loaded) clip.LoadAudioData();
            int channels=clip.channels; var data=new float[clip.samples*channels];
            if(!clip.GetData(data,0)) { Debug.LogWarning("Hit sound must use Decompress On Load: "+clip.name); return null; }
            int frames=clip.samples; var mono=new float[frames]; float peak=.0001f;
            for(int i=0;i<frames;i++) { float x=0; for(int c=0;c<channels;c++) x+=data[i*channels+c]; mono[i]=x/channels; peak=Mathf.Max(peak,Mathf.Abs(mono[i])); }
            int start=0; while(start<frames && Mathf.Abs(mono[start])<peak*.08f) start++;
            start=Mathf.Max(0,start-clip.frequency/1000);
            int length=Mathf.Min(frames-start,(int)(clip.frequency*maximum)),fade=Mathf.Min(length,(int)(clip.frequency*fadeOut));
            if(length<=0) return null;
            var samples=new float[length];
            for(int i=0;i<length;i++)
            {
                float gain=i>length-fade?(float)(length-i)/fade:1;
                samples[i]=mono[start+i]/peak*.95f*gain;
            }
            var result=AudioClip.Create(clip.name+" (trimmed)",length,1,clip.frequency,false); result.SetData(samples,0);
            return result;
        }
        AudioSource Voice() => voices[next++%voices.Length];
        // Delay is how far the note still is from its timestamp; late cuts play at once.
        public void Play(bool magic,bool perfect,float delay,float pan)
        {
            if(Settings.HitSoundVolume<=0) return;
            var voice=Voice(); voice.Stop();
            voice.clip=magic?spells[Random.Range(0,spells.Length)]:cuts[Random.Range(0,cuts.Length)];
            voice.volume=Settings.HitSoundVolume*(perfect?1:.8f); voice.pitch=Random.Range(.97f,1.04f); voice.panStereo=pan;
            voice.PlayScheduled(AudioSettings.dspTime+Mathf.Clamp(delay,0,.15f));
        }
        // Editor metronome on placed notes, at an exact DSP time.
        public void PlayAt(double dspTime,int lane)
        {
            if(Settings.HitSoundVolume<=0) return;
            var voice=Voice(); voice.Stop();
            voice.clip=lane==0?spells[0]:lane==1?cuts[0]:tick;
            voice.volume=Settings.HitSoundVolume*(lane==ModMap.Undecided?.5f:.85f); voice.pitch=lane==ModMap.Undecided?1.25f:1; voice.panStereo=lane==0?-.3f:lane==1?.3f:0;
            voice.PlayScheduled(dspTime);
        }
        static AudioClip Make(string name,float[] samples)
        {
            float peak=.0001f; foreach(float s in samples) peak=Mathf.Max(peak,Mathf.Abs(s));
            for(int i=0;i<samples.Length;i++) samples[i]*=.95f/peak;
            var clip=AudioClip.Create(name,samples.Length,1,Rate,false); clip.SetData(samples,0); return clip;
        }
        // Bright crack, a filtered swish sweeping down, a glassy ring, and a short body thump.
        static AudioClip Cut(int variant)
        {
            var samples=new float[(int)(Rate*.24f)]; uint state=(uint)(variant*2654435761u+12345u);
            float low=0,band=0,previous=0,v=1+(variant%4-1.5f)*.04f;
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/Rate;
                state^=state<<13; state^=state>>17; state^=state<<5; float noise=state/4294967296f*2-1;
                float centre=Mathf.Lerp(7200,1500,1-Mathf.Exp(-t*28))*v,f=2*Mathf.Sin(Mathf.PI*centre/Rate);
                low+=f*band; float high=noise-low-.7f*band; band+=f*high;
                float swish=band*Mathf.Exp(-t*24)*(1-Mathf.Exp(-t*900))*1.1f;
                float crack=(noise-previous)*Mathf.Exp(-t*280)*.45f; previous=noise;
                float ring=(Mathf.Sin(2*Mathf.PI*3150*v*t)*.2f+Mathf.Sin(2*Mathf.PI*5230*v*t)*.09f)*Mathf.Exp(-t*42);
                float thump=Mathf.Sin(2*Mathf.PI*(165-90*t)*t)*Mathf.Exp(-t*34)*.42f;
                samples[i]=(float)System.Math.Tanh((swish+crack+ring+thump)*1.4f);
            }
            return Make("Saber cut "+variant,samples);
        }
        // Crystal chord with an airy shimmer and a soft pop.
        static AudioClip Spell(int variant)
        {
            var samples=new float[(int)(Rate*.38f)]; uint state=(uint)(variant*747796405u+2891336453u);
            float low=0,band=0,root=1320*Mathf.Pow(2,variant*2/12f);
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/Rate;
                state^=state<<13; state^=state>>17; state^=state<<5; float noise=state/4294967296f*2-1;
                float f=2*Mathf.Sin(Mathf.PI*9000/Rate); low+=f*band; float high=noise-low-.5f*band; band+=f*high;
                float chime=(Mathf.Sin(2*Mathf.PI*root*t)+.6f*Mathf.Sin(2*Mathf.PI*root*1.5f*t)+.3f*Mathf.Sin(2*Mathf.PI*root*2*t))*Mathf.Exp(-t*15)*.33f;
                float shimmer=band*Mathf.Exp(-t*38)*.5f;
                float pop=Mathf.Sin(2*Mathf.PI*(250-130*t)*t)*Mathf.Exp(-t*42)*.5f;
                samples[i]=(float)System.Math.Tanh((chime+shimmer+pop)*(1-Mathf.Exp(-t*1500))*1.3f);
            }
            return Make("Spell release "+variant,samples);
        }
        void OnDestroy()
        {
            if(cuts!=null) foreach(var c in cuts) Destroy(c);
            if(spells!=null) foreach(var c in spells) Destroy(c);
            if(tick) Destroy(tick);
        }
    }
}
