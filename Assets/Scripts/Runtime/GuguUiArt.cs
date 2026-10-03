using System;
using System.Collections.Generic;
using UnityEngine;

namespace SealGugu
{
    /// <summary>floe.css ice-button filter: brightness(1.035), drop-shadow(0 5px 5px #4f9dbc35).</summary>
    public sealed class GuguUiArt : IDisposable
    {
        sealed class Filter {
            public Texture2D source, shadow;
            public Color32[] pixels;
            public readonly Texture2D[] brightness = new Texture2D[9];
            public float progress, from, target;
            public double started;
            public Rect bounds;
            public float blur=5, offset=5;
            public Color32 shadowColor=new Color32(79,157,188,53);
        }
        readonly Dictionary<Texture2D,Filter> filters = new Dictionary<Texture2D,Filter>();
        readonly Dictionary<Rect,Filter> buttonFilters=new Dictionary<Rect,Filter>();
        readonly List<Texture2D> generated = new List<Texture2D>();
        public static Rect Fit(Rect rect, Texture2D texture) {
            float scale = Mathf.Min(rect.width/texture.width, rect.height/texture.height);
            Vector2 size = new Vector2(texture.width, texture.height)*scale;
            return new Rect(rect.center-size*.5f, size);
        }
        Texture2D Texture(int width, int height, Color32[] pixels) {
            var texture = new Texture2D(width,height,TextureFormat.RGBA32,false);
            texture.SetPixels32(pixels);texture.Apply();texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;
            generated.Add(texture);return texture;
        }
        static float Ease(float x) {
            // CSS ease: cubic-bezier(.25,.1,.25,1), invert its x coordinate.
            float lo=0,hi=1,t=0;
            for(int i=0;i<12;i++){t=(lo+hi)*.5f;float u=1-t;float px=3*u*u*t*.25f+3*u*t*t*.25f+t*t*t;if(px<x)lo=t;else hi=t;}
            float v=1-t;return 3*v*v*t*.1f+3*v*t*t+t*t*t;
        }
        Filter Get(Texture2D source,Rect bounds) {
            if(filters.TryGetValue(source,out var filter))return filter;
            filter=new Filter{source=source,pixels=source.GetPixels32(),bounds=bounds};filter.brightness[0]=source;
            filters.Add(source,filter);return filter;
        }
        Texture2D Bright(Filter filter,int step) {
            if(filter.brightness[step])return filter.brightness[step];
            var pixels=new Color32[filter.pixels.Length];float factor=1+.035f*step/8;
            for(int i=0;i<pixels.Length;i++){Color32 p=filter.pixels[i];pixels[i]=new Color32((byte)Mathf.Min(255,Mathf.RoundToInt(p.r*factor)),(byte)Mathf.Min(255,Mathf.RoundToInt(p.g*factor)),(byte)Mathf.Min(255,Mathf.RoundToInt(p.b*factor)),p.a);}
            return filter.brightness[step]=Texture(filter.source.width,filter.source.height,pixels);
        }
        Texture2D Shadow(Filter filter) {
            if(filter.shadow)return filter.shadow;
            // Render the actual silhouette, padded by 3 sigma. Half-resolution is sufficient for a 5px Gaussian blur.
            float padding=filter.blur*3;
            int w=Mathf.CeilToInt((filter.bounds.width+padding*2)*.5f),h=Mathf.CeilToInt((filter.bounds.height+padding*2)*.5f);
            var alpha=new float[w*h];var horizontal=new float[w*h];int radius=Mathf.CeilToInt(filter.blur*1.5f);float sigma=filter.blur*.5f,sum=0;var weights=new float[radius*2+1];
            for(int d=-radius;d<=radius;d++){float value=Mathf.Exp(-d*d/(2*sigma*sigma));weights[d+radius]=value;sum+=value;}
            for(int i=0;i<weights.Length;i++)weights[i]/=sum;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                float u=(x*2+1-padding)/filter.bounds.width,v=(y*2+1-padding)/filter.bounds.height;
                if(u>=0&&u<1&&v>=0&&v<1){int sx=Mathf.Min(filter.source.width-1,(int)(u*filter.source.width)),sy=Mathf.Min(filter.source.height-1,(int)(v*filter.source.height));alpha[y*w+x]=filter.pixels[sy*filter.source.width+sx].a/255f;}
            }
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)for(int d=-radius;d<=radius;d++)if(x+d>=0&&x+d<w)horizontal[y*w+x]+=alpha[y*w+x+d]*weights[d+radius];
            var colors=new Color32[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){float a=0;for(int d=-radius;d<=radius;d++)if(y+d>=0&&y+d<h)a+=horizontal[(y+d)*w+x]*weights[d+radius];colors[y*w+x]=new Color32(filter.shadowColor.r,filter.shadowColor.g,filter.shadowColor.b,(byte)Mathf.RoundToInt(a*filter.shadowColor.a));}
            return filter.shadow=Texture(w,h,colors);
        }
        public void DrawButtonShadow(Rect rect,bool hover,bool enabled,bool reduced) {
            if(Event.current.type!=EventType.Repaint)return;
            var key=new Vector2Int(Mathf.CeilToInt(rect.width),Mathf.CeilToInt(rect.height));
            if(!buttonFilters.TryGetValue(rect,out var filter)) {
                if(!hover||!enabled)return;
                var pixels=new Color32[key.x*key.y];float radius=Mathf.Min(22,Mathf.Min(key.x,key.y)*.5f);
                for(int y=0;y<key.y;y++)for(int x=0;x<key.x;x++){
                    float dx=Mathf.Max(radius-x-.5f,x+.5f-(key.x-radius)),dy=Mathf.Max(radius-y-.5f,y+.5f-(key.y-radius));
                    float distance=Mathf.Sqrt(Mathf.Max(0,dx)*Mathf.Max(0,dx)+Mathf.Max(0,dy)*Mathf.Max(0,dy));
                    pixels[y*key.x+x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(Mathf.Clamp01(radius-distance)*255));
                }
                filter=new Filter{source=Texture(key.x,key.y,pixels),pixels=pixels,bounds=new Rect(0,0,key.x,key.y),blur=15,offset=4,shadowColor=new Color32(87,142,165,33)};
                buttonFilters.Add(rect,filter);
            }
            float target=hover&&enabled?1:0;double now=Time.realtimeSinceStartupAsDouble;
            if(filter.target!=target){filter.from=filter.progress;filter.target=target;filter.started=now;}
            filter.progress=!enabled?0:reduced?target:Mathf.Lerp(filter.from,filter.target,Ease(Mathf.Clamp01((float)(now-filter.started)/.16f)));
            if(filter.progress<=0)return;
            Color color=GUI.color;GUI.color=new Color(color.r,color.g,color.b,color.a*filter.progress);
            GUI.DrawTexture(new Rect(rect.x-45,rect.y-41,rect.width+90,rect.height+90),Shadow(filter));GUI.color=color;
        }
        public void Draw(Rect rect,Texture2D source,bool hover,bool enabled,bool reduced) {
            if(!source||Event.current.type!=EventType.Repaint)return;
            Rect bounds=Fit(rect,source);var filter=Get(source,bounds);float target=hover&&enabled?1:0;
            double now=Time.realtimeSinceStartupAsDouble;
            if(filter.target!=target){filter.from=filter.progress;filter.target=target;filter.started=now;}
            filter.progress=!enabled?0:reduced?target:Mathf.Lerp(filter.from,filter.target,Ease(Mathf.Clamp01((float)(now-filter.started)/.18f)));
            if(filter.progress>0){Color color=GUI.color;GUI.color=new Color(color.r,color.g,color.b,color.a*filter.progress);GUI.DrawTexture(new Rect(bounds.x-15,bounds.y-10,bounds.width+30,bounds.height+30),Shadow(filter));GUI.color=color;}
            GUI.DrawTexture(bounds,Bright(filter,Mathf.RoundToInt(filter.progress*8)),ScaleMode.StretchToFill,true);
        }
        public void Dispose(){foreach(var texture in generated)UnityEngine.Object.Destroy(texture);generated.Clear();filters.Clear();buttonFilters.Clear();}
    }
}
