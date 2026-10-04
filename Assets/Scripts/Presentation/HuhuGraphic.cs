using UnityEngine;

namespace SealGugu
{
    /// <summary>One editable native 2D graphic. Its parent stores artist offsets; only this child's pose is driven.</summary>
    [ExecuteAlways, RequireComponent(typeof(SpriteRenderer))]
    public sealed class HuhuGraphic : MonoBehaviour
    {
        [Tooltip("Optional replacement artwork; leave empty to use the original game asset.")]
        public Texture2D replacement;
        [Tooltip("Apply a different crop when replacing artwork. Top-left pixel coordinates; zero means whole image.")]
        public Rect replacementCrop;
        public Color tint=Color.white;
        public bool receiveLight=true;
        Texture2D source;
        Rect sourceRect;
        [SerializeField, HideInInspector] Vector2 size;
        [SerializeField, HideInInspector] float alpha=1;
        Sprite sprite;
        Texture2D lastTexture;
        Rect lastCrop;
        SpriteRenderer graphic;
        public void Paint(Texture2D texture,Rect crop,Vector2 dimensions,float opacity,Material lit,Material unlit,int order)
        {
            source=texture;sourceRect=crop;size=dimensions;alpha=opacity;
            if(!graphic)graphic=GetComponent<SpriteRenderer>();
            if(replacement){texture=replacement;crop=replacementCrop.width>0?replacementCrop:new Rect(0,0,texture.width,texture.height);}
            // Integer source bounds prevent duplicate sprites from subpixel crop drift.
            crop=Rect.MinMaxRect(Mathf.Clamp(Mathf.Floor(crop.x),0,texture.width-1),Mathf.Clamp(Mathf.Floor(crop.y),0,texture.height-1),Mathf.Clamp(Mathf.Ceil(crop.xMax),1,texture.width),Mathf.Clamp(Mathf.Ceil(crop.yMax),1,texture.height));
            if(!sprite||lastTexture!=texture||lastCrop!=crop){
                if(sprite){if(Application.isPlaying)Destroy(sprite);else DestroyImmediate(sprite);}
                sprite=UnityEngine.Sprite.Create(texture,new Rect(crop.x,texture.height-crop.yMax,crop.width,crop.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
                sprite.hideFlags=HideFlags.HideAndDontSave;sprite.name=texture.name+" crop";lastTexture=texture;lastCrop=crop;
            }
            graphic.sprite=sprite;graphic.sharedMaterial=receiveLight?lit:unlit;graphic.sortingOrder=order;
            graphic.color=new Color(tint.r,tint.g,tint.b,tint.a*alpha);
            transform.localScale=new Vector3(size.x/crop.width,size.y/crop.height,1);
        }
        void OnDestroy(){if(sprite){if(Application.isPlaying)Destroy(sprite);else DestroyImmediate(sprite);}}
    }
}
