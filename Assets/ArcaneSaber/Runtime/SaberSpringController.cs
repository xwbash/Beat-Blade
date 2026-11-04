using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcaneSaber
{
    // Blade-wide swept ribbon: the arc of the whole sword, not a thin line from its tip.
    public sealed class BladeRibbon
    {
        const int Samples=30; const float Lifetime=.10f;
        readonly Vector3[] roots=new Vector3[Samples],tips=new Vector3[Samples],vertices=new Vector3[Samples*2];
        readonly float[] ages=new float[Samples]; readonly Vector2[] uv=new Vector2[Samples*2];
        readonly Mesh mesh=new Mesh{name="Sword ribbon"}; public readonly Material Material; int head;
        public BladeRibbon(Transform parent)
        {
            Material=ArcaneVisuals.Glow(ArcaneVisuals.RibbonTexture,ArcaneVisuals.Cyan);
            var t=ArcaneVisuals.GlowQuad("Sword ribbon",parent,mesh,Material); t.position=Vector3.zero; t.rotation=Quaternion.identity;
            var triangles=new int[(Samples-1)*6];
            for(int i=0;i<Samples-1;i++)
            {
                int v=i*2,n=i*6;
                triangles[n]=v;triangles[n+1]=v+1;triangles[n+2]=v+2;triangles[n+3]=v+2;triangles[n+4]=v+1;triangles[n+5]=v+3;
            }
            mesh.MarkDynamic(); mesh.vertices=vertices; mesh.uv=uv; mesh.triangles=triangles;
            for(int i=0;i<Samples;i++) ages[i]=Lifetime;
        }
        public void Push(Vector3 root,Vector3 tip,float dt)
        {
            for(int i=0;i<Samples;i++) ages[i]+=dt;
            head=(head+1)%Samples; roots[head]=root; tips[head]=tip; ages[head]=0;
            for(int i=0;i<Samples;i++)
            {
                int s=(head-i+Samples)%Samples; float age=Mathf.Clamp01(ages[s]/Lifetime);
                // Expired samples collapse onto the newest point instead of leaving a stale edge.
                vertices[i*2]=age<1?roots[s]:root; vertices[i*2+1]=age<1?tips[s]:tip;
                uv[i*2]=new Vector2(age,0); uv[i*2+1]=new Vector2(age,1);
            }
            mesh.vertices=vertices; mesh.uv=uv; mesh.RecalculateBounds();
        }
    }

    public sealed class SaberSpringController : MonoBehaviour
    {
        public GameFeelSettings Settings;
        public SaberInputController Input=new SaberInputController();
        public Vector3 Tip => tip.position;
        public Vector3 Base => blade.position;
        public Vector3 Velocity {get;private set;}
        public Vector3 PreviousTip {get;private set;}
        public Vector3 PreviousBase {get;private set;}
        public bool HasSweep {get;private set;}
        public Vector3 HandVelocity => position.Velocity;
        public System.Action<SaberSpringController> AssistRequested;
        Vector3 guideOffset, completionDirection; Spring3 assistance;
        float completionAge=1;
        float contactAge=1, contactStrength; Vector2 contactDirection;
        Spring3 position, rotation, bladeRotation, motion;
        Transform hand, wrist, blade, tip; MagicFingerAnimator fingers; TrailRenderer trail;
        BladeRibbon ribbon; Material edge; Light glow; float swing;
        readonly Vector3 home=new Vector3(.53f,-.38f,1.08f);
        public void Initialize(GameFeelSettings settings)
        {
            Settings=settings; wrist=ArcaneVisuals.Empty("Saber wrist",transform,home);
            hand=ArcaneVisuals.Hand(wrist,false,out var joints,out var fingertips);
            blade=ArcaneVisuals.Empty("Blade follow through",wrist,new Vector3(0,.12f,0));
            hand.SetParent(blade,false);
            OrientGrip(joints); WrapAroundHilt(joints,fingertips); fingers=new MagicFingerAnimator(joints);
            var hilt=ArcaneVisuals.Empty("Procedural hilt",blade,Vector3.zero);
            ArcaneVisuals.Shape("Hilt",PrimitiveType.Cylinder,hilt,new Vector3(0,-.02f,0),new Vector3(.065f,.13f,.065f),ArcaneVisuals.Material(new Color(.18f,.20f,.27f)));
            var metal=ArcaneVisuals.Surface(new Color(.22f,.27f,.35f),.75f);
            for(int i=0;i<7;i++) ArcaneVisuals.Shape("Grip rib",PrimitiveType.Cylinder,hilt,new Vector3(0,-.12f+i*.03f,0),new Vector3(.078f,.005f,.078f),metal);
            ArcaneVisuals.Shape("Pommel",PrimitiveType.Sphere,hilt,new Vector3(0,-.16f,0),new Vector3(.085f,.055f,.085f),metal);
            ArcaneVisuals.Shape("Emitter housing",PrimitiveType.Cylinder,hilt,new Vector3(0,.12f,0),new Vector3(.10f,.045f,.10f),metal);
            for(int side=-1;side<=1;side+=2)
            {
                var guard=ArcaneVisuals.Shape("Swept guard",PrimitiveType.Cube,hilt,new Vector3(side*.095f,.08f,0),new Vector3(.12f,.035f,.075f),metal);
                guard.localRotation=Quaternion.Euler(0,0,side*22);
            }
            ArcaneVisuals.Shape("Emitter guard",PrimitiveType.Cube,hilt,new Vector3(0,.08f,0),new Vector3(.19f,.025f,.10f),ArcaneVisuals.Material(ArcaneVisuals.Cyan,2));
            if(ArcaneArt.Model("SaberHilt",blade,new Vector3(0,-.04f,0),.32f)) hilt.gameObject.SetActive(false);
            edge=ArcaneVisuals.LiveNeon(ArcaneVisuals.Cyan*4);
            ArcaneVisuals.Shape("Energy edge",PrimitiveType.Cylinder,blade,new Vector3(0,.56f,0),new Vector3(.032f,.47f,.032f),edge);
            ArcaneVisuals.Shape("White hot core",PrimitiveType.Cylinder,blade,new Vector3(0,.56f,-.006f),new Vector3(.012f,.47f,.012f),ArcaneVisuals.Material(Color.white,5));
            tip=ArcaneVisuals.Empty("Swept blade tip",blade,new Vector3(0,1.03f,0));
            trail=ArcaneVisuals.Trail(tip,ArcaneVisuals.Cyan,.03f,.08f); position.Position=home;
            ribbon=new BladeRibbon(transform);
            var light=ArcaneVisuals.Empty("Blade light",blade,new Vector3(0,.55f,0)); glow=light.gameObject.AddComponent<Light>();
            glow.type=LightType.Point; glow.color=ArcaneVisuals.Cyan; glow.range=5; glow.shadows=LightShadows.None;
            light.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>(); glow.lightUnit=UnityEngine.Rendering.LightUnit.Lumen;
            PreviousTip=Tip; PreviousBase=Base;
        }
        // Hammer grip measured from the hand's own knuckles, so any hand model ends up the same way:
        // the hand points away from the player, index above pinky (palm toward the hilt, thumb on top),
        // and the knuckle line sits just right of and behind the hilt.
        void OrientGrip(Transform[][] joints)
        {
            hand.localRotation=Quaternion.identity; hand.localPosition=Vector3.zero;
            Vector3 index=hand.InverseTransformPoint(joints[1][0].position),pinky=hand.InverseTransformPoint(joints[4][0].position);
            // Both hand builds put the wrist 10 cm below the hand origin.
            Vector3 knuckles=(index+pinky)*.5f,along=(knuckles-new Vector3(0,-.10f,0)).normalized,across=Vector3.ProjectOnPlane(pinky-index,along);
            Vector3 forward=new Vector3(0,.25f,1).normalized,down=Vector3.ProjectOnPlane(Vector3.down,forward);
            hand.localRotation=Quaternion.LookRotation(down,forward)*Quaternion.Inverse(Quaternion.LookRotation(across,along));
            hand.localPosition=new Vector3(.035f,-.10f,-.025f)-hand.localRotation*knuckles;
        }
        // Turns every finger joint so its bend axis runs along the hilt and bends toward it.
        // The grip curl then closes the fingers around the handle instead of folding them beside it.
        void WrapAroundHilt(Transform[][] joints,Transform[] fingertips)
        {
            Vector3 axis=blade.up,origin=blade.position;
            for(int f=0;f<joints.Length;f++) for(int j=0;j<joints[f].Length;j++)
            {
                var pivot=joints[f][j];
                Vector3 next=j+1<joints[f].Length?joints[f][j+1].position:fingertips[f].position;
                Vector3 up=Vector3.ProjectOnPlane(next-pivot.position,axis);
                if(up.sqrMagnitude<1e-8f) continue;
                // A positive curl turns a joint's Y toward its Z, so Z must face the hilt;
                // with Y along the finger that leaves the bend axis (X) along the hilt.
                Vector3 toward=Vector3.Cross(axis,up.normalized);
                Vector3 toHilt=origin+axis*Vector3.Dot(pivot.position-origin,axis)-pivot.position;
                if(Vector3.Dot(toward,toHilt)<0) toward=-toward;
                // Children keep their world pose while the pivot alone is re-aimed.
                var children=new Transform[pivot.childCount];
                for(int c=0;c<children.Length;c++) children[c]=pivot.GetChild(c);
                foreach(var child in children) child.SetParent(pivot.parent,true);
                pivot.rotation=Quaternion.LookRotation(toward,up.normalized);
                foreach(var child in children) child.SetParent(pivot,true);
            }
        }
        public void Animate(float dt, bool enabled,bool sampleInput=true)
        {
            PreviousTip=Tip; PreviousBase=Base;
            if(sampleInput) Input.Read(Settings,enabled); Vector2 aim=Input.Target;
            guideOffset=Vector3.zero;
            if(enabled) AssistRequested?.Invoke(this);
            completionAge+=dt;
            float follow=completionAge<.22f?Mathf.Sin(completionAge/.22f*Mathf.PI):0;
            assistance.Step(guideOffset+completionDirection*follow,420,.85f,dt);
            contactAge+=dt;
            float contact=Mathf.Exp(-contactAge*32)*contactStrength;
            Vector3 handTarget=home+new Vector3(aim.x*.65f,aim.y*.46f,-aim.sqrMagnitude*.10f);
            handTarget.z-=aim.y*aim.y*.09f+Mathf.Abs(aim.x)*.035f+contact*.025f;
            position.Step(handTarget,Settings.SaberPositionStiffness,Settings.SaberPositionDamping,dt);
            var vel=position.Velocity;
            motion.Step(Vector3.ClampMagnitude(vel*.35f+(Vector3)Input.IntentVelocity*.65f,1.6f),420,1,dt);
            rotation.Step(new Vector3(-motion.Position.y*3,aim.x*12+motion.Position.x*6,-aim.x*42-(vel.x*7+motion.Position.x*9)*Settings.SaberWristInfluence),Settings.SaberRotationStiffness,Settings.SaberRotationDamping,dt);
            bladeRotation.Step(new Vector3(Mathf.Clamp(-aim.y*16+VerticalPitch(motion.Position.y,Settings.SaberBladeLag)*.45f,-32,32)+contactDirection.y*contact*3,vel.x*6,-vel.x*16*Settings.SaberBladeLag),Settings.SaberRotationStiffness*1.8f,.8f,dt);
            wrist.localPosition=position.Position+assistance.Position; wrist.localRotation=Quaternion.Euler(rotation.Position);
            blade.localRotation=Quaternion.Euler(bladeRotation.Position);
            Velocity=dt>0 ? (Tip-PreviousTip)/dt : Vector3.zero;
            HasSweep=enabled && dt>0;
            // The swept ribbon is the only motion trail; a second tip trail doubled the flicker.
            fingers.Step(1,0,Settings,dt,true); trail.emitting=false;
            trail.widthMultiplier=Settings.SaberTrailIntensity*(1+contact*.7f);
            // Blade brightness follows swing speed, so a hard cut visibly charges the sword.
            // Smoothed so brightness eases with the swing instead of flickering with every mouse sample.
            swing=Mathf.Lerp(swing,Mathf.Clamp01(Velocity.magnitude/9),1-Mathf.Exp(-dt*10));
            edge.SetColor("_UnlitColor",ArcaneVisuals.Cyan*(3+swing*.8f+contact*2.5f));
            glow.intensity=220+swing*200+contact*600;
            ribbon.Material.SetColor("_UnlitColor",ArcaneVisuals.Cyan*(Settings.SaberTrailIntensity*(.15f+swing*.55f+contact*.5f)));
            ribbon.Push(Vector3.Lerp(Base,Tip,.12f),Tip,dt);
        }
        // Signed local X: downward motion is positive, upward motion is negative.
        public static float VerticalPitch(float velocity,float lag) => -36*(float)System.Math.Tanh(velocity*lag*.85f);
        public void Impact(Vector2 direction,bool perfect)
        {
            contactDirection=direction; contactAge=0; contactStrength=perfect?1.2f:1;
            position.Velocity+=new Vector3(-direction.x*.08f,-direction.y*.06f,-.16f)*contactStrength;
        }
        public void GuideTowards(Vector3 center,Vector2 direction,float strength)
        {
            Vector3 midpoint=Vector3.Lerp(Base,Tip,.4f);
            Vector3 delta=transform.InverseTransformVector(center-midpoint); delta.z=0;
            guideOffset=Vector3.ClampMagnitude(delta,.34f)*strength;
        }
        public void CompleteAssistedSwing(Vector2 direction,float strength)
        {
            completionDirection=new Vector3(direction.x,direction.y,0)*(.17f*strength); completionAge=0;
            assistance.Velocity+=completionDirection*5;
            bladeRotation.Velocity+=new Vector3(-direction.y*30,0,-direction.x*55)*strength;
        }
    }
}
