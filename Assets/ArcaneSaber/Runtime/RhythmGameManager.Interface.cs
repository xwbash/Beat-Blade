using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;
using UILabel = UnityEngine.UIElements.Label;

namespace ArcaneSaber
{
    // Beat and Blade's game screens, built with UI Toolkit at runtime: menu, results, mods, HUD, pause and Feel Lab.
    public sealed partial class RhythmGameManager
    {
        enum View { Menu, Results, Mods, Hud, Editor }
        public const string GameName="Beat and Blade";
        static readonly Color Gold=new Color(1,.84f,.36f), Soft=new Color(.59f,.67f,.8f);

        View shownView=(View)(-1); bool modsDirty=true, feelShown;
        VisualElement ui, overlay, brand, menuScreen, resultsScreen, modsScreen, hud, pauseScreen, feelScreen, edgeLeft, edgeRight;
        VisualElement difficultyRow, progressFill, heatFill, multFill, comboBlock, judgmentBlock, banner, modList, newBest;
        UILabel brandSub, packagedLabel, songTitle, songSub, scoreLabel, accuracyLabel, comboLabel, comboCaption, judgmentLabel, timingLabel;
        UILabel bannerTitle, bannerSub, milestoneLabel, hintLabel, laneLeft, laneRight, pauseSub, noticeLabel, loadingLabel, saveLabel;
        UILabel rankLabel, fullComboLabel, resultScore, resultSong, resultAccuracy, resultCombo, resultPerfect, resultGreat, resultGood, resultMiss;
        Button calibrateButton, pauseMenuButton; readonly Button[] difficultyButtons=new Button[3];
        readonly List<Action> feelRefresh=new List<Action>();

        // Feel state: ages in seconds since each event.
        float hitAge=9, missAge=9, milestoneAge=9, breakAge=9, introAge=9, saveAge=9, scorePop, shownScore, resultReveal;
        bool hitMagic, hitPerfect, newBestScore; int lastCombo, milestone; string packagedName;

        // ---------- data ----------
        static void MigrateLegacyData()
        {
            // The product used to be called BeatSaberProject; keep players' mods and feel settings across the rename.
            try
            {
                string current=Application.persistentDataPath, parent=Path.GetDirectoryName(current);
                if(string.IsNullOrEmpty(parent)) return;
                string legacy=Path.Combine(parent,"BeatSaberProject");
                if(!Directory.Exists(legacy) || string.Equals(Path.GetFullPath(legacy),Path.GetFullPath(current),StringComparison.OrdinalIgnoreCase)) return;
                CopyTree(Path.Combine(legacy,"Mods"),Path.Combine(current,"Mods"));
                string feel=Path.Combine(legacy,"ArcaneGameFeel.json"), target=Path.Combine(current,"ArcaneGameFeel.json");
                if(File.Exists(feel) && !File.Exists(target)) { Directory.CreateDirectory(current); File.Copy(feel,target); }
            }
            catch(Exception e) { Debug.LogWarning("Could not migrate old save data: "+e.Message); }
        }
        static void CopyTree(string from,string to)
        {
            if(!Directory.Exists(from) || Directory.Exists(to)) return;
            foreach(var dir in Directory.GetDirectories(from,"*",SearchOption.AllDirectories)) Directory.CreateDirectory(dir.Replace(from,to));
            Directory.CreateDirectory(to);
            foreach(var file in Directory.GetFiles(from,"*",SearchOption.AllDirectories)) File.Copy(file,file.Replace(from,to));
        }
        string BestKey() => "Best."+(activeMod!=null?activeMod.Id:packagedSong?"Packaged":CustomBeatmap?"Custom":"BuiltIn"+difficulty);
        void RecordBest()
        {
            newBestScore=false; if(testing || score.Score<=0) return;
            string key=BestKey();
            if(score.Score>PlayerPrefs.GetInt(key,0)) { PlayerPrefs.SetInt(key,score.Score); PlayerPrefs.Save(); newBestScore=true; }
        }
        string SongName()
        {
            if(activeMod!=null) return activeMod.Title;
            if(CustomBeatmap) return CustomBeatmap.name;
            if(packagedSong)
            {
                if(packagedName==null)
                {
                    var info=Resources.Load<TextAsset>("BellTolls/Info");
                    var match=info?Regex.Match(info.text,"\"_songName\"\\s*:\\s*\"([^\"]+)\""):null;
                    packagedName=match!=null && match.Success?match.Groups[1].Value:"BeatSaver Session";
                }
                return packagedName;
            }
            return MusicTrack?MusicTrack.name:"Procedural Session";
        }

