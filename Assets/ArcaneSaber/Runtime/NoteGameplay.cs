using System.Collections.Generic;
using UnityEngine;

namespace ArcaneSaber
{
    public sealed class NoteView
    {
        public Transform Root; public RhythmNote Data; public bool Active;
        public Vector3 PreviousPosition;
        public Transform Arrow; public MeshFilter Key; public float Approach;
        public Transform Dot,Rune;
        public readonly Transform[] Models=new Transform[2];
        public Transform FaceBacking;
        public Renderer Core; public Renderer[] Edges;
    }
    public sealed class NoteSpawner
    {
        public readonly List<NoteView> Active = new List<NoteView>(64);
        readonly Queue<NoteView> pool = new Queue<NoteView>();
        readonly Transform parent; readonly Material arrowMaterial,runeMaterial;
        int index;
        public const float HitZ=1.2f;
        public const float HitY=1.65f;
        // Notes appear far down the tunnel, rise into their lane, and fly the last 30 m upright,
        // so letters and arrows are readable long before the gate.
        public const float SpawnDistance=62,LevelDistance=30;
        // A small shift inside the lane, so consecutive notes do not hide behind one another:
        // keys sit toward their letter (W up, A left...), and a laser cut starts on the side it swings from.
        public static Vector2 Offset(RhythmNote note)
        {
            if(note.Lane==0) return GestureMath.Direction(note.Direction)*.16f;
            return note.Direction==1?new Vector2(.14f,0):note.Direction==3?new Vector2(-.14f,0):Vector2.zero;
        }
        public NoteSpawner(Transform parent)
        {
            this.parent=parent;
            var arrow=ArcaneArt.Texture("NoteArrow"); var rune=ArcaneArt.Texture("NoteRune"); var face=ArcaneArt.Texture("NoteFace");
            arrowMaterial=arrow?ArcaneVisuals.Glow(arrow,ArcaneVisuals.Cyan*2.5f):null; runeMaterial=rune?ArcaneVisuals.Glow(rune,ArcaneVisuals.Crimson*1.6f):null;
            ArcaneArt.Paint(ArcaneVisuals.Surface(new Color(.26f,.035f,.09f),.25f),face,Vector2.one,0,new Color(1,.3f,.45f));
            ArcaneArt.Paint(ArcaneVisuals.Surface(new Color(.025f,.17f,.25f),.25f),face,Vector2.one,0,new Color(.35f,.85f,1));
            for(int i=0;i<72;i++) pool.Enqueue(Create());
        }
        NoteView Create()
        {
            var n=new NoteView(); n.Root=ArcaneVisuals.Empty("Pooled note",parent,Vector3.zero);
            // A lit beveled solid, twelve edge rails, and tilted faces expose real depth.
            var core=ArcaneVisuals.Shape("Beveled solid core",PrimitiveType.Cube,n.Root,Vector3.zero,Vector3.one,ArcaneVisuals.Surface(new Color(.16f,.07f,.12f),.3f));
            core.GetComponent<MeshFilter>().sharedMesh=ArcaneVisuals.BeveledCube; n.Core=core.GetComponent<Renderer>();
            n.Edges=new Renderer[12]; int rail=0;
            for(int axis=0;axis<3;axis++) for(int a=-1;a<=1;a+=2) for(int b=-1;b<=1;b+=2)
            {
                Vector3 p=axis==0?new Vector3(0,a*.244f,b*.244f):axis==1?new Vector3(a*.244f,0,b*.244f):new Vector3(a*.244f,b*.244f,0);
                Vector3 scale=Vector3.one*.016f; scale[axis]=.40f;
                var edge=ArcaneVisuals.Shape("Luminous depth rail",PrimitiveType.Cube,n.Root,p,scale,ArcaneVisuals.Material(Color.white,2));
                n.Edges[rail++]=edge.GetComponent<Renderer>();
            }
            n.Arrow=ArcaneVisuals.Empty("Directional arrow",n.Root,new Vector3(0,0,-.27f));
            ArcaneVisuals.Shape("Shaft",PrimitiveType.Cube,n.Arrow,new Vector3(0,-.025f,0),new Vector3(.035f,.22f,.018f),ArcaneVisuals.Material(ArcaneVisuals.Cyan,3));
            for(int side=-1;side<=1;side+=2)
            {
                var wing=ArcaneVisuals.Shape("Chevron",PrimitiveType.Cube,n.Arrow,new Vector3(side*.055f,.052f,0),new Vector3(.035f,.16f,.018f),ArcaneVisuals.Material(ArcaneVisuals.Cyan,3));
                wing.localRotation=Quaternion.Euler(0,0,side*45);
            }
            var key=ArcaneVisuals.Empty("WASD face glyph",n.Root,new Vector3(0,0,-.278f));
            n.Key=key.gameObject.AddComponent<MeshFilter>(); key.gameObject.AddComponent<MeshRenderer>().sharedMaterial=ArcaneVisuals.Material(Color.white,1.35f);
            n.Dot=ArcaneVisuals.Shape("Free cut pearl",PrimitiveType.Sphere,n.Root,new Vector3(0,0,-.272f),new Vector3(.09f,.09f,.035f),ArcaneVisuals.Material(Color.white,3));
            if(arrowMaterial)
            {
                foreach(Transform part in n.Arrow) part.gameObject.SetActive(false);
                var glyph=ArcaneVisuals.GlowQuad("Arrow art",n.Arrow,ArcaneVisuals.CenterQuad,arrowMaterial); glyph.localPosition=new Vector3(0,0,-.003f); glyph.localScale=Vector3.one*.42f;
            }
            if(runeMaterial) { n.Rune=ArcaneVisuals.GlowQuad("Rune art",n.Root,ArcaneVisuals.CenterQuad,runeMaterial); n.Rune.localPosition=new Vector3(0,0,-.268f); n.Rune.localScale=Vector3.one*.44f; }
            for(int side=-1;side<=1;side+=2)
            {
                ArcaneVisuals.Shape("Side inset",PrimitiveType.Cube,n.Root,new Vector3(side*.254f,0,0),new Vector3(.006f,.29f,.29f),ArcaneVisuals.Surface(new Color(.08f,.11f,.17f),.65f));
                for(int row=-1;row<=1;row++) ArcaneVisuals.Shape("Machined side vent",PrimitiveType.Cube,n.Root,new Vector3(side*.259f,row*.065f,0),new Vector3(.006f,.012f,.18f),ArcaneVisuals.Material(new Color(.025f,.05f,.08f)));
            }
            n.Root.gameObject.SetActive(false); return n;
        }
        public void Reset()
        { for(int i=Active.Count-1;i>=0;i--) Release(Active[i]); index=0; }
        // Start spawning part way through a chart, for section tests and editor scrubbing.
        public void SkipTo(BeatmapController map,double time)
        { index=0; while(index<map.Notes.Count && map.Notes[index].Timestamp<time) index++; }
        public void ReleasePassed(double time)
        { for(int i=Active.Count-1;i>=0;i--) if(Active[i].Data.Timestamp<time) Release(Active[i]); }
        public void Update(BeatmapController map, double time)
        {
            // Sorted custom charts may use different speeds; inspect every upcoming note
            // within maximum lead. Notes move from absolute song time, never accumulated dt.
            while(index<map.Notes.Count && map.Notes[index].Timestamp-time <= ApproachTime(map,map.Notes[index]))
            {
                var data=map.Notes[index++]; if(pool.Count==0) pool.Enqueue(Create());
                var n=pool.Dequeue(); n.Data=data; n.Active=true; n.Approach=ApproachTime(map,data);
                n.Root.gameObject.SetActive(true); n.Root.localScale=Vector3.one;
                n.Arrow.gameObject.SetActive(data.Lane==1 && data.Direction!=8);
                n.Dot.gameObject.SetActive(data.Lane==1 && data.Direction==8); n.Key.gameObject.SetActive(data.Lane==0);
                if(!n.Models[data.Lane]) n.Models[data.Lane]=ArcaneArt.Model(data.Lane==0?"MagicCube":"SaberCube",n.Root,Vector3.zero,.50f,false,false);
                if(n.Models[1-data.Lane]) n.Models[1-data.Lane].gameObject.SetActive(false);
                bool imported=n.Models[data.Lane]!=null;
                if(imported) n.Models[data.Lane].gameObject.SetActive(true);
                n.Core.enabled=!imported;
                foreach(var edge in n.Edges) edge.enabled=!imported;
                foreach(Transform child in n.Root) if(child.name=="Side inset" || child.name=="Machined side vent") child.gameObject.SetActive(!imported);
                if(imported && !n.FaceBacking) n.FaceBacking=ArcaneVisuals.Shape("Glyph display",PrimitiveType.Cube,n.Root,new Vector3(0,0,-.254f),new Vector3(.4f,.4f,.008f),ArcaneVisuals.Surface(new Color(.034f,.039f,.05f)));
                if(n.FaceBacking) n.FaceBacking.gameObject.SetActive(imported);
                if(n.Rune) n.Rune.gameObject.SetActive(data.Lane==0);
                if(data.Lane==0) n.Key.sharedMesh=KeyGlyph.Get(data.Direction);
                else if(data.Direction!=8) { Vector2 d=GestureMath.Direction(data.Direction); n.Arrow.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(-d.x,d.y)*Mathf.Rad2Deg); }
                Color color=data.Lane==0?ArcaneVisuals.Crimson:ArcaneVisuals.Cyan;
                foreach(var edge in n.Edges) edge.sharedMaterial=ArcaneVisuals.Material(color,2.3f);
                n.Core.sharedMaterial=ArcaneVisuals.Surface(data.Lane==0?new Color(.26f,.035f,.09f):new Color(.025f,.17f,.25f),.25f);
                n.Root.localRotation=Quaternion.Euler(-10,data.Lane==0?-17:17,data.Lane==0?-3:3);
                Position(n,time); n.PreviousPosition=n.Root.position; Active.Add(n);
            }
            foreach(var n in Active) { n.PreviousPosition=n.Root.position; Position(n,time); }
        }
        static float ApproachTime(BeatmapController map,RhythmNote note)
        {
            float beat=60/Mathf.Max(1,map.BPM),halfBeats=4;
            while(note.TravelSpeed*beat*halfBeats>18) halfBeats*=.5f;
            return Mathf.Max(1,halfBeats+map.JumpOffset)*beat+.35f;
        }
        static void Position(NoteView n,double time)
        {
            Vector2 offset=Offset(n.Data); float x=(n.Data.Lane==0?-.70f:.65f)+offset.x;
            float remaining=(float)(n.Data.Timestamp-time);
            // Preserve approach speed, then smoothly settle within reach during late grace.
            // This keeps a forgiven late note in front of the player instead of behind them.
            float z=remaining>=0?HitZ+remaining*n.Data.TravelSpeed:HitZ-.38f*(1-Mathf.Exp(remaining*n.Data.TravelSpeed/.38f));
            // Notes launch from the floor far away and lock into their lane before the reading zone.
            float age=n.Approach-remaining;
            float jump=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.30f));
            n.Root.position=new Vector3(x*(1+jump*.45f),HitY+offset.y-jump*.18f,z+jump*2.4f);
            n.Root.localScale=Vector3.one*(1-jump*.55f);
            n.Root.localRotation=Quaternion.Euler(-8-jump*15,(n.Data.Lane==0?-1:1)*(12+jump*95),0);
        }
        public void Release(NoteView n)
        { if(!n.Active) return; n.Active=false; Active.Remove(n); n.Root.gameObject.SetActive(false); pool.Enqueue(n); }
    }

    public static class SaberSliceDetector
    {
        // Sweep the whole blade, not just its tip, against a moving expanded box.
        // Adaptive sampling bounds travel per substep. Relative note motion is included.
        public static bool Intersects(SaberSpringController saber, NoteView note,float padding=0,float depthPadding=0)
        {
            return Sweep(saber.PreviousBase,saber.PreviousTip,saber.Base,saber.Tip,note.PreviousPosition,note.Root.position,padding,depthPadding);
        }
        public static bool Sweep(Vector3 oldBase,Vector3 oldTip,Vector3 newBase,Vector3 newTip,Vector3 oldCenter,Vector3 center,float padding=0,float depthPadding=0)
        {
            float travel=Mathf.Max((newTip-oldTip).magnitude,(newBase-oldBase).magnitude)+(center-oldCenter).magnitude;
            int steps=Mathf.Max(1,Mathf.CeilToInt(travel/.035f));
            for(int i=0;i<=steps;i++)
            {
                float t=(float)i/steps;
                Vector3 c=Vector3.Lerp(oldCenter,center,t);
                Vector3 a=Vector3.Lerp(oldBase,newBase,t)-c;
                Vector3 b=Vector3.Lerp(oldTip,newTip,t)-c;
                if(SegmentBox(a,b,new Vector3(.32f+padding,.32f+padding,.32f+depthPadding))) return true;
            }
            return false;
        }
        static bool SegmentBox(Vector3 a,Vector3 b,Vector3 extents)
        {
            Vector3 d=b-a; float near=0,far=1;
            for(int axis=0;axis<3;axis++)
            {
                if(Mathf.Abs(d[axis])<.000001f) { if(Mathf.Abs(a[axis])>extents[axis]) return false; }
                else
                {
                    float u=(-extents[axis]-a[axis])/d[axis],v=(extents[axis]-a[axis])/d[axis];
                    if(u>v) { float temp=u; u=v; v=temp; }
                    near=Mathf.Max(near,u); far=Mathf.Min(far,v); if(near>far) return false;
                }
            }
            return true;
        }
    }

    public sealed class NoteHitEvaluator
    {
        public float InputLatencyMilliseconds;
        public GameFeelSettings AssistSettings;
        readonly NoteSpawner spawner; readonly ScoreManager score;
        readonly double[] bufferedPress={double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity};
        NoteView guidedNote; float consumedMotion=float.NegativeInfinity;
        public System.Action<NoteView,float,Vector2> Hit;
        public System.Action<NoteView> Missed;
        public NoteHitEvaluator(NoteSpawner spawner,ScoreManager score) { this.spawner=spawner; this.score=score; }
        public void Magic(int key,double time)
        {
            if(key<0 || key>3) return;
            bufferedPress[key]=time;
            TryMagic(key,time);
        }
        void TryMagic(int key,double time)
        {
            NoteView best=null; float error=float.MaxValue;
            foreach(var n in spawner.Active)
            {
                if(n.Data.Lane!=0 || n.Data.Direction!=key) continue;
                float e=(float)(time-n.Data.Timestamp)-InputLatencyMilliseconds*.001f;
                if(Mathf.Abs(e)<=RhythmAssist.Window(n.Data,AssistSettings) && Mathf.Abs(e)<Mathf.Abs(error)) { best=n; error=e; }
            }
            if(best!=null) { bufferedPress[key]=double.NegativeInfinity; Resolve(best,error,GestureMath.Direction(key)); }
            // Stray presses provide gesture feedback but never silently delete another note.
        }
        public void BufferedMagic(double time)
        {
            float buffer=AssistSettings?AssistSettings.MagicTimingAssist*.12f:0;
            for(int key=0;key<4;key++) if(buffer>0 && time>=bufferedPress[key] && time-bufferedPress[key]<=buffer) TryMagic(key,time);
        }
        public void GuideSaber(SaberSpringController saber,double time)
        {
            guidedNote=null; float strength=AssistSettings?AssistSettings.SaberAssist:0;
            if(strength<=0 || saber.Input.MotionTime<=consumedMotion) return;
            float nearest=float.MaxValue;
            foreach(var n in spawner.Active)
            {
                if(n.Data.Lane!=1 || !RhythmAssist.MatchesIntent(n.Data.Direction,saber.Input.IntentVelocity,strength)) continue;
                float error=Mathf.Abs((float)(time-n.Data.Timestamp)-InputLatencyMilliseconds*.001f);
                if(error>RhythmAssist.Window(n.Data,AssistSettings) || error>=nearest) continue;
                guidedNote=n; nearest=error;
            }
            if(guidedNote!=null)
            {
                int direction=guidedNote.Data.Direction;
                saber.GuideTowards(guidedNote.Root.position,direction==8?saber.Input.IntentVelocity.normalized:GestureMath.Direction(direction),strength*((direction==1 || direction==3)?.45f:1));
            }
        }
        public void Saber(SaberSpringController saber,double time)
        {
            if(!saber.HasSweep) return;
            Vector2 movement=new Vector2(saber.Velocity.x,saber.Velocity.y);
            float strength=AssistSettings?AssistSettings.SaberAssist:0;
            for(int i=spawner.Active.Count-1;i>=0;i--)
            {
                var n=spawner.Active[i]; if(n.Data.Lane!=1) continue;
                float error=(float)(time-n.Data.Timestamp)-InputLatencyMilliseconds*.001f;
                if(Mathf.Abs(error)>RhythmAssist.Window(n.Data,AssistSettings)) continue;
                bool strict=movement.magnitude>=.9f && (n.Data.Direction==8 || Vector2.Dot(movement.normalized,GestureMath.Direction(n.Data.Direction))>=.64f) && SaberSliceDetector.Intersects(saber,n);
                bool assisted=n==guidedNote && saber.Input.MotionTime>consumedMotion && RhythmAssist.MatchesIntent(n.Data.Direction,saber.Input.IntentVelocity,strength)
                    && SaberSliceDetector.Intersects(saber,n,.23f*strength,n.Data.TravelSpeed*.105f*strength);
                if(strict || assisted)
                {
                    Vector2 cut=assisted?(n.Data.Direction==8?saber.Input.IntentVelocity.normalized:GestureMath.Direction(n.Data.Direction)):movement.normalized;
                    if(assisted) saber.CompleteAssistedSwing(cut,strength);
                    consumedMotion=saber.Input.MotionTime; guidedNote=null;
                    Resolve(n,error,cut);
                }
            }
        }
        void Resolve(NoteView n,float error,Vector2 direction)
        { score.Hit(error,RhythmAssist.Window(n.Data,AssistSettings)); Hit?.Invoke(n,error,direction); spawner.Release(n); }
        public void Expire(double time)
        {
            for(int i=spawner.Active.Count-1;i>=0;i--)
            {
                var n=spawner.Active[i];
                if(time-n.Data.Timestamp-InputLatencyMilliseconds*.001f>RhythmAssist.Window(n.Data,AssistSettings)) { score.Miss(); Missed?.Invoke(n); spawner.Release(n); }
            }
        }
    }


}
