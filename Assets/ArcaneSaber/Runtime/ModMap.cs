using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Networking;

namespace ArcaneSaber
{
    public enum StageKind { Flash,Strobe,Blackout,SpotSweep,SpotCamera,SpotFan,Palette,RingSpin,CameraKick,CameraZoom,CameraRoll,Energy }

    // Timed light and camera cue. Value is 0-1 strength, or a palette index for Palette.
    [Serializable] public class StageEvent
    {
        public float Time; public int Kind; public float Value=1; public float Duration=.5f;
        public static readonly string[] Names={"Flaş","Strobe","Karartma","Spot: Tarama","Spot: Kameraya","Spot: Yelpaze","Renk Paleti","Halka Dönüşü","Kamera Darbe","Kamera Zoom","Kamera Yatış","Işık Seviyesi"};
    }

    [Serializable] public class ModMap
    {
        public const int Undecided=2; // Note lane of a K marker that has no hand yet.
        public int Format=1;
        public float JumpOffset;
        public string Id="",Title="Yeni Mod",Artist="",AudioFile="";
        public float BPM=120,BeatOffset,NoteSpeed=11,HitWindow=.16f,LightPower=1,CameraPower=1,BloomPower=1;
        public List<RhythmNote> Notes=new List<RhythmNote>();
        public List<StageEvent> Events=new List<StageEvent>();
        public int UndecidedCount { get { int n=0; foreach(var note in Notes) if(note.Lane==Undecided) n++; return n; } }
        public void Sort()
        {
            Notes.Sort((a,b)=>a.Timestamp.CompareTo(b.Timestamp));
            Events.Sort((a,b)=>a.Time.CompareTo(b.Time));
        }
        // Rebuilt from external files: every value is forced into the range gameplay accepts.
        public void Sanitize()
        {
            if(Notes==null) Notes=new List<RhythmNote>();
            if(Events==null) Events=new List<StageEvent>();
            Notes.RemoveAll(n=>n==null || !Finite(n.Timestamp) || n.Timestamp<0 || n.Lane<0 || n.Lane>Undecided);
            Events.RemoveAll(e=>e==null || !Finite(e.Time) || e.Time<0 || e.Kind<0 || e.Kind>(int)StageKind.Energy);
            foreach(var n in Notes) n.Direction=n.Lane==1?LaserDirection(n.Direction):Mathf.Clamp(n.Direction,0,3);
            foreach(var e in Events) { e.Value=Finite(e.Value)?Mathf.Clamp(e.Value,0,16):1; e.Duration=Finite(e.Duration)?Mathf.Clamp(e.Duration,0,30):.5f; }
            BPM=Finite(BPM)?Mathf.Clamp(BPM,30,300):120; BeatOffset=Finite(BeatOffset)?BeatOffset:0;
            NoteSpeed=Finite(NoteSpeed)?Mathf.Clamp(NoteSpeed,4,30):11; HitWindow=Finite(HitWindow)?Mathf.Clamp(HitWindow,.05f,.4f):.16f;
            LightPower=Finite(LightPower)?Mathf.Clamp(LightPower,0,2):1; CameraPower=Finite(CameraPower)?Mathf.Clamp(CameraPower,0,2):1; BloomPower=Finite(BloomPower)?Mathf.Clamp(BloomPower,0,2):1;
            Title=string.IsNullOrEmpty(Title)?"Adsız Mod":Title; Sort();
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        // The mouse laser only cuts left, right, or free: diagonals keep their side, up/down become free.
        public static int LaserDirection(int direction)
        {
            switch(direction)
            {
                case 1: case 4: case 6: return 1;
                case 3: case 5: case 7: return 3;
                default: return 8;
            }
        }
        // Undecided markers stay in the editor and never reach gameplay.
        public BeatmapController ToBeatmap(AudioClip clip,BeatmapController into=null)
        {
            var map=into?into:ScriptableObject.CreateInstance<BeatmapController>();
            map.Music=clip; map.JumpOffset=JumpOffset; map.BPM=BPM; map.MusicOffset=0; map.Notes.Clear();
            foreach(var n in Notes) if(n.Lane!=Undecided) map.Notes.Add(new RhythmNote{Timestamp=n.Timestamp,Lane=n.Lane,Direction=n.Lane==1?LaserDirection(n.Direction):n.Direction,TravelSpeed=NoteSpeed,HitWindow=HitWindow});
            map.Notes.Sort((a,b)=>a.Timestamp.CompareTo(b.Timestamp));
            return map;
        }
    }

    public static class ModLibrary
    {
        public const string Extension="arcanemap";
        const long MaximumAudioBytes=250L*1024*1024, MaximumMapBytes=16L*1024*1024;
        public static string Root => Path.Combine(Application.persistentDataPath,"Mods");
        static string Folder(ModMap mod) => Path.Combine(Root,mod.Id);
        public static string AudioPath(ModMap mod) => Path.Combine(Folder(mod),mod.AudioFile);
        static bool Supported(string extension) { extension=extension.ToLowerInvariant(); return extension==".mp3" || extension==".wav" || extension==".ogg"; }
        public static List<ModMap> List()
        {
            var mods=new List<ModMap>(); Directory.CreateDirectory(Root);
            foreach(var folder in Directory.GetDirectories(Root))
            {
                string file=Path.Combine(folder,"map.json"); if(!File.Exists(file)) continue;
                try { var mod=JsonUtility.FromJson<ModMap>(File.ReadAllText(file)); mod.Id=Path.GetFileName(folder); mod.Sanitize(); mods.Add(mod); }
                catch(Exception e) { Debug.LogWarning("Mod okunamadı: "+folder+" / "+e.Message); }
            }
            mods.Sort((a,b)=>string.Compare(a.Title,b.Title,StringComparison.CurrentCultureIgnoreCase));
            return mods;
        }
        public static ModMap Create(string audioPath)
        {
            string extension=Path.GetExtension(audioPath);
            if(!Supported(extension)) throw new InvalidOperationException("Desteklenen ses türleri: mp3, wav, ogg.");
            var mod=new ModMap{Id=Guid.NewGuid().ToString("N"),Title=Path.GetFileNameWithoutExtension(audioPath),AudioFile="audio"+extension.ToLowerInvariant()};
            Directory.CreateDirectory(Folder(mod)); File.Copy(audioPath,AudioPath(mod),true); Save(mod); return mod;
        }
        public static void Save(ModMap mod)
        {
            mod.Sort(); string file=Path.Combine(Folder(mod),"map.json"), temporary=file+".tmp";
            File.WriteAllText(temporary,JsonUtility.ToJson(mod,true));
            if(File.Exists(file)) File.Delete(file);
            File.Move(temporary,file);
        }
        public static void Delete(ModMap mod) { if(!string.IsNullOrEmpty(mod.Id) && Directory.Exists(Folder(mod))) Directory.Delete(Folder(mod),true); }
        public static void Export(ModMap mod,string path)
        {
            Save(mod);
            using(var output=new FileStream(path,FileMode.Create,FileAccess.Write))
            using(var zip=new ZipArchive(output,ZipArchiveMode.Create))
            {
                using(var entry=new StreamWriter(zip.CreateEntry("map.json",System.IO.Compression.CompressionLevel.Optimal).Open())) entry.Write(JsonUtility.ToJson(mod,true));
                // Audio is already compressed; storing it keeps export fast.
                using(var entry=zip.CreateEntry(mod.AudioFile,System.IO.Compression.CompressionLevel.NoCompression).Open())
                using(var audio=File.OpenRead(AudioPath(mod))) audio.CopyTo(entry);
            }
        }
        // Entry names are only matched, never used as paths, so an archive cannot write outside its folder.
        // A BeatSaver zip becomes one mod per playable difficulty.
        public static List<ModMap> Import(string path)
        {
            using(var zip=ZipFile.OpenRead(path))
            {
                if(BeatSaverImport.Detect(zip)) return BeatSaverImport.Convert(zip);
                ZipArchiveEntry json=null,audio=null;
                foreach(var entry in zip.Entries)
                {
                    if(entry.FullName=="map.json") json=entry;
                    else if(entry.FullName==Path.GetFileName(entry.FullName) && Supported(Path.GetExtension(entry.FullName))) audio=entry;
                }
                if(json==null || audio==null) throw new InvalidOperationException("Dosya geçerli bir Arcane Saber modu değil.");
                if(json.Length>MaximumMapBytes || audio.Length>MaximumAudioBytes) throw new InvalidOperationException("Mod dosyası çok büyük.");
                ModMap mod;
                using(var reader=new StreamReader(json.Open())) mod=JsonUtility.FromJson<ModMap>(reader.ReadToEnd());
                if(mod==null) throw new InvalidOperationException("map.json okunamadı.");
                Install(mod,audio,Path.GetExtension(audio.FullName));
                return new List<ModMap>{mod};
            }
        }
        // Gives the mod a fresh folder, copies its audio out of the archive, and saves it.
        internal static void Install(ModMap mod,ZipArchiveEntry audio,string extension)
        {
            if(audio.Length>MaximumAudioBytes) throw new InvalidOperationException("Ses dosyası çok büyük.");
            mod.Id=Guid.NewGuid().ToString("N"); mod.AudioFile="audio"+extension.ToLowerInvariant(); mod.Sanitize();
            Directory.CreateDirectory(Folder(mod));
            try
            {
                using(var source=audio.Open())
                using(var target=File.Create(AudioPath(mod))) source.CopyTo(target);
                Save(mod);
            }
            catch { Delete(mod); throw; }
        }
        public static string FileSafe(string title)
        {
            foreach(char c in Path.GetInvalidFileNameChars()) title=title.Replace(c,'_');
            return title;
        }
    }

    public static class AudioLoader
    {
        // Fully decoded so the editor can read samples for its waveform and analysis.
        public static IEnumerator Load(string path,Action<AudioClip,string> done)
        {
            string extension=Path.GetExtension(path).ToLowerInvariant();
            AudioType type=extension==".mp3"?AudioType.MPEG:extension==".ogg"?AudioType.OGGVORBIS:AudioType.WAV;
            using(var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri,type))
            {
                var handler=(DownloadHandlerAudioClip)request.downloadHandler;
                handler.streamAudio=false; handler.compressed=false;
                yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success) { done(null,request.error); yield break; }
                var clip=DownloadHandlerAudioClip.GetContent(request);
                while(clip && clip.loadState==AudioDataLoadState.Loading) yield return null;
                if(!clip || clip.loadState!=AudioDataLoadState.Loaded || clip.samples==0) done(null,"Ses çözülemedi: "+Path.GetFileName(path));
                else done(clip,null);
            }
        }
    }

    // Hundred-frame-per-second summary of a song, shared by waveform, marking and stage cues.
    // Onsets come from per-band spectral flux, so a drum hit counts even inside a loud wall of guitar.
    public sealed class SongAnalysis
    {
        public const int Rate=100,Window=1024;
        public const byte Kick=1,Snare=2,Hat=4;
        public float[] Peak,Energy,Onset,Low,Mid,High; public byte[] Accents;
        public float BPM=120,Phase,Length,Confidence; public List<float> Beats=new List<float>();
        public struct Hit { public float Time,Strength; }
        public static SongAnalysis Of(AudioClip clip) => Of(ReadMono(clip),clip.frequency);
        // Reading samples needs the main thread; the analysis itself can then run on a worker.
        public static float[] ReadMono(AudioClip clip)
        {
            if(clip.loadState!=AudioDataLoadState.Loaded) clip.LoadAudioData();
            int rate=clip.frequency,channels=clip.channels,length=clip.samples;
            var mono=new float[length]; var chunk=new float[rate*channels];
            for(int start=0;start<length;start+=rate)
            {
                if(!clip.GetData(chunk,start)) throw new InvalidOperationException("Ses verisi okunamadı (sıkıştırılmış klip).");
                int frames=Mathf.Min(rate,length-start);
                for(int i=0;i<frames;i++) { float x=0; for(int c=0;c<channels;c++) x+=chunk[i*channels+c]; mono[start+i]=x/channels; }
            }
            return mono;
        }
        public static SongAnalysis Of(float[] mono,int rate)
        {
            int hop=Mathf.Max(1,rate/Rate),length=mono.Length,count=length/hop;
            var song=new SongAnalysis{Peak=new float[count],Energy=new float[count],Onset=new float[count],Low=new float[count],Mid=new float[count],High=new float[count],Accents=new byte[count],Length=(float)length/rate};
            var re=new float[Window]; var im=new float[Window]; var window=new float[Window]; var previous=new float[Window/2];
            for(int i=0;i<Window;i++) window[i]=.5f-.5f*Mathf.Cos(2*Mathf.PI*i/(Window-1));
            float binHz=(float)rate/Window;
            int lowEnd=Mathf.Max(2,(int)(160/binHz)),midEnd=(int)(2500/binHz),highEnd=Mathf.Min(Window/2,(int)(12000/binHz));
            for(int f=0;f<count;f++)
            {
                int centre=f*hop,origin=centre-Window/2; float sum=0,peak=0;
                for(int i=0;i<hop && centre+i<length;i++) { float x=mono[centre+i]; sum+=x*x; peak=Mathf.Max(peak,Mathf.Abs(x)); }
                song.Peak[f]=peak; song.Energy[f]=Mathf.Sqrt(sum/hop);
                for(int i=0;i<Window;i++) { int s=origin+i; re[i]=s>=0 && s<length?mono[s]*window[i]:0; im[i]=0; }
                Fft(re,im);
                float low=0,mid=0,high=0;
                for(int b=1;b<highEnd;b++)
                {
                    // Log magnitude keeps quiet passages from vanishing next to loud ones.
                    float magnitude=Mathf.Log(1+40*Mathf.Sqrt(re[b]*re[b]+im[b]*im[b]));
                    float rise=Mathf.Max(0,magnitude-previous[b]); previous[b]=magnitude;
                    if(b<lowEnd) low+=rise; else if(b<midEnd) mid+=rise; else high+=rise;
                }
                song.Low[f]=low/lowEnd; song.Mid[f]=mid/(midEnd-lowEnd); song.High[f]=high/(highEnd-midEnd);
            }
            // The first window always "rises" from silence; that is not a hit.
            for(int f=0;f<Mathf.Min(count,12);f++) song.Low[f]=song.Mid[f]=song.High[f]=0;
            Whiten(song.Low); Whiten(song.Mid); Whiten(song.High);
            for(int f=0;f<count;f++) song.Onset[f]=Mathf.Clamp01((song.Low[f]*.5f+song.Mid[f]*.35f+song.High[f]*.15f)*1.4f);
            song.MarkAccents(); song.EstimateTempo(); song.TrackBeats();
            return song;
        }
        // In-place radix-2 FFT.
        static void Fft(float[] re,float[] im)
        {
            int n=re.Length;
            for(int i=1,j=0;i<n;i++)
            {
                int bit=n>>1; for(;(j&bit)!=0;bit>>=1) j^=bit; j^=bit;
                if(i<j) { float t=re[i]; re[i]=re[j]; re[j]=t; t=im[i]; im[i]=im[j]; im[j]=t; }
            }
            for(int size=2;size<=n;size<<=1)
            {
                float angle=-2*Mathf.PI/size,wr=Mathf.Cos(angle),wi=Mathf.Sin(angle); int half=size/2;
                for(int start=0;start<n;start+=size)
                {
                    float cr=1,ci=0;
                    for(int k=0;k<half;k++)
                    {
                        int a=start+k,b=a+half;
                        float tr=re[b]*cr-im[b]*ci,ti=re[b]*ci+im[b]*cr;
                        re[b]=re[a]-tr; im[b]=im[a]-ti; re[a]+=tr; im[a]+=ti;
                        float next=cr*wr-ci*wi; ci=cr*wi+ci*wr; cr=next;
                    }
                }
            }
        }
        // Subtracts the local half-second floor, then scales by the loud end of the song.
        static void Whiten(float[] flux)
        {
            int count=flux.Length; var result=new float[count]; const int radius=25;
            var prefix=new double[count+1]; for(int i=0;i<count;i++) prefix[i+1]=prefix[i]+flux[i];
            for(int f=0;f<count;f++)
            {
                int a=Mathf.Max(0,f-radius),b=Mathf.Min(count,f+radius+1);
                result[f]=Mathf.Max(0,flux[f]-(float)((prefix[b]-prefix[a])/(b-a))*1.1f);
            }
            var sorted=(float[])result.Clone(); Array.Sort(sorted);
            float top=Mathf.Max(.0001f,sorted[Mathf.Clamp((int)(count*.985f),0,count-1)]);
            for(int f=0;f<count;f++) flux[f]=Mathf.Clamp01(result[f]/top);
        }
        void MarkAccents()
        {
            void Pick(float[] band,byte flag,float threshold,int gap)
            {
                int last=-gap;
                for(int f=3;f<band.Length-3;f++)
                {
                    float v=band[f]; if(v<threshold || f-last<gap) continue;
                    bool peak=true; for(int k=-3;k<=3 && peak;k++) if(band[f+k]>v) peak=false;
                    if(peak) { Accents[f]|=flag; last=f; }
                }
            }
            Pick(Low,Kick,.32f,12); Pick(Mid,Snare,.38f,12); Pick(High,Hat,.45f,8);
        }
        // Autocorrelation of the onset curve, weighted toward common tempos, then a comb for phase.
        void EstimateTempo()
        {
            int count=Mathf.Min(Onset.Length,Rate*180); if(count<Rate*6) return;
            float bestScore=0,total=0; int bestLag=50;
            for(int i=0;i<count;i++) total+=Onset[i]*Onset[i];
            for(int lag=Rate*60/200;lag<=Rate*60/60;lag++)
            {
                float sum=0; for(int i=lag;i<count;i++) sum+=Onset[i]*Onset[i-lag];
                float bpm=60f*Rate/lag,prior=Mathf.Exp(-.5f*Mathf.Pow(Mathf.Log(bpm/120f,2)/.9f,2));
                if(sum*prior>bestScore) { bestScore=sum*prior; bestLag=lag; }
            }
            Confidence=total>0?Mathf.Clamp01(bestScore/total*1.6f):0;
            float best=0,center=60f*Rate/bestLag;
            for(float bpm=center-3;bpm<=center+3;bpm+=.05f)
            {
                float period=60f*Rate/bpm;
                for(int phase=0;phase<period;phase++)
                {
                    float sum=0; for(float at=phase;at<count;at+=period) sum+=Onset[(int)at];
                    if(sum>best) { best=sum; BPM=bpm; Phase=(float)phase/Rate; }
                }
            }
        }
        // Follows the grid through the song, nudging each beat toward the nearest real onset.
        void TrackBeats()
        {
            Beats.Clear(); float period=60f/BPM,t=Phase,drift=0;
            while(t<Length)
            {
                int centre=(int)((t+drift)*Rate),best=centre; float strongest=0;
                for(int f=Mathf.Max(0,centre-6);f<=Mathf.Min(Onset.Length-1,centre+6);f++)
                    if(Onset[f]>strongest*1.15f) { strongest=Onset[f]; best=f; }
                if(strongest>.2f) drift=Mathf.Clamp(drift+((float)best/Rate-(t+drift))*.25f,-.08f,.08f);
                Beats.Add(t+drift); t+=period;
            }
        }
        public float OnsetNear(float time,float radius,out float at)
        {
            int a=Mathf.Max(0,(int)((time-radius)*Rate)),b=Mathf.Min(Onset.Length-1,(int)((time+radius)*Rate)); float best=0; at=time;
            for(int f=a;f<=b;f++) if(Onset[f]>best) { best=Onset[f]; at=(float)f/Rate; }
            return best;
        }
        public List<Hit> Peaks(float density,float minimumGap)
        {
            var hits=new List<Hit>(); float threshold=Mathf.Lerp(.55f,.18f,Mathf.Clamp01(density)),last=-10;
            for(int f=4;f<Onset.Length-4;f++)
            {
                float value=Onset[f]; if(value<threshold) continue;
                bool peak=true; for(int k=-4;k<=4 && peak;k++) if(Onset[f+k]>value) peak=false;
                if(!peak) continue;
                float t=(float)f/Rate;
                if(t-last<minimumGap)
                {
                    // Keep the stronger of two crowded hits.
                    if(hits.Count>0 && hits[hits.Count-1].Strength<value) { hits[hits.Count-1]=new Hit{Time=t,Strength=value}; last=t; }
                    continue;
                }
                hits.Add(new Hit{Time=t,Strength=value}); last=t;
            }
            return hits;
        }
        // Beat and half-beat slots that really contain a hit: rhythmic like a hand-made chart,
        // but silent beats stay empty.
        public List<Hit> Rhythm(float density)
        {
            if(Beats.Count<8 || Confidence<.08f) return Peaks(density,Mathf.Lerp(.6f,.25f,density));
            var hits=new List<Hit>(); float threshold=Mathf.Lerp(.5f,.16f,Mathf.Clamp01(density)); bool halves=density>.45f;
            for(int i=0;i<Beats.Count;i++)
            {
                float beat=Beats[i],next=i+1<Beats.Count?Beats[i+1]:beat+60f/BPM;
                float strength=OnsetNear(beat,.045f,out _);
                if(strength>=threshold) hits.Add(new Hit{Time=beat,Strength=strength});
                // Off-beats must be clearly louder, so eighths appear only in busy passages.
                float half=(beat+next)*.5f; strength=OnsetNear(half,.04f,out _);
                if(halves && strength>=threshold*1.35f) hits.Add(new Hit{Time=half,Strength=strength});
            }
            return hits;
        }
        public float EnergyBetween(float a,float b)
        {
            int from=Mathf.Clamp((int)(a*Rate),0,Energy.Length),to=Mathf.Clamp((int)(b*Rate),from,Energy.Length); float sum=0;
            for(int f=from;f<to;f++) sum+=Energy[f];
            return to>from?sum/(to-from):0;
        }
    }

    public static class MapAutomation
    {
        static readonly int[] Keys={0,1,3,2,3,0,2,1};
        public static float Snap(ModMap mod,float time,int division)
        {
            if(division<=0) return time;
            float step=60f/mod.BPM/division;
            return Mathf.Max(0,Mathf.Round((time-mod.BeatOffset)/step)*step+mod.BeatOffset);
        }
        // Adds K-style markers on detected hits; timing already placed by hand is left alone.
        public static int Mark(ModMap mod,SongAnalysis song,float density,int snapDivision)
        {
            float gap=Mathf.Lerp(.45f,.22f,density),last=-10; int added=0;
            foreach(var hit in song.Rhythm(density))
            {
                float time=Snap(mod,hit.Time,snapDivision); bool taken=time-last<gap;
                foreach(var n in mod.Notes) if(Mathf.Abs(n.Timestamp-time)<gap*.6f) { taken=true; break; }
                if(taken) continue;
                mod.Notes.Add(new RhythmNote{Timestamp=time,Lane=ModMap.Undecided}); added++; last=time;
            }
            mod.Sort(); return added;
        }
        // Four-note phrases per hand; a long rest switches hands early so handovers feel natural.
        public static int Assign(ModMap mod)
        {
            int lane=1,within=0,key=0,saber=3,changed=0; float last=-10;
            foreach(var n in mod.Notes)
            {
                bool rest=n.Timestamp-last>1.1f;
                if(n.Lane==ModMap.Undecided)
                {
                    if(rest && within>0 || within>=4) { lane=1-lane; within=0; }
                    n.Lane=lane; n.Direction=lane==0?Keys[key++%Keys.Length]:saber; changed++;
                }
                else lane=n.Lane;
                if(n.Lane==1 && n.Direction==saber) saber=saber==3?1:3;
                within++; last=n.Timestamp;
            }
            return changed;
        }
        // Reads the song in eight-beat blocks and scores lights and camera to its intensity.
        public static void Stage(ModMap mod,SongAnalysis song)
        {
            mod.Events.Clear();
            float beat=60f/Mathf.Max(30,mod.BPM),block=beat*8,loudest=.0001f;
            int blocks=Mathf.CeilToInt(song.Length/block);
            for(int i=0;i<blocks;i++) loudest=Mathf.Max(loudest,song.EnergyBetween(i*block,(i+1)*block));
            int level=-1,palette=0; float roll=1;
            void Add(float time,StageKind kind,float value,float duration=.5f) => mod.Events.Add(new StageEvent{Time=Mathf.Max(0,time),Kind=(int)kind,Value=value,Duration=duration});
            var levels=new int[blocks];
            for(int i=0;i<blocks;i++)
            {
                float time=i*block+Mathf.Repeat(mod.BeatOffset,block),loud=song.EnergyBetween(i*block,(i+1)*block)/loudest;
                int now=loud<.5f?0:loud<.8f?1:2; levels[i]=now;
                if(now!=level)
                {
                    if(level>=0 && now>level)
                    {
                        Add(time-beat,StageKind.Strobe,.8f,beat); Add(time,StageKind.Flash,1); Add(time,StageKind.CameraZoom,.8f);
                        Add(time,StageKind.RingSpin,1); palette=(palette+1)%StageDirector.Palettes.Length; Add(time,StageKind.Palette,palette);
                    }
                    else if(level>=0) Add(time,StageKind.Blackout,.7f,beat*2);
                    Add(time,now==2?StageKind.SpotCamera:now==1?StageKind.SpotFan:StageKind.SpotSweep,1);
                    Add(time,StageKind.Energy,now==2?1:now==1?.75f:.45f); level=now;
                }
                else if(now==2 && i%2==0)
                {
                    Add(time,StageKind.RingSpin,.6f); Add(time,StageKind.CameraRoll,.7f*roll); roll=-roll;
                    Add(time,i%4==0?StageKind.SpotFan:StageKind.SpotCamera,1);
                }
            }
            float last=-10;
            foreach(var hit in song.Peaks(.45f,.3f))
            {
                int b=Mathf.Clamp((int)(hit.Time/block),0,blocks-1);
                if(hit.Strength<(levels[b]==2?.28f:.42f) || hit.Time-last<(levels[b]==2?.3f:.55f)) continue;
                Add(hit.Time,StageKind.CameraKick,Mathf.Clamp01(.3f+hit.Strength)); Add(hit.Time,StageKind.Flash,Mathf.Clamp01(hit.Strength*.5f)); last=hit.Time;
            }
            mod.Sort();
        }
    }
}
