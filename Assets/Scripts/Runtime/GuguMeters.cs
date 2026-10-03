using System;
using System.Collections.Generic;
using UnityEngine;

namespace SealGugu
{
    /// <summary>scenes.css / duet.css progress bars: 1px rim, 2px padding and native gradients.</summary>
    public sealed class GuguMeters : IDisposable
    {
        sealed class Motion { public float value,from,target; public double began; }
        readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        readonly Dictionary<string,Motion> motions=new Dictionary<string,Motion>();
        static Color Rgb(uint hex,float a=1) { return new Color((hex>>16&255)/255f,(hex>>8&255)/255f,(hex&255)/255f,a); }
        Texture2D Track(int w,int h,bool food) {
            string key="track"+w+"x"+h+food;if(textures.TryGetValue(key,out var cached))return cached;
            var texture=new Texture2D(w,h,TextureFormat.RGBA32,false);var data=new Color[w*h];
            float radius=h*.5f;Color bg=Rgb(0x416c87,food?102f/255:107f/255),rim=Rgb(food?0xedfaffu:0xeafaffu);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                float dx=Mathf.Abs(x+.5f-w*.5f)-(w*.5f-radius),dy=Mathf.Abs(y+.5f-h*.5f);
                float distance=Mathf.Sqrt(Mathf.Max(0,dx)*Mathf.Max(0,dx)+dy*dy)-radius;
                Color c=Color.Lerp(bg,rim,Mathf.Clamp01(1.5f+distance));c.a*=Mathf.Clamp01(.5f-distance);data[y*w+x]=c;
            }
            texture.SetPixels(data);texture.Apply();texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;textures.Add(key,texture);return texture;
        }
        Texture2D Gradient(string key,Color start,Color end) {
            if(textures.TryGetValue(key,out var cached))return cached;
            var texture=new Texture2D(256,1,TextureFormat.RGBA32,false);var pixels=new Color[256];
            for(int x=0;x<256;x++)pixels[x]=Color.Lerp(start,end,x/255f);
            texture.SetPixels(pixels);texture.Apply();texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;textures.Add(key,texture);return texture;
        }
        public void Draw(Rect rect,float amount,string kind,bool low,bool immediate) {
            if(Event.current.type!=EventType.Repaint)return;
            amount=Mathf.Clamp01(amount);double now=Time.realtimeSinceStartupAsDouble;
            if(!motions.TryGetValue(kind,out var motion)){motion=new Motion{value=amount,from=amount,target=amount,began=now};motions.Add(kind,motion);}
            motion.value=immediate?amount:Mathf.Lerp(motion.from,motion.target,Mathf.Clamp01((float)(now-motion.began)/.1f));
            // Advance the previous transition before accepting a moving target;
            // oxygen changes every frame, so resetting first would freeze the bar.
            if(motion.target!=amount){motion.from=motion.value;motion.target=amount;motion.began=now;}
            if(kind=="air")GUI.DrawTexture(new Rect(rect.x+rect.height*.5f,rect.y+rect.height,rect.width-rect.height,1),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,Rgb(0x3c6779,119f/255),0,0);
            GUI.DrawTexture(rect,Track(Mathf.CeilToInt(rect.width),Mathf.CeilToInt(rect.height),kind=="food"));
            Color start,end;
            if(kind=="stability"){start=Rgb(low?0xeea49cu:0xd8f0dau);end=Rgb(low?0xeea49cu:0xfff0b2u);}
            else if(kind=="food"){start=Rgb(0xbbdbbc);end=Rgb(0xf0dda5);}
            else {start=Rgb(low?0xdfc79cu:0xb1e8efu);end=Rgb(low?0xe8ac9du:0xc9f0eeu);}
            Rect fill=new Rect(rect.x+3,rect.y+3,Mathf.Max(0,rect.width-6)*motion.value,rect.height-6);
            if(fill.width>0)GUI.DrawTexture(fill,Gradient(kind+low,start,end),ScaleMode.StretchToFill,true,0,Color.white,0,Mathf.Min(fill.width,fill.height)*.5f);
            if(kind=="food")foreach(float at in new[]{1f/3,2f/3})GUI.DrawTexture(new Rect(rect.x+rect.width*at,rect.y+1,2,rect.height-2),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,Rgb(0xf9ffff,179f/255),0,0);
        }
        public void Dispose(){foreach(var texture in textures.Values)UnityEngine.Object.Destroy(texture);textures.Clear();motions.Clear();}
    }
}
