using UnityEngine;
namespace SealGugu {
    /// <summary>One small editable breathing mesh, keeping the spine and face stable.</summary>
    [ExecuteAlways,RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class HuhuBelly:MonoBehaviour {
        Mesh mesh;MaterialPropertyBlock properties;
        readonly Vector3[] vertices=new Vector3[65*3];
        public void Paint(Texture2D image,Rect rect,float expansion,Material material,int order,float strength) {
            if(!mesh){
                mesh=new Mesh{name="Seal abdomen deformation",hideFlags=HideFlags.HideAndDontSave};mesh.MarkDynamic();
                var uv=new Vector2[vertices.Length];var colors=new Color[vertices.Length];var normals=new Vector3[vertices.Length];var tris=new int[64*2*6];
                for(int row=0;row<3;row++)for(int col=0;col<65;col++){int i=row*65+col;float v=row==0?0:row==1?.57f:1;uv[i]=new Vector2(col/64f,1-v);colors[i]=Color.white;normals[i]=Vector3.back;}
                for(int row=0;row<2;row++)for(int col=0;col<64;col++){int t=(row*64+col)*6,i=row*65+col;tris[t]=i;tris[t+1]=i+65;tris[t+2]=i+1;tris[t+3]=i+1;tris[t+4]=i+65;tris[t+5]=i+66;}
                mesh.vertices=vertices;mesh.uv=uv;mesh.colors=colors;mesh.normals=normals;mesh.triangles=tris;GetComponent<MeshFilter>().sharedMesh=mesh;
            }
            transform.localPosition=new Vector3((rect.center.x-640)/100,(360-rect.center.y)/100,0);
            for(int row=0;row<3;row++)for(int col=0;col<65;col++){
                float u=col/64f,v=row==0?0:row==1?.57f:1;
                if(row==2){float q=Mathf.Clamp01((u-.18f)/.48f);v+=.43f*Mathf.Pow(Mathf.Sin(q*Mathf.PI),2)*strength*expansion;}
                vertices[row*65+col]=new Vector3((u-.5f)*rect.width/100,(.5f-v)*rect.height/100,0);
            }
            mesh.vertices=vertices;mesh.RecalculateBounds();var renderer=GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=order;
            if(properties==null)properties=new MaterialPropertyBlock();properties.SetTexture("_MainTex",image);properties.SetColor("_Color",Color.white);properties.SetColor("unity_SpriteColor",Color.white);properties.SetVector("unity_SpriteProps",new Vector4(1,1,1,1));renderer.SetPropertyBlock(properties);
        }
        void OnDestroy(){if(mesh){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}}
    }
}
