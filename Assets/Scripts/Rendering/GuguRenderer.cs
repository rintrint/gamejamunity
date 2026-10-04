using System;
using System.Collections.Generic;
using UnityEngine;

namespace SealGugu
{
    /// <summary>
    /// Native presentation port of gugu-view.js. All source rectangles are in the
    /// original, top-left-origin artwork; all seals retain their source aspect ratio.
    /// The renderer deliberately has no judgement clock or input cooldown of its own.
    /// </summary>
    public sealed class GuguRenderer : IDisposable
    {
        public HuhuStage stage;
        HuhuSealAnimation Rig=>stage?stage.sealAnimation:null;
        public float speedMultiplier = .7f;
        public float menuHungerProgress=-1;
        public double menuHungerElapsed=-1;
        [Serializable] public sealed class MenuGifData {
            public int frameWidth,frameHeight,cropX,cropY,cropWidth,cropHeight;
            public double[] durations;
            public int Frame(double elapsed){
                if(elapsed<0||durations==null||durations.Length==0)return -1;
                double cycle=0;foreach(double d in durations)cycle+=Math.Max(.01,d);
                double at=elapsed%cycle;
                for(int i=0;i<durations.Length;i++){double d=Math.Max(.01,durations[i]);if(at<d)return i;at-=d;}
                return 0;
            }
            public Rect Crop(int frame)=>new Rect(frame*frameWidth+cropX,cropY,cropWidth,cropHeight);
        }
        MenuGifData menuGif;
        public int MenuFrameAt(double elapsed)=>menuGif!=null?menuGif.Frame(elapsed):-1;
        public int MenuHungerFrame { get; private set; }
        public bool hideNotes, reducedMotion;
        public Font font;
        public int VisibleFish { get; private set; }
        public int VisibleMissedFish { get; private set; }
        public int BiteFrame { get; private set; }
        public int InputCount { get; private set; }
        public float SealX { get; private set; }
        public float SealY { get; private set; }
        public float SealWidth { get; private set; }
        public float HeadTint { get; private set; }
        public float BellyScale { get; private set; } = 1;
        public string Scene { get; private set; } = "menu";

        readonly Dictionary<string, Texture2D> images = new Dictionary<string, Texture2D>();
        readonly Dictionary<Texture2D, Color32[]> pixels = new Dictionary<Texture2D, Color32[]>();
        readonly Dictionary<string, TintedArt> heads = new Dictionary<string, TintedArt>();
        readonly List<Texture2D> generated = new List<Texture2D>();
        readonly List<Effect> fx = new List<Effect>();
        readonly List<Catch> caught = new List<Catch>();
        readonly Rect[] effectRects = {
            new Rect(0,24,383,334), new Rect(394,22,365,336), new Rect(778,38,352,328), new Rect(1154,95,375,262),
            new Rect(0,382,383,327), new Rect(390,414,390,292), new Rect(804,382,325,327), new Rect(1140,369,390,338),
            new Rect(0,719,383,290), new Rect(400,715,368,294), new Rect(795,720,335,289), new Rect(1138,779,394,230)
        };
        readonly Rect[] mistRects = {new Rect(30,35,700,585),new Rect(753,155,766,374),new Rect(20,634,842,342),new Rect(975,628,457,339)};
        readonly Art[] swimmers = {
            new Art("floe/swim-thin",new Rect(185,670,552,158),new Vector2(.99f,.58f)),
            new Art("floe/swim-medium",new Rect(165,579,501,175),new Vector2(.99f,.61f)),
            new Art("floe/swim-fat",new Rect(164,541,497,214),new Vector2(.99f,.72f))
        };
        readonly Dictionary<string,Art> illustrations = new Dictionary<string,Art> {
            {"fisher",new Art("drift/fisher",new Rect(13,60,762,565))},
            {"friend",new Art("gugu/friend-outlined",new Rect(0,7,1446,1048))},
            {"splash",new Art("drift/splash",new Rect(28,24,1245,846))},
            {"angel",new Art("huhu/angel-new",new Rect(0,0,775,616))},
            {"rest",new Art("huhu/resting",new Rect(0,0,1536,1024))}
        };
        readonly Head[] swimHeads = {
            new Head(.68f,.85f,new[]{new Vector4(.91f,.30f,.084f,.37f)},new Vector4(.966f,.625f,.060f,.17f)),
            new Head(.69f,.85f,new[]{new Vector4(.911f,.39f,.079f,.34f)},new Vector4(.958f,.66f,.058f,.16f)),
            new Head(.73f,.87f,new[]{new Vector4(.913f,.505f,.077f,.26f)},new Vector4(.965f,.752f,.058f,.13f))
        };
        readonly Head portraitHead = new Head(.44f,.68f,
            new[]{new Vector4(.685f,.38f,.17f,.22f),new Vector4(.850f,.32f,.17f,.22f)},
            new Vector4(.80f,.555f,.22f,.145f),.55f,.80f,new Rect(1607,328,423,183));
        readonly Vector2[][] biteEyes = {
            new[]{new Vector2(.667f,.48f),new Vector2(.869f,.425f)},new[]{new Vector2(.67f,.48f),new Vector2(.871f,.425f)},
            new[]{new Vector2(.638f,.466f),new Vector2(.85f,.407f)},new[]{new Vector2(.668f,.415f),new Vector2(.88f,.364f)},
            new[]{new Vector2(.661f,.405f),new Vector2(.878f,.36f)},new[]{new Vector2(.66f,.405f),new Vector2(.876f,.36f)}
        };
        readonly Vector2[] biteAnchors = {new Vector2(.84f,.57f),new Vector2(.84f,.57f),new Vector2(.81f,.57f),new Vector2(.83f,.49f),new Vector2(.84f,.49f),new Vector2(.84f,.49f)};

        GuguRun current;
        Rect viewport;
        float width, height, laneY = 1, growth = 1, targetGrowth = 1, endingTime, transition = 1;
        double motionTime, biteStart = -100, inputAt = -100;
        string lastScene = "menu";
        int updatedFrame = -1;
        Texture2D shade, vignette;
        GUIStyle textStyle;

