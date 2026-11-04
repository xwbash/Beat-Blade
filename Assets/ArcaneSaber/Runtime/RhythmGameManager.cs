using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ArcaneSaber
{
    public sealed class RhythmGameManager : MonoBehaviour
    {
        public GameFeelSettings Settings;
        public BeatmapController CustomBeatmap;
        public AudioClip MusicTrack;
        MusicConductor music; MagicHandController magic; SaberSpringController saber;
        ProceduralCameraController cameraMotion; CubeDestructionController destruction;
        NoteSpawner notes; NoteHitEvaluator evaluator; StageDirector stage; MapEditor editor;
        ScoreManager score=new ScoreManager(); BeatmapController map; AudioClip generated;
        Transform world, rig; Camera view;
        float generatedBpm=112; int generatedBeats=200;
        bool packagedSong;
        bool started, finished, tuning; int difficulty=1; float latency;
        GUIStyle title, subtitle, small, body, huge, button, tiny, accent, leftButton;
        internal GUIStyle Body=>body; internal GUIStyle Small=>small; internal GUIStyle Subtitle=>subtitle; internal GUIStyle ButtonStyle=>button;
        internal GUIStyle Tiny=>tiny; internal GUIStyle Accent=>accent; internal GUIStyle LeftButton=>leftButton;
        internal float LatencySeconds=>latency*.001f;
        Vector2 settingsScroll,modScroll; double calibrationStart; readonly List<float> taps=new List<float>(); bool calibrating;
        AudioSource metronome; AudioClip clickTrack;
        // Mod session: what is loaded, plus where a section test should return to.
        ModMap activeMod; AudioClip activeClip; SongAnalysis activeSong; bool testing; double testPoint;
        bool browsing,loading; string notice=""; List<ModMap> mods=new List<ModMap>(); string confirmDelete; Action pending;
        readonly BeatmapController[] builtInCharts=new BeatmapController[3]; List<StageEvent> builtInStage; float judgmentAge;
        Texture2D logo; bool logoChecked; SongAnalysis builtInSong; System.Threading.Tasks.Task<SongAnalysis> builtInWork; HitSounds hits;
        internal HitSounds Hits=>hits;
        // Samples are read on the main thread; the spectral analysis runs on a worker.
        static IEnumerator Analyze(AudioClip clip,Action<SongAnalysis,string> done)
        {
            float[] mono; int rate=clip.frequency;
            try { mono=SongAnalysis.ReadMono(clip); }
            catch(Exception e) { done(null,e.Message); yield break; }
            var work=System.Threading.Tasks.Task.Run(()=>SongAnalysis.Of(mono,rate));
            while(!work.IsCompleted) yield return null;
            if(work.IsFaulted) done(null,work.Exception.GetBaseException().Message); else done(work.Result,null);
        }
        SongAnalysis BuiltInSong()
        {
            if(builtInSong==null) builtInSong=builtInWork!=null?builtInWork.Result:SongAnalysis.Of(MusicTrack);
            return builtInSong;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(FindFirstObjectByType<RhythmGameManager>()) return;
            new GameObject("ARCANE SABER • runtime").AddComponent<RhythmGameManager>();
        }
        void Awake()
        {
            Application.targetFrameRate=144;
            foreach(var root in gameObject.scene.GetRootGameObjects()) if(root!=gameObject) root.SetActive(false);
            Settings=Settings ? Instantiate(Settings) : Resources.Load<GameFeelSettings>("ArcaneGameFeel");
            Settings=Settings ? Instantiate(Settings) : ScriptableObject.CreateInstance<GameFeelSettings>();
            string savedFeel=Path.Combine(Application.persistentDataPath,"ArcaneGameFeel.json");
            if(File.Exists(savedFeel))
            {
                try { Settings.Restore(File.ReadAllText(savedFeel)); }
                catch(Exception e) { Debug.LogWarning("Could not restore saved game feel: "+e.Message); }
            }
            Settings.ReducedMotion=PlayerPrefs.GetInt("Arcane.ReducedMotion",0)==1;
            latency=PlayerPrefs.GetFloat("Arcane.Latency",0);
            foreach(var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
            world=ArcaneVisuals.Empty("Arcane arena",transform,Vector3.zero);
            rig=ArcaneVisuals.Empty("Player • stationary",transform,new Vector3(0,1.6f,0));
            view=ArcaneVisuals.Empty("First person camera",rig,Vector3.zero).gameObject.AddComponent<Camera>();
            view.tag="MainCamera"; view.fieldOfView=ProceduralCameraController.BaseFieldOfView; view.nearClipPlane=.04f; view.farClipPlane=110;
            view.clearFlags=CameraClearFlags.SolidColor; view.backgroundColor=new Color(.006f,.008f,.02f);
            view.gameObject.AddComponent<AudioListener>();
            var hd=view.gameObject.AddComponent<HDAdditionalCameraData>(); hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;
            hd.backgroundColorHDR=new Color(.006f,.008f,.02f);
            hd.volumeLayerMask=~0;
            hd.antialiasing=HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            magic=ArcaneVisuals.Empty("Left hand system",rig,Vector3.zero).gameObject.AddComponent<MagicHandController>(); magic.Initialize(Settings);
            saber=ArcaneVisuals.Empty("Right hand system",rig,Vector3.zero).gameObject.AddComponent<SaberSpringController>(); saber.Initialize(Settings);
            music=gameObject.AddComponent<MusicConductor>();
            var magicVfx=ArcaneVisuals.Empty("Arcane energy",transform,Vector3.zero).gameObject.AddComponent<MagicVFXController>();
            magicVfx.Initialize(magic,Settings,music);
            cameraMotion=view.gameObject.AddComponent<ProceduralCameraController>();
            cameraMotion.Settings=Settings; cameraMotion.Magic=magic; cameraMotion.Saber=saber; cameraMotion.Music=music;
            destruction=ArcaneVisuals.Empty("Pooled effects",transform,Vector3.zero).gameObject.AddComponent<CubeDestructionController>(); destruction.Initialize();
            notes=new NoteSpawner(ArcaneVisuals.Empty("Note pool",transform,Vector3.zero));
            stage=ArcaneVisuals.Empty("Reactive stage",world,Vector3.zero).gameObject.AddComponent<StageDirector>(); stage.Initialize(music,Settings,view.transform);
            stage.Camera=cameraMotion;
            BindEvaluator(); magic.Pressed+=OnMagic;
            saber.AssistRequested=s=>evaluator.GuideSaber(s,music.SongTime);
            BuildArena(); generated=MusicConductor.Synthesize(112);
            if(!MusicTrack && !CustomBeatmap) { MusicTrack=Resources.Load<AudioClip>("BellTolls/Track"); packagedSong=MusicTrack!=null; }
            if(!MusicTrack) MusicTrack=Resources.Load<AudioClip>("Music");
            hits=gameObject.AddComponent<HitSounds>(); hits.Initialize(Settings);
            if(MusicTrack)
            {
                // Warm up the built-in song's analysis while the player is still in the menu.
                try { var mono=SongAnalysis.ReadMono(MusicTrack); int rate=MusicTrack.frequency; builtInWork=System.Threading.Tasks.Task.Run(()=>SongAnalysis.Of(mono,rate)); }
                catch(Exception e) { Debug.LogWarning("Music analysis unavailable: "+e.Message); }
            }
            editor=new MapEditor(this,music,notes,stage){Test=BeginTest,Exit=LeaveEditor};
            Unlock();
        }
        void BindEvaluator()
        {
            evaluator=new NoteHitEvaluator(notes,score);
            evaluator.AssistSettings=Settings;
            evaluator.Hit+=(n,error,direction)=>
            {
                bool isMagic=n.Data.Lane==0, perfect=Mathf.Abs(error)<.055f;
                Vector3 pos=n.Root.position;
                if(isMagic) destruction.PulseTo(magic.Palm,pos);
                if(!isMagic) saber.Impact(direction,perfect);
                destruction.DestroyCube(pos,isMagic,direction,perfect); cameraMotion.Impact(direction);
                hits.Play(isMagic,perfect,(float)(n.Data.Timestamp-(music.SongTime-latency*.001f)),isMagic?-.35f:.35f);
                stage.Hit(isMagic,perfect); stage.SetCombo(score.Combo); judgmentAge=0;
            };
            evaluator.Missed+=n=>{ stage.Miss(); cameraMotion.Kick(.35f); judgmentAge=0; };
        }
        void OnMagic(int direction)
        {
            if(!started || music.Paused || finished || tuning) return;
            evaluator.Magic(direction,music.SongTime);
        }
        BeatmapController BuiltInMap()
        {
            if(CustomBeatmap) return Instantiate(CustomBeatmap);
            if(packagedSong) return BeatSaverImport.Packaged(MusicTrack);
            if(!MusicTrack) return BeatmapController.Demo(difficulty);
            // Notes sit on detected beats that really contain a hit; each difficulty is built once.
            if(!builtInCharts[difficulty])
            {
                var song=BuiltInSong(); var chart=new ModMap{BPM=song.BPM,BeatOffset=song.Phase,NoteSpeed=9};
                MapAutomation.Mark(chart,song,new[]{.2f,.45f,.75f}[difficulty],0); MapAutomation.Assign(chart);
                chart.Notes.RemoveAll(n=>n.Timestamp<2.5f);
                builtInCharts[difficulty]=chart.ToBeatmap(MusicTrack);
            }
            return Instantiate(builtInCharts[difficulty]);
        }
        void StartSession() => Launch(0);
        void Launch(double from)
        {
            notes.Reset(); score=new ScoreManager(); BindEvaluator();
            if(map && map!=CustomBeatmap) Destroy(map);
            if(activeMod!=null)
            {
                map=activeMod.ToBeatmap(activeClip);
                stage.Load(activeMod.Events,activeMod.LightPower,activeMod.CameraPower,activeMod.BloomPower,activeSong);
            }
            else
            {
                map=BuiltInMap(); if(!packagedSong && !CustomBeatmap) MusicChartBuilder.HorizontalFlow(map);
                if(builtInStage==null && MusicTrack && !CustomBeatmap)
                {
                    var analysis=BuiltInSong(); var scored=new ModMap{BPM=analysis.BPM,BeatOffset=analysis.Phase};
                    MapAutomation.Stage(scored,analysis); builtInStage=scored.Events;
                }
                stage.Load(builtInStage,1,1,1,MusicTrack && !CustomBeatmap?BuiltInSong():null);
            }
            if(map.BPM<=0 || float.IsNaN(map.BPM) || float.IsInfinity(map.BPM)) throw new InvalidOperationException("Beatmap BPM must be finite and positive.");
            map.Notes.Sort((a,b)=>a.Timestamp.CompareTo(b.Timestamp));
            // Invalid authoring data is rejected before it can reach glyph or timing code.
            foreach(var n in map.Notes)
                if(n.Lane<0 || n.Lane>1 || n.Direction<0 || n.Direction>(n.Lane==0?3:8) || n.TravelSpeed<=0 || n.HitWindow<=0 || float.IsNaN(n.Timestamp))
                    throw new InvalidOperationException("Beatmap contains invalid lane, direction, timing window, or speed.");
            int requiredBeats=map.Notes.Count==0?16:Mathf.CeilToInt((map.Notes[map.Notes.Count-1].Timestamp+2)*map.BPM/60)+4;
            if(!map.Music && (Mathf.Abs(generatedBpm-map.BPM)>.001f || generatedBeats<requiredBeats))
            {
                if(generated) Destroy(generated); generatedBpm=map.BPM; generatedBeats=Mathf.Max(200,requiredBeats);
                generated=MusicConductor.Synthesize(generatedBpm,generatedBeats);
            }
            notes.SkipTo(map,from); music.Begin(map,generated,from); started=true; finished=false; tuning=false; browsing=false; Lock();
        }
        // Tests lead in a moment before the playhead, so the section starts in rhythm.
        void BeginTest(double time)
        {
            testPoint=time; testing=true; editor.Suspend();
            Launch(Math.Max(0,time-1.5));
        }
        void EndTest()
        {
            testing=false; started=false; finished=false; tuning=false; notes.Reset(); music.Stop(); Unlock();
            editor.Resume(testPoint);
        }
        void LeaveEditor()
        {
            editor.Close(); ReleaseMod(); started=false; browsing=true; RefreshMods(); Unlock();
        }
        void ReleaseMod() { if(activeClip) Destroy(activeClip); activeClip=null; activeSong=null; activeMod=null; }
        void RefreshMods() { mods=ModLibrary.List(); confirmDelete=null; }
        void OpenMod(ModMap mod,bool edit)
        {
            ReleaseMod(); loading=true; notice="Ses yükleniyor...";
            StartCoroutine(AudioLoader.Load(ModLibrary.AudioPath(mod),(clip,error)=>
            {
                if(!clip) { loading=false; notice="Ses yüklenemedi: "+error; return; }
                activeMod=mod; activeClip=clip; notice="Müzik analiz ediliyor...";
                StartCoroutine(Analyze(clip,(song,failure)=>
                {
                    loading=false; notice="";
                    if(song==null) { notice="Müzik analiz edilemedi: "+failure; ReleaseMod(); return; }
                    activeSong=song;
                    try
                    {
                        if(edit) { editor.Open(mod,clip,activeSong); browsing=false; }
                        else
                        {
                            if(mod.Notes.Count==mod.UndecidedCount) { notice="Bu modda oynanacak not yok; önce editörde not koy."; ReleaseMod(); return; }
                            Launch(0);
                        }
                    }
                    catch(Exception e) { Debug.LogException(e); notice="Mod açılamadı: "+e.Message; ReleaseMod(); }
                }));
            }));
        }
        void NewMod()
        {
            string path=NativeFileDialog.Open("Müzik seç",NativeFileDialog.AudioFilter); if(string.IsNullOrEmpty(path)) return;
            Guarded(()=>OpenMod(ModLibrary.Create(path),true));
        }
        void ImportMod()
        {
            string path=NativeFileDialog.Open("Mod veya BeatSaver haritası içe aktar",NativeFileDialog.ImportFilter); if(string.IsNullOrEmpty(path)) return;
            Guarded(()=>{ var imported=ModLibrary.Import(path); RefreshMods(); notice=imported.Count==1?$"\"{imported[0].Title}\" içe aktarıldı.":$"{imported.Count} zorluk içe aktarıldı: {imported[0].Title}"; });
        }
        void ExportMod(ModMap mod)
        {
            string path=NativeFileDialog.Save("Modu dışa aktar",NativeFileDialog.MapFilter,ModLibrary.Extension,ModLibrary.FileSafe(mod.Title)+"."+ModLibrary.Extension);
            if(string.IsNullOrEmpty(path)) return;
            Guarded(()=>{ ModLibrary.Export(mod,path); notice="Dışa aktarıldı: "+path; });
        }
        void Guarded(Action action)
        {
            try { action(); }
            catch(Exception e) { Debug.LogException(e); notice=e.Message; }
        }
        void Update()
        {
            // Native dialogs block the thread, so they run here rather than inside OnGUI.
            if(pending!=null) { var action=pending; pending=null; action(); }
            var k=Keyboard.current;
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            if(editor.Active)
            {
                editor.Tick(k); magic.ReadInput(false); magic.Animate(dt); saber.Animate(dt,false); cameraMotion.Animate(dt);
                return;
            }
            if(k!=null)
            {
                if(k.enterKey.wasPressedThisFrame && !browsing && (!started || finished)) StartSession();
                if(k.escapeKey.wasPressedThisFrame && testing) { EndTest(); return; }
                if(k.escapeKey.wasPressedThisFrame && browsing && !loading) browsing=false;
                if(k.escapeKey.wasPressedThisFrame && started && !finished)
                { tuning=false; music.TogglePause(); if(music.Paused) Unlock(); else Lock(); }
                if(k.f1Key.wasPressedThisFrame && started && !finished)
                { tuning=!tuning; if(tuning && !music.Paused) music.TogglePause(); if(tuning) Unlock(); else { if(music.Paused) music.TogglePause(); Lock(); } }
                if(k.rKey.wasPressedThisFrame && started && !tuning) Launch(testing?Math.Max(0,testPoint-1.5):0);
                if(calibrating && k.spaceKey.wasPressedThisFrame)
                {
                    float now=(float)(AudioSettings.dspTime-calibrationStart);
                    float beat=60f/112; if(now>beat*2) taps.Add((now/beat-Mathf.Round(now/beat))*beat*1000);
                    if(taps.Count>=12) { taps.Sort(); latency=Mathf.Clamp(taps[taps.Count/2],-200,200); calibrating=false; metronome.Stop(); PlayerPrefs.SetFloat("Arcane.Latency",latency); }
                }
            }
            if(calibrating && !tuning) { calibrating=false; if(metronome) metronome.Stop(); }
            bool active=started && !music.Paused && !finished;
            if(active)
            {
                evaluator.InputLatencyMilliseconds=latency;
                notes.Update(map,music.SongTime-latency*.001f);
                evaluator.BufferedMagic(music.SongTime);
            }
            // Judgment occurs in the same Update as the input press, before visual animation.
            magic.ReadInput(active || tuning); magic.Animate(dt); saber.Animate(dt,active);
            if(active)
            {
                evaluator.Saber(saber,music.SongTime); evaluator.Expire(music.SongTime);
                float end=map.Notes.Count>0?map.Notes[map.Notes.Count-1].Timestamp+2:2;
                if(music.SongTime>end)
                {
                    if(testing) EndTest();
                    else { finished=true; music.Stop(); Unlock(); }
                }
            }
            judgmentAge+=dt;
            cameraMotion.Animate(dt);
        }
        static void Lock() { Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false; }
        static void Unlock() { Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
        void OnDisable() { Unlock(); }
        void OnApplicationQuit() { if(editor!=null && editor.Active) editor.Save(); }
        void OnDestroy() { if(generated) Destroy(generated); if(clickTrack) Destroy(clickTrack); if(activeClip) Destroy(activeClip); }
        void StartCalibration()
        {
            if(!metronome) metronome=gameObject.AddComponent<AudioSource>();
            if(!clickTrack)
            {
                const int rate=24000; int length=Mathf.RoundToInt(60f/112*rate); var pcm=new float[length];
                for(int i=0;i<Mathf.Min(1600,length);i++) { float t=(float)i/rate; pcm[i]=Mathf.Sin(t*2*Mathf.PI*1200)*Mathf.Exp(-t*95)*.4f; }
                clickTrack=AudioClip.Create("Calibration click • 112 BPM",length,1,rate,false); clickTrack.SetData(pcm,0);
            }
            taps.Clear(); calibrating=true; metronome.clip=clickTrack; metronome.loop=true;
            calibrationStart=AudioSettings.dspTime+.2; metronome.PlayScheduled(calibrationStart);
        }

        void BuildArena()
        {
            var trim=ArcaneVisuals.Material(new Color(.055f,.065f,.10f));
            var floor=ArcaneVisuals.Surface(new Color(.044f,.057f,.085f),.48f);
            ArcaneVisuals.Shape("Stage",PrimitiveType.Cube,world,new Vector3(0,-.18f,36),new Vector3(26,.3f,80),floor);
            var keyLight=ArcaneVisuals.Empty("Soft overhead key",world,new Vector3(0,4,0)).gameObject.AddComponent<Light>();
            keyLight.type=LightType.Directional; keyLight.transform.rotation=Quaternion.Euler(46,-32,0);
            keyLight.color=new Color(.70f,.79f,1); keyLight.shadows=LightShadows.None;
            keyLight.gameObject.AddComponent<HDAdditionalLightData>(); keyLight.lightUnit=LightUnit.Lux; keyLight.intensity=4;
            for(int side=-1;side<=1;side+=2)
            {
                var fill=ArcaneVisuals.Empty("Chromatic fill",world,new Vector3(side*1.2f,1.9f,.7f)).gameObject.AddComponent<Light>();
                fill.type=LightType.Point; fill.color=side<0?ArcaneVisuals.Crimson:ArcaneVisuals.Cyan; fill.range=5;
                fill.gameObject.AddComponent<HDAdditionalLightData>(); fill.lightUnit=LightUnit.Lumen; fill.intensity=12;
            }
            for(int i=0;i<18;i++)
            {
                float z=i*4+3;
                for(int s=-1;s<=1;s+=2)
                {
                    ArcaneVisuals.Shape("Obsidian buttress",PrimitiveType.Cube,world,new Vector3(s*5.4f,2.5f,z),new Vector3(.5f,5,.55f),trim).localRotation=Quaternion.Euler(0,0,-s*8);
                    var line=ArcaneVisuals.Line("Runway",world,s<0?ArcaneVisuals.Crimson*.55f:ArcaneVisuals.Cyan*.55f,.022f,2);
                    line.SetPositions(new[]{new Vector3(s*1.5f,.015f,z),new Vector3(s*1.5f,.015f,z+3.2f)});
                    var shard=ArcaneVisuals.Shape("Floating prism",PrimitiveType.Cube,world,new Vector3(s*3.3f,2+(i%3)*.6f,z),Vector3.one*.20f,ArcaneVisuals.Material(s<0?ArcaneVisuals.Crimson:ArcaneVisuals.Cyan,.6f));
                    shard.localRotation=Quaternion.Euler(35,45,15);
                }
                var grid=ArcaneVisuals.Line("Floor grid",world,new Color(.055f,.08f,.12f),.008f,2);
                grid.SetPositions(new[]{new Vector3(-5,.01f,z),new Vector3(5,.01f,z)});
            }
            for(int s=-1;s<=1;s+=2)
            {
                var target=ArcaneVisuals.Line("Timing gate",world,s<0?ArcaneVisuals.Crimson:ArcaneVisuals.Cyan,.009f,5);
                float x=s<0?-.70f:.65f;
                float bottom=NoteSpawner.HitY-.32f,top=NoteSpawner.HitY+.32f;
                target.SetPositions(new[]{new Vector3(x-.32f,bottom,1.2f),new Vector3(x+.32f,bottom,1.2f),new Vector3(x+.32f,top,1.2f),new Vector3(x-.32f,top,1.2f),new Vector3(x-.32f,bottom,1.2f)});
            }
            var volume=ArcaneVisuals.Empty("Neon post processing",world,Vector3.zero).gameObject.AddComponent<Volume>();
            volume.isGlobal=true; volume.priority=100; volume.profile=ScriptableObject.CreateInstance<VolumeProfile>();
            var environment=volume.profile.Add<VisualEnvironment>(true); environment.skyType.Override(0);
            var indirect=volume.profile.Add<IndirectLightingController>(true); indirect.indirectDiffuseLightingMultiplier.Override(0);
            var exposure=volume.profile.Add<Exposure>(true); exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(0);
            cameraMotion.Lens=volume.profile.Add<LensDistortion>(true);
            cameraMotion.Lens.intensity.Override(-Settings.BodycamLens); cameraMotion.Lens.scale.Override(1.035f);
            var blur=volume.profile.Add<MotionBlur>(true); blur.intensity.Override(0);
            var bloom=volume.profile.Add<Bloom>(true); bloom.intensity.Override(.4f); bloom.threshold.Override(1.25f); bloom.scatter.Override(.6f);
            var vignette=volume.profile.Add<Vignette>(true); vignette.intensity.Override(.3f);
            var chroma=volume.profile.Add<ChromaticAberration>(true); chroma.intensity.Override(.05f);
            var tone=volume.profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var fog=volume.profile.Add<Fog>(true); fog.enabled.Override(true); fog.colorMode.Override(FogColorMode.ConstantColor);
            fog.color.Override(new Color(.018f,.025f,.055f)); fog.meanFreePath.Override(90);
            fog.baseHeight.Override(-1); fog.maximumHeight.Override(9); fog.enableVolumetricFog.Override(true);
            fog.albedo.Override(new Color(.10f,.14f,.22f)); fog.globalLightProbeDimmer.Override(0);
            stage.BloomEffect=bloom; stage.Chroma=chroma; stage.VignetteEffect=vignette; stage.FogEffect=fog;
        }

        void Styles()
        {
            if(title!=null) return;
            title=new GUIStyle(GUI.skin.label){fontSize=58,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter}; title.normal.textColor=Color.white;
            subtitle=new GUIStyle(title){fontSize=13,fontStyle=FontStyle.Normal}; subtitle.normal.textColor=new Color(.55f,.67f,.8f);
            body=new GUIStyle(title){fontSize=20};
            small=new GUIStyle(subtitle){fontSize=12,alignment=TextAnchor.MiddleLeft};
            huge=new GUIStyle(title){fontSize=45};
            tiny=new GUIStyle(subtitle){fontSize=10,fontStyle=FontStyle.Bold}; tiny.normal.textColor=Color.white;
            accent=new GUIStyle(small){fontStyle=FontStyle.Bold}; accent.normal.textColor=ArcaneVisuals.Cyan;
            button=new GUIStyle(GUI.skin.button){fontSize=15,fontStyle=FontStyle.Bold}; button.normal.textColor=Color.white;
            leftButton=new GUIStyle(GUI.skin.button){alignment=TextAnchor.MiddleLeft};
        }
        internal void Label(float x,float y,float w,float h,string text,GUIStyle style,Color? color=null)
        { Color old=GUI.color; if(color.HasValue) GUI.color=color.Value; GUI.Label(new Rect(x,y,w,h),text,style); GUI.color=old; }
        internal void Panel(Rect r,Color c) { Color old=GUI.color; GUI.color=c; GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=old; }
        void OnGUI()
        {
            Styles(); GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1440f,Screen.height/900f,1));
            if(stage.Overlay.a>.004f) Panel(new Rect(0,0,1440,900),stage.Overlay);
            if(editor.Active) { editor.Draw(); return; }
            Panel(new Rect(36,30,3,40),ArcaneVisuals.Crimson);
            Label(52,26,400,24,"A R C A N E  /  S A B E R",body);
            Label(52,56,400,20,testing?"BÖLÜM TESTİ":activeMod!=null?activeMod.Title:"DUAL-HAND RHYTHM     /     AUDIO REACTIVE",small);
            if(browsing) { ModBrowser(); return; }
            if(!started || finished) { Menu(); return; }
            Label(1060,28,320,35,score.Score.ToString("N0"),body);
            Label(1060,65,320,20,$"ACCURACY {score.Accuracy:0.0}%     /     BEST {score.BestCombo}",subtitle);
            float end=map.Notes.Count>0?map.Notes[map.Notes.Count-1].Timestamp:1;
            Panel(new Rect(40,100,1360,2),new Color(.08f,.13f,.19f));
            Panel(new Rect(40,100,1360*Mathf.Clamp01((float)music.SongTime/end),2),ArcaneVisuals.Cyan);
            Panel(new Rect(40,104,1360*stage.Heat,2),Color.Lerp(ArcaneVisuals.Crimson,Color.white,stage.Heat));
            // Combo and judgment pop on each hit, then settle.
            float pop=Mathf.Exp(-judgmentAge*11);
            if(score.Combo>0)
            {
                huge.fontSize=Mathf.RoundToInt(45+pop*16+stage.Heat*14);
                Label(480,142,480,76,score.Combo.ToString(),huge); Label(580,214,280,25,$"C O M B O     x{1+Mathf.Min(score.Combo/8,7)}",subtitle);
            }
            if(Time.unscaledTime-score.LastJudgment<.65f)
            {
                body.fontSize=Mathf.RoundToInt(20+pop*14);
                bool perfect=score.Judgment=="PERFECT";
                Label(420,272,600,50,score.Judgment,body,score.Judgment=="MISS"?ArcaneVisuals.Crimson:perfect?Color.white:ArcaneVisuals.Cyan);
                body.fontSize=20;
            }
            Label(40,836,600,25,"W A S D   /   CONDUCT MAGIC",small,ArcaneVisuals.Crimson);
            Label(1000,836,400,25,"MOUSE   /   LEFT - RIGHT",small,ArcaneVisuals.Cyan);
            Label(450,865,540,20,testing?"ESC  EDİTÖRE DÖN       R  BÖLÜMÜ TEKRARLA":"ESC  PAUSE       F1  FEEL LAB       R  RESTART",subtitle);
            if(music.Paused)
            {
                Panel(new Rect(0,0,1440,900),new Color(.004f,.008f,.018f,.94f));
                if(tuning) FeelLab();
                else
                {
                    Label(400,270,640,80,"TAKE A BREATH",title);
                    Label(440,366,560,35,"YOUR SESSION IS PAUSED",subtitle);
                    if(GUI.Button(new Rect(570,440,300,56),"RESUME  /  ESC",button)) { music.TogglePause(); Lock(); }
                    if(GUI.Button(new Rect(570,512,300,48),"OPEN FEEL LAB",button)) tuning=true;
                    if(GUI.Button(new Rect(570,574,300,48),"MENÜYE DÖN",button)) { music.Stop(); notes.Reset(); started=false; browsing=activeMod!=null; ReleaseMod(); RefreshMods(); }
                }
            }
        }
        void Menu()
        {
            Panel(new Rect(270,170,900,620),new Color(.008f,.012f,.03f,.72f));
            Label(370,200,700,35,finished?"S E S S I O N  C O M P L E T E":"T W O  H A N D S .   O N E  F L O W .",subtitle,ArcaneVisuals.Cyan);
            if(!logoChecked) { logo=ArcaneArt.Texture("Logo"); logoChecked=true; }
            if(logo) GUI.DrawTexture(new Rect(420,246,600,104),logo,ScaleMode.ScaleToFit); else Label(300,252,840,90,"ARCANE SABER",title);
            Label(340,350,760,40,finished?$"{score.Score:N0} POINTS    /    {score.Accuracy:0.0}%    /    BEST COMBO {score.BestCombo}":"Conduct the crimson. Carve the cyan.",body);
            Label(365,420,710,80,"W A S D  —  match the letters as they cross the gate.\nMOUSE  —  alternate left and right through the cubes.",subtitle);
            if(packagedSong) Label(440,520,560,42,"BEATSAVER  /  EXPERT STANDARD",body);
            else difficulty=GUI.SelectionGrid(new Rect(440,520,560,42),difficulty,new[]{"INITIATE","ADEPT","VIRTUOSO"},3,button);
            if(GUI.Button(new Rect(500,580,440,60),finished?"PLAY AGAIN  /  ENTER":"ENTER THE FLOW  /  ENTER",button)) { if(!finished) ReleaseMod(); StartSession(); }
            if(GUI.Button(new Rect(500,652,440,52),"MODLAR  /  EDİTÖR",button)) { if(finished) ReleaseMod(); finished=false; started=false; browsing=true; RefreshMods(); }
            Label(370,720,700,35,"F1  live spring tuning     •     ESC  pause     •     R  restart",subtitle);
        }
        void ModBrowser()
        {
            Panel(new Rect(170,90,1100,740),new Color(.008f,.012f,.03f,.82f));
            Label(200,104,1040,60,"MODLAR",title);
            Label(200,168,1040,24,"Müziğini yükle, notlarını koy, oyna, paylaş.",subtitle,ArcaneVisuals.Cyan);
            GUI.enabled=!loading;
            if(GUI.Button(new Rect(210,204,330,50),"+  YENİ MOD  /  MÜZİK SEÇ",button)) pending=NewMod;
            if(GUI.Button(new Rect(552,204,230,50),"İÇE AKTAR",button)) pending=ImportMod;
            if(GUI.Button(new Rect(794,204,216,50),"KLASÖRÜ AÇ",button)) Application.OpenURL(new Uri(ModLibrary.Root+Path.DirectorySeparatorChar).AbsoluteUri);
            if(GUI.Button(new Rect(1022,204,206,50),"GERİ  /  ESC",button)) browsing=false;
            if(!NativeFileDialog.Supported) Label(210,258,1020,20,"Bu platformda dosya penceresi yok; modları Mods klasörüne elle kopyala.",subtitle);
            Rect area=new Rect(210,284,1020,480);
            if(mods.Count==0) Label(210,420,1020,40,"Henüz mod yok. YENİ MOD ile bir müzik seç.",body);
            modScroll=GUI.BeginScrollView(area,modScroll,new Rect(0,0,1000,Mathf.Max(480,mods.Count*68)));
            for(int i=0;i<mods.Count;i++)
            {
                var mod=mods[i]; float y=i*68;
                Panel(new Rect(0,y,1000,60),new Color(.04f,.06f,.1f,.9f));
                Label(14,y+6,470,28,mod.Title,accent);
                Label(14,y+32,470,20,$"{mod.Notes.Count} not     {mod.Events.Count} sahne olayı     {(mod.UndecidedCount>0?mod.UndecidedCount+" kararsız":"")}",small);
                if(GUI.Button(new Rect(490,y+10,110,40),"OYNA",button)) OpenMod(mod,false);
                if(GUI.Button(new Rect(606,y+10,120,40),"DÜZENLE",button)) OpenMod(mod,true);
                if(GUI.Button(new Rect(732,y+10,120,40),"DIŞA AKTAR",button)) { var chosen=mod; pending=()=>ExportMod(chosen); }
                bool armed=confirmDelete==mod.Id;
                if(GUI.Button(new Rect(858,y+10,130,40),armed?"EMİN MİSİN?":"SİL",button))
                {
                    if(armed) { Guarded(()=>ModLibrary.Delete(mod)); RefreshMods(); GUI.EndScrollView(); GUI.enabled=true; return; }
                    confirmDelete=mod.Id;
                }
            }
            GUI.EndScrollView(); GUI.enabled=true;
            Label(210,770,1020,40,notice,subtitle,Color.white);
        }
        void Slider(string name,ref float value,float min,float max)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(name,GUILayout.Width(370));
            value=GUILayout.HorizontalSlider(value,min,max,GUILayout.Width(470));
            GUILayout.Label(value.ToString("0.00"),GUILayout.Width(80)); GUILayout.EndHorizontal(); GUILayout.Space(9);
        }
        void FeelLab()
        {
            Label(200,62,1040,65,"FEEL LAB",title);
            Label(200,130,1040,35,"LIVE SETTINGS  /  F1 TO RETURN TO YOUR SESSION",subtitle);
            GUILayout.BeginArea(new Rect(220,190,1000,600));
            settingsScroll=GUILayout.BeginScrollView(settingsScroll);
            Settings.ReducedMotion=GUILayout.Toggle(Settings.ReducedMotion,"Reduced camera motion");
            Slider("Stage brightness",ref Settings.StageBrightness,0,1.5f);
            Slider("Hit sound volume",ref Settings.HitSoundVolume,0,1);
            Slider("Bodycam lens",ref Settings.BodycamLens,0,.35f);
            Slider("Mouse camera motion",ref Settings.MouseCameraMotion,0,2);
            Slider("Saber flow assistance",ref Settings.SaberAssist,0,1);
            Slider("Magic timing assistance",ref Settings.MagicTimingAssist,0,1);
            Slider("Input latency compensation (ms)",ref latency,-200,200);
            if(GUILayout.Button(calibrating?$"TAP SPACE TO THE CLICK  ({taps.Count}/12)  /  CANCEL":"CALIBRATE LATENCY  /  TAP TEST"))
            { if(calibrating) { calibrating=false; metronome.Stop(); } else StartCalibration(); }
            Slider("Magic position stiffness",ref Settings.MagicHandPositionStiffness,20,1400);
            Slider("Magic position damping",ref Settings.MagicHandPositionDamping,.1f,2);
            Slider("Magic rotation stiffness",ref Settings.MagicHandRotationStiffness,20,1400);
            Slider("Magic rotation damping",ref Settings.MagicHandRotationDamping,.1f,2);
            Slider("Magic arc intensity",ref Settings.MagicGestureArcIntensity,0,1);
            Slider("Magic gesture distance",ref Settings.MagicGestureDistance,.1f,.7f);
            Slider("Magic momentum impulse",ref Settings.MagicGestureOvershoot,0,2);
            Slider("Magic finger curl",ref Settings.MagicFingerCurlIntensity,0,2);
            Slider("Magic finger release speed",ref Settings.MagicFingerReleaseSpeed,3,35);
            Slider("Magic trail intensity",ref Settings.MagicTrailIntensity,0,2);
            Slider("Magic idle sway",ref Settings.MagicIdleSway,0,.06f);
            Slider("Magic recovery",ref Settings.MagicRecoverySpeed,.2f,8);
            Slider("Saber position stiffness",ref Settings.SaberPositionStiffness,20,1400);
            Slider("Saber position damping",ref Settings.SaberPositionDamping,.1f,2);
            Slider("Saber rotation stiffness",ref Settings.SaberRotationStiffness,20,1400);
            Slider("Saber rotation damping",ref Settings.SaberRotationDamping,.1f,2);
            Slider("Saber blade lag",ref Settings.SaberBladeLag,0,2);
            Slider("Saber wrist influence",ref Settings.SaberWristInfluence,0,2);
            Slider("Saber overshoot",ref Settings.SaberOvershoot,0,1);
            Slider("Mouse sensitivity",ref Settings.SaberSwingSensitivity,.1f,3);
            Slider("Saber trail intensity",ref Settings.SaberTrailIntensity,0,2);
            Slider("Magic camera influence",ref Settings.MagicCameraInfluence,0,2);
            Slider("Saber camera influence",ref Settings.SaberCameraInfluence,0,2);
            Slider("Camera position stiffness",ref Settings.CameraPositionStiffness,20,1000);
            Slider("Camera position damping",ref Settings.CameraPositionDamping,.1f,2);
            Slider("Camera rotation stiffness",ref Settings.CameraRotationStiffness,20,1000);
            Slider("Camera rotation damping",ref Settings.CameraRotationDamping,.1f,2);
            Slider("Camera impact",ref Settings.CameraImpactIntensity,0,2);
            Slider("Maximum camera roll",ref Settings.MaximumCameraRoll,0,3);
            Slider("Camera breathing",ref Settings.IdleCameraInfluence,0,2);
            Slider("Camera beat pulse",ref Settings.MusicCameraInfluence,0,2);
            if(GUILayout.Button("SAVE GAME FEEL, ACCESSIBILITY & LATENCY"))
            {
                Settings.FeelVersion=GameFeelSettings.CurrentVersion;
                File.WriteAllText(Path.Combine(Application.persistentDataPath,"ArcaneGameFeel.json"),JsonUtility.ToJson(Settings,true));
                PlayerPrefs.SetFloat("Arcane.Latency",latency); PlayerPrefs.SetInt("Arcane.ReducedMotion",Settings.ReducedMotion?1:0); PlayerPrefs.Save();
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
    }
}
