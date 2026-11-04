using System.Collections.Generic;
using UnityEngine;

namespace ArcaneSaber
{
    public static class ArcaneVisuals
    {
        public static readonly Color Crimson = new Color(1,.025f,.23f);
        public static readonly Color Cyan = new Color(.02f,.8f,1);
        static readonly Dictionary<string,Material> materials = new Dictionary<string,Material>();
        static Mesh roundedCube;
        public static Mesh BeveledCube
        {
            get
            {
                if(roundedCube) return roundedCube;
                var vertices=new List<Vector3>(); var normals=new List<Vector3>(); var triangles=new List<int>();
                float[] grid={-.25f,-.20f,.20f,.25f};
                for(int face=0;face<6;face++)
                {
                    Vector3 normal=face==0?Vector3.right:face==1?Vector3.left:face==2?Vector3.up:face==3?Vector3.down:face==4?Vector3.forward:Vector3.back;
                    Vector3 u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.up;
                    Vector3 v=Vector3.Cross(normal,u); int start=vertices.Count;
                    for(int y=0;y<4;y++) for(int x=0;x<4;x++)
                    {
                        Vector3 p=normal*.25f+u*grid[x]+v*grid[y];
                        Vector3 inner=new Vector3(Mathf.Clamp(p.x,-.20f,.20f),Mathf.Clamp(p.y,-.20f,.20f),Mathf.Clamp(p.z,-.20f,.20f));
                        Vector3 n=(p-inner).normalized; vertices.Add(inner+n*.05f); normals.Add(n);
                    }
                    for(int y=0;y<3;y++) for(int x=0;x<3;x++)
                    {
                        int a=start+y*4+x,b=a+1,c=a+4,d=c+1;
                        triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(b);triangles.Add(d);triangles.Add(c);
                    }
                }
                roundedCube=new Mesh{name="Rounded solid note • six beveled faces"};
                roundedCube.SetVertices(vertices);roundedCube.SetNormals(normals);roundedCube.SetTriangles(triangles,0);roundedCube.RecalculateBounds();
                return roundedCube;
            }
        }
        public static Material Material(Color color, float glow = 1)
        {
            string key = color.ToString()+glow;
            if (materials.TryGetValue(key,out var m) && m) return m;
            var template=Resources.Load<Material>("ArcaneNeon");
            m = template ? new Material(template) : new Material(Shader.Find("HDRP/Unlit"));
            m.SetColor("_UnlitColor",color*glow);
            m.SetColor("_EmissiveColor",color*Mathf.Max(0,glow-1));
            materials[key]=m; return m;
        }
        public static Material Surface(Color color,float metallic=0)
        {
            string key="surface"+color+metallic;
            if(materials.TryGetValue(key,out var material) && material) return material;
            var template=Resources.Load<Material>("ArcaneSurface");
            material=template?new Material(template):new Material(Shader.Find("HDRP/Lit"));
            material.SetColor("_BaseColor",color); material.SetFloat("_Metallic",metallic); material.SetFloat("_Smoothness",.42f);
            materials[key]=material; return material;
        }
        // Unshared neon whose colour is animated every frame.
        public static Material LiveNeon(Color color)
        {
            var template=Resources.Load<Material>("ArcaneNeon");
            var m=template?new Material(template):new Material(Shader.Find("HDRP/Unlit"));
            m.SetColor("_UnlitColor",color); return m;
        }
        // Unshared additive material for light shafts, flares and sword ribbons.
        public static Material Glow(Texture2D texture,Color color)
        {
            var template=Resources.Load<Material>("ArcaneGlow");
            var m=template?new Material(template):new Material(Shader.Find("HDRP/Unlit"));
            m.SetTexture("_UnlitColorMap",texture); m.SetColor("_UnlitColor",color); return m;
        }
        static Texture2D beamTexture,flareTexture,ribbonTexture,ringTexture;
        static Texture2D Bake(string name,int width,int height,System.Func<float,float,float> shade)
        {
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false,true){name=name,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[width*height];
            for(int y=0;y<height;y++) for(int x=0;x<width;x++)
            { byte v=(byte)Mathf.RoundToInt(Mathf.Clamp01(shade((x+.5f)/width,(y+.5f)/height))*255); pixels[y*width+x]=new Color32(v,v,v,255); }
            texture.SetPixels32(pixels); texture.Apply(false,true); return texture;
        }
        // Across: soft edge around a hot core. Along: strong at the source, fading into the air.
        public static Texture2D BeamTexture => beamTexture?beamTexture:(beamTexture=Bake("Light shaft",64,256,(u,v)=>{
            float a=Mathf.Abs(u*2-1),edge=Mathf.Pow(1-a,2.2f)*.55f+Mathf.Pow(1-a,14)*.9f;
            return edge*Mathf.Pow(1-v,1.6f)*Mathf.SmoothStep(0,1,v*14); }));
        public static Texture2D FlareTexture => flareTexture?flareTexture:(flareTexture=Bake("Lens flare",128,128,(u,v)=>{
            float x=u*2-1,y=v*2-1,r=Mathf.Sqrt(x*x+y*y);
            float streak=Mathf.Exp(-Mathf.Abs(y)*38)*(1-Mathf.Abs(x))*.7f;
            return Mathf.Pow(Mathf.Clamp01(1-r),3)+streak; }));
        // U is age along the swing, V is base to tip.
        public static Texture2D RibbonTexture => ribbonTexture?ribbonTexture:(ribbonTexture=Bake("Sword ribbon",128,64,(u,v)=>
            Mathf.Pow(1-u,2.4f)*Mathf.Lerp(.25f,1,v*v)*Mathf.SmoothStep(0,1,(1-v)*18)));
        public static Texture2D RingTexture => ringTexture?ringTexture:(ringTexture=Bake("Shock ring",128,128,(u,v)=>{
            float x=u*2-1,y=v*2-1,r=Mathf.Sqrt(x*x+y*y);
            return Mathf.Exp(-Mathf.Pow((r-.82f)*16,2)); }));
        static Texture2D aurora, reflection;
        public static Texture2D Aurora => aurora?aurora:(aurora=Bake("Horizon veil",512,256,(u,v)=>{
            float edge=Mathf.Sin(Mathf.PI*u);
            float ridge=.30f+.115f*Mathf.Sin(6.8f*u)+.045f*Mathf.Sin(17*u);
            float height=v-ridge;
            float folds=.45f+.55f*Mathf.PerlinNoise(u*38,.4f);
            float hem=.32f*Mathf.Exp(-Mathf.Abs(height)*65);
            float veil=height>0?Mathf.Exp(-height*9)*Mathf.SmoothStep(0,1,height*55)*folds:0;
            return edge*edge*(hem+veil)*Mathf.Clamp01((1-v)*8); }));
        public static Texture2D FloorReflection => reflection?reflection:(reflection=Bake("Brushed light reflection",256,512,(u,v)=>{
            float across=(u-.5f)*2;
            float brushed=Mathf.PerlinNoise(u*3,v*90);
            float spine=Mathf.Exp(-across*across*10);
            return spine*Mathf.Pow(Mathf.Sin(Mathf.PI*v),.75f)*(.20f+.80f*brushed*brushed); }));
        static Mesh shaftMesh,centerQuad;
        static Mesh Quad(string name,float bottom)
        {
            var mesh=new Mesh{name=name};
            mesh.vertices=new[]{new Vector3(-.5f,bottom,0),new Vector3(.5f,bottom,0),new Vector3(-.5f,bottom+1,0),new Vector3(.5f,bottom+1,0)};
            mesh.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1)};
            mesh.normals=new[]{Vector3.back,Vector3.back,Vector3.back,Vector3.back};
            mesh.triangles=new[]{0,2,1,1,2,3}; mesh.bounds=new Bounds(new Vector3(0,bottom+.5f,0),new Vector3(1.5f,1.5f,1.5f));
            return mesh;
        }
        // Rises from the origin, so scale.y is the shaft's length.
        public static Mesh ShaftMesh => shaftMesh?shaftMesh:(shaftMesh=Quad("Light shaft quad",0));
        public static Mesh CenterQuad => centerQuad?centerQuad:(centerQuad=Quad("Centred glow quad",-.5f));
        public static Transform GlowQuad(string name,Transform parent,Mesh mesh,Material material)
        {
            var t=Empty(name,parent,Vector3.zero); t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=t.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
            return t;
        }
        public static Transform Empty(string name, Transform parent, Vector3 position)
        {
            var t = new GameObject(name).transform; t.SetParent(parent,false); t.localPosition=position; return t;
        }
        public static Transform Shape(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type); go.name=name;
            var t=go.transform; t.SetParent(parent,false); t.localPosition=pos; t.localScale=scale;
            if(Application.isPlaying) Object.Destroy(go.GetComponent<Collider>());
            else Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer=go.GetComponent<Renderer>();
            renderer.sharedMaterial=mat; renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }
        public static LineRenderer Line(string name, Transform parent, Color color, float width, int count)
        {
            var line=Empty(name,parent,Vector3.zero).gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial=Material(color,2); line.positionCount=count; line.startWidth=width; line.endWidth=width;
            line.useWorldSpace=false; line.numCornerVertices=3; line.numCapVertices=3; return line;
        }
        public static TrailRenderer Trail(Transform parent, Color color, float width, float time)
        {
            var t=parent.gameObject.AddComponent<TrailRenderer>(); t.sharedMaterial=Material(color,2);
            t.time=time; t.minVertexDistance=.012f; t.startWidth=width; t.endWidth=0;
            t.startColor=color; t.endColor=new Color(color.r,color.g,color.b,0); t.numCapVertices=3;
            return t;
        }
        public static TextMesh Text(string text, Transform parent, Vector3 p, float size, Color color)
        {
            var t=Empty("Glyph",parent,p);
            var label=t.gameObject.AddComponent<TextMesh>(); label.text=text;
            label.anchor=TextAnchor.MiddleCenter; label.alignment=TextAlignment.Center;
            label.fontSize=64; label.characterSize=size; label.color=color;
            return label;
        }
        public static Transform Hand(Transform parent, bool left, out Transform[][] joints, out Transform[] tips)
        {
            var hand=Empty(left?"Telekinetic hand":"Saber hand",parent,Vector3.zero);
            var armor=Surface(new Color(.105f,.075f,.14f),.35f);
            var skin=Surface(new Color(.46f,.29f,.32f));
            var accent=Material(left?Crimson:Cyan,2);
            Shape("Forearm",PrimitiveType.Capsule,hand,new Vector3(0,-.17f,-.10f),new Vector3(.13f,.21f,.12f),armor).localRotation=Quaternion.Euler(-26,0,0);
            Shape("Palm",PrimitiveType.Sphere,hand,Vector3.zero,new Vector3(.17f,.20f,.065f),skin);
            Shape("Gauntlet",PrimitiveType.Cube,hand,new Vector3(0,-.09f,-.012f),new Vector3(.18f,.10f,.08f),armor);
            var plate=Shape("Contoured dorsal plate",PrimitiveType.Cube,hand,new Vector3(0,-.012f,-.045f),new Vector3(.14f,.15f,.026f),armor);
            plate.GetComponent<MeshFilter>().sharedMesh=BeveledCube; plate.localScale*=2;
            var gem=Shape("Inset arcane crystal",PrimitiveType.Cube,hand,new Vector3(0,-.015f,-.065f),new Vector3(.038f,.038f,.012f),accent);
            gem.localRotation=Quaternion.Euler(0,0,45);
            Shape("Wrist seal",PrimitiveType.Cube,hand,new Vector3(0,-.104f,-.058f),new Vector3(.145f,.014f,.014f),accent);
            // A modelled bracer replaces the blocky armour; the procedural fingers keep animating.
            if(ArcaneArt.Model("Gauntlet",hand,new Vector3(0,-.13f,-.05f),.3f,left))
                foreach(var part in new[]{"Forearm","Gauntlet","Contoured dorsal plate"}) hand.Find(part).gameObject.SetActive(false);
            if(ArcaneArt.RiggedHand(hand,left,accent,out joints,out tips))
            {
                foreach(string primitive in new[]{"Palm","Forearm","Gauntlet","Contoured dorsal plate","Inset arcane crystal"})
                    hand.Find(primitive).gameObject.SetActive(false);
                return hand;
            }
            joints=new Transform[5][]; tips=new Transform[5];
            for(int finger=0;finger<5;finger++)
            {
                joints[finger]=new Transform[3];
                float x=(finger-2)*.036f, y=.072f;
                if(finger==0) { x=left?.095f:-.095f; y=-.008f; }
                var root=Empty("Finger "+finger,hand,new Vector3(x,y,0));
                root.localRotation=Quaternion.Euler(0,0,finger==0?(left?-48:48):(2-finger)*-6);
                Transform previous=root;
                for(int bone=0;bone<3;bone++)
                {
                    var joint=Empty("Joint "+bone,previous,bone==0?Vector3.zero:new Vector3(0,.042f,0));
                    joints[finger][bone]=joint;
                    Shape("Phalanx",PrimitiveType.Capsule,joint,new Vector3(0,.019f,0),new Vector3(.025f,.027f,.025f),skin);
                    if(bone==0) Shape("Knuckle armor",PrimitiveType.Sphere,joint,new Vector3(0,.02f,-.012f),new Vector3(.027f,.032f,.012f),armor);
                    previous=joint;
                }
                tips[finger]=Shape("Fingertip energy",PrimitiveType.Sphere,previous,new Vector3(0,.042f,0),Vector3.one*.018f,accent);
                if(left) Trail(tips[finger],Crimson,.015f,.22f);
            }
            return hand;
        }
    }

    // Optional drop-in art from Resources/Art. Every hook falls back to the procedural look.
    public static class ArcaneArt
    {
        public const string Folder="Art/";
        public static Texture2D Texture(string name) => Resources.Load<Texture2D>(Folder+name);
        public static void Paint(Material surface,Texture2D texture,Vector2 tiling,float glow=0,Color? tint=null)
        {
            if(!texture || !surface) return;
            surface.SetTexture("_BaseColorMap",texture); surface.SetTextureScale("_BaseColorMap",tiling);
            surface.SetColor("_BaseColor",tint??Color.white);
            if(glow>0)
            {
                surface.SetTexture("_EmissiveColorMap",texture); surface.SetTextureScale("_EmissiveColorMap",tiling);
                surface.EnableKeyword("_EMISSIVE_COLOR_MAP"); surface.SetColor("_EmissiveColor",Color.white*glow);
            }
        }
        // Instantiates an imported model, swaps non-HDRP materials for lit ones, turns its longest
        // side upright, and scales that side to the given length, centred on the holder.
        public static Transform Model(string name,Transform parent,Vector3 position,float length,bool mirror=false,bool upright=true)
        {
            var prefab=Resources.Load<GameObject>(Folder+name); if(!prefab) return null;
            var holder=ArcaneVisuals.Empty(name+" model",parent,position);
            var model=Object.Instantiate(prefab,holder,false).transform;
            model.localPosition=Vector3.zero; // Retain the imported FBX axis conversion.
            foreach(var collider in model.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++) materials[i]=ModelSurface(name,materials[i]);
                renderer.sharedMaterials=materials; renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var size=Bounds(holder,model).size;
            if(upright && size.x>size.y && size.x>=size.z) model.localRotation=Quaternion.Euler(0,0,90)*model.localRotation;
            else if(upright && size.z>size.y && size.z>size.x) model.localRotation=Quaternion.Euler(90,0,0)*model.localRotation;
            var bounds=Bounds(holder,model);
            float extent=upright?bounds.size.y:Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            if(extent>0) model.localScale*=length/extent;
            model.localPosition-=Bounds(holder,model).center;
            if(mirror) holder.localScale=new Vector3(-1,1,1);
            return holder;
        }
        static Bounds Bounds(Transform space,Transform model)
        {
            bool any=false; var result=new Bounds();
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                Mesh mesh=renderer is SkinnedMeshRenderer skinned?skinned.sharedMesh:renderer.GetComponent<MeshFilter>()?renderer.GetComponent<MeshFilter>().sharedMesh:null;
                if(!mesh) continue;
                var b=mesh.bounds;
                for(int corner=0;corner<8;corner++)
                {
                    var p=b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                    p=space.InverseTransformPoint(renderer.transform.TransformPoint(p));
                    if(!any) { result=new Bounds(p,Vector3.zero); any=true; } else result.Encapsulate(p);
                }
            }
            return result;
        }
        static readonly Dictionary<string,Material> modelSurfaces=new Dictionary<string,Material>();
        static Material ModelSurface(string name,Material source)
        {
            if(modelSurfaces.TryGetValue(name,out var cached) && cached) return cached;
            var albedo=Texture(name+"_Albedo");
            if(!albedo) return Convert(source);
            if(source && source.HasProperty("_BaseColorMap") && source.GetTexture("_BaseColorMap")==albedo) return modelSurfaces[name]=source;
            var surface=new Material(Resources.Load<Material>("ArcaneSurface"));
            surface.SetTexture("_BaseColorMap",albedo); surface.SetColor("_BaseColor",Color.white);
            surface.SetFloat("_Metallic",name=="VRHands"?.06f:.36f); surface.SetFloat("_Smoothness",.42f);
            modelSurfaces[name]=surface; return surface;
        }
        public static bool RiggedHand(Transform hand,bool left,Material accent,out Transform[][] joints,out Transform[] tips)
        {
            joints=null; tips=null;
            var prefab=Resources.Load<GameObject>(Folder+"VRHands");
            if(!prefab) return false;
            var model=Object.Instantiate(prefab,hand,false).transform;
            var bones=new Dictionary<string,Transform>();
            foreach(var t in model.GetComponentsInChildren<Transform>(true)) bones[t.name]=t;
            string side=left?"J_Left_Hand":"J_Right_Hand";
            string[] names={"Thumb","Index","Middle","Ring","Pinky"};
            bool valid=bones.ContainsKey(side);
            foreach(string finger in names) for(int j=1;j<=4;j++) valid &= bones.ContainsKey(side+finger+j);
            if(!valid) { model.gameObject.SetActive(false); return false; }
            foreach(var anim in model.GetComponentsInChildren<Animator>()) anim.enabled=false;
            var wrist=bones[side]; var middle=bones[side+"Middle4"];
            Vector3 up=hand.InverseTransformVector(middle.position-wrist.position);
            Vector3 across=hand.InverseTransformVector(bones[side+"Index1"].position-bones[side+"Pinky1"].position)*(left?1:-1);
            var basis=Quaternion.LookRotation(Vector3.Cross(across,up),up);
            model.localRotation=Quaternion.Inverse(basis)*model.localRotation;
            model.localScale*=.245f/up.magnitude;
            model.localPosition+=Vector3.down*.10f-hand.InverseTransformPoint(wrist.position);
            if(bones.TryGetValue(left?"J_Right_Hand":"J_Left_Hand",out var other)) { var branch=other.parent; if(branch.name!=(left?"J_Right":"J_Left")) branch=other; branch.localScale=Vector3.zero; }
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var mats=renderer.sharedMaterials; for(int i=0;i<mats.Length;i++) mats[i]=ModelSurface("VRHands",mats[i]);
                renderer.sharedMaterials=mats;
                if(renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen=true;
            }
            joints=new Transform[5][]; tips=new Transform[5];
            for(int f=0;f<5;f++)
            {
                joints[f]=new Transform[3];
                for(int j=0;j<3;j++)
                {
                    var bone=bones[side+names[f]+(j+1)];
                    var pivot=ArcaneVisuals.Empty("Finger flex",bone.parent,bone.localPosition);
                    pivot.rotation=hand.rotation;
                    if(!left)
                    {
                        Vector3 segment=bones[side+names[f]+(j+2)].position-bone.position;
                        Vector3 inward=Vector3.ProjectOnPlane(hand.forward,segment);
                        if(inward.sqrMagnitude>.0001f) pivot.rotation=Quaternion.LookRotation(inward,segment);
                    }
                    bone.SetParent(pivot,true); joints[f][j]=pivot;
                }
                tips[f]=ArcaneVisuals.Empty("Finger tip",bones[side+names[f]+"4"],Vector3.zero);
                if(left) ArcaneVisuals.Trail(tips[f],ArcaneVisuals.Crimson,.015f,.22f);
            }
            return true;
        }
        static Material Convert(Material source)
        {
            if(source && source.shader && source.shader.name.StartsWith("HDRP")) return source;
            var template=Resources.Load<Material>("ArcaneSurface");
            var material=template?new Material(template):new Material(Shader.Find("HDRP/Lit"));
            material.SetFloat("_Metallic",.5f); material.SetFloat("_Smoothness",.55f);
            if(!source) return material;
            Texture map=source.HasProperty("_BaseMap")?source.GetTexture("_BaseMap"):source.HasProperty("_MainTex")?source.GetTexture("_MainTex"):null;
            Color color=source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.HasProperty("_Color")?source.GetColor("_Color"):Color.white;
            if(map) material.SetTexture("_BaseColorMap",map);
            material.SetColor("_BaseColor",color);
            if(source.HasProperty("_EmissionMap") && source.GetTexture("_EmissionMap") && source.IsKeywordEnabled("_EMISSION"))
            {
                material.SetTexture("_EmissiveColorMap",source.GetTexture("_EmissionMap")); material.EnableKeyword("_EMISSIVE_COLOR_MAP");
                material.SetColor("_EmissiveColor",Color.white*2);
            }
            return material;
        }
    }
}
