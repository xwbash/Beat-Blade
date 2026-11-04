using UnityEngine;

namespace ArcaneSaber
{
    public sealed class MagicVFXController : MonoBehaviour
    {
        MagicHandController hand; GameFeelSettings settings; MusicConductor music;
        Transform[] motes=new Transform[28]; LineRenderer[] wisps=new LineRenderer[3];
        struct Wave { public LineRenderer Line; public Vector3 Origin, Direction; public float Age; }
        Wave[] waves=new Wave[12]; int next;
        public void Initialize(MagicHandController hand,GameFeelSettings settings,MusicConductor music)
        {
            this.hand=hand; this.settings=settings; this.music=music;
            for(int i=0;i<motes.Length;i++)
                motes[i]=ArcaneVisuals.Shape("Orbiting arcane fragment",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one*.009f,ArcaneVisuals.Material(i%5==0?new Color(1,.5f,.72f):ArcaneVisuals.Crimson,2));
            for(int i=0;i<wisps.Length;i++) wisps[i]=ArcaneVisuals.Line("Palm energy filament",transform,ArcaneVisuals.Crimson*.6f,.005f,24);
            for(int i=0;i<waves.Length;i++)
            {
                waves[i].Line=ArcaneVisuals.Line("Directional wave",transform,ArcaneVisuals.Crimson,.018f,18);
                waves[i].Line.useWorldSpace=true; waves[i].Line.enabled=false; waves[i].Age=1;
            }
            hand.Pressed+=Release;
        }
        void Release(int direction)
        {
            int i=next++%waves.Length; waves[i].Origin=hand.Palm;
            Vector2 d=GestureMath.Direction(direction); waves[i].Direction=new Vector3(d.x,d.y,.6f).normalized;
            waves[i].Age=0; waves[i].Line.enabled=true;
        }
        void LateUpdate()
        {
            if(!hand) return;
            float dt=Time.deltaTime, time=Time.time;
            transform.position=hand.Palm;
            float beat=music && music.Playing ? Mathf.Exp(-Mathf.Repeat(music.Beat,1)*8):0;
            for(int i=0;i<motes.Length;i++)
            {
                float phase=time*(.7f+i*.017f)+i*2.4f, radius=.075f+(i%4)*.025f;
                motes[i].localPosition=new Vector3(Mathf.Cos(phase)*radius,Mathf.Sin(phase*1.3f)*radius,Mathf.Sin(phase)*radius*.65f);
                motes[i].localScale=Vector3.one*(.006f+.004f*beat);
            }
            for(int i=0;i<wisps.Length;i++)
                for(int j=0;j<24;j++)
                {
                    float a=j/23f*Mathf.PI*1.45f+time*(i%2==0?1:-1)+i*2;
                    wisps[i].SetPosition(j,new Vector3(Mathf.Cos(a)*.115f,Mathf.Sin(a)*.115f,Mathf.Sin(a*2+i)*.04f));
                }
            for(int i=0;i<waves.Length;i++)
            {
                ref Wave wave=ref waves[i]; if(wave.Age>.28f) continue;
                wave.Age+=dt; wave.Line.enabled=wave.Age<=.28f && settings.MagicTrailIntensity>0;
                Vector3 sideways=Vector3.Cross(wave.Direction,Vector3.forward).normalized;
                for(int j=0;j<18;j++)
                {
                    float t=j/17f, a=(t-.5f)*Mathf.PI;
                    wave.Line.SetPosition(j,wave.Origin+wave.Direction*wave.Age*3+sideways*Mathf.Sin(a)*(.10f+wave.Age*.5f)+Vector3.forward*Mathf.Cos(a)*.10f);
                }
                wave.Line.widthMultiplier=(1-wave.Age/.28f)*settings.MagicTrailIntensity;
            }
        }
    }
}