        struct Art
        {
            public string key; public Rect rect; public Vector2 anchor;
            public Art(string key,Rect rect) {this.key=key;this.rect=rect;anchor=new Vector2(.5f,.5f);}
            public Art(string key,Rect rect,Vector2 anchor) {this.key=key;this.rect=rect;this.anchor=anchor;}
        }
        sealed class Head
        {
            public float neck0,neck1,bottom0,bottom1; public Vector4[] eyes; public Vector4 mouth; public Rect mouthSource;
            public Head(float n0,float n1,Vector4[] eyes,Vector4 mouth=default(Vector4),float b0=1,float b1=1,Rect source=default(Rect))
            {neck0=n0;neck1=n1;this.eyes=eyes;this.mouth=mouth;bottom0=b0;bottom1=b1;mouthSource=source.width>0?source:new Rect(1753,346,134,165);}
            public float Mask(float x,float y) {return Smooth((x-neck0)/(neck1-neck0))*(bottom1>bottom0?1-Smooth((y-bottom0)/(bottom1-bottom0)):1);}
        }
        sealed class TintedArt
        {
            public Texture2D image; public Color32[] original,expression,work; public float[] mask; public int tone=-1;
        }
        struct Effect { public double at;public string lane;public int index;public bool fall,miss; }
        struct Catch { public double at;public string lane;public bool gold; }
        struct Health { public float severity,tint,heave,shiver,tailStrength,stoop; }
        struct Gate { public string mode;public float strength;public Note note; }

        static float Smooth(float n) {n=Mathf.Clamp01(n);return n*n*(3-2*n);}
        static float F(double n) {return (float)n;}
        static float Mod(float n,float divisor) {return (n%divisor+divisor)%divisor;}

        /// <summary>Prepare source images and face masks before the song clock starts.</summary>
        public void Preload()
        {
            if(stage&&stage.sealArt){
                var art=stage.sealArt;
                if(art.portrait)images["tide/seal"]=art.portrait;
                if(art.surfaceFisher)images["huhu/fisher-poses"]=art.surfaceFisher;
                if(art.resting)images["huhu/resting"]=art.resting;
                if(art.angel)images["huhu/angel-new"]=art.angel;
                if(art.eatingAtlas)images["duet/seal-eating"]=art.eatingAtlas;
                if(art.menuHungerAtlas)images["v14-2/menu-hunger"]=art.menuHungerAtlas;
                if(art.menuBackground)images["v15-1/menu-background"]=art.menuBackground;
                if(art.menuGifAtlas)images["v15-1/hunger-gif"]=art.menuGifAtlas;
                for(int i=0;i<Math.Min(3,art.swimming.Length);i++){var form=art.swimming[i];if(form.image){images[swimmers[i].key]=form.image;swimmers[i]=new Art(swimmers[i].key,form.crop,form.mouthAnchor);}}
            }
            var gifTiming=stage&&stage.sealArt&&stage.sealArt.menuGifTiming?stage.sealArt.menuGifTiming:Resources.Load<TextAsset>("Art/v15-1/hunger-gif");
            if(gifTiming)menuGif=JsonUtility.FromJson<MenuGifData>(gifTiming.text);
            Image("v15-1/menu-background");Image("v15-1/hunger-gif");
            foreach(string key in new[]{"tide/ocean","tide/props","tide/seal","floe/menu-ocean","duet/effects-atlas","duet/seal-eating","drift/inhale-atlas","gugu/breath-crisis","gugu/breathless-expressions","scenes/hunger-strip","v14-2/menu-hunger","huhu/fisher-poses"})Image(key);
            foreach(var illustration in illustrations.Values)Image(illustration.key);
            for(int i=0;i<swimmers.Length;i++)Tinted(Image(swimmers[i].key),swimmers[i].rect,0,swimHeads[i],"swim"+i);
            var portrait=Image("tide/seal");if(portrait!=null)Tinted(portrait,new Rect(0,0,portrait.width,portrait.height),0,portraitHead,"portrait");
            var image=Image("duet/seal-eating");if(image==null)return;
            for(int frame=0;frame<6;frame++)
            {
                var eyes=biteEyes[frame];var head=new Head(.46f,.72f,new[]{new Vector4(eyes[0].x,eyes[0].y,.17f,.225f),new Vector4(eyes[1].x,eyes[1].y,.17f,.225f)},b0:.60f,b1:.78f);
                float cell=image.width/3f,sh=image.height/2f;Tinted(image,new Rect(frame%3*cell,frame/3*sh,cell,sh),0,head,"bite"+frame);
            }
        }

        public void Reset(GuguRun run)
        {
            current=run;laneY=1;growth=targetGrowth=run==null?1:F(run.growth);motionTime=run==null?0:run.time;
            biteStart=inputAt=-100;endingTime=0;transition=1;lastScene="menu";fx.Clear();caught.Clear();InputCount=0;
        }

        public void Input(string lane,double time)
        {
            if(lane!="upper"&&lane!="lower")return;
            laneY=lane=="upper"?0:1;inputAt=time;InputCount++;
        }

        public void Emit(GameEvent e,GuguRun run)
        {
            if(e==null||run==null)return;
            if(e.type=="hit"&&e.note!=null)
            {
                bool fish=e.note.kind=="fish";
                if(fish) {biteStart=run.time;targetGrowth=Math.Max(targetGrowth,F(run.growth));caught.Add(new Catch{at=run.time,lane=e.note.lane,gold=e.note.lane=="upper"});}
                fx.Add(new Effect {at=run.time,lane=e.note.lane,index=fish?0:(e.note.kind=="surface"||e.note.kind=="exit"||e.note.kind=="dive")?1:3});
                // Every ice-hole arrival is rendered by the same clock-driven Shore sequence.
            }
            else if(e.type=="splash"||e.type=="bigBreath") {Input("lower",run.time);fx.Add(new Effect{at=run.time,lane="lower",index=1});}
            else if(e.type=="miss"&&e.note!=null) fx.Add(new Effect{at=run.time,lane=e.note.lane??"lower",index=6,miss=true});
        }

