using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// True 3D geometry over the pixel-art stage. The planar flight solver owns the position.
public sealed class AngelGiftModel : MonoBehaviour
{
    readonly List<Material> materials=new List<Material>();
    readonly List<Mesh> meshes=new List<Mesh>();
    Texture2D paperTexture;
    GameObject worldKeyLight;
    Camera previewCamera;
    GameObject previewRoot;
    RenderTexture preview;
    public RenderTexture Preview => preview;
    Material paper, ribbon, gold;
    Mesh box;

    public void Build()
    {
        paper=Clone("ParcelPaper",new Color(.94f,.82f,.57f),.02f,.25f);
        ribbon=Clone("ParcelRibbon",new Color(.47f,.025f,.07f),.25f,.58f);
        gold=Clone("ParcelGold",new Color(1,.68f,.20f),.72f,.65f);
        paperTexture=new Texture2D(64,64,TextureFormat.RGB24,false);paperTexture.name="Wrapping paper fibres";
        var pixels=new Color[64*64];
        for(int y=0;y<64;y++) for(int x=0;x<64;x++)
        {
            float grain=.91f+Mathf.PerlinNoise(x*.75f,y*.75f)*.09f;
            bool woven=x%16==0 || y%16==0;
            pixels[y*64+x]=Color.white*(grain-(woven?.055f:0));
        }
        paperTexture.SetPixels(pixels);paperTexture.Apply();paper.mainTexture=paperTexture;
        box=BevelBox(); BuildParcel(transform,0);
        var light=new GameObject("Parcel soft key light").AddComponent<Light>();light.transform.SetParent(transform.root,false);worldKeyLight=light.gameObject;
        light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(35,-30,0);light.intensity=.85f;light.color=new Color(1,.86f,.66f);light.cullingMask=(1<<0)|(1<<30);light.shadows=LightShadows.None;
        previewRoot=new GameObject("Gift preview studio");previewRoot.transform.position=new Vector3(1000,1000,0);
        BuildParcel(previewRoot.transform,30);previewRoot.transform.rotation=Quaternion.Euler(18,28,-6);
        preview=new RenderTexture(256,256,16,RenderTextureFormat.ARGB32);preview.name="Real 3D gift portrait";preview.Create();
        previewCamera=new GameObject("Gift portrait camera").AddComponent<Camera>();previewCamera.transform.SetParent(previewRoot.transform,false);
        previewCamera.transform.position=new Vector3(1000,1000,-3);previewCamera.transform.rotation=Quaternion.identity;
        previewCamera.orthographic=true;previewCamera.orthographicSize=.87f;previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=Color.clear;
        previewCamera.cullingMask=1<<30;previewCamera.targetTexture=preview;previewCamera.allowHDR=false;previewCamera.enabled=false;
    }
    Material Clone(string resource,Color color,float metallic,float smoothness)
    {
        var source=Resources.Load<Material>(resource);
        if(!source) throw new System.InvalidOperationException("Missing shader resource: "+resource+". Refresh the Editor first.");
        var mat=new Material(source);mat.color=color;mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Glossiness",smoothness);materials.Add(mat);return mat;
    }
    void Piece(string name,Transform parent,Vector3 p,Vector3 size,Material mat,Mesh mesh=null,int layer=0)
    {
        var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.layer=layer;g.transform.SetParent(parent,false);
        g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<MeshFilter>().sharedMesh=mesh?mesh:box;
        var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.sortingOrder=18;
    }
    void BuildParcel(Transform parent,int layer)
    {
        Piece("Bevelled paper box",parent,Vector3.zero,Vector3.one,paper,null,layer);
        foreach(float sign in new[]{-1f,1f})
        {
            Piece("Front/back satin ribbon",parent,new Vector3(0,0,sign*.505f),new Vector3(.115f,1.015f,.018f),ribbon,null,layer);
            Piece("Side satin ribbon",parent,new Vector3(sign*.505f,0,0),new Vector3(.018f,1.015f,.115f),ribbon,null,layer);
            Piece("Top/bottom ribbon A",parent,new Vector3(0,sign*.505f,0),new Vector3(.115f,.018f,1.015f),ribbon,null,layer);
            Piece("Top/bottom ribbon B",parent,new Vector3(0,sign*.515f,0),new Vector3(1.015f,.018f,.115f),ribbon,null,layer);
            Piece("Satin bow",parent,Vector3.zero,Vector3.one,ribbon,Bow(sign),layer);
        }
        Piece("Bow gold knot",parent,new Vector3(0,.57f,0),new Vector3(.18f,.12f,.16f),gold,null,layer);
        Piece("Gift tag",parent,new Vector3(.24f,-.13f,-.53f),new Vector3(.24f,.16f,.018f),paper,null,layer);
        Piece("Tag gold seal",parent,new Vector3(.24f,-.13f,-.547f),new Vector3(.045f,.055f,.012f),gold,null,layer);
    }
    Mesh BevelBox()
    {
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        float[] grid={-.5f,-.46f,.46f,.5f};
        foreach(var n in new[]{Vector3.left,Vector3.right,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
        {
            var up=Mathf.Abs(n.y)>.5f?Vector3.forward:Vector3.up;var right=Vector3.Cross(up,n);int first=vertices.Count;
            for(int y=0;y<4;y++) for(int x=0;x<4;x++)
            {
                Vector3 raw=n*.5f+right*grid[x]+up*grid[y];
                Vector3 inner=new Vector3(Mathf.Clamp(raw.x,-.46f,.46f),Mathf.Clamp(raw.y,-.46f,.46f),Mathf.Clamp(raw.z,-.46f,.46f));
                var normal=(raw-inner).normalized;vertices.Add(inner+normal*.04f);normals.Add(normal);uv.Add(new Vector2(grid[x]+.5f,grid[y]+.5f));
            }
            for(int y=0;y<3;y++) for(int x=0;x<3;x++) {int a=first+y*4+x;triangles.AddRange(new[]{a,a+1,a+4,a+1,a+5,a+4});}
        }
        var mesh=new Mesh {name="Rounded wrapping-paper box"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();meshes.Add(mesh);return mesh;
    }
    Mesh Bow(float side)
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
        for(int i=0;i<=20;i++)
        {
            float t=i/20f,u=1-t;
            Vector3 a=new Vector3(0,.57f,0),b=new Vector3(side*.45f,.96f,-.12f),c=new Vector3(side*.64f,.57f,.12f),d=new Vector3(0,.57f,.025f);
            Vector3 p=u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;
            Vector3 tangent=3*u*u*(b-a)+6*u*t*(c-b)+3*t*t*(d-c);
            Vector3 width=Vector3.Cross(tangent,new Vector3(0,1,-1)).normalized*.065f;
            vertices.Add(p-width);vertices.Add(p+width);uv.Add(new Vector2(0,t));uv.Add(new Vector2(1,t));
            if(i<20) {int k=i*2;triangles.AddRange(new[]{k,k+1,k+2,k+1,k+3,k+2,k+2,k+1,k,k+2,k+3,k+1});}
        }
        var mesh=new Mesh {name="Curved satin bow"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);return mesh;
    }
    public void ShowPreview(bool show) {if(previewCamera)previewCamera.enabled=show;}
    void OnDestroy()
    {
        if(worldKeyLight)Destroy(worldKeyLight);
        if(previewRoot)Destroy(previewRoot);if(preview){preview.Release();Destroy(preview);}
        if(paperTexture)Destroy(paperTexture);foreach(var m in materials)if(m)Destroy(m);foreach(var m in meshes)if(m)Destroy(m);
    }
}
