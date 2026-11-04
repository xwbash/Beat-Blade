using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace ArcaneSaber
{
    // Converts a BeatSaver level zip (Info.dat + difficulty .dat files, formats v2, v3 and v4).
    // Red notes go to the left hand, blue notes to the laser; grid positions, bombs, walls,
    // arcs and chains have no equivalent here and are dropped.
    public static class BeatSaverImport
    {
        const long MaximumChartBytes=32L*1024*1024;
        #pragma warning disable 0649
        [Serializable] class Info
        {
            public string _songName,_songSubName,_songAuthorName,_levelAuthorName,_songFilename;
            public float _beatsPerMinute,_songTimeOffset;
            public List<Set> _difficultyBeatmapSets;
            public Song song; public Audio audio; public List<Difficulty4> difficultyBeatmaps;
        }
        [Serializable] class Set { public string _beatmapCharacteristicName; public List<Difficulty2> _difficultyBeatmaps; }
        [Serializable] class Difficulty2 { public string _difficulty,_beatmapFilename; public float _noteJumpMovementSpeed,_noteJumpStartBeatOffset; }
        [Serializable] class Song { public string title,subTitle,author; }
        [Serializable] class Audio { public string songFilename; public float bpm; }
        [Serializable] class Difficulty4 { public string characteristic,difficulty,beatmapDataFilename,lightshowDataFilename; public float noteJumpMovementSpeed,noteJumpStartBeatOffset; }
        [Serializable] class Chart
        {
            public string _version,version;
            public List<Note2> _notes; public List<Event2> _events;
            public List<Note3> colorNotes; public List<Bpm3> bpmEvents; public List<Event3> basicBeatmapEvents; public List<Boost3> colorBoostBeatmapEvents;
            public List<NoteData4> colorNotesData; public List<Reference4> basicEvents; public List<EventData4> basicEventsData;
        }
        [Serializable] class Note2 { public float _time; public int _type,_cutDirection,_lineIndex,_lineLayer; }
        [Serializable] class Event2 { public float _time; public int _type,_value; }
        [Serializable] class Note3 { public float b; public int c,d,i,x,y; }
        [Serializable] class Bpm3 { public float b,m; }
        [Serializable] class Event3 { public float b; public int et,i; }
        [Serializable] class Boost3 { public float b; public bool o; }
        [Serializable] class NoteData4 { public int c,d,x,y; }
        [Serializable] class Reference4 { public float b; public int i; }
        [Serializable] class EventData4 { public int t,i; }
        #pragma warning restore 0649
        struct Level { public string Name,File,Lights,Characteristic; public float Speed,Offset; }
        struct Note { public float Beat; public int Color,Direction,X,Y; }
        struct Light { public float Beat; public int Type,Value; }

        // Beat Saber: 0 up, 1 down, 2 left, 3 right, 4 up-left, 5 up-right, 6 down-left, 7 down-right, 8 any.
        // The laser only cuts left, right or free: diagonals keep their side, up/down become free.
        static readonly int[] SaberDirection={8,8,1,3,1,3,1,3,8};
        // WASD has no diagonals, and real maps are mostly down cuts, which would all be S.
        // Left/right cuts keep A/D; the rest take their key from where the red note sits:
        // top row W, middle row outer A / inner D, bottom row outer A / inner S.
        static int HandKey(int direction,int x,int y)
        {
            if(direction==2) return 1;
            if(direction==3) return 3;
            if(y>=2) return 0;
            if(y==1) return x==0?1:3;
            return x==0?1:2;
        }
        // Keyboard-friendly versions of a dense map: minimum gap per hand, and between any two notes.
        static readonly (string name,float hand,float any)[] Thinned={("Easy",.55f,.24f),("Medium",.34f,.14f)};
        static ModMap Thin(ModMap source,float handGap,float anyGap)
        {
            var mod=JsonUtility.FromJson<ModMap>(JsonUtility.ToJson(source)); mod.Notes.Clear();
            float[] last={-10,-10}; float any=-10;
            foreach(var n in source.Notes)
            {
                if(n.Timestamp-last[n.Lane]<handGap || n.Timestamp-any<anyGap) continue;
                mod.Notes.Add(n); last[n.Lane]=n.Timestamp; any=n.Timestamp;
            }
            return mod;
        }
        static readonly string[] Order={"Easy","Normal","Hard","Expert","ExpertPlus"};

        static ZipArchiveEntry Find(ZipArchive zip,string name)
        {
            if(string.IsNullOrEmpty(name)) return null;
            name=Path.GetFileName(name);
            foreach(var entry in zip.Entries) if(string.Equals(entry.Name,name,StringComparison.OrdinalIgnoreCase)) return entry;
            return null;
        }
        public static bool Detect(ZipArchive zip) => Find(zip,"Info.dat")!=null;
        static T Read<T>(ZipArchiveEntry entry)
        {
            if(entry.Length>MaximumChartBytes) throw new InvalidOperationException("Map file is too large: "+entry.Name);
            using(var reader=new StreamReader(entry.Open())) return JsonUtility.FromJson<T>(reader.ReadToEnd());
        }
        public static List<ModMap> Convert(ZipArchive zip)
        {
            var info=Read<Info>(Find(zip,"Info.dat"));
            bool v4=info.audio!=null && !string.IsNullOrEmpty(info.audio.songFilename);
            string title=v4?info.song?.title:info._songName, author=v4?info.song?.author:info._songAuthorName;
            string songFile=v4?info.audio.songFilename:info._songFilename; float bpm=v4?info.audio.bpm:info._beatsPerMinute;
            float offset=v4?0:info._songTimeOffset;
            var audio=Find(zip,songFile);
            if(audio==null) throw new InvalidOperationException("The map's music file is missing from the zip: "+songFile);
            string extension=Path.GetExtension(audio.Name).ToLowerInvariant();
            // .egg is Ogg Vorbis with a different name.
            if(extension==".egg") extension=".ogg";
            if(extension!=".ogg" && extension!=".wav" && extension!=".mp3") throw new InvalidOperationException("Unsupported music format: "+audio.Name);
            if(bpm<=0) throw new InvalidOperationException("The map's BPM value is invalid.");

            var levels=new List<Level>();
            if(v4 && info.difficultyBeatmaps!=null)
                foreach(var d in info.difficultyBeatmaps) levels.Add(new Level{Name=d.difficulty,File=d.beatmapDataFilename,Lights=d.lightshowDataFilename,Characteristic=d.characteristic,Speed=d.noteJumpMovementSpeed,Offset=d.noteJumpStartBeatOffset});
            else if(info._difficultyBeatmapSets!=null)
                foreach(var set in info._difficultyBeatmapSets) if(set._difficultyBeatmaps!=null)
                    foreach(var d in set._difficultyBeatmaps) levels.Add(new Level{Name=d._difficulty,File=d._beatmapFilename,Characteristic=set._beatmapCharacteristicName,Speed=d._noteJumpMovementSpeed,Offset=d._noteJumpStartBeatOffset});
            // Rotation and lightshow sets do not translate; prefer Standard when it exists.
            bool hasStandard=levels.Exists(l=>l.Characteristic=="Standard");
            levels.RemoveAll(l=>hasStandard?l.Characteristic!="Standard":(l.Characteristic=="360Degree" || l.Characteristic=="90Degree" || l.Characteristic=="Lightshow"));
            levels.Sort((a,b)=>Array.IndexOf(Order,a.Name).CompareTo(Array.IndexOf(Order,b.Name)));
            if(levels.Count==0) throw new InvalidOperationException("No playable difficulty in the zip.");

            var mods=new List<ModMap>();
            // Without an Easy or Normal chart, the easiest one also yields thinned keyboard versions.
            bool gentle=levels.Exists(l=>l.Name=="Easy" || l.Name=="Normal");
            string name=string.IsNullOrEmpty(title)?"BeatSaver":title;
            try
            {
                foreach(var level in levels)
                {
                    var file=Find(zip,level.File); if(file==null) continue;
                    var chart=Read<Chart>(file);
                    var lightFile=Find(zip,level.Lights);
                    var lights=lightFile!=null && lightFile!=file?Read<Chart>(lightFile):chart;
                    var mod=Build(chart,lights,bpm,offset);
                    if(mod.Notes.Count==0) continue;
                    mod.Artist=author??"";
                    mod.NoteSpeed=level.Speed>0?level.Speed:10; mod.JumpOffset=level.Offset;
                    if(!gentle)
                    {
                        gentle=true;
                        foreach(var t in Thinned)
                        {
                            var easier=Thin(mod,t.hand,t.any); easier.Title=name+"  ["+t.name+"]";
                            easier.NoteSpeed=Mathf.Min(mod.NoteSpeed,t.name=="Easy"?10:12);
                            ModLibrary.Install(easier,audio,extension); mods.Add(easier);
                        }
                    }
                    mod.Title=name+(levels.Count>1 || mods.Count>0?"  ["+Pretty(level.Name)+"]":"");
                    ModLibrary.Install(mod,audio,extension); mods.Add(mod);
                }
            }
            catch { foreach(var mod in mods) ModLibrary.Delete(mod); throw; }
            if(mods.Count==0) throw new InvalidOperationException("No convertible notes found in the map.");
            return mods;
        }
        public static BeatmapController Packaged(AudioClip audio)
        {
            var info=JsonUtility.FromJson<Info>(Resources.Load<TextAsset>("BellTolls/Info").text);
            var level=info._difficultyBeatmapSets[0]._difficultyBeatmaps[0];
            var chart=JsonUtility.FromJson<Chart>(Resources.Load<TextAsset>("BellTolls/Expert").text);
            var mod=Build(chart,chart,info._beatsPerMinute,info._songTimeOffset);
            mod.NoteSpeed=level._noteJumpMovementSpeed; mod.JumpOffset=level._noteJumpStartBeatOffset;
            return mod.ToBeatmap(audio);
        }
        static string Pretty(string name) => name=="ExpertPlus"?"Expert+":name;

        static ModMap Build(Chart chart,Chart lightChart,float bpm,float offset)
        {
            var notes=new List<Note>(); var lights=new List<Light>(); var changes=new List<Bpm3>(); var boosts=new List<Boost3>();
            if(chart._notes!=null)
                foreach(var n in chart._notes) if(n._type==0 || n._type==1) notes.Add(new Note{Beat=n._time,Color=n._type,Direction=n._cutDirection,X=n._lineIndex,Y=n._lineLayer});
            if(chart.colorNotes!=null)
            {
                bool split=chart.colorNotesData!=null && chart.colorNotesData.Count>0;
                foreach(var n in chart.colorNotes)
                {
                    int color=n.c,direction=n.d,x=n.x,y=n.y;
                    if(split) { if(n.i<0 || n.i>=chart.colorNotesData.Count) continue; color=chart.colorNotesData[n.i].c; direction=chart.colorNotesData[n.i].d; x=chart.colorNotesData[n.i].x; y=chart.colorNotesData[n.i].y; }
                    if(color==0 || color==1) notes.Add(new Note{Beat=n.b,Color=color,Direction=direction,X=x,Y=y});
                }
            }
            if(chart.bpmEvents!=null) changes.AddRange(chart.bpmEvents);
            changes.RemoveAll(c=>c.m<=0); changes.Sort((a,b)=>a.b.CompareTo(b.b));
            if(lightChart._events!=null) foreach(var e in lightChart._events) lights.Add(new Light{Beat=e._time,Type=e._type,Value=e._value});
            if(lightChart.basicBeatmapEvents!=null) foreach(var e in lightChart.basicBeatmapEvents) lights.Add(new Light{Beat=e.b,Type=e.et,Value=e.i});
            if(lightChart.basicEvents!=null && lightChart.basicEventsData!=null)
                foreach(var e in lightChart.basicEvents) if(e.i>=0 && e.i<lightChart.basicEventsData.Count) lights.Add(new Light{Beat=e.b,Type=lightChart.basicEventsData[e.i].t,Value=lightChart.basicEventsData[e.i].i});
            if(lightChart.colorBoostBeatmapEvents!=null) boosts.AddRange(lightChart.colorBoostBeatmapEvents);

            float Seconds(float beat)
            {
                // Tempo changes accumulate piecewise from the base BPM.
                float time=0,from=0,rate=bpm;
                foreach(var c in changes) { if(c.b>=beat) break; time+=(c.b-from)*60/rate; from=c.b; rate=c.m; }
                return Mathf.Max(0,time+(beat-from)*60/rate+offset);
            }

            var mod=new ModMap{BPM=bpm};
            notes.Sort((a,b)=>a.Beat.CompareTo(b.Beat));

            foreach(var n in notes)
            {
                // One note per hand per moment: our lanes have a single position, so stacks would overlap.
                float time=Seconds(n.Beat); int lane=n.Color==0?0:1;

                int d=Mathf.Clamp(n.Direction,0,8);
                int direction=lane==1?SaberDirection[d]:HandKey(d,n.X,n.Y);
                mod.Notes.Add(new RhythmNote{Timestamp=time,Lane=lane,Direction=direction});
            }
            Lights(mod,lights,boosts,Seconds);
            return mod;
        }
        // Beat Saber light groups 0-4, ring spin 8, ring zoom 9, laser speed 12/13, boost 5.
        // Their thousands of fine cues are thinned into our flashes, spins and spot modes.
        static void Lights(ModMap mod,List<Light> lights,List<Boost3> boosts,Func<float,float> seconds)
        {
            if(lights.Count==0 && boosts.Count==0) return;
            lights.Sort((a,b)=>a.Beat.CompareTo(b.Beat));
            void Add(float time,StageKind kind,float value,float duration=.5f) => mod.Events.Add(new StageEvent{Time=time,Kind=(int)kind,Value=value,Duration=duration});
            Add(0,StageKind.Energy,.8f); Add(0,StageKind.SpotSweep,1);
            float lastFlash=-1,lastKick=-1,lastSpin=-1,lastSpot=-1; int spot=0;
            foreach(var l in lights)
            {
                float t=seconds(l.Beat); int v=l.Value;
                bool flash=v==2 || v==3 || v==6 || v==7 || v==10 || v==11;
                if(l.Type>=0 && l.Type<=4 && flash && t-lastFlash>.12f)
                {
                    Add(t,StageKind.Flash,l.Type==4?.7f:.45f); lastFlash=t;
                    if(l.Type==4 && t-lastKick>.4f) { Add(t,StageKind.CameraKick,.4f); lastKick=t; }
                }
                else if(l.Type==8 && t-lastSpin>.2f) { Add(t,StageKind.RingSpin,.7f); lastSpin=t; }
                else if(l.Type==9 && t-lastSpin>.2f) { Add(t,StageKind.RingSpin,.35f); lastSpin=t; }
                else if(l.Type==12 || l.Type==13)
                {
                    int mode=v==0?0:v<=4?2:1;
                    if(mode!=spot && t-lastSpot>.5f) { Add(t,mode==0?StageKind.SpotSweep:mode==1?StageKind.SpotCamera:StageKind.SpotFan,1); spot=mode; lastSpot=t; }
                }
                else if(l.Type==5) Add(t,StageKind.Palette,v==1?1:0);
            }
            foreach(var b in boosts) Add(seconds(b.b),StageKind.Palette,b.o?1:0);
        }
    }
}
