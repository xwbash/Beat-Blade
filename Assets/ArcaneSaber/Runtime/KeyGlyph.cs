using System.Collections.Generic;
using UnityEngine;

namespace ArcaneSaber
{
    // Solid strokes share the depth buffer with note bodies, including the imported FBXs.
    static class KeyGlyph
    {
        static readonly Mesh[] meshes=new Mesh[4];
        public static Mesh Get(int key)
        {
            if(meshes[key]) return meshes[key];
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            void Stroke(float x,float y,float u,float v)
            {
                var a=new Vector3(x,y,0)*.072f; var b=new Vector3(u,v,0)*.072f;
                var offset=Vector3.Cross(Vector3.forward,(b-a).normalized)*.009f;
                int i=vertices.Count;
                vertices.Add(a+offset); vertices.Add(b+offset); vertices.Add(b-offset); vertices.Add(a-offset);
                triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
            }
            if(key==0) { Stroke(-1,.85f,-.55f,-.85f); Stroke(-.55f,-.85f,0,.28f); Stroke(0,.28f,.55f,-.85f); Stroke(.55f,-.85f,1,.85f); }
            if(key==1) { Stroke(-.85f,-.85f,0,.85f); Stroke(0,.85f,.85f,-.85f); Stroke(-.58f,-.30f,.58f,-.30f); }
            if(key==2) { Stroke(.72f,.8f,-.52f,.8f); Stroke(-.52f,.8f,-.76f,.53f); Stroke(-.76f,.53f,-.76f,.2f); Stroke(-.76f,.2f,.69f,-.18f); Stroke(.69f,-.18f,.69f,-.58f); Stroke(.69f,-.58f,.46f,-.84f); Stroke(.46f,-.84f,-.75f,-.84f); }
            if(key==3) { Stroke(-.7f,-.85f,-.7f,.85f); Stroke(-.7f,.85f,.26f,.85f); Stroke(.26f,.85f,.76f,.36f); Stroke(.76f,.36f,.76f,-.36f); Stroke(.76f,-.36f,.26f,-.85f); Stroke(.26f,-.85f,-.7f,-.85f); }
            var mesh=new Mesh{name="Key "+GestureMath.Label(key)}; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals();
            return meshes[key]=mesh;
        }
    }
}