        Texture2D Image(string key)
        {
            Texture2D value;
            if(!images.TryGetValue(key,out value))
            {
                value=Resources.Load<Texture2D>("Art/"+key);images[key]=value;
                if(value==null)Debug.LogError("Missing original Gugu artwork: Art/"+key);
            }
            return value;
        }

        Color32[] Pixels(Texture2D image)
        {
            Color32[] value;if(!pixels.TryGetValue(image,out value)) {value=image.GetPixels32();pixels[image]=value;}return value;
        }

        Texture2D Texture(int w,int h,string name)
        {
            var tex=new Texture2D(w,h,TextureFormat.RGBA32,false) {name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};generated.Add(tex);return tex;
        }

        void Slice(Texture2D image,Rect source,Rect destination,float alpha=1)
        {
            if(image==null||destination.width<=0||destination.height<=0||alpha<=0)return;
            if(stage){stage.Slice(image,source,destination,alpha);return;}
            var old=GUI.color;GUI.color=new Color(old.r,old.g,old.b,old.a*alpha);
            GUI.DrawTextureWithTexCoords(destination,image,new Rect(source.x/image.width,1-(source.y+source.height)/image.height,source.width/image.width,source.height/image.height),true);
            GUI.color=old;
        }

        void Full(Texture2D image,Rect destination,float alpha=1)
        {
            if(image!=null)Slice(image,new Rect(0,0,image.width,image.height),destination,alpha);
        }

        void Sprite(Texture2D image,Rect crop,float x,float y,float w,float h,float alpha=1,float rotation=0,bool flip=false,Vector2? anchor=null,float scale=1)
        {
            if(image==null)return;
            if(stage){var p=anchor??new Vector2(.5f,.5f);stage.Slice(image,crop,new Rect(x-w*p.x*scale,y-h*p.y*scale,w*scale,h*scale),alpha,rotation,flip,new Vector2(x,y));return;}
            var matrix=GUI.matrix;var point=new Vector2(x,y);
            if(rotation!=0)GUIUtility.RotateAroundPivot(rotation*Mathf.Rad2Deg,point);
            if(flip||scale!=1)GUIUtility.ScaleAroundPivot(new Vector2(flip?-scale:scale,scale),point);
            var pivot=anchor??new Vector2(.5f,.5f);
            Slice(image,crop,new Rect(x-w*pivot.x,y-h*pivot.y,w,h),alpha);GUI.matrix=matrix;
        }

        void Seal(Texture2D image,Rect crop,float x,float y,float w,Vector2? anchor=null,float alpha=1,float rotation=0,bool flip=false,float scale=1,float tint=0,Head head=null,string cacheKey=null)
        {
            float h=w*crop.height/crop.width;
            if(tint>0&&head!=null)
            {
                image=Tinted(image,crop,tint,head,cacheKey??image.name+crop.ToString());crop=new Rect(0,0,image.width,image.height);
            }
            Sprite(image,crop,x,y,w,h,alpha,rotation,flip,anchor,scale);
        }

        void Portrait(float x,float y,float w,float time,bool flat=false,float scale=1)
        {
            var image=Image("tide/seal");if(image==null)return;
            if(flat&&Rig){Rig.Sample("Rest",time);scale*=Rig.bodyScale;}
            Seal(image,new Rect(0,0,image.width,image.height),x,y,w,rotation:flat?-.08f:0,scale:scale);
        }

        void Illustration(string key,float x,float y,float w,float alpha=1,float rotation=0,bool flip=false)
        {
            Art a;if(!illustrations.TryGetValue(key,out a))return;Seal(Image(a.key),a.rect,x,y,w,alpha:alpha,rotation:rotation,flip:flip);
        }

        void Prop(string key,float x,float y,float w,float h,float alpha=1)
        {
            var image=Image("tide/props");if(image==null)return;
            Rect q=key=="ice"?new Rect(0,.15f,.56f,.30f):key=="fish"?new Rect(.60f,.15f,.38f,.29f):key=="gold"?new Rect(.07f,.57f,.43f,.33f):new Rect(.57f,.54f,.41f,.44f);
            Sprite(image,new Rect(q.x*image.width,q.y*image.height,q.width*image.width,q.height*image.height),x,y,w,h,alpha);
        }

        void Fish(float x,float y,float size,bool gold) {Prop(gold?"gold":"fish",x,y,54*size,42*size);}

        void Asset(int index,float x,float y,float w,float h,float alpha=1,float rotation=0,bool flip=false)
        {
            if(index==1||index==9) {Illustration(index==1?"splash":"fisher",x,y,w,alpha,rotation,flip);return;}
            var image=Image("duet/effects-atlas");if(image==null)return;
            var q=effectRects[index];q=new Rect(q.x*image.width/1536,q.y*image.height/1024,q.width*image.width/1536,q.height*image.height/1024);
            Sprite(image,q,x,y,w,h,alpha,rotation,flip);
        }

        void Mist(int index,float x,float y,float w,float h,float rotation=0,float alpha=1)
        {
            var image=Image("drift/inhale-atlas");if(image==null)return;
            var q=mistRects[index];q=new Rect(q.x*image.width/1536,q.y*image.height/1024,q.width*image.width/1536,q.height*image.height/1024);
            Sprite(image,q,x,y,w,h,alpha,rotation);
        }

        void Text(string value,float x,float baseline,int size,Color color)
        {
            if(stage){stage.LabelText(value,x,baseline,size,color);return;}
            if(textStyle==null)textStyle=new GUIStyle(GUI.skin.label) {alignment=TextAnchor.MiddleCenter,clipping=TextClipping.Overflow,wordWrap=false};
            textStyle.font=font;textStyle.fontSize=Math.Max(16,size);GuguTypography.SetLabelColor(textStyle,color);
            GUI.Label(new Rect(x-260,baseline-size*1.05f,520,size*1.4f),value,textStyle);
        }

