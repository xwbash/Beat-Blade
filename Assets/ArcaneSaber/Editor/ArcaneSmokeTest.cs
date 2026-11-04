using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArcaneSaber.Editor
{
    // Invoked explicitly in an isolated validation checkout, never on normal editor startup.
    [InitializeOnLoad]
    public static class ArcaneSmokeTest
    {
        const string Flag="Arcane.Smoke";
        static double began;
        static int phase;
        static string error;
        static ArcaneSmokeTest()
        {
            if(SessionState.GetBool(Flag,false)) { EditorApplication.update+=Tick; Application.logMessageReceived+=Log; }
        }
        public static void Begin()
        {
            // A failed check must close this editor, or the copy stays locked.
            try { ArcaneProjectTools.EnsureAssets(); ArcaneProjectTools.OpenScene(); ArcaneProjectTools.Check(); }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
            SessionState.SetBool(Flag,true); EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type)
        { if((type==LogType.Exception || type==LogType.Error || type==LogType.Assert) && !trace.Contains("UnityEditor.Search")) error=message+"\n"+trace; }
        static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                if(began==0) began=EditorApplication.timeSinceStartup;
                var game=UnityEngine.Object.FindFirstObjectByType<RhythmGameManager>();
                if(!game) throw new Exception("Runtime did not create a game manager");
                if(!string.IsNullOrEmpty(error)) throw new Exception(error);
                double elapsed=EditorApplication.timeSinceStartup-began;
                if(phase==0 && elapsed>2)
                {
                    game.SendMessage("StartSession"); phase=1;
                }
                if(phase==1 && elapsed>9)
                {
                    const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    var spawner=(NoteSpawner)typeof(RhythmGameManager).GetField("notes",flags).GetValue(game);
                    var conductor=UnityEngine.Object.FindFirstObjectByType<MusicConductor>();
                    if(!conductor.Playing || conductor.SongTime<4) throw new Exception("DSP playback or note spawning failed");
                    foreach(var n in spawner.Active)
                    {
                        float z=NoteSpawner.HitZ+(n.Data.Timestamp-(float)conductor.SongTime)*n.Data.TravelSpeed;
                        if(n.Data.Timestamp>=conductor.SongTime && n.Approach-(n.Data.Timestamp-conductor.SongTime)>.30 && Mathf.Abs(n.Root.position.z-z)>.5f) throw new Exception("Notes diverged from DSP time");
                        if(n.Data.Timestamp<conductor.SongTime && (n.Root.position.z<.8f || n.Root.position.z>NoteSpawner.HitZ+.01f)) throw new Exception("Late grace note left the playable area");
                    }
                    var magic=UnityEngine.Object.FindFirstObjectByType<MagicHandController>();
                    var saber=UnityEngine.Object.FindFirstObjectByType<SaberSpringController>();
                    if(!magic || !saber || Vector3.Distance(saber.Tip,saber.Base)<.8f) throw new Exception("Procedural hand or blade rig failed");
                    // A deterministic visual sample containing both gestures and incoming notes.
                    var fx=UnityEngine.Object.FindFirstObjectByType<CubeDestructionController>();
                    fx.DestroyCube(new Vector3(10,1.7f,4),false,new Vector2(.6f,.8f),true);
                    fx.PulseTo(magic.Palm,new Vector3(-.7f,NoteSpawner.HitY,2.2f));
                    Capture();
                    var cameraMotion=UnityEngine.Object.FindFirstObjectByType<ProceduralCameraController>();
                    if(cameraMotion.Lens==null || cameraMotion.Lens.intensity.value>=0 || Camera.main.fieldOfView<80) throw new Exception("Bodycam lens setup missing");
                    saber.AssistRequested=null;
                    float sampleTime=Time.unscaledTime;
                    for(int i=0;i<40;i++) { saber.Input.ApplyMouseDelta(Vector2.up*4,game.Settings,1/144f,sampleTime+=1/144f); saber.Animate(1/144f,true,false); cameraMotion.Animate(1/144f); }
                    Capture("Logs/ArcaneUpstroke.png");
                    saber.Impact(Vector2.down,true); cameraMotion.Impact(Vector2.down);
                    for(int i=0;i<70;i++) { saber.Input.ApplyMouseDelta(Vector2.down*4,game.Settings,1/144f,sampleTime+=1/144f); saber.Animate(1/144f,true,false); cameraMotion.Animate(1/144f); }
                    if(cameraMotion.transform.localPosition.magnitude>ProceduralCameraController.PositionLimit+.001f) throw new Exception("Bodycam impact exceeded movement limit");
                    Capture("Logs/ArcaneDownstroke.png");
                    SetPhase(2,elapsed);
                }
                if(phase>=2) ModFlow(game,elapsed);
                if(elapsed>240) throw new Exception("Runtime smoke test timed out");
            }
            catch(Exception e) { Debug.LogException(e); if(mod!=null) ModLibrary.Delete(mod); SessionState.SetBool(Flag,false); EditorApplication.Exit(1); }
        }
        static double phaseStart; static ModMap mod;
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static void SetPhase(int value,double now) { phase=value; phaseStart=now; }
        static object Field(object owner,string name) => owner.GetType().GetField(name,Hidden).GetValue(owner);
        static void Call(object owner,string name,params object[] arguments) => owner.GetType().GetMethod(name,Hidden).Invoke(owner,arguments);
        // Walks the real player path: pick audio, chart it, preview it, test a section, come back.
        static void ModFlow(RhythmGameManager game,double elapsed)
        {
            double wait=elapsed-phaseStart; var editor=(MapEditor)Field(game,"editor");
            var conductor=UnityEngine.Object.FindFirstObjectByType<MusicConductor>();
            if(phase==2)
            {
                conductor.Stop(); ((NoteSpawner)Field(game,"notes")).Reset(); typeof(RhythmGameManager).GetField("started",Hidden).SetValue(game,false);
                mod=ModLibrary.Create(Path.GetFullPath("Assets/ArcaneSaber/Resources/Music.mp3"));
                Call(game,"OpenMod",mod,true); SetPhase(3,elapsed);
            }
            else if(phase==3)
            {
                if((bool)Field(game,"loading")) return;
                if(!editor.Active) throw new Exception("Editor did not open: "+Field(game,"notice"));
                var song=(SongAnalysis)Field(editor,"song");
                mod.BPM=song.BPM; mod.BeatOffset=song.Phase; MapAutomation.Mark(mod,song,.5f,0); MapAutomation.Assign(mod); MapAutomation.Stage(mod,song);
                mod.Events.Add(new StageEvent{Time=30,Kind=(int)StageKind.SpotCamera}); mod.Sort();
                typeof(MapEditor).GetField("mapDirty",Hidden).SetValue(editor,true);
                Call(editor,"Seek",30.0); Call(editor,"Play"); SetPhase(4,elapsed);
            }
            else if(phase==4 && wait>4)
            {
                if(conductor.SongTime<32) throw new Exception("Editor playback did not advance: "+conductor.SongTime);
                Shot("Logs/ArcaneEditor"); Capture("Logs/ArcaneBeamsCamera.png");
                typeof(StageDirector).GetField("spotMode",Hidden).SetValue(UnityEngine.Object.FindFirstObjectByType<StageDirector>(),1);
                SetPhase(20,elapsed);
            }
            else if(phase==20 && wait>1.5)
            {
                // Worst case for glare: every beam on the lens with a full flash and kick.
                var lights=UnityEngine.Object.FindFirstObjectByType<StageDirector>();
                typeof(StageDirector).GetField("spotMode",Hidden).SetValue(lights,1);
                typeof(StageDirector).GetField("flash",Hidden).SetValue(lights,1f); typeof(StageDirector).GetField("kick",Hidden).SetValue(lights,1f);
                SetPhase(21,elapsed);
            }
            else if(phase==21)
            {
                Capture("Logs/ArcanePeak.png"); SetPhase(5,elapsed);
            }
            else if(phase==5 && wait>1)
            {
                Call(editor,"Pause"); testPoint=editor.Head; Call(game,"BeginTest",testPoint); SetPhase(6,elapsed);
            }
            else if(phase==6 && wait>5)
            {
                if(editor.Active || !(bool)Field(game,"testing") || conductor.SongTime<testPoint+1) throw new Exception("Section test did not run at "+conductor.SongTime);
                if(((NoteSpawner)Field(game,"notes")).Active.Count==0) throw new Exception("Section test spawned no notes");
                Shot("Logs/ArcaneTest"); Capture("Logs/ArcaneGameplayBeams.png");
                {
                    // Close look at the nearest note, where surface artefacts show.
                    NoteView nearest=null; foreach(var n in ((NoteSpawner)Field(game,"notes")).Active) if(nearest==null || n.Root.position.z<nearest.Root.position.z) nearest=n;
                    var close=new GameObject("Close note camera").AddComponent<Camera>(); close.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
                    var target=nearest.Root.position; close.transform.position=target+new Vector3(-.55f,.3f,-.85f); close.transform.LookAt(target); close.fieldOfView=38;
                    Capture("Logs/ArcaneNoteCloseup.png",close); UnityEngine.Object.DestroyImmediate(close.gameObject);
                }
                Call(game,"EndTest");
                if(!editor.Active || Math.Abs(editor.Head-testPoint)>.01) throw new Exception("Test did not return to the editor playhead");
                SetPhase(7,elapsed);
            }
            else if(phase==7 && wait>1.5)
            {
                Call(game,"LeaveEditor"); ModLibrary.Delete(mod); mod=null;
                File.WriteAllText("Logs/ArcaneRuntimeChecks.txt","PASS: runtime startup, DSP playback, moving pooled notes, procedural hand rigs, blade geometry, pooled destruction, render request, MP3 mod load, editor playback, section test and return.\n"+DateTime.UtcNow.ToString("O"));
                Debug.Log("ARCANE RUNTIME CHECKS PASSED");
                SessionState.SetBool(Flag,false); phase=9; EditorApplication.Exit(0);
            }
        }
        static double testPoint;
        // Includes the IMGUI layer, which a camera render request cannot see.
        static void Shot(string name) { if(!Application.isBatchMode) ScreenCapture.CaptureScreenshot(Path.GetFullPath(name+".png")); }
        static void Capture(string output="Logs/ArcaneArena.png",Camera from=null)
        {
            var camera=from?from:Camera.main;
            var target=new RenderTexture(1440,900,24,RenderTextureFormat.ARGBHalf);
            target.Create();
            var request=new RenderPipeline.StandardRequest{destination=target};
            RenderPipeline.SubmitRenderRequest(camera,request);
            var old=RenderTexture.active; RenderTexture.active=target;
            var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1440,900),0,0); texture.Apply();
            // The render request returns linear HDR values; PNG viewers expect sRGB.
            var pixels=texture.GetPixels(); float average=0;
            for(int i=0;i<pixels.Length;i++) { pixels[i]=pixels[i].gamma; average+=pixels[i].grayscale; }
            average/=pixels.Length;
            if(average>.45f || average<.004f) throw new Exception("Render exposure outside readable range: "+average);
            texture.SetPixels(pixels); texture.Apply();
            File.WriteAllBytes(output,texture.EncodeToPNG());
            RenderTexture.active=old; UnityEngine.Object.DestroyImmediate(texture); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