        // ---------- feel ----------
        void BeginRunFeel(double from)
        {
            hitAge=missAge=milestoneAge=breakAge=9; judgmentAge=9; introAge=from>0 && !testing?9:0; if(testing) introAge=.8f;
            shownScore=0; scorePop=0; lastCombo=0; newBestScore=false;
        }
        void FeelHit(bool isMagic,bool perfect,float error)
        {
            hitAge=0; hitMagic=isMagic; hitPerfect=perfect; scorePop=1;
            if(score.Combo>0 && score.Combo%25==0)
            {
                milestone=score.Combo; milestoneAge=0; breakAge=9;
                cameraMotion.Zoom(.1f); stage.Hit(isMagic,true);
            }
            lastCombo=score.Combo;
        }
        void FeelMiss()
        {
            missAge=0;
            if(lastCombo>=10) { breakAge=0; milestoneAge=9; }
            lastCombo=0;
        }

        // ---------- building ----------
        static T Add<T>(VisualElement parent,T child,params string[] classes) where T:VisualElement
        { foreach(var c in classes) child.AddToClassList(c); parent.Add(child); return child; }
        static VisualElement Box(VisualElement parent,params string[] classes) => Add(parent,new VisualElement(),classes);
        static UILabel Text(VisualElement parent,string text,params string[] classes) => Add(parent,new UILabel(text),classes);
        static Button MakeButton(VisualElement parent,string text,Action click,params string[] classes)
        {
            var b=Add(parent,new Button(click){text=text},classes); b.AddToClassList("btn");
            b.focusable=false; return b;
        }
        static VisualElement MakeScreen(VisualElement parent,params string[] classes)
        {
            var s=Box(parent,classes); s.AddToClassList("screen"); s.AddToClassList("hidden"); return s;
        }
        static void Show(VisualElement e,bool on)
        {
            bool shown=!e.ClassListContains("hidden");
            if(on==shown) return;
            if(on)
            {
                e.RemoveFromClassList("hidden"); e.RemoveFromClassList("screen--in");
                if(e.ClassListContains("screen")) e.schedule.Execute(()=>e.AddToClassList("screen--in")).StartingIn(16);
            }
            else { e.AddToClassList("hidden"); e.RemoveFromClassList("screen--in"); }
        }
        static void Ignore(params VisualElement[] elements) { foreach(var e in elements) e.pickingMode=PickingMode.Ignore; }