        void Flat(Color color)
        {
            if(stage){stage.Group("Transitions");stage.Slice(Texture2D.whiteTexture,new Rect(0,0,1,1),viewport,color.a);return;}
            var old=GUI.color;GUI.color=color;GUI.DrawTexture(viewport,Texture2D.whiteTexture);GUI.color=old;
        }

        public static float InhaleScale(float expansion) { return 1+.075f*Mathf.Clamp01(expansion); }

        // First compose in 16:9; crop uniformly to cover any actual fullscreen aspect.
        public static Rect CoverSource(Rect source,float aspect) {
            if(source.width/source.height>aspect){float w=source.height*aspect;source.x+=(source.width-w)*.5f;source.width=w;}
            else {float h=source.width/aspect;source.y+=(source.height-h)*.5f;source.height=h;}
            return source;
        }
        public void DrawBackdrop(GuguRun run,string scene,float w,float h) {
            bool menu=run==null||run.status=="ready"||scene=="menu";
            bool ending=run!=null&&(run.status=="won"||run.status=="lost"||scene=="end");
            bool dive=!menu&&run!=null&&(run.phase=="underwater"&&!ending||ending&&run.outcome=="hungryGhost");
            var image=Image(menu?"v15-1/menu-background":"tide/ocean");if(image==null)return;
            Rect source=menu?new Rect(0,0,image.width,image.height):new Rect(0,dive?image.height*.39f:0,image.width,image.height*(dive?.61f:.49f));
            source=CoverSource(CoverSource(source,16f/9),w/h);
            Slice(image,source,stage?stage.Viewport:new Rect(0,0,w,h));
            if(dive){
                if(shade==null){
                    shade=Texture(1,128,"Underwater wash");var colors=new Color[128];
                    for(int y=0;y<128;y++)colors[y]=Color.Lerp(new Color(21/255f,74/255f,105/255f,179/255f),new Color(61/255f,140/255f,165/255f,56/255f),y/127f);
                    shade.SetPixels(colors);shade.Apply(false,false);
                }
                Full(shade,stage?stage.Viewport:new Rect(0,0,w,h));
            }
        }
        public static int HungerFrame(float progress) {
            if(progress<0||progress>=1)return 0;
            int[] sequence={0,1,2,3,2,3,4,5};
            return sequence[Mathf.Min(sequence.Length-1,Mathf.FloorToInt(progress*sequence.Length))];
        }
        void MenuSeal(float x,float y,float w) {
            MenuHungerFrame=MenuFrameAt(menuHungerElapsed);
            var gif=Image("v15-1/hunger-gif");
            if(gif&&MenuHungerFrame>=0){
                // Decode-disposal and timing come from the supplied GIF. One shared
                // crop preserves alignment and aspect across all three original poses.
                Seal(gif,menuGif.Crop(MenuHungerFrame),x,y,w);return;
            }
            var image=Image("v14-2/menu-hunger");if(image==null)return;
            if(Rig)Rig.Sample("Idle",0,false);
            Seal(image,new Rect(0,100,image.width/3f,395),x,y,w);
        }

        void Scenery(bool dive,float time,GuguRun run)
        {
            if(dive)
            {
                for(int i=0;i<3;i++)Asset(7,width*(.15f+i*.42f)+Mathf.Sin(time*.12f+i)*35,height*.4f,width*.7f,height*.9f,.17f);
                for(int i=0;i<8;i++)Asset(6,Mod(i*179-time*(12+i%3)+width*30,width+100)-50,Mod(i*119-time*8+height*30,height+100)-50,24+i%3*13,35+i%3*15,.15f);
                for(int i=0;i<6;i++)Prop("plant",i*width/5-time*5%80,height,110+i%3*30,165,.35f);
            }
            else
            {
                for(int i=0;i<3;i++)Asset(11,width*(.2f+i*.35f)-time*3%90,height*(.61f+i*.11f),width*.46f,height*.06f,.5f);
                float floe=Math.Min(width*.9f,930);bool friends=run!=null&&run.outcome=="friends";
                bool ending=run!=null&&(run.status=="won"||run.status=="lost");
                float iceY=friends?FriendsBaseline()+floe*.07f:height*(ending?.60f:run!=null&&run.breathingActive&&width<760?.64f:.72f);
                Prop("ice",width*(friends?.5f:.52f),iceY,floe,floe*.32f);
            }
        }

        static Health Oxygen(double air,float time,bool active,bool reduced)
        {
            float severity=active?Mathf.Clamp01(F((60-air)/60)):0,critical=active?Mathf.Clamp01(F((25-air)/25)):0;
            return new Health {severity=severity,tint=severity*.78f,heave=(reduced?0:Mathf.Sin(time*(5+severity*7)))*severity*.055f,
                shiver=reduced?0:Mathf.Sin(time*27)*critical*.009f,tailStrength=1-severity*.65f,stoop=severity*.09f};
        }

        Gate Cue(GuguRun run)
        {
            bool active=run.status=="playing"||run.status=="paused"&&run.beforePause=="playing";
            if(!active)return new Gate {mode="none"};
            if(run.arriving)return new Gate {mode="arrival",strength=.35f};
            if(run.phase=="surface")return new Gate {mode="refill",strength=run.air<80?1:.4f};
            if(run.phase!="underwater")return new Gate{mode="none"};
            if(run.time-run.missedGateAt<1.5)return new Gate {mode="missed",strength=1};
            foreach(var note in run.notes)
            {
                if(!string.IsNullOrEmpty(note.result)||(note.kind!="surface"&&note.kind!="exit"))continue;
                double remaining=run.noteTime(note)-run.time;
                if(remaining>=-run.window&&remaining<=3.5)return new Gate{mode="approach",note=note,strength=.45f+.55f*Mathf.Clamp01(1-F(remaining/3.5))};
                break;
            }
            return new Gate{mode="none"};
        }

