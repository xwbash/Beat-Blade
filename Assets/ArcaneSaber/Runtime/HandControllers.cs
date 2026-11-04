using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcaneSaber
{
    public sealed class MagicGestureController
    {
        Vector3 a,b,c,d; float elapsed=1, duration=.17f;
        public Vector3 Target;
        public void Redirect(Vector3 current, Vector3 velocity, Vector3 destination, float arc, float energy)
        {
            // Plan from the current trajectory, never from an idle pose. The motion spring
            // retains its own velocity even when this guide curve is replaced.
            a=Target; if(elapsed>=duration) a=current;
            d=destination; Vector3 delta=d-a;
            duration=Mathf.Clamp(.15f-energy*.04f,.08f,.2f); elapsed=0;
            b=a+Vector3.ClampMagnitude(velocity*duration/3,.22f);
            Vector3 tangent=new Vector3(-delta.y,delta.x,-.15f);
            c=d-delta*.22f+tangent.normalized*arc;
        }
        public Vector3 Step(float dt)
        { elapsed+=dt; Target=GestureMath.Bezier(a,b,c,d,Mathf.Clamp01(elapsed/duration)); return Target; }
    }

    public sealed class MagicFingerAnimator
    {
        readonly Transform[][] bones; readonly Quaternion[][] rest; Spring3 curl;
        public MagicFingerAnimator(Transform[][] joints)
        {
            bones=joints; rest=new Quaternion[5][];
            for(int f=0;f<5;f++) { rest[f]=new Quaternion[3]; for(int j=0;j<3;j++) rest[f][j]=bones[f][j].localRotation; }
        }
        public void Step(float releaseAge, float speed, GameFeelSettings s, float dt, bool gripping=false)
        {
            float target=gripping?65:releaseAge<.035f?38:releaseAge<.20f?-7:18;
            if(!gripping) target+=Mathf.Min(speed*2,8)*s.MagicFingerCurlIntensity;
            curl.Step(new Vector3(target,0,0),s.MagicFingerReleaseSpeed*s.MagicFingerReleaseSpeed,.8f,dt);
            for(int f=0;f<5;f++) for(int j=0;j<3;j++)
            {
                float wave=Mathf.Sin(Time.time*1.7f+f*1.3f+j*.4f)*2;
                float bend=gripping?(f==0?(j==0?20:13):(j==0?30:54)+f*2):curl.Position.x*(j==0?.6f:1)+wave;
                bones[f][j].localRotation=rest[f][j]*Quaternion.Euler(bend,0,0);
            }
        }
    }



    public sealed class SaberInputController
    {
        public Vector2 Target;
        public Vector2 IntentVelocity {get;private set;}
        public float MotionTime {get;private set;} = float.NegativeInfinity;
        public void Read(GameFeelSettings s, bool enabled)
        {
            if(!enabled || Mouse.current==null) { IntentVelocity=Vector2.zero; return; }
            // Relative mouse motion: independent of screen resolution and cursor boundaries.
            ApplyMouseDelta(Mouse.current.delta.ReadValue(),s,Time.unscaledDeltaTime,Time.unscaledTime);
            // A resting hand drifts home, so the next swing always has room on both sides.
            if(Time.unscaledTime-MotionTime>.4f) Target*=Mathf.Exp(-Time.unscaledDeltaTime*.8f);
        }
        public void ApplyMouseDelta(Vector2 pixels,GameFeelSettings s,float dt,float time)
        {
            Vector2 delta=pixels*.0025f*s.SaberSwingSensitivity;
            if(delta.magnitude>.0012f) { IntentVelocity=delta/Mathf.Max(.001f,dt); MotionTime=time; }
            else if(time-MotionTime>.085f) IntentVelocity=Vector2.zero;
            delta.y*=1+.3f*s.SaberAssist;
            Target+=delta;
            Target=Vector2.ClampMagnitude(Target,1);
        }
    }




}