        void BuildInterface()
        {
            if(EventSystem.current==null)
            {
                var events=new GameObject("UI events"); events.transform.SetParent(transform,false);
                events.AddComponent<EventSystem>(); events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var panel=ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet=Resources.Load<ThemeStyleSheet>("UI/BeatBladeTheme");
            panel.scaleMode=PanelScaleMode.ScaleWithScreenSize; panel.referenceResolution=new Vector2Int(1920,1080);
            panel.screenMatchMode=PanelScreenMatchMode.MatchWidthOrHeight; panel.match=.5f;
            var host=new GameObject("Interface"); host.SetActive(false); host.transform.SetParent(transform,false);
            var document=host.AddComponent<UIDocument>(); document.panelSettings=panel; host.SetActive(true);

            ui=document.rootVisualElement; ui.AddToClassList("root");
            var sheet=Resources.Load<StyleSheet>("UI/BeatBlade"); if(sheet) ui.styleSheets.Add(sheet);
            overlay=Box(ui,"fill"); Ignore(ui,overlay);

            BuildHud(); BuildMenu(); BuildResults(); BuildMods(); BuildPause(); BuildFeelLab();

            brand=Box(ui,"brand"); Ignore(brand);
            Box(brand,"brand-bar");
            var words=Box(brand); Text(words,"B E A T   A N D   B L A D E","brand-name"); brandSub=Text(words,"DUAL-HAND RHYTHM   ·   AUDIO REACTIVE","brand-sub");
        }
        void BuildMenu()
        {
            menuScreen=MakeScreen(ui,"center"); Ignore(menuScreen);
            var column=Box(menuScreen,"menu-column");
            var art=ArcaneArt.Texture("Logo");
            if(art) { var logo=Box(column,"logo"); logo.style.backgroundImage=new StyleBackground(art); }
            else
            {
                var words=Box(column,"row"); Text(words,"BEAT ","logo-text","crimson"); Text(words,"&","logo-text"); Text(words," BLADE","logo-text","cyan");
            }
            Text(column,"T W O   H A N D S .   O N E   F L O W .","kicker");
            difficultyRow=Box(column,"row","difficulty");
            string[] names={"INITIATE","ADEPT","VIRTUOSO"};
            for(int i=0;i<3;i++) { int level=i; difficultyButtons[i]=MakeButton(difficultyRow,names[i],()=>{ difficulty=level; RefreshDifficulty(); }); }
            packagedLabel=Text(column,"BEATSAVER   ·   EXPERT STANDARD","kicker","difficulty");
            MakeButton(column,"PLAY",()=>{ ReleaseMod(); StartSession(); },"btn-primary","menu-play");
            var secondary=Box(column,"row","menu-secondary");
            MakeButton(secondary,"MODS & EDITOR",()=>{ browsing=true; RefreshMods(); });
            MakeButton(secondary,"FEEL LAB",()=>tuning=true);
            MakeButton(secondary,"QUIT",Application.Quit,"btn-ghost");
            var controls=Box(column,"controls");
            var left=Box(controls,"control-card","left"); Text(left,"W  A  S  D","control-key","crimson"); Text(left,"Left hand conducts magic. Match the letter as it crosses the gate.","control-text");
            var right=Box(controls,"control-card","right"); Text(right,"MOUSE","control-key","cyan"); Text(right,"Right hand swings the blade. Cut through the cubes, alternating sides.","control-text");
            Text(column,"ENTER  play     ·     ESC  pause     ·     F1  feel lab     ·     R  restart","muted").style.marginTop=22;
            RefreshDifficulty();
        }
        void RefreshDifficulty()
        {
            for(int i=0;i<3;i++) difficultyButtons[i].EnableInClassList("selected",i==difficulty);
            difficultyRow.EnableInClassList("hidden",packagedSong || CustomBeatmap!=null);
            packagedLabel.EnableInClassList("hidden",!packagedSong);
        }
        void BuildResults()
        {
            resultsScreen=MakeScreen(ui,"center"); Ignore(resultsScreen);
            var card=Box(resultsScreen,"panel"); card.style.alignItems=Align.Center; card.style.width=860;
            Text(card,"S E S S I O N   C O M P L E T E","kicker");
            resultSong=Text(card,"","body"); resultSong.style.marginTop=6;
            rankLabel=Text(card,"S","rank");
            fullComboLabel=Text(card,"FULL COMBO","full-combo");
            resultScore=Text(card,"0","result-score");
            newBest=Text(card,"NEW BEST","new-best");
            var stats=Box(card,"stats");
            UILabel Stat(string name,Color tint) { var s=Box(stats,"stat"); var v=Text(s,"0","stat-value"); v.style.color=tint; Text(s,name,"stat-name"); return v; }
            resultAccuracy=Stat("ACCURACY",Color.white); resultCombo=Stat("BEST COMBO",Gold);
            resultPerfect=Stat("PERFECT",Color.white); resultGreat=Stat("GREAT",ArcaneVisuals.Cyan); resultGood=Stat("GOOD",Soft); resultMiss=Stat("MISS",ArcaneVisuals.Crimson);
            var actions=Box(card,"row");
            MakeButton(actions,"PLAY AGAIN",StartSession,"btn-primary").style.width=300;
            MakeButton(actions,"MODS",()=>{ ReleaseMod(); finished=false; started=false; browsing=true; RefreshMods(); }).style.width=170;
            MakeButton(actions,"MENU",()=>{ ReleaseMod(); finished=false; started=false; }).style.width=170;
        }
        void FillResults()
        {
            resultSong.text=SongName();
            rankLabel.text=score.Rank;
            rankLabel.style.color=score.Rank=="S"?Gold:score.Rank=="A"?ArcaneVisuals.Cyan:score.Rank=="D"?ArcaneVisuals.Crimson:Color.white;
            fullComboLabel.EnableInClassList("hidden",!score.FullCombo);
            newBest.EnableInClassList("hidden",!newBestScore);
            resultAccuracy.text=$"{score.Accuracy:0.0}%"; resultCombo.text=score.BestCombo.ToString();
            resultPerfect.text=score.Perfects.ToString(); resultGreat.text=score.Greats.ToString();
            resultGood.text=score.Goods.ToString(); resultMiss.text=score.Misses.ToString();
            resultReveal=0;
        }
        void BuildMods()
        {
            modsScreen=MakeScreen(ui,"center"); Ignore(modsScreen);
            var card=Box(modsScreen,"panel","mods-panel");
            var head=Box(card,"mods-head");
            var titles=Box(head); Text(titles,"MODS","mods-title"); Text(titles,"Load your music, place your notes, play, share.","muted").style.unityTextAlign=TextAnchor.MiddleLeft;
            loadingLabel=Text(head,"LOADING…","loading");
            var actions=Box(card,"mods-actions");
            MakeButton(actions,"+  NEW MOD",()=>pending=NewMod,"btn-primary").style.width=260;
            MakeButton(actions,"IMPORT",()=>pending=ImportMod).style.width=180;
            MakeButton(actions,"OPEN FOLDER",()=>{ Directory.CreateDirectory(ModLibrary.Root); Application.OpenURL(new Uri(ModLibrary.Root+Path.DirectorySeparatorChar).AbsoluteUri); }).style.width=220;
            var spacer=Box(actions); spacer.style.flexGrow=1;
            MakeButton(actions,"BACK  ·  ESC",()=>browsing=false,"btn-ghost").style.width=200;
            if(!NativeFileDialog.Supported) Text(card,"No file dialog on this platform; copy mods into the Mods folder manually.","muted");
            modList=Add(card,new ScrollView(ScrollViewMode.Vertical),"mods-list");
            noticeLabel=Text(card,"","notice");
        }
        void BuildModRows()
        {
            modsDirty=false; modList.Clear();
            if(mods.Count==0) { Text(modList,"No mods yet. Pick a song with NEW MOD, or IMPORT a BeatSaver map.","body","empty"); return; }
            foreach(var entry in mods)
            {
                var mod=entry; var row=Box(modList,"mod-row");
                var info=Box(row,"mod-info"); Ignore(info);
                Text(info,mod.Title,"mod-title");
                Text(info,$"{mod.Notes.Count} notes   ·   {mod.Events.Count} stage events{(mod.UndecidedCount>0?"   ·   "+mod.UndecidedCount+" undecided":"")}","mod-meta");
                int best=PlayerPrefs.GetInt("Best."+mod.Id,0); if(best>0) Text(info,$"BEST  {best:N0}","mod-best");
                MakeButton(row,"PLAY",()=>OpenMod(mod,false),"btn-small").style.width=110;
                MakeButton(row,"EDIT",()=>OpenMod(mod,true),"btn-small").style.width=100;
                MakeButton(row,"EXPORT",()=>pending=()=>ExportMod(mod),"btn-small").style.width=120;
                bool armed=confirmDelete==mod.Id;
                MakeButton(row,armed?"SURE?":"DELETE",()=>
                {
                    if(confirmDelete==mod.Id) { Guarded(()=>ModLibrary.Delete(mod)); RefreshMods(); }
                    else { confirmDelete=mod.Id; modsDirty=true; }
                },"btn-small","btn-danger").style.width=120;
            }
        }
        void BuildHud()
        {
            hud=Box(ui,"fill","hidden"); Ignore(hud);
            edgeLeft=Box(hud,"edge","edge-left"); edgeRight=Box(hud,"edge","edge-right");
            var gradient=new Texture2D(128,1,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
            for(int x=0;x<128;x++) { float a=1-x/127f; gradient.SetPixel(x,0,new Color(1,1,1,a*a*a)); }
            gradient.Apply();
            foreach(var edge in new[]{edgeLeft,edgeRight}) { edge.style.backgroundImage=new StyleBackground(gradient); edge.style.opacity=0; }

            var top=Box(hud,"hud-top");
            var row=Box(top,"hud-row");
            var left=Box(row); songTitle=Text(left,"","song-title"); songSub=Text(left,"","song-sub");
            var right=Box(row); scoreLabel=Text(right,"0","score"); accuracyLabel=Text(right,"","accuracy");
            var progress=Box(top,"bar"); progressFill=Box(progress,"bar-fill");
            var heat=Box(top,"bar","bar-thin"); heatFill=Box(heat,"bar-fill");

            comboBlock=Box(hud,"combo-block");
            comboLabel=Text(comboBlock,"0","combo"); comboCaption=Text(comboBlock,"COMBO","combo-caption");
            var track=Box(comboBlock,"mult-track"); multFill=Box(track,"bar-fill");
            judgmentBlock=Box(hud,"judgment-block");
            judgmentLabel=Text(judgmentBlock,"","judgment"); timingLabel=Text(judgmentBlock,"","timing");
            milestoneLabel=Text(hud,"","milestone");
            banner=Box(hud,"banner"); bannerTitle=Text(banner,"","banner-title"); bannerSub=Text(banner,"","banner-sub");

            var bottom=Box(hud,"hud-bottom");
            laneLeft=Text(bottom,"W A S D   ·   CONDUCT MAGIC","lane-hint","crimson");
            hintLabel=Text(bottom,"","keys");
            laneRight=Text(bottom,"MOUSE   ·   CARVE LEFT / RIGHT","lane-hint","cyan");
        }
        void BuildPause()
        {
            pauseScreen=MakeScreen(ui,"center","dim");
            Text(pauseScreen,"PAUSED","title");
            pauseSub=Text(pauseScreen,"TAKE A BREATH","kicker"); pauseSub.style.marginBottom=30;
            var column=Box(pauseScreen,"pause-column");
            MakeButton(column,"RESUME  ·  ESC",()=>{ music.TogglePause(); Lock(); },"btn-primary");
            MakeButton(column,"RESTART  ·  R",()=>Launch(testing?Math.Max(0,testPoint-1.5):0));
            MakeButton(column,"FEEL LAB  ·  F1",()=>tuning=true);
            pauseMenuButton=MakeButton(column,"BACK TO MENU",()=>
            {
                if(testing) { EndTest(); return; }
                music.Stop(); notes.Reset(); started=false; browsing=activeMod!=null; ReleaseMod(); RefreshMods();
            },"btn-ghost");
        }
        void BuildFeelLab()
        {
            feelScreen=MakeScreen(ui,"center","dim");
            var card=Box(feelScreen,"panel","feel-panel");
            var head=Box(card,"feel-head");
            var titles=Box(head); Text(titles,"FEEL LAB","feel-title"); Text(titles,"LIVE SETTINGS   ·   CHANGES APPLY INSTANTLY   ·   F1 / ESC TO RETURN","muted").style.unityTextAlign=TextAnchor.MiddleLeft;
            var scroll=Add(card,new ScrollView(ScrollViewMode.Vertical),"feel-scroll");
            var s=Settings;
            void Section(string name) => Text(scroll,name,"feel-section");
            void Slide(string name,Func<float> get,Action<float> set,float min,float max)
            {
                var r=Box(scroll,"feel-row"); Text(r,name,"feel-name");
                var slider=Add(r,new Slider(min,max),"feel-slider"); slider.focusable=false;
                var value=Text(r,"","feel-value");
                slider.RegisterValueChangedCallback(e=>{ set(e.newValue); value.text=e.newValue.ToString("0.00"); });
                feelRefresh.Add(()=>{ float v=get(); slider.SetValueWithoutNotify(v); value.text=v.ToString("0.00"); });
            }
            Section("ACCESSIBILITY  &  AUDIO");
            var reduced=Add(scroll,new Toggle("  Reduced camera motion"),"feel-toggle"); reduced.focusable=false;
            reduced.RegisterValueChangedCallback(e=>s.ReducedMotion=e.newValue);
            feelRefresh.Add(()=>reduced.SetValueWithoutNotify(s.ReducedMotion));
            Slide("Stage brightness",()=>s.StageBrightness,v=>s.StageBrightness=v,0,1.5f);
            Slide("Hit sound volume",()=>s.HitSoundVolume,v=>s.HitSoundVolume=v,0,1);
            Slide("Bodycam lens",()=>s.BodycamLens,v=>s.BodycamLens=v,0,.35f);
            Slide("Mouse camera motion",()=>s.MouseCameraMotion,v=>s.MouseCameraMotion=v,0,2);
            Section("TIMING  &  ASSISTANCE");
            Slide("Input latency compensation (ms)",()=>latency,v=>latency=v,-200,200);
            calibrateButton=MakeButton(scroll,"CALIBRATE LATENCY  ·  TAP TEST",()=>{ if(calibrating) { calibrating=false; metronome.Stop(); } else StartCalibration(); },"btn-small");
            calibrateButton.style.alignSelf=Align.FlexStart; calibrateButton.style.marginLeft=12;
            Slide("Saber flow assistance",()=>s.SaberAssist,v=>s.SaberAssist=v,0,1);
            Slide("Magic timing assistance",()=>s.MagicTimingAssist,v=>s.MagicTimingAssist=v,0,1);
            Section("LEFT HAND  ·  MAGIC");
            Slide("Position stiffness",()=>s.MagicHandPositionStiffness,v=>s.MagicHandPositionStiffness=v,20,1400);
            Slide("Position damping",()=>s.MagicHandPositionDamping,v=>s.MagicHandPositionDamping=v,.1f,2);
            Slide("Rotation stiffness",()=>s.MagicHandRotationStiffness,v=>s.MagicHandRotationStiffness=v,20,1400);
            Slide("Rotation damping",()=>s.MagicHandRotationDamping,v=>s.MagicHandRotationDamping=v,.1f,2);
            Slide("Arc intensity",()=>s.MagicGestureArcIntensity,v=>s.MagicGestureArcIntensity=v,0,1);
            Slide("Gesture distance",()=>s.MagicGestureDistance,v=>s.MagicGestureDistance=v,.1f,.7f);
            Slide("Momentum impulse",()=>s.MagicGestureOvershoot,v=>s.MagicGestureOvershoot=v,0,2);
            Slide("Finger curl",()=>s.MagicFingerCurlIntensity,v=>s.MagicFingerCurlIntensity=v,0,2);
            Slide("Finger release speed",()=>s.MagicFingerReleaseSpeed,v=>s.MagicFingerReleaseSpeed=v,3,35);
            Slide("Trail intensity",()=>s.MagicTrailIntensity,v=>s.MagicTrailIntensity=v,0,2);
            Slide("Idle sway",()=>s.MagicIdleSway,v=>s.MagicIdleSway=v,0,.06f);
            Slide("Recovery",()=>s.MagicRecoverySpeed,v=>s.MagicRecoverySpeed=v,.2f,8);
            Section("RIGHT HAND  ·  BLADE");
            Slide("Mouse sensitivity",()=>s.SaberSwingSensitivity,v=>s.SaberSwingSensitivity=v,.1f,3);
            Slide("Position stiffness",()=>s.SaberPositionStiffness,v=>s.SaberPositionStiffness=v,20,1400);
            Slide("Position damping",()=>s.SaberPositionDamping,v=>s.SaberPositionDamping=v,.1f,2);
            Slide("Rotation stiffness",()=>s.SaberRotationStiffness,v=>s.SaberRotationStiffness=v,20,1400);
            Slide("Rotation damping",()=>s.SaberRotationDamping,v=>s.SaberRotationDamping=v,.1f,2);
            Slide("Blade lag",()=>s.SaberBladeLag,v=>s.SaberBladeLag=v,0,2);
            Slide("Wrist influence",()=>s.SaberWristInfluence,v=>s.SaberWristInfluence=v,0,2);
            Slide("Overshoot",()=>s.SaberOvershoot,v=>s.SaberOvershoot=v,0,1);
            Slide("Trail intensity",()=>s.SaberTrailIntensity,v=>s.SaberTrailIntensity=v,0,2);
            Section("CAMERA");
            Slide("Magic camera influence",()=>s.MagicCameraInfluence,v=>s.MagicCameraInfluence=v,0,2);
            Slide("Blade camera influence",()=>s.SaberCameraInfluence,v=>s.SaberCameraInfluence=v,0,2);
            Slide("Position stiffness",()=>s.CameraPositionStiffness,v=>s.CameraPositionStiffness=v,20,1000);
            Slide("Position damping",()=>s.CameraPositionDamping,v=>s.CameraPositionDamping=v,.1f,2);
            Slide("Rotation stiffness",()=>s.CameraRotationStiffness,v=>s.CameraRotationStiffness=v,20,1000);
            Slide("Rotation damping",()=>s.CameraRotationDamping,v=>s.CameraRotationDamping=v,.1f,2);
            Slide("Impact",()=>s.CameraImpactIntensity,v=>s.CameraImpactIntensity=v,0,2);
            Slide("Maximum roll",()=>s.MaximumCameraRoll,v=>s.MaximumCameraRoll=v,0,3);
            Slide("Breathing",()=>s.IdleCameraInfluence,v=>s.IdleCameraInfluence=v,0,2);
            Slide("Beat pulse",()=>s.MusicCameraInfluence,v=>s.MusicCameraInfluence=v,0,2);
            var foot=Box(card,"feel-foot");
            saveLabel=Text(foot,"SAVED","loading"); saveLabel.style.marginRight=18;
            MakeButton(foot,"SAVE",()=>{ SaveFeel(); saveAge=0; },"btn-primary").style.width=200;
            MakeButton(foot,"BACK",()=>tuning=false,"btn-ghost").style.width=160;
        }
        void RefreshFeel() { foreach(var refresh in feelRefresh) refresh(); }

        // ---------- per frame ----------
        void LateUpdate()
        {
            if(ui==null) return;
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            hitAge+=dt; missAge+=dt; milestoneAge+=dt; breakAge+=dt; introAge+=dt; saveAge+=dt; resultReveal+=dt;
            var flash=stage.Overlay; overlay.style.backgroundColor=flash.a>.004f?flash:Color.clear;

            var view=editor.Active?View.Editor:browsing?View.Mods:finished?View.Results:!started?View.Menu:View.Hud;
            if(view!=shownView)
            {
                shownView=view;
                if(view==View.Results) FillResults();
                if(view==View.Menu) RefreshDifficulty();
            }
            bool feel=tuning && view!=View.Editor && view!=View.Mods;
            brand.EnableInClassList("hidden",feel || !(view==View.Menu || view==View.Mods || view==View.Results));
            Show(menuScreen,view==View.Menu && !feel); Show(resultsScreen,view==View.Results && !feel);
            Show(modsScreen,view==View.Mods); Show(hud,view==View.Hud);
            Show(pauseScreen,view==View.Hud && music.Paused && !feel); Show(feelScreen,feel);
            if(feel && !feelShown) RefreshFeel();
            feelShown=feel;

            switch(view)
            {
                case View.Hud: TickHud(dt); break;
                case View.Mods:
                    if(modsDirty) BuildModRows();
                    modsScreen.SetEnabled(!loading); loadingLabel.EnableInClassList("hidden",!loading);
                    noticeLabel.text=notice; break;
                case View.Results: TickResults(); break;
            }
            brandSub.text=view==View.Mods?"MOD LIBRARY":"DUAL-HAND RHYTHM   ·   AUDIO REACTIVE";
            if(feel)
            {
                calibrateButton.text=calibrating?$"TAP SPACE ON THE CLICK  ({taps.Count}/12)  ·  CANCEL":"CALIBRATE LATENCY  ·  TAP TEST";
                saveLabel.style.opacity=Mathf.Clamp01(1.6f-saveAge);
            }
            if(view==View.Hud && music.Paused) pauseMenuButton.text=testing?"BACK TO EDITOR":"BACK TO MENU";
        }
        void TickResults()
        {
            // The score counts up and the rank lands with a punch.
            float t=Mathf.Clamp01(resultReveal/1.1f), ease=1-Mathf.Pow(1-t,3);
            resultScore.text=Mathf.RoundToInt(score.Score*ease).ToString("N0");
            float land=Mathf.Clamp01((resultReveal-.9f)/.35f);
            rankLabel.style.opacity=land; float s=1+(1-land)*1.2f; rankLabel.style.scale=new Scale(new Vector3(s,s,1));
        }
        void TickHud(float dt)
        {
            float motion=Settings.ReducedMotion?.4f:1;
            songTitle.text=SongName();
            songSub.text=testing?"SECTION TEST":activeMod!=null?(string.IsNullOrEmpty(activeMod.Artist)?"MOD":activeMod.Artist.ToUpperInvariant()):packagedSong?"BEATSAVER   ·   EXPERT":new[]{"INITIATE","ADEPT","VIRTUOSO"}[difficulty];
            hintLabel.text=testing?"ESC  back to editor     ·     R  repeat section":"ESC  pause     ·     F1  feel lab     ·     R  restart";

            // Score rolls up toward the real value and kicks on every hit.
            shownScore=Mathf.Abs(score.Score-shownScore)<1?score.Score:Mathf.Lerp(shownScore,score.Score,1-Mathf.Exp(-dt*14));
            scorePop*=Mathf.Exp(-dt*10);
            scoreLabel.text=Mathf.RoundToInt(shownScore).ToString("N0");
            float sp=1+scorePop*.1f*motion; scoreLabel.style.scale=new Scale(new Vector3(sp,sp,1));
            accuracyLabel.text=$"{score.Accuracy:0.0}%   ·   BEST COMBO {score.BestCombo}";

            float end=map && map.Notes.Count>0?map.Notes[map.Notes.Count-1].Timestamp:1;
            progressFill.style.width=Length.Percent(Mathf.Clamp01((float)music.SongTime/end)*100);
            heatFill.style.width=Length.Percent(stage.Heat*100);
            heatFill.style.backgroundColor=Color.Lerp(ArcaneVisuals.Crimson,Color.white,stage.Heat);

            // Combo: grows with heat, pops on hits, turns gold at the top multiplier.
            int mult=1+Mathf.Min(score.Combo/8,7); bool maxed=mult==8;
            comboBlock.style.opacity=score.Combo>0?1:0;
            comboLabel.text=score.Combo.ToString();
            float pop=Mathf.Exp(-hitAge*11), cs=1+(pop*.3f+stage.Heat*.18f)*motion;
            comboLabel.style.scale=new Scale(new Vector3(cs,cs,1));
            comboLabel.style.color=maxed?Color.Lerp(Gold,Color.white,pop):Color.white;
            comboCaption.text=$"C O M B O     ×{mult}";
            comboCaption.style.color=maxed?Gold:Soft;
            multFill.style.width=Length.Percent(maxed?100:score.Combo%8/8f*100);
            multFill.style.backgroundColor=maxed?Gold:ArcaneVisuals.Cyan;

            // Judgment: snaps in large, drifts up, fades; GREAT/GOOD say whether you were early or late.
            float age=judgmentAge; bool showJudgment=age<.7f;
            judgmentBlock.style.opacity=showJudgment?Mathf.Clamp01((.7f-age)/.25f):0;
            if(showJudgment)
            {
                string j=score.Judgment; judgmentLabel.text=j;
                judgmentLabel.style.color=j=="MISS"?ArcaneVisuals.Crimson:j=="PERFECT"?Color.white:j=="GREAT"?ArcaneVisuals.Cyan:Soft;
                float js=1+Mathf.Exp(-age*14)*.55f*motion; judgmentLabel.style.scale=new Scale(new Vector3(js,js,1));
                judgmentBlock.style.translate=new Translate(0,-age*34*motion);
                bool timed=j=="GREAT" || j=="GOOD";
                timingLabel.text=timed?(score.LastError<0?"EARLY":"LATE"):"";
            }

            // Milestones and combo breaks own the space above the combo.
            if(breakAge<1.1f)
            {
                milestoneLabel.text="COMBO BREAK"; milestoneLabel.style.color=ArcaneVisuals.Crimson;
                milestoneLabel.style.opacity=Mathf.Clamp01((1.1f-breakAge)/.4f);
                float shake=Mathf.Sin(breakAge*70)*Mathf.Exp(-breakAge*7)*16*motion;
                milestoneLabel.style.translate=new Translate(shake,0); milestoneLabel.style.scale=new Scale(Vector3.one);
            }
            else if(milestoneAge<1.5f)
            {
                milestoneLabel.text=$"{milestone}  COMBO"; milestoneLabel.style.color=Gold;
                milestoneLabel.style.opacity=Mathf.Clamp01((1.5f-milestoneAge)/.5f);
                float ms=1+Mathf.Exp(-milestoneAge*8)*.7f*motion;
                milestoneLabel.style.scale=new Scale(new Vector3(ms,ms,1)); milestoneLabel.style.translate=new Translate(0,-milestoneAge*18*motion);
            }
            else milestoneLabel.style.opacity=0;

            // Intro card: song title and a ready cue while the first notes fly in.
            float intro=introAge<.25f?introAge/.25f:Mathf.Clamp01((2.6f-introAge)/.7f);
            banner.style.opacity=intro;
            if(intro>0)
            {
                bannerTitle.text=SongName(); bannerSub.text=testing?"S E C T I O N   T E S T":"G E T   R E A D Y";
                banner.style.translate=new Translate(0,(1-intro)*20);
            }

            // Screen edges glow in the colour of the hand that just scored; both go red on a miss.
            float hitGlow=Mathf.Exp(-hitAge*7)*(hitPerfect?.55f:.32f), missGlow=Mathf.Exp(-missAge*5)*.6f;
            Color hitColor=hitMagic?ArcaneVisuals.Crimson:ArcaneVisuals.Cyan;
            SetEdge(edgeLeft,hitMagic?hitGlow:0,missGlow,hitColor);
            SetEdge(edgeRight,hitMagic?0:hitGlow,missGlow,hitColor);
        }
        static void SetEdge(VisualElement edge,float hit,float miss,Color hitColor)
        {
            bool red=miss>hit; float a=Mathf.Max(hit,miss);
            edge.style.opacity=a;
            edge.style.unityBackgroundImageTintColor=red?new Color(1,.04f,.08f):hitColor;
        }
    }
}