        public void Draw(GuguRun run,string scene,float time,float dt,float w,float h,Rect viewportBounds)
        {
            if(!stage&&Event.current!=null&&Event.current.type!=EventType.Repaint)return;
            width=w;height=h;viewport=viewportBounds;
            if(current!=run)Reset(run);
            bool menu=run==null||run.status=="ready"||scene=="menu";
            bool playing=run!=null&&(run.status=="playing"||run.status=="paused"&&run.beforePause=="playing");
            bool ending=run!=null&&(run.status=="won"||run.status=="lost"||scene=="end");
            bool dive=!menu&&run!=null&&(run.phase=="underwater"&&!ending||ending&&run.outcome=="hungryGhost");
            Scene=menu?"menu":ending?"end":run.breathingActive?"breath":"play";
            if(updatedFrame!=Time.frameCount)
            {
                updatedFrame=Time.frameCount;
                // Artwork review freezes dt at zero, just as the HTML review sets its
                // scene transition to complete. Do not freeze the white transition
                // veil over every static capture; ordinary paused play keeps its veil.
                if(Scene!=lastScene) {lastScene=Scene;transition=dt<=0&&(run==null||run.status!="paused")?1:0;}
                if(run==null||run.status!="paused") {transition=Math.Min(1,transition+Math.Max(0,dt));if(ending)endingTime+=dt;}
                if(run!=null&&run.time>motionTime)
                {
                    double advance=run.time-motionTime;growth+=(targetGrowth-growth)*(1-Mathf.Exp(F(-advance*7)));motionTime=run.time;
                }
            }
            if(menu)
            {
                bool small=w<760;
                if(stage)stage.Group("Seal");
                MenuSeal(w*(small?.24f:.255f),h*(small?.59f:.64f)+(reducedMotion?0:Mathf.Sin(time*1.3f)*3),small?Math.Min(205,w*.52f):Math.Min(350,w*.28f));
                return;
            }
            if(stage)stage.Group("Scenery");
            Scenery(dive,time,run);
            var health=Oxygen(run.air,time,playing&&run.phase!="shore",reducedMotion);HeadTint=health.tint;
            Gate gate=Cue(run);float urgency=Math.Max(gate.strength*.66f,health.severity*.32f);
            if(urgency>0) {
                if(stage)stage.Group("Crisis");
                var crisis=Image("gugu/breath-crisis");
                if(crisis!=null)Slice(crisis,CoverSource(new Rect(0,0,crisis.width,crisis.height),viewport.width/viewport.height),viewport,
                    urgency*(reducedMotion?1:.86f+.14f*Mathf.Pow(Mathf.Sin(time*5),2)));
            }
            VisibleFish=VisibleMissedFish=0;
            if(playing&&(run.phase=="surface"||run.phase=="shore"))Shore(run,time,health);
            else if(playing&&!run.breathingActive)Play(run,time,dive,health,gate);
            else
            {
                if(stage)stage.Group("Seal");
                float bob=reducedMotion?0:Mathf.Sin(endingTime*2)*5;
                bool breathing=run.breathingActive;float sw=breathing?Math.Min(430,w*.7f):Math.Min(340,w*.47f);
                float sx=breathing?w*.4f:w*.52f,sy=h*(breathing?.50f:ending?(w<760?.49f:.46f):w<760?.64f:.61f);
                if(breathing) {InhalePortrait(sx,sy,sw,run.breathVisual(),health);Inhalation(sx,sy,sw*InhaleScale(F(run.breathVisual().expansion)),time,run);}
                else if(run.outcome=="angel")Illustration("angel",w*.5f,h*.345f+bob,Math.Min(350,w*.46f));
                else if(run.outcome=="hungryGhost")Hungry(sx,sy+bob,sw*1.12f,endingTime);
                else if(run.outcome=="friends")Friends(time,bob);
                else if(run.outcome=="rest")Resting(time);
                else Portrait(sx,sy,sw,time,false,ending?1.12f:1);
            }
            if(transition<.75f&&!reducedMotion)Flat(new Color(225/255f,251/255f,1,(1-transition/.75f)*.18f));
        }

