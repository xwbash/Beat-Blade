using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcaneSaber
{
    // In-game chart editor. Timing lives in ModMap; a preview beatmap mirrors it into the arena.
    public sealed class MapEditor
    {
        const float Left=30,Width=1380,OverviewY=566,WaveY=584,WaveHeight=104,LaserY=692,MarkY=730,HandY=768,LaneHeight=36,StageY=806,StageHeight=34;
        public bool Active { get; private set; }
        public ModMap Mod { get; private set; }
        public double Head { get; private set; }
        public string Status="";
        public Action<double> Test; public Action Exit;
        readonly RhythmGameManager game; readonly MusicConductor music; readonly NoteSpawner notes; readonly StageDirector stage;
        readonly List<RhythmNote> selected=new List<RhythmNote>(); StageEvent selectedEvent;
        readonly List<string> undo=new List<string>(),redo=new List<string>();
        readonly float[] speeds={1,.75f,.5f}; readonly string[] speedNames={"1x",".75x",".5x"};
        readonly string[] snapNames={"Serbest","1/1","1/2","1/4"}; readonly int[] snapDivisions={0,1,2,4};
        AudioClip clip; SongAnalysis song; BeatmapController preview; Texture2D wave; Color32[] wavePixels;
        bool playing,mapDirty,seekDirty,ticks=true; double scheduledUntil; double snippetEnd=-1,snippetHome; int speedIndex,snapIndex; float density=.5f;
        double viewStart,viewSpan=12,drawnStart=-1,drawnSpan; string bpmText=""; int handKey; Vector2 leftScroll,rightScroll;
        enum Drag { None,Scrub,Overview,Notes,Event,Box } Drag drag; double dragOrigin; float dragX; Dictionary<RhythmNote,float> dragTimes; float dragEventTime; bool dragMoved;
        static readonly int[] HandCycle={0,1,3,2,3,0,2,1};
        public MapEditor(RhythmGameManager game,MusicConductor music,NoteSpawner notes,StageDirector stage)
        { this.game=game; this.music=music; this.notes=notes; this.stage=stage; }

        public void Open(ModMap mod,AudioClip clip,SongAnalysis song)
        {
            Mod=mod; this.clip=clip; this.song=song; Active=true; undo.Clear(); redo.Clear(); selected.Clear(); selectedEvent=null;
            bpmText=mod.BPM.ToString("0.##"); viewStart=0; viewSpan=Mathf.Min(12,clip.length); drawnStart=-1; Head=0; playing=false;
            if(!preview) preview=ScriptableObject.CreateInstance<BeatmapController>();
            mod.ToBeatmap(clip,preview); music.Prepare(preview,clip); mapDirty=true;
            Status=$"{clip.length/60:0}:{clip.length%60:00} ses yüklendi. BOŞLUK çal, K ile vuruşları işaretle.";
        }
        // Back from a section test: the arena, music and playhead return to where editing stopped.
        public void Resume(double time)
        {
            Active=true; playing=false; mapDirty=true; music.Prepare(preview,clip); Seek(time);
        }
        public void Suspend() { Pause(); Active=false; }
        public void Close() { Pause(); Save(); music.Stop(); notes.Reset(); Active=false; Mod=null; clip=null; song=null; }
        public void Save() { if(Mod!=null) { ModLibrary.Save(Mod); Status="Kaydedildi."; } }

        double Snap(double time) => MapAutomation.Snap(Mod,(float)time,snapDivisions[snapIndex]);
        void Snapshot()
        {
            undo.Add(JsonUtility.ToJson(Mod)); if(undo.Count>48) undo.RemoveAt(0);
            redo.Clear();
        }
        void Restore(List<string> from,List<string> to)
        {
            if(from.Count==0) return;
            to.Add(JsonUtility.ToJson(Mod)); string id=Mod.Id;
            JsonUtility.FromJsonOverwrite(from[from.Count-1],Mod); Mod.Id=id; from.RemoveAt(from.Count-1);
            selected.Clear(); selectedEvent=null; mapDirty=true;
        }
        void Play() { music.PlayFrom(Head,speeds[speedIndex]); playing=true; snippetEnd=-1; scheduledUntil=Head-.001; }
        void Pause() { if(playing) music.Hold(Head); playing=false; snippetEnd=-1; }
        void Seek(double time)
        {
            Head=Math.Max(0,Math.Min(time,clip.length-.01f)); seekDirty=true;
            if(playing) music.PlayFrom(Head,speeds[speedIndex]); else music.Hold(Head);
            scheduledUntil=Head-.001;
            KeepInView();
        }
        void KeepInView()
        {
            if(Head<viewStart || Head>viewStart+viewSpan*.9) viewStart=Math.Max(0,Head-viewSpan*.1);
        }
        // Lets a marker be heard while it is being decided, without moving away from it.
        void PlaySnippet(double time)
        {
            snippetHome=time; Head=Math.Max(0,time-.45f); music.PlayFrom(Head); playing=true; snippetEnd=time+.3f; seekDirty=true; scheduledUntil=Head-.001; KeepInView();
        }
        // Queues each note's hit sound slightly ahead, on the DSP clock, so it lands exactly on time.
        void Schedule()
        {
            double horizon=Head+.15*speeds[speedIndex];
            foreach(var n in Mod.Notes)
            {
                if(n.Timestamp<=scheduledUntil) continue;
                if(n.Timestamp>horizon) break;
                game.Hits.PlayAt(music.DspAt(n.Timestamp),n.Lane);
            }
            scheduledUntil=horizon;
        }
        public void Tick(Keyboard k)
        {
            if(playing)
            {
                Head=music.SongTime;
                if(snippetEnd>=0 && Head>=snippetEnd) { playing=false; snippetEnd=-1; Head=snippetHome; music.Hold(Head); seekDirty=true; }
                else if(Head>=clip.length-.02f) { Head=clip.length-.02f; Pause(); }
                else if(Head>viewStart+viewSpan*.9) viewStart=Head-viewSpan*.1;
                if(playing && ticks) Schedule();
            }
            if(k!=null && GUIUtility.keyboardControl==0) Keys(k);
            if(mapDirty) { Mod.Sort(); Mod.ToBeatmap(clip,preview); stage.Load(Mod.Events,Mod.LightPower,Mod.CameraPower,Mod.BloomPower,song); seekDirty=true; mapDirty=false; }
            if(seekDirty)
            {
                notes.Reset(); notes.SkipTo(preview,Head); notes.Update(preview,Head); seekDirty=false;
            }
            else if(playing) { notes.Update(preview,Head); notes.ReleasePassed(Head); }
        }
        void Keys(Keyboard k)
        {
            bool ctrl=k.ctrlKey.isPressed,shift=k.shiftKey.isPressed;
            if(k.spaceKey.wasPressedThisFrame) { if(playing) Pause(); else Play(); }
            if(ctrl)
            {
                if(k.sKey.wasPressedThisFrame) Save();
                if(k.zKey.wasPressedThisFrame) Restore(undo,redo);
                if(k.yKey.wasPressedThisFrame) Restore(redo,undo);
                if(k.aKey.wasPressedThisFrame) { selected.Clear(); selected.AddRange(Mod.Notes); selectedEvent=null; }
                return;
            }
            if(k.kKey.wasPressedThisFrame) Place(ModMap.Undecided);
            if(k.jKey.wasPressedThisFrame) LaneKey(0);
            if(k.lKey.wasPressedThisFrame) LaneKey(1);
            if(k.tabKey.wasPressedThisFrame) NextUndecided();
            if(k.tKey.wasPressedThisFrame) { Pause(); Save(); Test?.Invoke(Head); return; }
            if(k.wKey.wasPressedThisFrame) SetDirection(0);
            if(k.aKey.wasPressedThisFrame) SetDirection(1);
            if(k.sKey.wasPressedThisFrame) SetDirection(2);
            if(k.dKey.wasPressedThisFrame) SetDirection(3);
            if(k.fKey.wasPressedThisFrame) SetDirection(8);
            if(k.deleteKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame) DeleteSelection();
            float beat=60f/Mod.BPM;
            if(k.leftArrowKey.wasPressedThisFrame) Seek(Head-(shift?beat*4:beat*.25f));
            if(k.rightArrowKey.wasPressedThisFrame) Seek(Head+(shift?beat*4:beat*.25f));
            if(k.homeKey.wasPressedThisFrame) Seek(0);
            if(k.endKey.wasPressedThisFrame) Seek(clip.length);
            if(k.commaKey.wasPressedThisFrame) Nudge(-.01f);
            if(k.periodKey.wasPressedThisFrame) Nudge(.01f);
            var row=new[]{k.digit1Key,k.digit2Key,k.digit3Key,k.digit4Key,k.digit5Key,k.digit6Key,k.digit7Key,k.digit8Key,k.digit9Key,k.digit0Key,k.minusKey,k.equalsKey};
            for(int i=0;i<row.Length;i++) if(row[i].wasPressedThisFrame) AddEvent((StageKind)i);
            if(k.escapeKey.wasPressedThisFrame) Exit?.Invoke();
        }
        // While playing J/L place a decided note; while stopped they decide the selection.
        void LaneKey(int lane)
        {
            if(playing || selected.Count==0) { Place(lane); return; }
            Snapshot();
            foreach(var n in selected) SetLane(n,lane);
            mapDirty=true;
            if(selected.Count==1) NextUndecided();
        }
        void SetLane(RhythmNote n,int lane)
        {
            if(n.Lane==lane) return;
            n.Lane=lane; n.Direction=DefaultDirection(lane,n.Timestamp);
        }
        int DefaultDirection(int lane,float time)
        {
            if(lane==0) return HandCycle[handKey++%HandCycle.Length];
            // Cuts alternate from whatever laser note comes before this time.
            int previous=1;
            foreach(var n in Mod.Notes) { if(n.Timestamp>=time) break; if(n.Lane==1) previous=n.Direction; }
            return previous==3?1:3;
        }
        void Place(int lane)
        {
            float time=(float)Snap(Math.Max(0,Head-(playing?game.LatencySeconds:0)));
            foreach(var n in Mod.Notes) if(Mathf.Abs(n.Timestamp-time)<.012f && n.Lane==lane) return;
            Snapshot();
            var note=new RhythmNote{Timestamp=time,Lane=lane,Direction=lane==ModMap.Undecided?0:DefaultDirection(lane,time)};
            Mod.Notes.Add(note); mapDirty=true;
            selected.Clear(); selected.Add(note); selectedEvent=null;
        }
        void NextUndecided()
        {
            Mod.Sort(); RhythmNote next=null,first=null;
            foreach(var n in Mod.Notes)
            {
                if(n.Lane!=ModMap.Undecided) continue;
                if(first==null) first=n;
                if(n.Timestamp>Head+.001f) { next=n; break; }
            }
            next=next??first;
            selected.Clear(); selectedEvent=null;
            if(next==null) { Status="Karar bekleyen işaret kalmadı."; return; }
            selected.Add(next); PlaySnippet(next.Timestamp);
            Status=$"{Mod.UndecidedCount} işaret kaldı: J = sol el, L = sağ lazer.";
        }
        void SetDirection(int direction)
        {
            if(selected.Count==0) return;
            Snapshot();
            foreach(var n in selected) if(n.Lane==0 && direction<4) n.Direction=direction; else if(n.Lane==1) n.Direction=ModMap.LaserDirection(direction);
            mapDirty=true;
        }
        void DeleteSelection()
        {
            if(selected.Count==0 && selectedEvent==null) return;
            Snapshot();
            foreach(var n in selected) Mod.Notes.Remove(n);
            if(selectedEvent!=null) Mod.Events.Remove(selectedEvent);
            selected.Clear(); selectedEvent=null; mapDirty=true;
        }
        void Nudge(float amount)
        {
            if(selected.Count==0 && selectedEvent==null) return;
            Snapshot();
            foreach(var n in selected) n.Timestamp=Mathf.Max(0,n.Timestamp+amount);
            if(selectedEvent!=null) selectedEvent.Time=Mathf.Max(0,selectedEvent.Time+amount);
            mapDirty=true;
        }
        void AddEvent(StageKind kind)
        {
            Snapshot();
            float value=kind==StageKind.Palette?NextPalette():kind==StageKind.Energy?.8f:kind==StageKind.CameraRoll?.7f:.8f;
            float duration=kind==StageKind.Strobe?60f/Mod.BPM*2:kind==StageKind.Blackout?60f/Mod.BPM:.5f;
            var cue=new StageEvent{Time=(float)Snap(Head),Kind=(int)kind,Value=value,Duration=duration};
            Mod.Events.Add(cue); selected.Clear(); selectedEvent=cue; mapDirty=true;
        }
        int NextPalette()
        {
            int last=-1; foreach(var e in Mod.Events) if(e.Kind==(int)StageKind.Palette && e.Time<=Head) last=(int)e.Value;
            return (last+1)%StageDirector.Palettes.Length;
        }

        float X(double time) => Left+(float)((time-viewStart)/viewSpan)*Width;
        double Time(float x) => viewStart+(x-Left)/Width*viewSpan;
        void Rebuild()
        {
            if(drawnStart==viewStart && drawnSpan==viewSpan) return;
            drawnStart=viewStart; drawnSpan=viewSpan;
            const int width=1380,height=104;
            if(!wave) { wave=new Texture2D(width,height,TextureFormat.RGBA32,false); wavePixels=new Color32[width*height]; }
            var dim=new Color32(10,16,28,255); var bright=new Color32(46,150,196,255);
            Array.Clear(wavePixels,0,wavePixels.Length);
            for(int x=0;x<width;x++)
            {
                int a=Mathf.Clamp((int)((viewStart+viewSpan*x/width)*SongAnalysis.Rate),0,song.Peak.Length-1);
                int b=Mathf.Clamp((int)((viewStart+viewSpan*(x+1)/width)*SongAnalysis.Rate),a+1,song.Peak.Length);
                float peak=0; for(int f=a;f<b;f++) peak=Mathf.Max(peak,song.Peak[f]);
                int half=Mathf.RoundToInt(Mathf.Clamp01(peak)*height*.48f);
                for(int y=0;y<height;y++) wavePixels[y*width+x]=Mathf.Abs(y-height/2)<=half?bright:dim;
            }
            wave.SetPixels32(wavePixels); wave.Apply(false);
        }
        static Color LaneColor(int lane) => lane==0?ArcaneVisuals.Crimson:lane==1?ArcaneVisuals.Cyan:new Color(1,.86f,.2f);
        static float LaneY(int lane) => lane==1?LaserY:lane==0?HandY:MarkY;
        static string Clock(double t) => $"{(int)(t/60)}:{t%60:00.00}";
        public void Draw()
        {
            var ui=game; Rebuild();
            ui.Panel(new Rect(0,0,1440,86),new Color(.006f,.01f,.022f,.9f));
            ui.Label(30,12,620,34,Mod.Title,ui.Body);
            ui.Label(30,46,800,24,$"{Clock(Head)}  /  {Clock(clip.length)}     {Mod.Notes.Count} not     {Mod.UndecidedCount} kararsız     {Mod.Events.Count} sahne     {Status}",ui.Small);
            if(GUI.Button(new Rect(860,16,120,50),playing?"DURDUR":"ÇAL",ui.ButtonStyle)) { if(playing) Pause(); else Play(); }
            speedIndex=GUI.SelectionGrid(new Rect(990,16,120,24),speedIndex,speedNames,3);
            if(GUI.Button(new Rect(990,42,120,24),"Geri Al")) Restore(undo,redo);
            if(GUI.Button(new Rect(1120,16,110,50),"KAYDET",ui.ButtonStyle)) Save();
            if(GUI.Button(new Rect(1236,16,86,50),"TEST (T)",ui.ButtonStyle)) { Pause(); Save(); Test?.Invoke(Head); }
            if(GUI.Button(new Rect(1328,16,88,50),"ÇIKIŞ",ui.ButtonStyle)) Exit?.Invoke();
            ToolPanel(); Inspector(); Timeline();
            ui.Panel(new Rect(0,846,1440,54),new Color(.006f,.01f,.022f,.9f));
            ui.Label(30,850,1380,20,"BOŞLUK çal/durdur     K işaret     J sol el     L sağ lazer     TAB sıradaki kararsız     W A S D yön     F serbest kesim     SİL sil     1-0 - = sahne     , . ince ayar",ui.Subtitle);
            ui.Label(30,872,1380,20,"T buradan test et     Sol tık seç/sürükle     Boş alanda sürükle: çoklu seçim     Sağ tık sil     Teker: zoom     Ctrl+Teker: kaydır     Ctrl+Z / Y     Ctrl+S",ui.Subtitle);
        }
        void Rule(string text,bool first=false) { if(!first) GUILayout.Space(10); GUILayout.Label(text,game.Accent); }
        void ToolPanel()
        {
            game.Panel(new Rect(16,94,272,462),new Color(.006f,.01f,.022f,.86f));
            GUILayout.BeginArea(new Rect(24,100,256,450)); leftScroll=GUILayout.BeginScrollView(leftScroll);
            Rule("OTOMATİK",true);
            GUILayout.BeginHorizontal(); GUILayout.Label("Yoğunluk",GUILayout.Width(66)); density=GUILayout.HorizontalSlider(density,0,1); GUILayout.EndHorizontal();
            if(GUILayout.Button("Vuruşları işaretle")) { Snapshot(); int added=MapAutomation.Mark(Mod,song,density,snapDivisions[snapIndex]); mapDirty=true; Status=$"{added} işaret eklendi."; }
            if(GUILayout.Button("Sağ / sol otomatik karar")) { Snapshot(); int n=MapAutomation.Assign(Mod); mapDirty=true; Status=$"{n} işarete karar verildi."; }
            if(GUILayout.Button("Işık + kamera üret")) { Snapshot(); MapAutomation.Stage(Mod,song); mapDirty=true; Status=$"{Mod.Events.Count} sahne olayı üretildi."; }
            Rule("SAHNE OLAYI EKLE");
            string[] keys={"1","2","3","4","5","6","7","8","9","0","-","="};
            for(int i=0;i<StageEvent.Names.Length;i++)
                if(GUILayout.Button($"{keys[i]}   {StageEvent.Names[i]}",game.LeftButton)) AddEvent((StageKind)i);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        void Slider(string name,ref float value,float min,float max,bool cue=true)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(name,GUILayout.Width(92));
            float next=GUILayout.HorizontalSlider(value,min,max);
            GUILayout.Label(next.ToString("0.00"),GUILayout.Width(36)); GUILayout.EndHorizontal();
            if(next!=value) { value=next; mapDirty=true; }
        }
        void Inspector()
        {
            game.Panel(new Rect(1152,94,272,462),new Color(.006f,.01f,.022f,.86f));
            GUILayout.BeginArea(new Rect(1160,100,256,450)); rightScroll=GUILayout.BeginScrollView(rightScroll);
            Rule("SEÇİM",true);
            if(selected.Count>0)
            {
                GUILayout.Label(selected.Count==1?$"Not  {Clock(selected[0].Timestamp)}":$"{selected.Count} not seçili");
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("SOL EL (J)")) LaneButton(0);
                if(GUILayout.Button("SAĞ LAZER (L)")) LaneButton(1);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                string[] names={"Yukarı","Sol","Aşağı","Sağ"};
                for(int d=0;d<4;d++) if(GUILayout.Button(names[d])) SetDirection(d);
                GUILayout.EndHorizontal();
                if(GUILayout.Button("Seçimi sil")) DeleteSelection();
            }
            else if(selectedEvent!=null)
            {
                GUILayout.Label($"{StageEvent.Names[selectedEvent.Kind]}  {Clock(selectedEvent.Time)}");
                var kind=(StageKind)selectedEvent.Kind;
                if(kind==StageKind.Palette)
                {
                    int index=Mathf.Clamp((int)selectedEvent.Value,0,StageDirector.PaletteNames.Length-1);
                    int chosen=GUILayout.SelectionGrid(index,StageDirector.PaletteNames,1);
                    if(chosen!=index) { selectedEvent.Value=chosen; mapDirty=true; }
                }
                else
                {
                    Slider("Güç",ref selectedEvent.Value,kind==StageKind.CameraRoll?-1:0,1);
                    if(kind==StageKind.Strobe || kind==StageKind.Blackout) Slider("Süre (sn)",ref selectedEvent.Duration,.05f,8);
                }
                if(GUILayout.Button("Olayı sil")) DeleteSelection();
            }
            else GUILayout.Label("Zaman çizelgesinden not veya sahne olayı seç.");
            Rule("TEMPO");
            GUILayout.BeginHorizontal(); GUILayout.Label("BPM",GUILayout.Width(40));
            string text=GUILayout.TextField(bpmText,GUILayout.Width(70));
            if(text!=bpmText) { bpmText=text; if(float.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float bpm) && bpm>=30 && bpm<=300) { Mod.BPM=bpm; mapDirty=true; } }
            if(GUILayout.Button("Algıla")) { Mod.BPM=song.BPM; Mod.BeatOffset=song.Phase; bpmText=song.BPM.ToString("0.##"); GUIUtility.keyboardControl=0; mapDirty=true; Status=$"Tempo {song.BPM:0.##} BPM."; }
            GUILayout.EndHorizontal();
            Slider("Vuruş kayması",ref Mod.BeatOffset,0,60f/Mod.BPM);
            ticks=GUILayout.Toggle(ticks," Notlarda vuruş sesi");
            GUILayout.Label("Izgaraya oturt");
            snapIndex=GUILayout.SelectionGrid(snapIndex,snapNames,4);
            Rule("MOD");
            GUILayout.BeginHorizontal(); GUILayout.Label("Ad",GUILayout.Width(40)); Mod.Title=GUILayout.TextField(Mod.Title); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.Label("Sanatçı",GUILayout.Width(50)); Mod.Artist=GUILayout.TextField(Mod.Artist); GUILayout.EndHorizontal();
            Slider("Not hızı",ref Mod.NoteSpeed,5,24);
            Slider("Vuruş penceresi",ref Mod.HitWindow,.08f,.3f);
            Slider("Işık gücü",ref Mod.LightPower,0,2);
            Slider("Kamera gücü",ref Mod.CameraPower,0,2);
            Slider("Bloom",ref Mod.BloomPower,0,2);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        void LaneButton(int lane) { Snapshot(); foreach(var n in selected) SetLane(n,lane); mapDirty=true; }
        void Timeline()
        {
            var ui=game; float beat=60f/Mod.BPM;
            ui.Panel(new Rect(0,560,1440,286),new Color(.006f,.01f,.022f,.92f));
            // Whole-song overview: note density and the visible window.
            ui.Panel(new Rect(Left,OverviewY,Width,14),new Color(.05f,.08f,.13f));
            ui.Panel(new Rect(Left+(float)(viewStart/clip.length)*Width,OverviewY,Mathf.Max(3,(float)(viewSpan/clip.length)*Width),14),new Color(.2f,.32f,.45f));
            foreach(var n in Mod.Notes) ui.Panel(new Rect(Left+n.Timestamp/clip.length*Width,OverviewY+3,1,8),LaneColor(n.Lane));
            GUI.DrawTexture(new Rect(Left,WaveY,Width,WaveHeight),wave);
            for(int i=0;i<3;i++) ui.Panel(new Rect(Left,LaneYIndex(i),Width,LaneHeight-2),new Color(.03f,.05f,.085f));
            ui.Panel(new Rect(Left,StageY,Width,StageHeight),new Color(.045f,.04f,.08f));
            ui.Label(Left+6,LaserY+8,120,20,"SAĞ / LAZER",ui.Small,ArcaneVisuals.Cyan); ui.Label(Left+6,MarkY+8,120,20,"İŞARET",ui.Small,LaneColor(2));
            ui.Label(Left+6,HandY+8,120,20,"SOL / EL",ui.Small,ArcaneVisuals.Crimson); ui.Label(Left+6,StageY+7,120,20,"SAHNE",ui.Small);
            // Grid lines thin out as the view widens, so they never crowd the lanes.
            float step=beat; while(step/viewSpan*Width<14) step*=2;
            int firstLine=Mathf.CeilToInt((float)((viewStart-Mod.BeatOffset)/step));
            for(int i=firstLine;;i++)
            {
                double t=i*step+Mod.BeatOffset; if(t>viewStart+viewSpan) break;
                bool bar=Mathf.RoundToInt(i*step/beat)%4==0;
                ui.Panel(new Rect(X(t),WaveY,1,LaserY-WaveY+LaneHeight*3+StageHeight+6),bar?new Color(.3f,.42f,.55f,.55f):new Color(.2f,.3f,.4f,.22f));
            }
            foreach(var n in Mod.Notes)
            {
                float x=X(n.Timestamp); if(x<Left-8 || x>Left+Width+8) continue;
                bool on=selected.Contains(n);
                if(on) ui.Panel(new Rect(x-8,LaneY(n.Lane)+3,16,LaneHeight-8),Color.white);
                ui.Panel(new Rect(x-6,LaneY(n.Lane)+5,12,LaneHeight-12),LaneColor(n.Lane));
                string glyph=n.Lane==0?GestureMath.Label(n.Direction):n.Lane==1?(n.Direction==1?"<":n.Direction==3?">":n.Direction==0?"^":n.Direction==2?"v":"•"):"";
                if(glyph!="") ui.Label(x-10,LaneY(n.Lane)+7,20,20,glyph,ui.Tiny,Color.black);
            }
            foreach(var e in Mod.Events)
            {
                float x=X(e.Time); if(x<Left-4 || x>Left+Width+4) continue;
                Color color=Color.HSVToRGB(e.Kind/12f,.65f,1);
                if(e==selectedEvent) ui.Panel(new Rect(x-5,StageY+1,10,StageHeight-2),Color.white);
                ui.Panel(new Rect(x-3,StageY+3,6,StageHeight-6),color);
                if(viewSpan<5) ui.Label(x+5,StageY+7,120,20,StageEvent.Names[e.Kind],ui.Tiny,color);
            }
            ui.Panel(new Rect(X(Head)-1,WaveY-4,2,StageY+StageHeight-WaveY+8),Color.white);
            if(drag==Drag.Box) { var box=BoxRect(); ui.Panel(box,new Color(1,1,1,.14f)); }
            Mouse();
        }
        static float LaneYIndex(int i) => i==0?LaserY:i==1?MarkY:HandY;
        Rect BoxRect() { float a=dragX,b=Event.current.mousePosition.x; return new Rect(Mathf.Min(a,b),LaserY,Mathf.Abs(b-a),StageY+StageHeight-LaserY); }
        RhythmNote NoteAt(Vector2 m)
        {
            RhythmNote best=null; float nearest=10;
            foreach(var n in Mod.Notes)
            {
                float y=LaneY(n.Lane); if(m.y<y || m.y>y+LaneHeight) continue;
                float d=Mathf.Abs(X(n.Timestamp)-m.x); if(d<nearest) { nearest=d; best=n; }
            }
            return best;
        }
        StageEvent EventAt(Vector2 m)
        {
            if(m.y<StageY || m.y>StageY+StageHeight) return null;
            StageEvent best=null; float nearest=8;
            foreach(var e in Mod.Events) { float d=Mathf.Abs(X(e.Time)-m.x); if(d<nearest) { nearest=d; best=e; } }
            return best;
        }
        void Mouse()
        {
            var e=Event.current; Vector2 m=e.mousePosition;
            bool inside=m.x>=Left && m.x<=Left+Width;
            if(e.type==EventType.ScrollWheel && m.y>=OverviewY && m.y<=StageY+StageHeight)
            {
                if(e.control) viewStart+=e.delta.y*viewSpan*.05f;
                else
                {
                    double anchor=Time(m.x);
                    viewSpan=Math.Max(1.5,Math.Min(clip.length,viewSpan*(e.delta.y>0?1.18:.85)));
                    viewStart=anchor-(m.x-Left)/Width*viewSpan;
                }
                viewStart=Math.Max(0,Math.Min(viewStart,Math.Max(0,clip.length-viewSpan)));
                e.Use();
            }
            if(e.type==EventType.MouseDown && inside && m.y>=OverviewY && m.y<=StageY+StageHeight)
            {
                GUIUtility.keyboardControl=0;
                if(m.y<WaveY) { drag=Drag.Overview; Seek((m.x-Left)/Width*clip.length); }
                else if(m.y<LaserY) { drag=Drag.Scrub; Seek(Time(m.x)); }
                else if(e.button==1) Delete(m);
                else Press(m,e.shift);
                e.Use();
            }
            if(e.type==EventType.MouseDrag && drag!=Drag.None)
            {
                if(drag==Drag.Overview) Seek(Mathf.Clamp01((m.x-Left)/Width)*clip.length);
                else if(drag==Drag.Scrub) Seek(Time(m.x));
                else if(drag==Drag.Notes || drag==Drag.Event) Move(m);
                e.Use();
            }
            if(e.type==EventType.MouseUp && drag!=Drag.None)
            {
                if(drag==Drag.Box) Release(m,e.shift);
                drag=Drag.None; dragTimes=null; if(dragMoved) mapDirty=true; dragMoved=false;
                e.Use();
            }
        }
        void Delete(Vector2 m)
        {
            var note=NoteAt(m); var cue=EventAt(m); if(note==null && cue==null) return;
            Snapshot();
            if(note!=null) { Mod.Notes.Remove(note); selected.Remove(note); }
            if(cue!=null) { Mod.Events.Remove(cue); if(selectedEvent==cue) selectedEvent=null; }
            mapDirty=true;
        }
        void Press(Vector2 m,bool shift)
        {
            var note=NoteAt(m); var cue=EventAt(m); dragX=m.x; dragOrigin=Time(m.x); dragMoved=false;
            if(note!=null)
            {
                if(!selected.Contains(note)) { if(!shift) selected.Clear(); selected.Add(note); }
                else if(shift) { selected.Remove(note); return; }
                selectedEvent=null; drag=Drag.Notes;
                dragTimes=new Dictionary<RhythmNote,float>(); foreach(var n in selected) dragTimes[n]=n.Timestamp;
            }
            else if(cue!=null)
            {
                selected.Clear(); selectedEvent=cue; drag=Drag.Event; dragEventTime=cue.Time;
            }
            else drag=Drag.Box;
        }
        void Move(Vector2 m)
        {
            if(Mathf.Abs(m.x-dragX)<2 && !dragMoved) return;
            if(!dragMoved) { Snapshot(); dragMoved=true; }
            float delta=(float)(Time(m.x)-dragOrigin);
            if(drag==Drag.Notes) foreach(var pair in dragTimes) pair.Key.Timestamp=(float)Snap(Math.Max(0,pair.Value+delta));
            else if(selectedEvent!=null) selectedEvent.Time=(float)Snap(Math.Max(0,dragEventTime+delta));
        }
        void Release(Vector2 m,bool shift)
        {
            // A click without travel on an empty lane just moves the playhead.
            if(Mathf.Abs(m.x-dragX)<4) { selected.Clear(); selectedEvent=null; Seek(Time(m.x)); return; }
            double a=Time(Mathf.Min(dragX,m.x)),b=Time(Mathf.Max(dragX,m.x));
            if(!shift) selected.Clear();
            selectedEvent=null;
            foreach(var n in Mod.Notes) if(n.Timestamp>=a && n.Timestamp<=b && !selected.Contains(n)) selected.Add(n);
            Status=$"{selected.Count} not seçildi.";
        }
    }
}
