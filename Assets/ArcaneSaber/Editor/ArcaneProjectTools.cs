using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.HighDefinition;

namespace ArcaneSaber.Editor
{
    [InitializeOnLoad]
    public static class ArcaneProjectTools
    {
        const string ScenePath="Assets/ArcaneSaber/Scenes/ArcaneSaber.unity";
        static ArcaneProjectTools() { EditorApplication.delayCall+=EnsureAssets; }
        public static void EnsureAssets()
        {
            if(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory("Assets/ArcaneSaber/Resources");
            Directory.CreateDirectory("Assets/ArcaneSaber/Scenes");
            if(!AssetDatabase.LoadAssetAtPath<GameFeelSettings>("Assets/ArcaneSaber/Resources/ArcaneGameFeel.asset"))
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameFeelSettings>(),"Assets/ArcaneSaber/Resources/ArcaneGameFeel.asset");
            if(!AssetDatabase.LoadAssetAtPath<Material>("Assets/ArcaneSaber/Resources/ArcaneNeon.mat"))
            {
                var mat=new Material(Shader.Find("HDRP/Unlit"));
                HDMaterial.ValidateMaterial(mat);
                AssetDatabase.CreateAsset(mat,"Assets/ArcaneSaber/Resources/ArcaneNeon.mat");
            }
            if(!AssetDatabase.LoadAssetAtPath<Material>("Assets/ArcaneSaber/Resources/ArcaneGlow.mat"))
            {
                // Additive, depth-free and double-sided: every light shaft and ribbon starts from this.
                var mat=new Material(Shader.Find("HDRP/Unlit"));
                mat.SetFloat("_SurfaceType",1); mat.SetFloat("_BlendMode",1); mat.SetFloat("_ZWrite",0); mat.SetFloat("_DoubleSidedEnable",1);
                mat.SetFloat("_TransparentZWrite",0); mat.SetFloat("_ZTestDepthEqualForOpaque",4);
                HDMaterial.ValidateMaterial(mat);
                AssetDatabase.CreateAsset(mat,"Assets/ArcaneSaber/Resources/ArcaneGlow.mat");
            }
            if(!AssetDatabase.LoadAssetAtPath<BeatmapController>("Assets/ArcaneSaber/DemoBeatmap.asset"))
                AssetDatabase.CreateAsset(BeatmapController.Demo(1),"Assets/ArcaneSaber/DemoBeatmap.asset");
            if(!AssetDatabase.LoadAssetAtPath<Material>("Assets/ArcaneSaber/Resources/ArcaneSurface.mat"))
            {
                var mat=new Material(Shader.Find("HDRP/Lit")); HDMaterial.ValidateMaterial(mat);
                AssetDatabase.CreateAsset(mat,"Assets/ArcaneSaber/Resources/ArcaneSurface.mat");
            }
            var audio=AssetImporter.GetAtPath("Assets/ArcaneSaber/Resources/Music.mp3") as AudioImporter;
            if(audio!=null)
            {
                var sample=audio.defaultSampleSettings;
                if(sample.loadType!=AudioClipLoadType.DecompressOnLoad || !sample.preloadAudioData)
                { sample.loadType=AudioClipLoadType.DecompressOnLoad; sample.preloadAudioData=true; audio.defaultSampleSettings=sample; audio.loadInBackground=false; audio.SaveAndReimport(); }
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Arcane Saber/Open Playable Scene")]
        public static void OpenScene()
        {
            EnsureAssets();
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if(!File.Exists(ScenePath))
            {
                var previous=SceneManager.GetActiveScene();
                bool additive=!string.IsNullOrEmpty(previous.path);
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,additive?NewSceneMode.Additive:NewSceneMode.Single);
                SceneManager.SetActiveScene(scene);
                var manager=new GameObject("ARCANE SABER").AddComponent<RhythmGameManager>();
                manager.Settings=AssetDatabase.LoadAssetAtPath<GameFeelSettings>("Assets/ArcaneSaber/Resources/ArcaneGameFeel.asset");
                EditorSceneManager.SaveScene(scene,ScenePath);
                if(additive) { EditorSceneManager.CloseScene(scene,true); SceneManager.SetActiveScene(previous); }
            }
            EditorSceneManager.OpenScene(ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        }
        [MenuItem("Arcane Saber/Play")]
        public static void Play() { OpenScene(); EditorApplication.isPlaying=true; }
        [MenuItem("Arcane Saber/Build Windows Player")]
        public static void Build()
        {
            OpenScene(); Directory.CreateDirectory("Builds/ArcaneSaber");
            var result=BuildPipeline.BuildPlayer(new[]{ScenePath},"Builds/ArcaneSaber/ArcaneSaber.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Arcane Saber build failed: "+result.summary.result);
        }
        [MenuItem("Arcane Saber/Run Gameplay Checks")]
        public static void Check()
        {
            void Assert(bool condition,string message) { if(!condition) throw new Exception("ARCANE CHECK FAILED: "+message); }
            var spring=new Spring3();
            for(int i=0;i<144;i++) spring.Step(Vector3.one,360,.72f,1/144f);
            Assert(Vector3.Distance(spring.Position,Vector3.one)<.001f,"spring must settle at 144 Hz");
            var low=new Spring3();
            for(int i=0;i<30;i++) low.Step(Vector3.one,360,.72f,1/30f);
            Assert(Vector3.Distance(low.Position,spring.Position)<.0001f,"spring refresh-rate independence");
            foreach(float damping in new[]{.1f,1f,2f})
            {
                var stable=new Spring3(); for(int i=0;i<100;i++) stable.Step(Vector3.one,1400,damping,.1f);
                Assert(!float.IsNaN(stable.Position.x) && stable.Position.magnitude<2,"stable under frame spikes");
            }
            var momentum=new Spring3(); momentum.Step(Vector3.left,360,.7f,.04f); float before=momentum.Velocity.x;
            momentum.Step(Vector3.right,360,.7f,.0001f);
            Assert(before<0 && momentum.Velocity.x<0,"reversal must preserve momentum");
            Assert(Mathf.Abs(new Vector2(1,1).normalized.magnitude-1)<.00001f,"normalized diagonal");
            Assert(SaberSpringController.VerticalPitch(-1,.7f)>0 && SaberSpringController.VerticalPitch(1,.7f)<0,"downstroke positive X / upstroke negative X");
            for(int difficulty=0;difficulty<3;difficulty++)
            {
                var demo=BeatmapController.Demo(difficulty); int previous=-1; float last=-10;
                foreach(var note in demo.Notes) if(note.Lane==1) { Assert(note.Direction==1 || note.Direction==3,"horizontal only"); Assert(note.Direction!=previous,"alternating saber direction"); Assert(note.Timestamp>last,"no simultaneous notes"); previous=note.Direction; last=note.Timestamp; }
                 UnityEngine.Object.DestroyImmediate(demo);
            }
            Assert(SaberSliceDetector.Sweep(new Vector3(-2,-.5f,0),new Vector3(-2,.5f,0),new Vector3(2,-.5f,0),new Vector3(2,.5f,0),Vector3.zero,Vector3.zero),"fast blade must not tunnel");
            Assert(!SaberSliceDetector.Sweep(new Vector3(-2,-.5f,2),new Vector3(-2,.5f,2),new Vector3(2,-.5f,2),new Vector3(2,.5f,2),Vector3.zero,Vector3.zero),"swept near miss");
            Assert(SaberSliceDetector.Sweep(Vector3.down,Vector3.up,Vector3.down,Vector3.up,Vector3.forward*2,Vector3.back*2),"relative sweep includes moving note");
            var track=Resources.Load<AudioClip>("Music");
            if(track)
            {
                var chart=MusicChartBuilder.Analyze(track,1); MusicChartBuilder.HorizontalFlow(chart);
                Assert(chart.Notes.Count>10,"music analysis must find playable onsets");
                int previous=-1,lane=-1; float last=-10;
                foreach(var note in chart.Notes)
                {
                    Assert(note.Timestamp-last>=(lane!=note.Lane?.649f:.319f),"music chart needs handover gaps and no simultaneous notes");
                    if(note.Lane==1) { Assert(note.Direction==1 || note.Direction==3,"music saber only horizontal"); Assert(note.Direction!=previous,"music saber alternates"); previous=note.Direction; }
                    lane=note.Lane; last=note.Timestamp;
                }
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/MusicAnalysis.txt","Track: "+track.name+" / seconds: "+track.length+" / notes: "+chart.Notes.Count+" / first note: "+chart.Notes[0].Timestamp+" / estimated pulse BPM: "+chart.BPM);
                UnityEngine.Object.DestroyImmediate(chart);
            }
            CheckMods(Assert,track);
            var authored=BeatSaverImport.Packaged(Resources.Load<AudioClip>("BellTolls/Track"));
            Assert(authored.Music && authored.Notes.Count==1117 && Mathf.Abs(authored.Notes[0].Timestamp-14.1875f*60/118)<.001f,"packaged Expert chart retains source notes and timing");
            UnityEngine.Object.DestroyImmediate(authored);
            var root=new GameObject("Gameplay check fixture");
            try
            {
                var chart=ScriptableObject.CreateInstance<BeatmapController>();
                chart.Notes.Add(new RhythmNote{Timestamp=1,Lane=0,Direction=0});
                chart.Notes.Add(new RhythmNote{Timestamp=1,Lane=0,Direction=3});
                var spawner=new NoteSpawner(root.transform); var score=new ScoreManager(); var judge=new NoteHitEvaluator(spawner,score);
                spawner.Update(chart,1); judge.Magic(1,1);
                Assert(score.Hits==0 && spawner.Active.Count==2,"wrong key must not hit");
                judge.Magic(0,1); judge.Magic(3,1);
                Assert(score.Hits==2 && score.Combo==2 && spawner.Active.Count==0,"simultaneous keys judged independently");
                spawner.Reset(); spawner.Update(chart,.8); judge.Magic(0,.8);
                Assert(score.Hits==2,"outside timing window");
                judge.InputLatencyMilliseconds=100; judge.Magic(0,1.1);
                Assert(score.Hits==3 && score.Judgment=="PERFECT","latency compensation");
                judge.Expire(1.5); Assert(score.Misses==1 && score.Combo==0,"miss resets combo");
                var feel=ScriptableObject.CreateInstance<GameFeelSettings>();
                for(int d=0;d<8;d++)
                {
                    Assert(RhythmAssist.MatchesIntent(d,GestureMath.Direction(d),feel.SaberAssist),"assistance accepts intended direction "+d);
                    Assert(!RhythmAssist.MatchesIntent(d,-GestureMath.Direction(d),feel.SaberAssist),"assistance rejects opposite direction "+d);
                    Assert(!RhythmAssist.MatchesIntent(d,Vector2.zero,feel.SaberAssist),"no automatic hits at rest");
                }
                Assert(RhythmAssist.MatchesIntent(5,Vector2.right,feel.SaberAssist),"diagonal completion from partial motion");
                Assert(!RhythmAssist.MatchesIntent(5,Vector2.right,0),"strict mode disables help");
                spawner.Reset(); spawner.Update(chart,.68);
                var assistedScore=new ScoreManager(); var assistedJudge=new NoteHitEvaluator(spawner,assistedScore){AssistSettings=feel};
                assistedJudge.Magic(0,.68); Assert(assistedScore.Hits==0,"early magic must wait for buffer window");
                assistedJudge.BufferedMagic(.75); Assert(assistedScore.Hits==1,"early magic press buffered into timing window");
                spawner.Reset(); spawner.Update(chart,.5); assistedJudge.Magic(0,.5); assistedJudge.BufferedMagic(.75);
                Assert(assistedScore.Hits==1,"stale key buffer must expire");
                spawner.Reset(); spawner.Update(chart,1.23); assistedJudge.Magic(0,1.23);
                Assert(assistedScore.Hits==2,"late magic forgiveness");
                Assert(spawner.Active[0].Root.position.z>.8f,"late grace keeps notes visible and reachable");
                Assert(Mathf.Abs(spawner.Active[0].Root.position.y-(1.65f+NoteSpawner.Offset(spawner.Active[0].Data).y))<.001f,"lower incoming note height");
                Assert(ArcaneVisuals.BeveledCube.bounds.size.z>.45f,"solid note has real depth");
                // Drive the gameplay input model without injecting operating-system input.
                var rig=new GameObject("Assisted saber fixture"); rig.transform.SetParent(root.transform); rig.transform.position=Vector3.up*1.6f;
                var blade=rig.AddComponent<SaberSpringController>(); blade.Initialize(feel);
                var saberChart=ScriptableObject.CreateInstance<BeatmapController>();
                var saberScore=new ScoreManager(); var saberJudge=new NoteHitEvaluator(spawner,saberScore){AssistSettings=feel};
                blade.AssistRequested=s=>saberJudge.GuideSaber(s,1);
                float inputTime=0;
                foreach(int d in new[]{0,2,4,5,6,7,8})
                {
                    saberChart.Notes.Clear(); saberChart.Notes.Add(new RhythmNote{Timestamp=1,Lane=1,Direction=d});
                    spawner.Reset(); spawner.Update(saberChart,1);
                    blade.Input.ApplyMouseDelta(GestureMath.Direction(d)*2,feel,1/144f,inputTime+=.1f);
                    blade.Animate(1/144f,true,false); saberJudge.Saber(blade,1);
                }
                Assert(saberScore.Hits==7,"vertical, diagonal and free assisted cuts");
                blade.AssistRequested=null;
                for(int step=0;step<50;step++) { blade.Input.ApplyMouseDelta(Vector2.up*4,feel,1/144f,inputTime+=1/144f); blade.Animate(1/144f,true,false); }
                Transform visualBlade=rig.transform.Find("Saber wrist/Blade follow through");
                Assert(Mathf.DeltaAngle(0,visualBlade.localEulerAngles.x)<0,"actual upward blade rotation negative X");
                for(int step=0;step<70;step++) { blade.Input.ApplyMouseDelta(Vector2.down*4,feel,1/144f,inputTime+=1/144f); blade.Animate(1/144f,true,false); }
                Assert(Mathf.DeltaAngle(0,visualBlade.localEulerAngles.x)>0,"actual downward blade rotation positive X");
                UnityEngine.Object.DestroyImmediate(saberChart); UnityEngine.Object.DestroyImmediate(feel);
                UnityEngine.Object.DestroyImmediate(chart);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/ArcaneChecks.txt","PASS: mod export/import, sanitizing, auto mark/assign/stage; spring stability/momentum; swept collision; timing/chords/latency/combo; assisted vertical and diagonal cuts; no idle/opposite assistance; buffered/late magic; expired buffers; solid 3D note geometry; lowered note height.\n"+DateTime.UtcNow.ToString("O"));
            Debug.Log("ARCANE CHECKS PASSED");
        }
        // Small v2, v3 and v4 BeatSaver zips: timing, hands, directions, tempo changes and lights.
        static void CheckBeatSaver(Action<bool,string> Assert,string scratch)
        {
            string Zip(string name,params (string file,string text)[] files)
            {
                string path=Path.Combine(scratch,name+".zip");
                using(var zip=System.IO.Compression.ZipFile.Open(path,System.IO.Compression.ZipArchiveMode.Create))
                {
                    foreach(var f in files) using(var w=new StreamWriter(zip.CreateEntry("level/"+f.file).Open())) w.Write(f.text);
                    using(var w=zip.CreateEntry("level/song.egg").Open()) w.Write(new byte[64],0,64);
                }
                return path;
            }
            var imported=new System.Collections.Generic.List<ModMap>();
            try
            {
                string v2=Zip("v2",("Info.dat","{\"_version\":\"2.0.0\",\"_songName\":\"Test Song\",\"_songAuthorName\":\"Band\",\"_beatsPerMinute\":120,\"_songFilename\":\"song.egg\",\"_difficultyBeatmapSets\":[{\"_beatmapCharacteristicName\":\"Standard\",\"_difficultyBeatmaps\":[{\"_difficulty\":\"Expert\",\"_beatmapFilename\":\"ExpertStandard.dat\",\"_noteJumpMovementSpeed\":16},{\"_difficulty\":\"Easy\",\"_beatmapFilename\":\"EasyStandard.dat\",\"_noteJumpMovementSpeed\":10}]},{\"_beatmapCharacteristicName\":\"360Degree\",\"_difficultyBeatmaps\":[{\"_difficulty\":\"Easy\",\"_beatmapFilename\":\"Easy360.dat\"}]}]}"),
                    ("EasyStandard.dat","{\"_version\":\"2.2.0\",\"_notes\":[{\"_time\":4,\"_lineIndex\":1,\"_lineLayer\":0,\"_type\":0,\"_cutDirection\":1},{\"_time\":4,\"_lineIndex\":1,\"_lineLayer\":0,\"_type\":0,\"_cutDirection\":1},{\"_time\":5,\"_lineIndex\":3,\"_lineLayer\":1,\"_type\":1,\"_cutDirection\":2},{\"_time\":6,\"_type\":3,\"_cutDirection\":0}],\"_events\":[{\"_time\":4,\"_type\":4,\"_value\":2},{\"_time\":5,\"_type\":8,\"_value\":0},{\"_time\":6,\"_type\":12,\"_value\":7}]}"),
                    ("ExpertStandard.dat","{\"_version\":\"2.2.0\",\"_notes\":[{\"_time\":2,\"_type\":1,\"_cutDirection\":7}]}"));
                var maps=ModLibrary.Import(v2); imported.AddRange(maps);
                Assert(maps.Count==2 && maps[0].Title.Contains("[Easy]") && maps[1].Title.Contains("[Expert]"),"BeatSaver v2: one mod per Standard difficulty, easiest first");
                var easy=maps[0];
                Assert(easy.Notes.Count==3 && easy.Notes[1].Timestamp==easy.Notes[0].Timestamp && easy.Notes[0].Lane==0 && easy.Notes[0].Direction==2 && Mathf.Abs(easy.Notes[0].Timestamp-2)<.001f,"BeatSaver v2: red down note becomes hand S at 2 s, stack retained and bomb dropped");
                Assert(easy.Notes[2].Lane==1 && easy.Notes[2].Direction==1 && Mathf.Abs(easy.Notes[2].Timestamp-2.5f)<.001f,"BeatSaver v2: blue left cut becomes laser left");
                Assert(easy.AudioFile=="audio.ogg" && File.Exists(ModLibrary.AudioPath(easy)),"BeatSaver audio .egg installed as ogg");
                Assert(easy.Events.Exists(e=>e.Kind==(int)StageKind.Flash) && easy.Events.Exists(e=>e.Kind==(int)StageKind.RingSpin) && easy.Events.Exists(e=>e.Kind==(int)StageKind.SpotCamera),"BeatSaver lights become stage cues");
                Assert(Mathf.Abs(maps[1].NoteSpeed-16f)<.01f && maps[1].Notes[0].Direction==3,"BeatSaver jump speed; down-right cut becomes laser right");

                string v3=Zip("v3",("Info.dat","{\"_version\":\"2.1.0\",\"_songName\":\"Three\",\"_beatsPerMinute\":120,\"_songFilename\":\"song.egg\",\"_difficultyBeatmapSets\":[{\"_beatmapCharacteristicName\":\"Standard\",\"_difficultyBeatmaps\":[{\"_difficulty\":\"Normal\",\"_beatmapFilename\":\"Normal.dat\"}]}]}"),
                    ("Normal.dat","{\"version\":\"3.3.0\",\"bpmEvents\":[{\"b\":0,\"m\":120},{\"b\":4,\"m\":60}],\"colorNotes\":[{\"b\":2,\"x\":1,\"y\":0,\"c\":1,\"d\":0},{\"b\":6,\"x\":1,\"y\":0,\"c\":0,\"d\":8}],\"basicBeatmapEvents\":[{\"b\":2,\"et\":1,\"i\":6,\"f\":1}]}"));
                var three=ModLibrary.Import(v3)[0]; imported.Add(three);
                Assert(three.Notes.Count==2 && Mathf.Abs(three.Notes[0].Timestamp-1)<.001f && three.Notes[0].Lane==1 && three.Notes[0].Direction==8,"BeatSaver v3 notes; up cut becomes free laser cut");
                for(int d=0;d<9;d++) { int laser=ModMap.LaserDirection(d); Assert(laser==1 || laser==3 || laser==8,"laser cuts are only left, right or free"); }
                Assert(Mathf.Abs(three.Notes[1].Timestamp-4)<.001f && three.Notes[1].Lane==0,"BeatSaver v3 tempo change: beat 6 after 60 BPM from beat 4 is 4 s");

                string v4=Zip("v4",("Info.dat","{\"version\":\"4.0.0\",\"song\":{\"title\":\"Four\",\"author\":\"Band\"},\"audio\":{\"songFilename\":\"song.egg\",\"bpm\":100},\"difficultyBeatmaps\":[{\"characteristic\":\"Standard\",\"difficulty\":\"Hard\",\"beatmapDataFilename\":\"Hard.dat\",\"lightshowDataFilename\":\"Lights.dat\",\"noteJumpMovementSpeed\":12}]}"),
                    ("Hard.dat","{\"version\":\"4.0.0\",\"colorNotes\":[{\"b\":5,\"i\":0,\"r\":0},{\"b\":10,\"i\":1,\"r\":0}],\"colorNotesData\":[{\"x\":1,\"y\":0,\"c\":1,\"d\":3},{\"x\":1,\"y\":2,\"c\":0,\"d\":0}]}"),
                    ("Lights.dat","{\"version\":\"4.0.0\",\"basicEvents\":[{\"b\":5,\"i\":0}],\"basicEventsData\":[{\"t\":8,\"i\":0,\"f\":0}]}"));
                var fours=ModLibrary.Import(v4); imported.AddRange(fours); var four=fours[fours.Count-1];
                Assert(fours.Count==3 && fours[0].Title.Contains("[Kolay]") && fours[1].Title.Contains("[Orta]"),"BeatSaver: a map without Easy/Normal also gets thinned keyboard versions");
                Assert(four.Title.StartsWith("Four") && four.Title.Contains("[Hard]") && four.Notes.Count==2 && Mathf.Abs(four.Notes[0].Timestamp-3)<.001f && four.Notes[0].Lane==1 && four.Notes[0].Direction==3 && four.Notes[1].Lane==0 && four.Notes[1].Direction==0,"BeatSaver v4 notes");
                Assert(four.Events.Exists(e=>e.Kind==(int)StageKind.RingSpin),"BeatSaver v4 lightshow file");
            }
            finally { foreach(var m in imported) ModLibrary.Delete(m); }
        }
        static void CheckMods(Action<bool,string> Assert,AudioClip track)
        {
            if(IntPtr.Size==8) Assert(NativeFileDialog.StructSize==152,"Win32 OPENFILENAME layout");
            {
                // Drum hits every half second under a constant drone: auto notes must land on them.
                const int rate=44100; var pcm=new float[rate*24]; uint seed=1;
                for(int i=0;i<pcm.Length;i++) pcm[i]=Mathf.Sin(i*2*Mathf.PI*220f/rate)*.25f*Mathf.Clamp01(i/(rate*.6f));
                var clicks=new System.Collections.Generic.List<float>();
                for(float t=1.03f;t<23;t+=.5f)
                {
                    clicks.Add(t); int start=(int)(t*rate);
                    for(int i=0;i<rate/10 && start+i<pcm.Length;i++)
                    { seed^=seed<<13; seed^=seed>>17; seed^=seed<<5; float s=(float)i/rate; pcm[start+i]+=(Mathf.Sin(2*Mathf.PI*(90-200*s)*s)+(seed/4294967296f*2-1)*.6f)*Mathf.Exp(-s*45)*.9f; }
                }
                var song=SongAnalysis.Of(pcm,rate);
                Assert(Mathf.Abs(song.BPM-120)<1.5f,"tempo detection finds 120 BPM, got "+song.BPM);
                var synced=new ModMap{BPM=song.BPM,BeatOffset=song.Phase}; MapAutomation.Mark(synced,song,.5f,0);
                float worst=0; foreach(var n in synced.Notes) { float nearest=10; foreach(float c in clicks) nearest=Mathf.Min(nearest,Mathf.Abs(c-n.Timestamp)); worst=Mathf.Max(worst,nearest); }
                Assert(synced.Notes.Count>=clicks.Count-3 && worst<.03f,$"auto notes land on hits ({synced.Notes.Count}/{clicks.Count}, worst {worst*1000:0} ms)");
                int kicks=0; foreach(byte a in song.Accents) if((a&SongAnalysis.Kick)!=0) kicks++;
                Assert(kicks>=clicks.Count-3,"kick accents drive the lights");
                File.WriteAllText("Logs/SyncCheck.txt",$"bpm {song.BPM:0.00} notes {synced.Notes.Count}/{clicks.Count} worst {worst*1000:0} ms kicks {kicks}");
            }
            string scratch=Path.Combine(Path.GetTempPath(),"ArcaneModCheck"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(scratch);
            ModMap mod=null,copy=null;
            try
            {
                string source=Path.Combine(scratch,"song.wav"); File.WriteAllBytes(source,new byte[64]);
                mod=ModLibrary.Create(source); mod.Title="Check / mod";
                mod.Notes.Add(new RhythmNote{Timestamp=2,Lane=ModMap.Undecided});
                mod.Notes.Add(new RhythmNote{Timestamp=1,Lane=1,Direction=3});
                mod.Events.Add(new StageEvent{Time=1,Kind=(int)StageKind.Palette,Value=2});
                string archive=Path.Combine(scratch,ModLibrary.FileSafe(mod.Title)+".arcanemap");
                ModLibrary.Export(mod,archive); copy=ModLibrary.Import(archive)[0];
                Assert(copy.Id!=mod.Id && copy.Notes.Count==2 && copy.Events.Count==1 && File.Exists(ModLibrary.AudioPath(copy)),"mod export/import round trip");
                var map=copy.ToBeatmap(null); Assert(map.Notes.Count==1 && map.Notes[0].Lane==1 && map.Notes[0].TravelSpeed==copy.NoteSpeed,"undecided markers never reach gameplay");
                UnityEngine.Object.DestroyImmediate(map);
                var hostile=new ModMap{BPM=float.NaN}; hostile.Notes.Add(new RhythmNote{Timestamp=float.NaN}); hostile.Notes.Add(new RhythmNote{Timestamp=1,Lane=9}); hostile.Notes.Add(new RhythmNote{Timestamp=1,Lane=0,Direction=7});
                hostile.Events.Add(new StageEvent{Time=-1}); hostile.Sanitize();
                Assert(hostile.Notes.Count==1 && hostile.Notes[0].Direction==3 && hostile.Events.Count==0 && hostile.BPM==120,"imported data is sanitized");
                CheckBeatSaver(Assert,scratch);
                if(track)
                {
                    var song=SongAnalysis.Of(track); var auto=new ModMap{BPM=song.BPM,BeatOffset=song.Phase};
                    int marked=MapAutomation.Mark(auto,song,.5f,0); Assert(marked>20,"auto marking finds hits");
                    MapAutomation.Assign(auto); Assert(auto.UndecidedCount==0,"auto assign decides every marker");
                    bool hands=false,lasers=false; int previous=-1,same=0;
                    foreach(var n in auto.Notes) { hands|=n.Lane==0; lasers|=n.Lane==1; if(n.Lane==1) { if(n.Direction==previous) same++; previous=n.Direction; } }
                    Assert(hands && lasers && same==0,"auto assign uses both hands and alternates cuts");
                    MapAutomation.Stage(auto,song); Assert(auto.Events.Count>10,"auto stage produces cues");
                    File.WriteAllText("Logs/ModAnalysis.txt",$"bpm {song.BPM} phase {song.Phase} marked {marked} events {auto.Events.Count}");
                }
            }
            finally
            {
                if(mod!=null) ModLibrary.Delete(mod);
                if(copy!=null) ModLibrary.Delete(copy);
                Directory.Delete(scratch,true);
            }
        }
        public static void BatchValidate()
        {
            try { EnsureAssets(); OpenScene(); Check(); EditorApplication.Exit(0); }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