        void FisherPose(int pose,float x,float y,float w,float rotation=0,float alpha=1)
        {
            var image=Image("huhu/fisher-poses");if(!image)return;
            Rect[] parts={new Rect(6,8,505,555),new Rect(565,52,470,490),new Rect(1052,91,479,480),new Rect(9,688,509,295),new Rect(559,614,476,352),new Rect(1065,605,470,401)};
            Rect q=parts[pose];q=new Rect(q.x*image.width/1536,q.y*image.height/1024,q.width*image.width/1536,q.height*image.height/1024);
            Seal(image,q,x,y,w,alpha:alpha,rotation:rotation);
        }
        // One choreography for every song, body size, refill gate and final exit.
        // The seal ends at the fisherman's original seat before refill input is enabled.
        void Shore(GuguRun run,float time,Health health)
        {
            float age=Mathf.Max(0,F(run.time-run.arrivalAt));
            float x=width*.50f,y=height*.46f,body=Math.Min(355,width*.48f);
            if(stage)stage.Group("Shore props");
            FisherPose(3,x,height*.585f,215);
            float hit=F(GuguRun.SURFACE_IMPACT_SECONDS),flight=Mathf.Clamp01((age-hit)/.50f);
            if(stage)stage.Group("Fisher collision");
            if(age<hit)FisherPose(0,x,y-7,215);
            else{
                float fx=x+flight*width*.32f,fy=y-7-Mathf.Sin(flight*Mathf.PI)*155+flight*42;
                FisherPose(flight<.95f?1:2,fx,fy,215,flight<.95f?flight*.62f:0);
                FisherPose(4,x+flight*width*.40f,y-60-Mathf.Sin(flight*Mathf.PI)*170+flight*76,180,flight*2.6f);
                if(flight>.85f&&age<1.2f)FisherPose(5,fx,fy+70,240,alpha:Mathf.Clamp01((1.2f-age)/.35f));
            }
            if(stage)stage.Group("Seal");
            if(run.arriving){
                float rise=Smooth(age/hit),settle=Smooth((age-hit)/(F(GuguRun.SURFACE_ARRIVAL_SECONDS)-hit));
                float sx=Mathf.Lerp(width*.35f,x,rise),sy=age<hit?Mathf.Lerp(height*.81f,y-65,rise):Mathf.Lerp(y-65,y,settle);
                Portrait(sx,sy,body,time);SealX=sx;SealY=sy;SealWidth=body;
                if(stage)stage.Group("Arrival splash");
                float splash=Mathf.Clamp01(age/.65f);Illustration("splash",x,height*.58f-splash*25,420*(.65f+splash*.35f),Mathf.Sin(splash*Mathf.PI)*.9f);
                if(age>=hit&&age<hit+.25f)Asset(0,x,y-45,200,175,1-(age-hit)/.25f);
            }else if(run.phase=="surface"){
                InhalePortrait(x,y,body,run.breathVisual(),health);
                Inhalation(x,y,body*InhaleScale(F(run.breathVisual().expansion)),time,run);
            }else Portrait(x,y,body,time);
        }
        void Resting(float time)
        {
            if(Rig)Rig.Sample("Rest",time);
            float w=Math.Min(500,width*.62f),scale=reducedMotion?1:1+Mathf.Sin(time*1.7f)*.006f;
            Illustration("rest",width*.5f,height*.40f,w*scale);
        }
        void Play(GuguRun run,float time,bool dive,Health health,Gate gate)
        {
            bool small=width<760;float x=width*(small?.47f:.35f),upper=small?Math.Max(360,height*.47f):height*.49f,lower=height*(small?.63f:.68f);
            float speed=(width*.68f+60)/F(run.profile.approach)*speedMultiplier,body=Math.Min(225,width*.24f)*1.2f*growth;
            float sy=Mathf.Lerp(upper,lower,laneY);SealX=x;SealY=sy;SealWidth=body;
            if(stage)stage.Group("Lanes");
            foreach(string lane in new[]{"upper","lower"})
            {
                float y=lane=="upper"?upper:lower;
                Asset(11,(x+width)/2,y,width-x,30,dive?.22f:.45f);Asset(10,x,y,55,65,.5f);
                Text(lane=="upper"?"D / F / ↑":"J / K / ↓",Math.Max(54,x-body*.8f),y-34,16,dive?new Color(.96f,.984f,1):new Color(.192f,.373f,.475f));
            }
            Asset(2,x-body*.75f,sy+20,body*.7f,body*.43f,.45f,-.15f,true);
            if(stage)stage.Group("Seal");
            EatingSeal(run,x,sy,body,time,health);
            if(stage)stage.Group("Notes");
            if(!hideNotes)foreach(var note in run.notes)
            {
                float nx=x+F(run.noteTime(note)-run.time)*speed,ny=note.lane=="upper"?upper:lower;
                if(note.result=="hit"||nx < -90||nx>width+90)continue;
                if(note.kind=="fish")
                {
                    VisibleFish++;if(note.result=="miss")VisibleMissedFish++;
                    Fish(nx,ny,small?.77f:1.12f,note.lane=="upper");
                    if(note.fish>1)Text("×"+note.fish,nx,ny+31,16,dive?new Color(.953f,1,1):new Color(.192f,.357f,.467f));
                }
                else
                {
                    int index=note.kind=="surface"||note.kind=="exit"?5:note.kind=="dive"?1:3;
                    if(gate.note==note)Asset(10,nx,ny,small?112:146,small?94:120,.30f+.1f*Mathf.Sin(time*5));
                    Asset(index,nx,ny,small?82:112,small?70:91);
                    string label=note.kind=="surface"?"換氣":note.kind=="exit"?"上岸":note.kind=="dive"?"下海":note.kind=="leap"?"大吸氣":"呼喚";
                    Text(label,nx,ny-40,small?16:19,dive?new Color(1,.973f,.847f):new Color(.204f,.373f,.471f));
                    if(note.kind=="surface"||note.kind=="exit")FisherPose(0,nx+12,ny-101,small?95:135);
                }
            }
            if(stage)stage.Group("Hit effects");
            foreach(var item in caught)
            {
                float age=F(run.time-item.at);if(age<0||age>.12f)continue;
                float q=age/.12f,ease=1-(1-q)*(1-q),y=item.lane=="upper"?upper:lower;
                Fish(x+30*(1-ease),y+(sy-y)*ease-Mathf.Sin(q*Mathf.PI)*18,(small?.77f:1.12f)*(1-q*.8f),item.gold);
            }
            caught.RemoveAll(item=>run.time-item.at>=.12);
            foreach(var item in fx)
            {
                float age=F(run.time-item.at),span=item.fall?1.1f:.46f;if(age<0||age>span)continue;float q=age/span;
                if(item.fall)Asset(9,x+q*120,upper-55+q*q*130,120,107,1-q,q*2.3f);
                else Asset(item.index,x,item.lane=="upper"?upper:lower,(item.index==1?190:115)*(1+q*.8f),(item.index==1?155:102)*(1+q*.5f),(1-q)*(item.miss?.45f:.83f),item.miss?q*.2f:0);
            }
            fx.RemoveAll(item=>run.time-item.at>=1.1);
            if(run.danger>0)Danger(x,sy,F(run.danger));
        }

