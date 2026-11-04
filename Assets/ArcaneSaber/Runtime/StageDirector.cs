using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ArcaneSaber
{
    // Runs the arena: live audio reaction, timed mod cues, and the player's combo heat
    // all feed one brightness model, so a cue and a kick drum read as the same light rig.
    public sealed class StageDirector : MonoBehaviour
    {
        public static readonly Color[][] Palettes=
        {
            new[]{ArcaneVisuals.Crimson,ArcaneVisuals.Cyan},
            new[]{new Color(.75f,.1f,1),new Color(1,.62f,.08f)},
            new[]{new Color(.1f,1,.42f),new Color(1,.08f,.72f)},
            new[]{new Color(.2f,.45f,1),new Color(.8f,.95f,1)},
            new[]{new Color(1,.1f,.03f),new Color(1,.5f,.05f)},
            new[]{new Color(1,1,1),new Color(.6f,.8f,1)},
        };
        public static readonly string[] PaletteNames={"Crimson / Cyan","Purple / Gold","Green / Pink","Blue / Ice","Red / Orange","White"};
        const int BeamCount=12,RingCount=16,StreakCount=90,WaveCount=6;
        class Beam { public Transform Shaft,Flare; public Material ShaftMaterial,FlareMaterial; public Light Spot; public Vector3 Origin,Direction; public int Side; }
        class Wave { public LineRenderer Line; public Material Material; public float Z=-1; }
        public Color Overlay { get; private set; }
        public float Heat { get; private set; }
        public MusicConductor Music; public GameFeelSettings Settings; public ProceduralCameraController Camera;
        public Bloom BloomEffect; public ChromaticAberration Chroma; public Vignette VignetteEffect; public Fog FogEffect;
        readonly float[] spectrum=new float[512]; readonly float[] levels=new float[24];
        readonly Transform[] bars=new Transform[48]; readonly Beam[] beams=new Beam[BeamCount];
        readonly Transform[] rings=new Transform[RingCount]; readonly Material[] ringMaterials=new Material[RingCount]; readonly float[] ringAngles=new float[RingCount];
        readonly Transform[] streaks=new Transform[StreakCount]; readonly float[] streakSpeed=new float[StreakCount];
        readonly Wave[] waves=new Wave[WaveCount]; int nextWave;
        Material neonA,neonB,railA,railB,horizonMaterial; Transform horizon,head;
        readonly Transform[] veils=new Transform[2];
        readonly Material[] skyLight=new Material[2],floorLight=new Material[2];
        List<StageEvent> events; int cursor; double lastTime;
        int spotMode; float energyLevel=.8f,lightPower=1,cameraPower=1,bloomPower=1;
        Color colorA=ArcaneVisuals.Crimson,colorB=ArcaneVisuals.Cyan; int palette;
        public SongAnalysis Song { get; private set; }
        int accentFrame=-1; float loudest=1,loudness=.6f,snare;
        float kick,flash,hitFlash,missDip,bassAverage=.01f,lastKick,ringTarget,ringWave=99,blackout,strobe,strobeRate=14,spin=1,comboHeat; Color hitColor;
        public void Initialize(MusicConductor music,GameFeelSettings settings,Transform viewpoint)
        {
            Music=music; Settings=settings; head=viewpoint;
            neonA=ArcaneVisuals.LiveNeon(colorA); neonB=ArcaneVisuals.LiveNeon(colorB); railA=ArcaneVisuals.LiveNeon(colorA); railB=ArcaneVisuals.LiveNeon(colorB);
            for(int side=0;side<2;side++) for(int i=0;i<24;i++)
                bars[side*24+i]=ArcaneVisuals.Shape("Spectrum column",PrimitiveType.Cube,transform,new Vector3(side==0?-4.5f:4.5f,.1f,6+i*1.2f),new Vector3(.08f,.2f,.5f),side==0?neonA:neonB);
            for(int i=0;i<BeamCount;i++)
            {
                var beam=beams[i]=new Beam{Side=i%2};
                float along=i/2/(BeamCount/2-1f),side=beam.Side==0?-1:1;
                beam.Origin=new Vector3(side*Mathf.Lerp(2.2f,9.5f,along),Mathf.Lerp(8,3.2f,along),Mathf.Lerp(50,30,along));
                beam.Direction=(new Vector3(0,1.6f,0)-beam.Origin).normalized;
                beam.ShaftMaterial=ArcaneVisuals.Glow(ArcaneVisuals.BeamTexture,Color.black);
                beam.FlareMaterial=ArcaneVisuals.Glow(ArcaneVisuals.FlareTexture,Color.black);
                beam.Shaft=ArcaneVisuals.GlowQuad("Light shaft",transform,ArcaneVisuals.ShaftMesh,beam.ShaftMaterial);
                beam.Flare=ArcaneVisuals.GlowQuad("Lens flare",transform,ArcaneVisuals.CenterQuad,beam.FlareMaterial);
                if(i%3==0)
                {
                    var t=ArcaneVisuals.Empty("Stage spotlight",transform,beam.Origin);
                    beam.Spot=t.gameObject.AddComponent<Light>(); beam.Spot.type=LightType.Spot; beam.Spot.range=70; beam.Spot.spotAngle=26; beam.Spot.shadows=LightShadows.None;
                    t.gameObject.AddComponent<HDAdditionalLightData>(); beam.Spot.lightUnit=LightUnit.Lumen;
                }
            }
            for(int i=0;i<RingCount;i++)
            {
                rings[i]=ArcaneVisuals.Empty("Neon ring",transform,new Vector3(0,3.1f,7+i*4.2f));
                var line=ArcaneVisuals.Line("Octagon",rings[i],Color.white,.085f,8); line.loop=true;
                if(i%4==1 && ArcaneArt.Model("Gate",transform,new Vector3(0,5.5f,7+i*4.2f),14,false,false)) line.enabled=false;
                ringMaterials[i]=ArcaneVisuals.LiveNeon(colorA); line.sharedMaterial=ringMaterials[i];
                for(int p=0;p<8;p++) { float a=(p+.5f)*Mathf.PI/4; line.SetPosition(p,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*(i%2==0?8.2f:7.4f)); }
            }
            for(int s=-1;s<=1;s+=2) foreach(var (x,y) in new[]{(2.3f,.03f),(6.6f,6.4f)})
            {
                var rail=ArcaneVisuals.Line("Neon rail",transform,Color.white,.05f,2); rail.sharedMaterial=s<0?railA:railB;
                rail.SetPositions(new[]{new Vector3(s*x,y,-1),new Vector3(s*x,y,74)});
            }
            for(int i=0;i<StreakCount;i++)
            {
                float a=Random.value*Mathf.PI*2,r=Random.Range(4,13f);
                streaks[i]=ArcaneVisuals.Shape("Speed streak",PrimitiveType.Cube,transform,new Vector3(Mathf.Cos(a)*r,3+Mathf.Sin(a)*r*.6f,Random.Range(0,70f)),new Vector3(.018f,.018f,.6f),i%2==0?railA:railB);
                streakSpeed[i]=Random.Range(.7f,1.3f);
            }
            for(int i=0;i<WaveCount;i++)
            {
                waves[i]=new Wave{Line=ArcaneVisuals.Line("Floor wave",transform,Color.white,.09f,2),Material=ArcaneVisuals.LiveNeon(colorA)};
                waves[i].Line.sharedMaterial=waves[i].Material; waves[i].Line.useWorldSpace=true; waves[i].Line.enabled=false;
            }
            horizonMaterial=ArcaneVisuals.Glow(ArcaneVisuals.FlareTexture,Color.black);
            horizon=ArcaneVisuals.GlowQuad("Horizon glow",transform,ArcaneVisuals.CenterQuad,horizonMaterial);
            horizon.position=new Vector3(0,3,72); horizon.localScale=new Vector3(46,34,1);
            for(int i=0;i<2;i++)
            {
                float side=i==0?-1:1;
                skyLight[i]=ArcaneVisuals.Glow(ArcaneVisuals.Aurora,Color.black);
                skyLight[i].DisableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
                skyLight[i].SetFloat("_EnableFogOnTransparent",0);
                veils[i]=ArcaneVisuals.GlowQuad("Distant aurora",transform,ArcaneVisuals.CenterQuad,skyLight[i]);
                veils[i].localScale=new Vector3(145,65,1);
                veils[i].localPosition=new Vector3(side*62,20,92-i*6);
                floorLight[i]=ArcaneVisuals.Glow(ArcaneVisuals.FloorReflection,Color.black);
                var reflection=ArcaneVisuals.GlowQuad("Runway light spill",transform,ArcaneVisuals.CenterQuad,floorLight[i]);
                reflection.localRotation=Quaternion.Euler(90,0,0);
                reflection.localScale=new Vector3(5.4f,74,1);
                reflection.localPosition=new Vector3(side*3.3f,-.018f,33);
            }
        }
        // Applies a mod's cues; persistent state (palette, spots, level) is rebuilt at the current time.
        // With a song analysis the lights follow its kicks and snares at the exact song time,
        // instead of guessing from the live spectrum a frame late.
        public void Load(List<StageEvent> cues,float lights,float cameraPower,float bloom,SongAnalysis song=null)
        {
            events=cues; lightPower=lights; this.cameraPower=cameraPower; bloomPower=bloom;
            if(song!=Song)
            {
                Song=song; loudest=.0001f;
                if(song!=null) { var sorted=(float[])song.Energy.Clone(); System.Array.Sort(sorted); loudest=Mathf.Max(.0001f,sorted[(int)(sorted.Length*.97f)]); }
            }
            accentFrame=-1;
            Rescan(Music?Music.SongTime:0);
        }
        void Rescan(double time)
        {
            cursor=0; spotMode=0; energyLevel=.8f; palette=0;
            if(events!=null) for(;cursor<events.Count && events[cursor].Time<=time;cursor++)
            {
                var e=events[cursor]; var kind=(StageKind)e.Kind;
                if(kind==StageKind.SpotSweep || kind==StageKind.SpotCamera || kind==StageKind.SpotFan) spotMode=kind-StageKind.SpotSweep;
                else if(kind==StageKind.Palette) palette=(int)e.Value;
                else if(kind==StageKind.Energy) energyLevel=e.Value;
            }
            lastTime=time;
        }
        void Fire(StageEvent e)
        {
            bool calm=Settings.ReducedMotion;
            switch((StageKind)e.Kind)
            {
                case StageKind.Flash: flash=Mathf.Max(flash,e.Value*(calm?.4f:1)); break;
                case StageKind.Strobe: if(!calm) { strobe=e.Duration; strobeRate=Mathf.Lerp(6,18,e.Value); } break;
                case StageKind.Blackout: blackout=e.Duration; break;
                case StageKind.SpotSweep: case StageKind.SpotCamera: case StageKind.SpotFan: spotMode=e.Kind-(int)StageKind.SpotSweep; break;
                case StageKind.Palette: palette=(int)e.Value; break;
                case StageKind.RingSpin: spin=-spin; ringTarget+=spin*(30+e.Value*105); ringWave=0; break;
                case StageKind.CameraKick: if(Camera) Camera.Kick(e.Value*cameraPower); break;
                case StageKind.CameraZoom: if(Camera) Camera.Zoom(e.Value*cameraPower); break;
                case StageKind.CameraRoll: if(Camera) Camera.Roll(e.Value*cameraPower); break;
                case StageKind.Energy: energyLevel=e.Value; break;
            }
        }
        public void Hit(bool magic,bool perfect)
        {
            hitFlash=perfect?1:.6f; hitColor=magic?ArcaneVisuals.Crimson:ArcaneVisuals.Cyan;
            if(perfect) kick=Mathf.Max(kick,.6f);
        }
        public void Miss() { missDip=1; comboHeat*=.45f; hitFlash=.8f; hitColor=new Color(1,.04f,.04f); }
        public void SetCombo(int combo) { comboHeat=Mathf.MoveTowards(comboHeat,Mathf.Clamp01(combo/40f),.04f); }
        void Pulse()
        {
            var wave=waves[nextWave++%WaveCount]; wave.Z=70; wave.Line.enabled=true;
        }
        void Update()
        {
            if(!Music) return;
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f),clock=Time.unscaledTime;
            bool live=Music.Playing && !Music.Paused;
            if(live)
            {
                double now=Music.SongTime;
                if(now<lastTime-.02 || now>lastTime+.5) Rescan(now);
                else if(events!=null) while(cursor<events.Count && events[cursor].Time<=now) Fire(events[cursor++]);
                lastTime=now;
            }
            else if(Music.Playing && Music.Paused && System.Math.Abs(Music.SongTime-lastTime)>.001) Rescan(Music.SongTime);
            Music.Spectrum(spectrum);
            float bass=0; for(int i=1;i<9;i++) bass+=spectrum[i];
            bassAverage=Mathf.Lerp(bassAverage,bass,1-Mathf.Exp(-dt*3));
            if(live && Song!=null)
            {
                int now=Mathf.Clamp((int)(Music.SongTime*SongAnalysis.Rate),0,Song.Accents.Length-1);
                if(accentFrame<0 || now<accentFrame-1 || now>accentFrame+30) accentFrame=now;
                for(;accentFrame<=now;accentFrame++)
                {
                    byte accent=Song.Accents[accentFrame];
                    if((accent&SongAnalysis.Kick)!=0) { kick=Mathf.Max(kick,.45f+Song.Low[accentFrame]*.55f); lastKick=clock; Pulse(); }
                    if((accent&SongAnalysis.Snare)!=0) { snare=Mathf.Max(snare,.5f+Song.Mid[accentFrame]*.5f); ringWave=0; }
                }
                loudness=Mathf.Lerp(loudness,Mathf.Clamp01(Song.Energy[now]/loudest),1-Mathf.Exp(-dt*5));
            }
            else
            {
                if(live && bass>bassAverage*1.5f && bass>.012f && clock-lastKick>.14f) { lastKick=clock; kick=1; Pulse(); }
                loudness=Mathf.Lerp(loudness,.6f,1-Mathf.Exp(-dt*2));
            }
            kick*=Mathf.Exp(-dt*9); snare*=Mathf.Exp(-dt*10); flash*=Mathf.Exp(-dt*6); hitFlash*=Mathf.Exp(-dt*11); missDip*=Mathf.Exp(-dt*5);
            blackout=Mathf.Max(0,blackout-dt); strobe=Mathf.Max(0,strobe-dt);
            comboHeat*=live?1:Mathf.Exp(-dt);
            Heat=Mathf.Lerp(Heat,comboHeat,1-Mathf.Exp(-dt*4));
            var target=Palettes[Mathf.Clamp(palette,0,Palettes.Length-1)];
            float blend=1-Mathf.Exp(-dt*6);
            colorA=Color.Lerp(colorA,target[0],blend); colorB=Color.Lerp(colorB,target[1],blend);
            // Scene brightness: level + beat + heat, then cues that cut or spike it.
            float strobeGate=strobe>0?(Mathf.Repeat(clock*strobeRate,1)<.5f?1.8f:.05f):1;
            float idle=live?0:.35f+.1f*Mathf.Sin(clock*1.6f);
            float light=Mathf.Min(1.3f,lightPower*Settings.StageBrightness*strobeGate*(blackout>0?.08f:1)*(1-missDip*.55f)*Mathf.Max(idle,energyLevel*(.25f+.45f*loudness+.25f*Heat)+kick*.55f+snare*.3f+flash*.7f));
            Bars(dt); Beams(dt,clock,light); Rings(dt,clock,light); Streaks(dt,live,light); Waves(dt,light);
            railA.SetColor("_UnlitColor",colorA*(1.1f+light*1.6f)); railB.SetColor("_UnlitColor",colorB*(1.1f+light*1.6f));
            neonA.SetColor("_UnlitColor",colorA*(.3f+light*.6f)); neonB.SetColor("_UnlitColor",colorB*(.3f+light*.6f));
            horizonMaterial.SetColor("_UnlitColor",Color.Lerp(colorA,colorB,.5f+.5f*Mathf.Sin(clock*.3f))*(.07f+light*.2f));
            float scale=1+kick*.08f; horizon.localScale=new Vector3(46*scale,34*scale,1);
            for(int i=0;i<2;i++)
            {
                float side=i==0?-1:1;
                Color tint=i==0?colorA:colorB;
                float drift=Settings.ReducedMotion?0:Mathf.Sin((float)Music.SongTime*.12f+i)*1.8f;
                veils[i].localPosition=new Vector3(side*62+drift,20+drift*.4f,92-i*6);
                skyLight[i].SetColor("_UnlitColor",Color.Lerp(tint,new Color(.20f,.13f,.42f),.32f)*(.24f+light*.46f));
                floorLight[i].SetColor("_UnlitColor",tint*(.16f+light*.32f+kick*.10f));
            }
            if(BloomEffect!=null) BloomEffect.intensity.Override(bloomPower*Mathf.Clamp(.4f+Heat*.12f+kick*.15f+flash*.15f,0,.7f));
            if(Chroma!=null) Chroma.intensity.Override(Settings.ReducedMotion?0:Mathf.Clamp01(.05f+kick*.32f+hitFlash*.2f+missDip*.5f));
            if(VignetteEffect!=null)
            {
                VignetteEffect.intensity.Override(Mathf.Clamp01(.3f+missDip*.25f-kick*.08f));
                VignetteEffect.color.Override(Color.Lerp(Color.black,new Color(.5f,0,.04f),missDip));
            }
            if(FogEffect!=null) FogEffect.color.Override(Color.Lerp(new Color(.016f,.022f,.05f),Color.Lerp(colorA,colorB,.5f)*.22f,Mathf.Clamp01(light*.55f)));
            var overlay=Color.Lerp(colorA,Color.white,.4f); overlay.a=Mathf.Clamp(flash*.06f+(strobe>0 && strobeGate>1?.05f:0),0,.08f);
            if(hitFlash>overlay.a*2) { overlay=hitColor; overlay.a=hitFlash*.05f; }
            Overlay=overlay;
        }
        void Bars(float dt)
        {
            for(int i=0;i<24;i++)
            {
                int bin=Mathf.Clamp(Mathf.RoundToInt(Mathf.Pow(400,i/23f)),1,500);
                float value=Mathf.Clamp01(Mathf.Sqrt(spectrum[bin])*7);
                levels[i]=Mathf.MoveTowards(levels[i],value,dt*(value>levels[i]?14:2.5f));
                float h=.12f+levels[i]*2.6f;
                for(int side=0;side<2;side++) { var t=bars[side*24+i]; t.localScale=new Vector3(.08f,h,.5f); var p=t.localPosition; p.y=h*.5f; t.localPosition=p; }
            }
        }
        void Beams(float dt,float clock,float light)
        {
            Vector3 eye=head.position; bool calm=Settings.ReducedMotion;
            for(int i=0;i<BeamCount;i++)
            {
                var b=beams[i]; float side=b.Side==0?-1:1,phase=clock*(calm?.2f:.85f)+i*.9f; Vector3 aim;
                if(spotMode==1 && !calm)
                {
                    // Straight down the lens: a slow wobble lets each shaft cross the eye.
                    aim=eye+new Vector3(Mathf.Sin(phase*1.4f)*3.2f,Mathf.Cos(phase*1.1f)*2.2f+.6f,0);
                }
                else if(spotMode==2)
                {
                    float a=(i-(BeamCount-1)*.5f)*.32f+Mathf.Sin(clock*.5f)*.5f;
                    aim=new Vector3(Mathf.Sin(a)*22,Mathf.Cos(a)*16+2,0);
                }
                else aim=new Vector3(side*(5+Mathf.Sin(phase)*9),.4f+Mathf.Abs(Mathf.Sin(phase*.7f))*3,Mathf.Sin(phase*.6f)*8+4);
                b.Direction=Vector3.Slerp(b.Direction,(aim-b.Origin).normalized,1-Mathf.Exp(-dt*7));
                Color color=(b.Side==0?colorA:colorB);
                float strength=(.18f+light*1.05f)*(i%4==0?1.25f:1);
                Vector3 toEye=(eye-b.Origin).normalized;
                float facing=Mathf.Clamp01(Vector3.Dot(b.Direction,toEye));
                // The closer a shaft points at the lens, the harder its source flares.
                float glare=Mathf.Pow(facing,90),blinding=Mathf.Pow(facing,40);
                b.ShaftMaterial.SetColor("_UnlitColor",color*strength*(1-blinding*.8f));
                b.FlareMaterial.SetColor("_UnlitColor",Color.Lerp(color,Color.white,glare*.7f)*(.06f+light*(.16f+glare*.45f)));
                // Billboard around the shaft's own axis; a dead-on shaft falls back to any side axis.
                Vector3 viewAxis=Vector3.Cross(b.Direction,eye-b.Origin);
                if(viewAxis.sqrMagnitude<.0001f) viewAxis=Vector3.Cross(b.Direction,Vector3.up);
                Vector3 normal=Vector3.Cross(viewAxis.normalized,b.Direction);
                b.Shaft.SetPositionAndRotation(b.Origin,Quaternion.LookRotation(normal,b.Direction));
                b.Shaft.localScale=new Vector3(2.4f+light*1.3f,62,1);
                float size=2+light*1.2f+glare*2.5f;
                b.Flare.SetPositionAndRotation(b.Origin,Quaternion.LookRotation(b.Origin-eye,head.up));
                b.Flare.localScale=new Vector3(size*1.8f,size,1);
                if(b.Spot)
                {
                    b.Spot.transform.rotation=Quaternion.LookRotation(b.Direction);
                    b.Spot.color=color; b.Spot.intensity=Mathf.Min(light,3)*9000;
                }
            }
        }
        void Rings(float dt,float clock,float light)
        {
            ringWave+=dt*26;
            for(int i=0;i<RingCount;i++)
            {
                // Later rings trail the target, turning each spin into a travelling twist.
                float rate=Mathf.Lerp(9,2.2f,i/(RingCount-1f));
                ringAngles[i]=Mathf.Lerp(ringAngles[i],ringTarget+(i-RingCount*.5f)*(Settings.ReducedMotion?0:1.6f),1-Mathf.Exp(-dt*rate));
                rings[i].localRotation=Quaternion.Euler(0,0,ringAngles[i]+clock*(i%2==0?3:-3));
                float crest=Mathf.Exp(-Mathf.Pow((i-ringWave)*.55f,2));
                Color color=Color.Lerp(colorA,colorB,(Mathf.Sin(i*.5f+clock*.8f)+1)*.5f);
                ringMaterials[i].SetColor("_UnlitColor",color*(.25f+light*1.5f+crest*3));
            }
        }
        void Streaks(float dt,bool live,float light)
        {
            float speed=(live?9+Heat*22+kick*26:2.5f)*dt;
            for(int i=0;i<StreakCount;i++)
            {
                var p=streaks[i].localPosition; p.z-=speed*streakSpeed[i]; if(p.z<-1) p.z+=71; streaks[i].localPosition=p;
                streaks[i].localScale=new Vector3(.018f,.018f,.5f+speed*18*streakSpeed[i]);
            }
        }
        void Waves(float dt,float light)
        {
            foreach(var wave in waves)
            {
                if(wave.Z<0) continue; wave.Z-=dt*62;
                if(wave.Z<0) { wave.Line.enabled=false; continue; }
                wave.Line.SetPositions(new[]{new Vector3(-2.3f,.03f,wave.Z),new Vector3(2.3f,.03f,wave.Z)});
                wave.Material.SetColor("_UnlitColor",Color.Lerp(colorA,colorB,wave.Z/70)*(1+light*2));
            }
        }
    }
}
