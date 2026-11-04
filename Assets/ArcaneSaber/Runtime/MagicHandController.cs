using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcaneSaber
{
    public sealed class MagicHandController : MonoBehaviour
    {
        public GameFeelSettings Settings;
        public Vector3 Velocity => position.Velocity;
        public Vector3 Palm => hand.TransformPoint(new Vector3(0,.08f,.02f));
        public Quaternion WorldRotation => hand.rotation;
        public Vector2 InputDirection {get;private set;}
        public System.Action<int> Pressed;
        Spring3 position, rotation, wrist; MagicGestureController gesture=new MagicGestureController();
        MagicFingerAnimator fingers; Transform hand, pivot; Transform[] tips;
        Vector3 home=new Vector3(-.48f,-.29f,.90f); Vector2 previous;
        float releaseAge=10, energy, previousTime;
        TrailRenderer[] trails;
        public void Initialize(GameFeelSettings settings)
        {
            Settings=settings; pivot=ArcaneVisuals.Empty("Magic wrist spring",transform,home);
            hand=ArcaneVisuals.Hand(pivot,true,out var bones,out tips);
            fingers=new MagicFingerAnimator(bones); position.Position=home; gesture.Target=home;
            gesture.Redirect(home,Vector3.zero,home,0,0);
            trails=new TrailRenderer[tips.Length]; for(int i=0;i<tips.Length;i++) trails[i]=tips[i].GetComponent<TrailRenderer>();
            var seal=ArcaneVisuals.Line("Palm sigil",hand,ArcaneVisuals.Crimson,.008f,49);
            for(int i=0;i<49;i++) { float a=i/48f*Mathf.PI*2; seal.SetPosition(i,new Vector3(Mathf.Cos(a)*.065f,Mathf.Sin(a)*.065f,-.043f)); }
        }
        public void ReadInput(bool enabled)
        {
            var k=Keyboard.current; if(k==null || !enabled) { InputDirection=Vector2.zero; return; }
            Vector2 v=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
            InputDirection=Vector2.ClampMagnitude(v,1);
            for(int i=0;i<4;i++) if(i==0?k.wKey.wasPressedThisFrame:i==1?k.aKey.wasPressedThisFrame:i==2?k.sKey.wasPressedThisFrame:k.dKey.wasPressedThisFrame)
            {
                releaseAge=0; energy=Mathf.Clamp01(.22f/(Time.unscaledTime-previousTime+.03f)); previousTime=Time.unscaledTime;
                position.Velocity+=(Vector3)GestureMath.Direction(i)*Settings.MagicGestureOvershoot*.7f;
                Pressed?.Invoke(i);
            }
        }
        public void Animate(float dt)
        {
            releaseAge+=dt; energy=Mathf.MoveTowards(energy,0,dt*Settings.MagicRecoverySpeed);
            Vector2 input=InputDirection;
            if(input!=previous)
            {
                Vector3 destination=home+new Vector3(input.x*Settings.MagicGestureDistance,input.y*Settings.MagicGestureDistance*.75f,-input.magnitude*.10f);
                gesture.Redirect(position.Position,position.Velocity,destination,Settings.MagicGestureArcIntensity,energy); previous=input;
            }
            Vector3 idle=new Vector3(Mathf.Sin(Time.time*1.1f),Mathf.Sin(Time.time*1.6f),Mathf.Cos(Time.time*.8f)) * Settings.MagicIdleSway;
            position.Step(gesture.Step(dt)+idle,Settings.MagicHandPositionStiffness,Settings.MagicHandPositionDamping,dt);
            rotation.Step(new Vector3(-input.y*22+position.Velocity.y*5,input.x*24,-input.x*30-position.Velocity.x*6),Settings.MagicHandRotationStiffness,Settings.MagicHandRotationDamping,dt);
            wrist.Step(new Vector3(0,0,Mathf.Sin(releaseAge*18)*Mathf.Exp(-releaseAge*8)*15),220,.7f,dt);
            pivot.localPosition=position.Position; pivot.localRotation=Quaternion.Euler(rotation.Position);
            hand.localRotation=Quaternion.Euler(wrist.Position);
            fingers.Step(releaseAge,Velocity.magnitude,Settings,dt);
            foreach(var trail in trails) { trail.emitting=Settings.MagicTrailIntensity>0; trail.widthMultiplier=Settings.MagicTrailIntensity; }
        }
    }
}