        void EatingSeal(GuguRun run,float x,float y,float w,float time,Health health)
        {
            int form=Mathf.Clamp(run.form,0,2);Art swim=swimmers[form];float age=F(run.time-biteStart);
            bool biting=age>=0&&age<.19f;float mix=biting?Math.Min(1,(.19f-age)/.035f):0;
            BiteFrame=biting?Math.Min(5,2+Mathf.FloorToInt(age/.19f*4)):0;
            if(mix<1)
            {
                if(Rig)Rig.Sample("Swim",run.time);
                float breath=reducedMotion?1:Rig?Rig.bodyScale:1+Mathf.Sin(F(run.time)*3.65f)*.025f;
                float impulse=Math.Max(0,1-F(run.time-inputAt)/.12f),scale=breath+health.heave*.5f+(reducedMotion?0:impulse*(Rig?Rig.inputPulse:.018f));
                float roll=reducedMotion?0:Rig?Rig.roll:Mathf.Sin(F(run.time)*3.65f)*.013f;
                Seal(Image(swim.key),swim.rect,x,y,w,swim.anchor,1-mix,roll*health.tailStrength+health.shiver-health.stoop*.45f,scale:scale,tint:health.tint,head:swimHeads[form],cacheKey:"swim"+form);
            }
            if(!biting||mix<=0)return;
            if(Rig){Rig.Sample("Eat",age,false);BiteFrame=Mathf.Clamp(Mathf.RoundToInt(Rig.frame),0,5);}
            var image=Image("duet/seal-eating");if(image==null)return;int frame=BiteFrame;float cell=image.width/3f,sh=image.height/2f;
            var eyes=biteEyes[frame];var head=new Head(.46f,.72f,new[]{new Vector4(eyes[0].x,eyes[0].y,.17f,.225f),new Vector4(eyes[1].x,eyes[1].y,.17f,.225f)},b0:.60f,b1:.78f);
            Seal(image,new Rect(frame%3*cell,frame/3*sh,cell,sh),x,y,w,biteAnchors[frame],mix,tint:health.tint,head:head,cacheKey:"bite"+frame);
        }

        void InhalePortrait(float x,float y,float w,BreathVisual state,Health health)
        {
            var image=Image("tide/seal");if(image==null)return;
            if(health.tint>0)image=Tinted(image,new Rect(0,0,image.width,image.height),health.tint,portraitHead,"portrait");
            if(Rig)Rig.Sample("Inhale",state.expansion,false);
            w*=Rig?Rig.bodyScale:InhaleScale(F(state.expansion));
            float h=w*image.height/image.width,hinge=.57f,sourceH=image.height*hinge,top=x-w/2,leftY=y-h/2;
            if(stage){
                stage.Belly(image,new Rect(top,leftY,w,h),F(state.expansion));
                BellyScale=Belly(.42f,F(state.expansion));SealX=x;SealY=y;SealWidth=w;return;
            }
            Slice(image,new Rect(0,0,image.width,sourceH),new Rect(top,leftY,w,h*hinge));
            // After a uniform whole-body enlargement, the abdomen alone expands below
            // the spine. Head/flippers retain their proportions; the back does not bulge.
            int columns=Mathf.CeilToInt(w);float column=w/columns;
            for(int i=0;i<columns;i++)
            {
                float q=(i+.5f)/columns,stretch=Belly(q,F(state.expansion));
                Slice(image,new Rect(i*image.width/(float)columns,sourceH,image.width/(float)columns,image.height-sourceH),new Rect(top+i*column,leftY+h*hinge,column,h*(1-hinge)*stretch));
            }
            BellyScale=Belly(.42f,F(state.expansion));SealX=x;SealY=y;SealWidth=w;
        }

        public static float Belly(float x,float expansion)
        {
            float u=Mathf.Clamp01((x-.18f)/.48f);return 1+Mathf.Pow(Mathf.Sin(u*Mathf.PI),2)*.32f*Mathf.Clamp01(expansion);
        }

        void Inhalation(float x,float y,float w,float time,GuguRun run)
        {
            if(stage)stage.Group("Breath mist");
            var state=run.breathVisual();float mouthX=x+w*.306f,size=Math.Min(height*.34f,width*.31f),center=mouthX+size*(.13f+.55f*(1-F(state.expansion)));
            Mist(0,center,y-10,size*F(state.ringScale),size*F(state.ringScale),reducedMotion?0:-time*.42f,F(state.mist)*.82f);
            if(!reducedMotion)for(int i=0;i<8;i++)
            {
                float p=Mod(time*.9f+i/8f,1),angle=-1.05f+i%4*.66f,reach=size*1.15f*Mathf.Pow(1-p,1.4f)+12;
                float px=mouthX+Mathf.Cos(angle)*reach,py=y+Mathf.Sin(angle)*reach+Mathf.Sin(p*Mathf.PI*2+i)*6*(1-p);
                Mist(i%2+1,px,py,(45+size*.29f)*(1-p*.48f),19+size*.06f,angle+Mathf.PI,Mathf.Sin(p*Mathf.PI)*.68f*F(state.mist));
            }
            Mist(3,mouthX+13,y+2,35+F(state.fill)*27,22+F(state.fill)*17,Mathf.PI,F(state.mist)*(.14f+.4f*F(state.fill)));
            if(!run.openingBreath)Text(run.breathRemaining.ToString("F1")+" 秒後下海",width*(width<760?.5f:.8f),height*.695f,width<760?27:34,new Color(.647f,.255f,.224f));
        }

        void Hungry(float x,float y,float w,float time)
        {
            var image=Image("scenes/hunger-strip");if(image==null)return;
            float age=Mod(time,7);int frame=reducedMotion||age>=2.4f?0:Mathf.FloorToInt(age/.08f)%3;
            float cell=image.width/3f;Seal(image,new Rect(frame*cell,Mathf.Round(image.height*.22f),cell,Mathf.Round(image.height*.67f)),x,y+w*.25f,w,new Vector2(.5f,1),.76f,scale:1+(reducedMotion?0:Mathf.Sin(time*1.8f)*.008f));
        }

        float FriendsWidth() {return Math.Min(330,Math.Min(width*.42f,height*.4f));}
        float FriendsBaseline() {return Math.Max(238,height*.53f);}
        void Friends(float time,float bob)
        {
            if(Rig){Rig.Sample("Friends",endingTime);if(!reducedMotion)bob=Rig.bob;}
            float w=FriendsWidth(),baseline=FriendsBaseline(),y=baseline-w*.3f,gap=w*.04f;
            Portrait(width*.5f-(w+gap)/2,y,w,time);var friend=illustrations["friend"];
            Illustration("friend",width*.5f+(w+gap)/2,baseline-w*friend.rect.height/friend.rect.width/2+bob*.35f,w);
            Asset(10,width*.5f,y-w*.38f,w*.56f,w*.21f,.75f);
        }

