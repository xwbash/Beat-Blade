using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;

namespace ArcaneSaber
{
    public sealed class ProceduralCameraController : MonoBehaviour
    {
        public const float BaseFieldOfView=86,PositionLimit=.09f;
        public GameFeelSettings Settings; public MagicHandController Magic; public SaberSpringController Saber; public MusicConductor Music;
        public LensDistortion Lens;
        Spring3 position, rotation, mouse, zoom, roll; Vector3 impact; Camera lens;
        public void Impact(Vector2 direction)
        {
            if(Settings.ReducedMotion) return;
            float strength=Mathf.Clamp01(Settings.CameraImpactIntensity);
            position.Velocity+=new Vector3(-direction.x*.035f,-direction.y*.025f,-.07f)*strength;
            rotation.Velocity+=new Vector3(direction.y*3,-direction.x*2,-direction.x*2)*strength;
            zoom.Velocity+=Vector3.right*3*strength;
        }
        // Stage cues: strength 0-1, already scaled by the mod's camera power.
        public void Kick(float strength)
        {
            if(Settings.ReducedMotion) return;
            position.Velocity+=new Vector3(Random.Range(-.1f,.1f),-.16f,-.42f)*strength;
            rotation.Velocity+=new Vector3(14,Random.Range(-6f,6f),Random.Range(-8f,8f))*strength;
        }
        public void Zoom(float strength) { if(!Settings.ReducedMotion) zoom.Velocity+=Vector3.right*70*strength; }
        public void Roll(float strength) { if(!Settings.ReducedMotion) roll.Velocity+=Vector3.forward*55*strength; }
        public void Animate(float dt)
        {
            if(!Settings || !Magic || !Saber) return;
            if(!lens) lens=GetComponent<Camera>();
            float reduced=Settings.ReducedMotion?0:1;
            if(Lens!=null) { Lens.intensity.Override(-Settings.BodycamLens*(Settings.ReducedMotion?.35f:1)); Lens.scale.Override(1.035f); }
            Vector2 intent=Saber.HasSweep?Saber.Input.IntentVelocity:Vector2.zero;
            mouse.Step(Vector3.ClampMagnitude(new Vector3(-intent.y*.75f,intent.x*.65f,-intent.x*.45f),1.4f)*Settings.MouseCameraMotion*reduced,95,.85f,dt);
            impact*=Mathf.Exp(-dt*16);
            Vector3 magic=(Vector3)Magic.InputDirection*.013f*Settings.MagicCameraInfluence;
            Vector3 saber=Vector3.ClampMagnitude(Saber.HandVelocity,.8f)*-.012f*Settings.SaberCameraInfluence;
            float beat=Mathf.Exp(-Mathf.Repeat(Music.Beat,1)*10);
            Vector3 idle=new Vector3(0,Mathf.Sin(Time.time*1.3f)*.003f,0)*Settings.IdleCameraInfluence;
            Vector3 pulse=Vector3.forward*beat*.008f*Settings.MusicCameraInfluence;
            position.Step(Vector3.ClampMagnitude(magic+saber+impact+idle+pulse,.05f)*reduced,Settings.CameraPositionStiffness,Settings.CameraPositionDamping,dt);
            float headYaw=Saber.HasSweep?Mathf.Clamp(Saber.Input.Target.x*Settings.MouseCameraMotion,-1,1)*2.5f:0;
            rotation.Step(new Vector3(-magic.y*20,headYaw+saber.x*15,Mathf.Clamp(-magic.x*40-saber.x*50,-Settings.MaximumCameraRoll,Settings.MaximumCameraRoll))*reduced+mouse.Position,Settings.CameraRotationStiffness,Settings.CameraRotationDamping,dt);
            zoom.Step(Vector3.zero,190,.5f,dt); roll.Step(Vector3.zero,90,.6f,dt);
            position.Position=Vector3.ClampMagnitude(position.Position,PositionLimit);
            rotation.Position=new Vector3(Mathf.Clamp(rotation.Position.x,-3.5f,3.5f),Mathf.Clamp(rotation.Position.y,-3.5f,3.5f),Mathf.Clamp(rotation.Position.z,-Settings.MaximumCameraRoll,Settings.MaximumCameraRoll));
            transform.localPosition=position.Position;
            transform.localRotation=Quaternion.Euler(rotation.Position+new Vector3(0,0,Mathf.Clamp(roll.Position.z,-7,7)));
            if(lens) lens.fieldOfView=BaseFieldOfView+Mathf.Clamp(zoom.Position.x,-5,11);
        }
    }
}
