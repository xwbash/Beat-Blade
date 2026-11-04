using System.Collections.Generic;
using UnityEngine;

namespace ArcaneSaber
{
    public sealed class CubeDestructionController : MonoBehaviour
    {
        struct Bit { public Transform T; public Vector3 Velocity, Spin; public float Life, Duration, Size; public bool Magic; public Mesh CutMesh; public Vector3[] Vertices; public MeshFilter Filter; public Renderer Renderer; }
        struct Pulse { public LineRenderer Line; public Vector3 A,B; public float Life; }
        struct Burst { public Transform Ring,Slash; public Material RingMaterial,SlashMaterial; public Color Color; public float Age; public Vector2 Cut; }
        Bit[] bits = new Bit[360]; Pulse[] pulses = new Pulse[24]; Burst[] bursts=new Burst[10]; int next, nextPulse, nextBurst;
        Mesh cube; readonly Vector2[] polygon=new Vector2[8];
        Material magicMaterial,saberMaterial;
        public void Initialize()
        {
            magicMaterial=ArcaneVisuals.Material(ArcaneVisuals.Crimson,2); saberMaterial=ArcaneVisuals.Material(ArcaneVisuals.Cyan,2);
            int[] triangles=new int[60]; for(int n=0;n<60;n++) triangles[n]=n;
            for(int i=0;i<bits.Length;i++)
            {
                bits[i].T=ArcaneVisuals.Shape("Pooled fragment",PrimitiveType.Cube,transform,Vector3.zero,Vector3.one,ArcaneVisuals.Material(i%2==0?ArcaneVisuals.Crimson:ArcaneVisuals.Cyan,2));
                bits[i].T.gameObject.SetActive(false);
                bits[i].Filter=bits[i].T.GetComponent<MeshFilter>(); cube=bits[i].Filter.sharedMesh;
                bits[i].Renderer=bits[i].T.GetComponent<Renderer>();
                bits[i].Vertices=new Vector3[60]; bits[i].CutMesh=new Mesh {name="Pooled cut prism"};
                bits[i].CutMesh.MarkDynamic(); bits[i].CutMesh.vertices=bits[i].Vertices; bits[i].CutMesh.triangles=triangles;
            }
            for(int i=0;i<pulses.Length;i++) { pulses[i].Line=ArcaneVisuals.Line("Spell conduit",transform,ArcaneVisuals.Crimson,.035f,12); pulses[i].Line.useWorldSpace=true; pulses[i].Line.enabled=false; }
            for(int i=0;i<bursts.Length;i++)
            {
                ref Burst b=ref bursts[i]; b.Age=1;
                b.RingMaterial=ArcaneVisuals.Glow(ArcaneVisuals.RingTexture,Color.black); b.SlashMaterial=ArcaneVisuals.Glow(ArcaneVisuals.FlareTexture,Color.black);
                b.Ring=ArcaneVisuals.GlowQuad("Shock ring",transform,ArcaneVisuals.CenterQuad,b.RingMaterial); b.Slash=ArcaneVisuals.GlowQuad("Slash flash",transform,ArcaneVisuals.CenterQuad,b.SlashMaterial);
                b.Ring.gameObject.SetActive(false); b.Slash.gameObject.SetActive(false);
            }
        }
        void Flash(Vector3 p,bool magic,Vector2 swing,bool perfect)
        {
            ref Burst b=ref bursts[nextBurst++%bursts.Length];
            b.Age=0; b.Cut=swing; b.Color=(magic?ArcaneVisuals.Crimson:ArcaneVisuals.Cyan)*(perfect?5:3);
            b.Ring.gameObject.SetActive(true); b.Slash.gameObject.SetActive(!magic);
            b.Ring.position=p; b.Slash.position=p;
        }
        public void PulseTo(Vector3 from, Vector3 to)
        {
            int i=nextPulse++%pulses.Length;
            pulses[i].A=from; pulses[i].B=to; pulses[i].Life=.18f; pulses[i].Line.enabled=true;
        }
        public void DestroyCube(Vector3 p, bool magic, Vector2 swing, bool perfect)
        {
            Flash(p,magic,swing,perfect);
            int count=perfect?38:24;
            for(int n=0;n<count;n++)
            {
                int i=next++%bits.Length; ref Bit b=ref bits[i];
                b.T.gameObject.SetActive(true); b.T.position=p+Random.insideUnitSphere*.22f;
                b.T.rotation=Random.rotation; b.Size=Random.Range(.035f,.11f);
                b.T.localScale=Vector3.one*b.Size;
                b.Filter.sharedMesh=cube; b.Renderer.sharedMaterial=magic?magicMaterial:saberMaterial;
                b.Duration=b.Life=Random.Range(.4f,.85f); b.Magic=magic;
                b.Velocity=magic?Random.insideUnitSphere*3.4f: new Vector3(swing.x,swing.y,.5f)*4.5f+Random.insideUnitSphere*3.2f;
                b.Spin=Random.insideUnitSphere*520;
            }
            if(!magic)
            {
                // A square prism clipped by a plane through its centre. These are real cut
                // surfaces, with each half matching the measured blade travel direction.
                for(int side=-1;side<=1;side+=2)
                {
                    int i=next++%bits.Length; ref Bit b=ref bits[i];
                    Vector3 normal=new Vector3(-swing.y,swing.x,0);
                    b.T.gameObject.SetActive(true); b.T.position=p+normal*side*.09f;
                    b.T.rotation=Quaternion.identity;
                    b.Size=1; b.T.localScale=Vector3.one;
                    BuildHalf(ref b,new Vector2(normal.x,normal.y)*side);
                    b.Filter.sharedMesh=b.CutMesh; b.Renderer.sharedMaterial=saberMaterial;
                    b.Duration=b.Life=.6f; b.Magic=false;
                    b.Velocity=normal*side*3.6f+new Vector3(swing.x,swing.y,.6f)*2.2f; b.Spin=normal*280+Vector3.forward*side*120;
                }
            }
        }
        void BuildHalf(ref Bit b,Vector2 normal)
        {
            int count=0;
            // Clip the front-face square by the measured swing plane, then extrude it.
            for(int i=0;i<4;i++)
            {
                Vector2 a=Corner(i), c=Corner((i+1)%4);
                float da=Vector2.Dot(normal,a), dc=Vector2.Dot(normal,c);
                if(da>=0) polygon[count++]=a;
                if((da>0 && dc<0)||(da<0 && dc>0)) polygon[count++]=a+(c-a)*(da/(da-dc));
            }
            int v=0;
            for(int i=1;i<count-1;i++)
            {
                Triangle(b.Vertices,ref v,At(polygon[0],-.245f),At(polygon[i+1],-.245f),At(polygon[i],-.245f));
                Triangle(b.Vertices,ref v,At(polygon[0],.245f),At(polygon[i],.245f),At(polygon[i+1],.245f));
            }
            for(int i=0;i<count;i++)
            {
                Vector3 a=At(polygon[i],-.245f), c=At(polygon[(i+1)%count],-.245f), d=c+Vector3.forward*.49f, e=a+Vector3.forward*.49f;
                Triangle(b.Vertices,ref v,a,c,d); Triangle(b.Vertices,ref v,a,d,e);
            }
            for(int i=v;i<60;i++) b.Vertices[i]=Vector3.zero;
            b.CutMesh.vertices=b.Vertices; b.CutMesh.RecalculateNormals(); b.CutMesh.RecalculateBounds();
        }
        static Vector2 Corner(int n) => n==0?new Vector2(-.245f,-.245f):n==1?new Vector2(.245f,-.245f):n==2?new Vector2(.245f,.245f):new Vector2(-.245f,.245f);
        static Vector3 At(Vector2 p,float z) => new Vector3(p.x,p.y,z);
        static void Triangle(Vector3[] vertices,ref int index,Vector3 a,Vector3 b,Vector3 c) { vertices[index++]=a; vertices[index++]=b; vertices[index++]=c; }
        void OnDestroy() { foreach(var b in bits) if(b.CutMesh) Destroy(b.CutMesh); }
        void Update()
        {
            float dt=Time.deltaTime;
            var viewer=Camera.main;
            for(int i=0;i<bursts.Length;i++)
            {
                ref Burst b=ref bursts[i]; if(b.Age>=.32f) continue;
                b.Age+=dt;
                if(b.Age>=.32f || !viewer) { b.Ring.gameObject.SetActive(false); b.Slash.gameObject.SetActive(false); b.Age=1; continue; }
                float t=b.Age/.32f,size=.5f+(1-(1-t)*(1-t))*2.8f;
                Quaternion face=Quaternion.LookRotation(b.Ring.position-viewer.transform.position,Vector3.up);
                b.Ring.rotation=face; b.Ring.localScale=new Vector3(size,size,1);
                b.RingMaterial.SetColor("_UnlitColor",b.Color*(1-t)*(1-t));
                b.Slash.rotation=face*Quaternion.Euler(0,0,Mathf.Atan2(b.Cut.y,b.Cut.x)*Mathf.Rad2Deg);
                b.Slash.localScale=new Vector3(5*(1-t*.3f),.9f,1);
                b.SlashMaterial.SetColor("_UnlitColor",b.Color*Mathf.Exp(-t*9)*1.6f);
            }
            for(int i=0;i<bits.Length;i++)
            {
                ref Bit b=ref bits[i]; if(b.Life<=0) continue;
                b.Life-=dt; if(b.Life<=0) { b.T.gameObject.SetActive(false); continue; }
                float age=b.Duration-b.Life;
                if(b.Magic && age<.08f) b.T.position-=b.Velocity*dt;
                else { b.T.position+=b.Velocity*dt; b.Velocity+=Vector3.down*(b.Magic?.5f:3)*dt; }
                b.T.Rotate(b.Spin*dt); b.T.localScale*=Mathf.Exp(-dt*(age>.2f?4:0));
            }
            for(int i=0;i<pulses.Length;i++)
            {
                ref Pulse p=ref pulses[i]; if(p.Life<=0) continue; p.Life-=dt;
                p.Line.enabled=p.Life>0;
                for(int j=0;j<12;j++) { float t=j/11f; p.Line.SetPosition(j,Vector3.Lerp(p.A,p.B,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.3f+Vector3.right*Mathf.Sin(t*12+p.Life*30)*.025f); }
                p.Line.widthMultiplier=Mathf.Max(0,p.Life/.18f);
            }
        }
    }
}
