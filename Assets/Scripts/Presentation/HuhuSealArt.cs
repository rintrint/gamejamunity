using UnityEngine;
namespace SealGugu {
    [CreateAssetMenu(menuName="海豹呼呼/海豹美術設定",fileName="SealArt")]
    public sealed class HuhuSealArt:ScriptableObject {
        [System.Serializable] public struct SwimForm { public Texture2D image;public Rect crop;public Vector2 mouthAnchor; }
        [Tooltip("Thin, medium, fat; top-left pixel crop, proportional body size, mouth pivot.")]
        public SwimForm[] swimming=new SwimForm[3];
        public Texture2D portrait,eatingAtlas,menuHungerAtlas;
        [Header("Artist menu assets · GIF uses original per-frame timing")]
        public Texture2D menuBackground,menuGifAtlas;
        public TextAsset menuGifTiming;
        [Header("Shore arrival and endings")] public Texture2D surfaceFisher,resting,angel;
    }
}