        void Danger(float x,float y,float amount)
        {
            if(stage)stage.Group("Danger vignette");
            if(vignette==null)
            {
                vignette=Texture(128,128,"Low oxygen camera vignette");var data=new Color32[128*128];
                for(int iy=0;iy<128;iy++)for(int ix=0;ix<128;ix++)
                {
                    float distance=new Vector2((ix-63.5f)/63.5f,(iy-63.5f)/63.5f).magnitude;
                    data[iy*128+ix]=new Color(16/255f,45/255f,65/255f,Mathf.Clamp01((distance-.105f)/.895f));
                }
                vignette.SetPixels32(data);vignette.Apply(false,false);
            }
            float radius=Math.Max(viewport.width,viewport.height)*.8f;Full(vignette,new Rect(x-radius,y-radius,radius*2,radius*2),amount*.8f);
        }

        Texture2D Tinted(Texture2D source,Rect crop,float amount,Head head,string key)
        {
            if(source==null)return source;int tone=Mathf.FloorToInt(amount*30+.5f);
            TintedArt art;
            if(!heads.TryGetValue(key,out art))
            {
                int w=Mathf.RoundToInt(crop.width),h=Mathf.RoundToInt(crop.height);var original=new Color32[w*h];var src=Pixels(source);
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                {
                    int sx=Mathf.Clamp(Mathf.FloorToInt(crop.x+x),0,source.width-1),sy=Mathf.Clamp(source.height-1-Mathf.FloorToInt(crop.y+y),0,source.height-1);
                    original[(h-1-y)*w+x]=src[sy*source.width+sx];
                }
                art=new TintedArt {image=Texture(w,h,"Gugu head mask "+key),original=original,expression=(Color32[])original.Clone(),work=new Color32[w*h],mask=new float[w*h]};
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)art.mask[(h-1-y)*w+x]=head.Mask(x/(float)w,y/(float)h);
                for(int i=0;i<head.eyes.Length;i++)PaintMark(art.expression,w,h,head.eyes[i],new Rect(977,221,195,227),head.eyes.Length==2&&i==1);
                if(head.mouth.z>0)PaintMark(art.expression,w,h,head.mouth,head.mouthSource,false);
                heads[key]=art;
            }
            if(art.tone!=tone)
            {
                float alpha=tone/30f,expression=Mathf.Clamp01((alpha/.78f*60-5)/40);
                for(int i=0;i<art.work.Length;i++)
                {
                    var original=art.original[i];float mask=art.mask[i];
                    if(mask<=0||original.a==0) {art.work[i]=original;continue;}
                    Color32 face=art.expression[i];float tint=alpha*mask;
                    art.work[i]=new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(original.r,face.r,expression)*(1-tint*(1-128/255f))),0,255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(original.g,face.g,expression)*(1-tint*(1-83/255f))),0,255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(original.b,face.b,expression)*(1-tint*(1-154/255f))),0,255),original.a);
                }
                art.image.SetPixels32(art.work);art.image.Apply(false,false);art.tone=tone;
            }
            return art.image;
        }

        void PaintMark(Color32[] destination,int w,int h,Vector4 feature,Rect source,bool flip)
        {
            var image=Image("gugu/breathless-expressions");if(image==null)return;var src=Pixels(image);
            float cx=feature.x*w,cy=feature.y*h,rx=feature.z*w*.6f,ry=feature.w*h*.6f;
            int minX=Math.Max(0,Mathf.FloorToInt(cx-rx)),maxX=Math.Min(w-1,Mathf.CeilToInt(cx+rx)),minY=Math.Max(0,Mathf.FloorToInt(cy-ry)),maxY=Math.Min(h-1,Mathf.CeilToInt(cy+ry));
            for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
            {
                float distance=new Vector2((x+.5f-cx)/rx,(y+.5f-cy)/ry).magnitude,opacity=1-Mathf.Clamp01((distance-.8f)/.2f);
                if(opacity<=0)continue;int p=(h-1-y)*w+x;var old=destination[p];
                destination[p]=new Color32((byte)Mathf.RoundToInt(Mathf.Lerp(old.r,244,opacity)),(byte)Mathf.RoundToInt(Mathf.Lerp(old.g,250,opacity)),(byte)Mathf.RoundToInt(Mathf.Lerp(old.b,253,opacity)),old.a);
            }
            float dw=Math.Min(feature.z*w*.9f,feature.w*h*.9f*source.width/source.height),dh=dw*source.height/source.width;
            minX=Math.Max(0,Mathf.FloorToInt(cx-dw/2));maxX=Math.Min(w-1,Mathf.CeilToInt(cx+dw/2));minY=Math.Max(0,Mathf.FloorToInt(cy-dh/2));maxY=Math.Min(h-1,Mathf.CeilToInt(cy+dh/2));
            for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
            {
                float u=(x+.5f-cx+dw/2)/dw,v=(y+.5f-cy+dh/2)/dh;
                if(u<0||u>1||v<0||v>1)continue;if(flip)u=1-u;
                int sx=Mathf.Clamp(Mathf.FloorToInt(source.x+u*source.width),0,image.width-1),sy=Mathf.Clamp(image.height-1-Mathf.FloorToInt(source.y+v*source.height),0,image.height-1);
                Color32 ink=src[sy*image.width+sx];float opacity=ink.a/255f;int p=(h-1-y)*w+x;Color32 old=destination[p];
                destination[p]=new Color32((byte)Mathf.RoundToInt(Mathf.Lerp(old.r,ink.r,opacity)),(byte)Mathf.RoundToInt(Mathf.Lerp(old.g,ink.g,opacity)),(byte)Mathf.RoundToInt(Mathf.Lerp(old.b,ink.b,opacity)),old.a);
            }
        }

        public void Dispose()
        {
            foreach(var image in generated)if(image!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(image);else UnityEngine.Object.DestroyImmediate(image);}
            generated.Clear();heads.Clear();pixels.Clear();images.Clear();
        }
    }
}
