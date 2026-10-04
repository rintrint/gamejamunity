using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SealGugu
{
    /// <summary>The native game shell. The deterministic run never depends on GUI animation.</summary>
    public sealed class GuguGame : MonoBehaviour
    {
        public const string Version="V15.0.0";
        [Tooltip("小組成員可在此資產的 entries 直接輸入名字。")]
        public GameCredits credits;
        public HuhuStage stage;
        public GuguRun run { get; private set; }
        GuguRenderer view;
        GuguAudio sound;
        GuguDesktop desktop;
        readonly GuguUiArt uiArt=new GuguUiArt();
        readonly GuguMeters meters=new GuguMeters();
        Rect canvasBounds;
        Track[] tracks;
        int song;
        string level="expert",modal="",error="";
        bool practice,blind,calibration,reduced;
        float speed=1,delay,window=150,visual,comboScale=1,comboVelocity,judgeScale=1,judgeVelocity,judgeAge=10;
        string grade="",timing="";
        double lastBreathCycle=-1,lastHungerCycle=-1;
        readonly List<InputAction> inputs=new List<InputAction>();
        readonly List<double> calibrationClicks=new List<double>(),calibrationSamples=new List<double>();
        int lastCalibration=-1;
        Vector2 setupScroll,creditsScroll;
        Font sans;
        Texture2D panel,button,sliderTrack,sliderThumb;
        GUIStyle label,heading,action,primaryAction,tuningAction,choice,toggle,scrollbar,thumb,panelStyle;
        GUIStyle setupAction,setupPrimary,choiceSelected,setupToggle,setupTrack,setupThumb;
        GUISkin theme;
        readonly Dictionary<string,Texture2D> art=new Dictionary<string,Texture2D>();
        static readonly Color Ink=new Color(.157f,.318f,.427f),Muted=new Color(.306f,.451f,.533f);
        static readonly Color DiveInk=Css(0xe6f8fc),ScoreInk=Css(0xdbf8ff),SongShadow=Css(0x16485a);
        static Color Css(uint hex,float alpha=1){return new Color((hex>>16&255)/255f,(hex>>8&255)/255f,(hex&255)/255f,alpha);}
        bool DiveUi => Scene=="play";
        Color WorldInk => DiveUi?DiveInk:Ink;
        const float W=1280,H=720;
        public bool IsPreview { get; private set; }
        public bool hidePreviewBadge;
#if UNITY_EDITOR || GUGU_QA
        public bool automatedTest;
        public InputDevice automatedKeyboard;
        public int ignoredExternalTestInputs;
        public string previewIceHover="";
#endif
        float lastDt;
        string Scene => run==null||run.status=="ready"?"menu":run.breathingActive?"breath":run.status=="won"||run.status=="lost"?"end":"play";
        void Awake()
        {
            HuhuSettingsMigration.Once();
            Application.targetFrameRate=144;QualitySettings.vSyncCount=0;Application.runInBackground=false;
            if(!credits)credits=Resources.Load<GameCredits>("GameCredits");
            sans=GuguTypography.Create();
            desktop=gameObject.AddComponent<GuguDesktop>();sound=gameObject.AddComponent<GuguAudio>();view=new GuguRenderer{font=sans,stage=stage};view.Preload();
            try{
                tracks=JsonUtility.FromJson<ChartDocument>(Resources.Load<TextAsset>("Data/chart").text).tracks;
                for(int i=0;i<tracks.Length;i++)tracks[i].title="音樂"+(i+1);
                speed=(float)GuguRun.speedValue(PlayerPrefs.GetFloat("gugu.speed",1));delay=(float)GuguRun.delayValue(PlayerPrefs.GetFloat("gugu.delay",0));
                window=(float)GuguRun.windowValue(PlayerPrefs.GetFloat("gugu.window",150));
                reduced=PlayerPrefs.GetInt("gugu.reduced",0)==1;NewPreview();sound.Menu(tracks[song]);
            }catch(Exception e){error="遊戲資源載入失敗："+e.Message;Debug.LogException(e);}
            foreach(string key in new[]{"d","f","upArrow","j","k","downArrow","space","escape"}){
                string captured=key;var input=new InputAction("Seal "+key,InputActionType.PassThrough,"<Keyboard>/"+key);
                input.performed+=ctx=>{
#if UNITY_EDITOR || GUGU_QA
                    // A background QA run must not score typing in another application.
                    if(automatedTest&&automatedKeyboard!=null&&ctx.control.device!=automatedKeyboard){if(ctx.ReadValue<float>()>.5f)ignoredExternalTestInputs++;return;}
#endif
                    if(ctx.ReadValue<float>()>.5f)Key(captured,ctx.time);
                };input.Enable();inputs.Add(input);
            }
        }
        void OnDestroy(){foreach(var input in inputs)input.Dispose();view?.Dispose();uiArt.Dispose();meters.Dispose();}
        void OnApplicationFocus(bool focus){
#if UNITY_EDITOR || GUGU_QA
            if(automatedTest)return;
#endif
            if(!focus){Pause();StopCalibration();sound?.StopMenu();sound?.StopEffects();}else if(run?.status=="ready")sound.Menu(tracks[song]);}
        void OnApplicationPause(bool pause){
#if UNITY_EDITOR || GUGU_QA
            if(automatedTest)return;
#endif
            if(pause)Pause();}
        void Key(string key,double stamp)
        {
            if(key=="escape"){
                if(IsPreview){Back();return;}
                if(modal=="pause"){Resume();return;}
                if(modal!=""){CloseModal();return;}
                Pause();return;
            }
            if(calibration){CalTap(stamp);return;}
            if(modal!=""||IsPreview)return;
            if(key=="space"&&!run.breathingActive)return;
            Press(key=="d"||key=="f"||key=="upArrow"?"upper":"lower",stamp,key!="space");
        }
        public void Press(string lane,double stamp,bool bite=true)
        {
            if(run==null||IsPreview||modal!="")return;
            if(run.status=="breathing"){
                run.inhale();sound.StopEffects();sound.Play(run.track);Events();
            }else if(run.status=="playing"){
                if(bite)sound.Bite();
                double time=Math.Max(0,sound.TimeAt(stamp));if(!run.breathingActive)view.Input(lane,time);
                run.press(lane,time);Events();
            }
        }
        void NewPreview(){run=new GuguRun(tracks[song],level,practice||blind,new RunSettings{delay=delay,window=window});view.Reset(run);view.hideNotes=blind;view.speedMultiplier=speed*.7f;view.reducedMotion=reduced;judgeAge=10;comboScale=judgeScale=1;comboVelocity=judgeVelocity=0;}
        void StartRun(){IsPreview=false;modal="";StopCalibration();sound.Silence();NewPreview();run.startBreath();lastBreathCycle=-1;lastHungerCycle=-1;sound.Sample("start",.3f);}
        void Back(){IsPreview=false;sound.Silence();calibration=false;modal="";NewPreview();sound.Menu(tracks[song]);visual=0;lastHungerCycle=-1;}
        void Pause(){if(run==null||IsPreview||run.status!="playing"&&run.status!="breathing")return;if(run.status=="playing")sound.Pause();sound.StopEffects();run.pause();modal="pause";}
        void Resume(){modal="";if(run.status!="paused")return;run.resume();if(run.status=="playing")sound.Resume(run.track);else lastBreathCycle=-1;}
        void CloseModal(){if(modal=="pause"){Resume();return;}StopCalibration();modal="";if(run?.status=="ready")sound.Menu(tracks[song]);}
        void OpenTuning(){if(run.status=="playing"||run.status=="breathing")Pause();else modal="tuning";}
        void SaveTuning(){speed=(float)GuguRun.speedValue(speed);delay=(float)GuguRun.delayValue(delay);window=(float)GuguRun.windowValue(window);run?.setTiming(delay,window);view.speedMultiplier=speed*.7f;view.reducedMotion=reduced;PlayerPrefs.SetFloat("gugu.speed",speed);PlayerPrefs.SetFloat("gugu.delay",delay);PlayerPrefs.SetFloat("gugu.window",window);PlayerPrefs.SetInt("gugu.reduced",reduced?1:0);PlayerPrefs.Save();}
        void Events()
        {
            foreach(var e in run.events){
                if(e.type=="lost")sound.Pause();else if(e.type=="won")sound.Won();
                sound.Event(e,run);view.Emit(e,run);
                if(e.type=="hit"||e.type=="miss"){
                    grade=e.result.ToUpperInvariant();judgeAge=0;comboVelocity=Mathf.Min(6,comboVelocity+(e.type=="hit"?3.6f:-2));judgeVelocity=Mathf.Min(6,judgeVelocity+3);
                    timing=e.type=="miss"?(e.reason=="early"?"TOO EARLY · 太早按":e.reason=="lane"?"WRONG LANE · 按錯軌":e.reason=="late"?"TOO LATE · 太晚按":"錯過了，跟上下一拍"):
                        Math.Abs(e.error)<.008?"":(e.error<0?"EARLY ":"LATE ")+Math.Round(Math.Abs(e.error)*1000)+" ms";
                }

            }run.events.Clear();
        }
        void Update()
        {
            if(run==null)return;float dt=Time.unscaledDeltaTime;lastDt=Mathf.Min(.05f,dt);
            view.menuHungerProgress=sound.MenuHungerProgress;
            if(IsPreview)return;
            if(run.status=="breathing"){
                run.advanceBreath(dt);double cycle=Math.Floor(run.breathElapsed/3.6);
                if(cycle!=lastBreathCycle){lastBreathCycle=cycle;sound.Sample("inhale",.5f,0,2.7f);}
            }
            if(run.status=="ready"){
                visual+=lastDt;double cycle=Math.Floor(visual/7);
                if(!calibration&&modal==""&&cycle!=lastHungerCycle){lastHungerCycle=cycle;sound.MenuHunger();}
            }
            if(run.status=="playing"){run.update(Math.Max(0,sound.time));Events();sound.Danger(run.danger,run.time);}
            if(run.status!="paused"){
                judgeAge+=dt;float remain=Mathf.Min(dt,.1f);while(remain>0){float d=Mathf.Min(remain,1f/120);comboVelocity+=((1-comboScale)*210-comboVelocity*19)*d;comboScale+=comboVelocity*d;judgeVelocity+=((1-judgeScale)*180-judgeVelocity*19)*d;judgeScale+=judgeVelocity*d;remain-=d;}
            }
            if(calibration&&sound.time>=22.9)StopCalibration();
        }
        static string Fmt(double time){int t=Math.Max(0,(int)time);return (t/60)+":"+(t%60).ToString("00");}
        Texture2D Art(string path){if(!art.TryGetValue(path,out var im)){im=Resources.Load<Texture2D>("Art/"+path);art[path]=im;}return im;}
        void Styles()
        {
            if(label!=null)return;
            panel=Round(new Color(.93f,.98f,.965f,.96f),24);button=Round(Css(0xedf9fc,219f/255),22);
            sliderTrack=Round(new Color(.55f,.72f,.80f,.8f),4);sliderThumb=Round(new Color(.24f,.48f,.59f),10);
            label=new GUIStyle(GUI.skin.label){font=sans,fontSize=18,wordWrap=true,normal={textColor=Ink},alignment=TextAnchor.UpperLeft,padding=new RectOffset(0,0,0,0)};
            heading=new GUIStyle(label){font=sans,fontSize=32,fontStyle=FontStyle.Bold};
            action=new GUIStyle(GUI.skin.button){font=sans,fontSize=21,normal={background=button,textColor=Ink},hover={background=Round(Color.white,22),textColor=Ink},active={background=Round(Color.white,22),textColor=Ink},focused={background=button,textColor=Ink},border=new RectOffset(22,22,22,22),padding=new RectOffset(14,14,8,8)};
            primaryAction=new GUIStyle(action);primaryAction.normal.background=Round(Css(0xf3faf1,236f/255),22);primaryAction.hover.background=Round(Css(0xfffdf0),22);primaryAction.active.background=primaryAction.hover.background;
            tuningAction=new GUIStyle(action);tuningAction.normal.background=Round(Css(0xedf9f5,237f/255),22);tuningAction.hover.background=Round(Css(0xfff7df),22);tuningAction.active.background=tuningAction.hover.background;
            // Setup controls have opaque surfaces and distinct idle / hover / pressed states.
            setupAction=SetupButtonStyle(0xd4edf6,0xbce4f4,0xa5d5e8,0x83b4c9,0x4b8eaa);
            setupPrimary=SetupButtonStyle(0xffe49a,0xffd66f,0xefc054,0xc5a15b,0xa77c31);
            setupPrimary.fontStyle=FontStyle.Bold;
            choice=new GUIStyle(setupAction){fontSize=20,wordWrap=true,alignment=TextAnchor.MiddleCenter};
            choiceSelected=SetupButtonStyle(0xffefb7,0xffde87,0xf3cb69,0xc6a45c,0xa77c31);
            choiceSelected.fontSize=20;choiceSelected.wordWrap=true;choiceSelected.fontStyle=FontStyle.Bold;
            setupToggle=new GUIStyle(setupAction){fontSize=18,wordWrap=true,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(12,12,5,5)};
            setupToggle.onNormal.background=choiceSelected.normal.background;
            setupToggle.onHover.background=choiceSelected.hover.background;
            setupToggle.onActive.background=choiceSelected.active.background;
            setupToggle.onFocused.background=choiceSelected.focused.background;
            toggle=new GUIStyle(GUI.skin.toggle){font=sans,fontSize=18,normal={textColor=Ink},onNormal={textColor=Ink},hover={textColor=Ink},onHover={textColor=Ink},padding=new RectOffset(28,0,0,0)};
            scrollbar=new GUIStyle(GUI.skin.horizontalSlider){fixedHeight=8,normal={background=sliderTrack},border=new RectOffset(4,4,4,4),margin=new RectOffset(0,0,9,8)};
            thumb=new GUIStyle(GUI.skin.horizontalSliderThumb){fixedWidth=22,fixedHeight=22,normal={background=sliderThumb},hover={background=sliderThumb},active={background=sliderThumb},border=new RectOffset(10,10,10,10),overflow=new RectOffset(0,0,7,7)};
            setupTrack=new GUIStyle(scrollbar);
            setupTrack.normal.background=Round(Css(0x94bbc9),4);
            setupTrack.hover.background=Round(Css(0x76a9be),4);
            setupTrack.active.background=setupTrack.hover.background;
            setupThumb=new GUIStyle(thumb);
            setupThumb.normal.background=Round(Css(0xd4edf6),10,Css(0x508da7),2);
            setupThumb.hover.background=Round(Css(0xffffff),10,Css(0x397b99),2);
            setupThumb.active.background=Round(Css(0xa5d5e8),10,Css(0x397b99),2);
            setupThumb.focused.background=setupThumb.hover.background;
            panelStyle=new GUIStyle{normal={background=panel},border=new RectOffset(24,24,24,24)};
            theme=Instantiate(GUI.skin);
            theme.verticalScrollbar.normal.background=sliderTrack;theme.verticalScrollbar.fixedWidth=12;theme.verticalScrollbar.border=new RectOffset(4,4,4,4);
            foreach(var state in new[]{theme.verticalScrollbarThumb.normal,theme.verticalScrollbarThumb.hover,theme.verticalScrollbarThumb.active})state.background=sliderThumb;
            theme.verticalScrollbarThumb.border=new RectOffset(10,10,10,10);theme.verticalScrollbarThumb.fixedWidth=12;
            theme.verticalScrollbarUpButton.fixedHeight=0;theme.verticalScrollbarDownButton.fixedHeight=0;
        }
        GUIStyle SetupButtonStyle(uint normal,uint hover,uint active,uint rim,uint hoverRim)
        {
            var style=new GUIStyle(action){border=new RectOffset(16,16,16,16)};
            style.normal.background=Round(Css(normal),16,Css(rim));
            style.hover.background=Round(Css(hover),16,Css(hoverRim),2);
            style.active.background=Round(Css(active),16,Css(hoverRim),2);
            style.focused.background=style.hover.background;
            style.onNormal.textColor=style.onHover.textColor=style.onActive.textColor=style.onFocused.textColor=Ink;
            return style;
        }
        static Texture2D Round(Color color,int radius,Color? rim=null,float rimWidth=1.5f)
        {
            int size=radius*2+4;var t=new Texture2D(size,size,TextureFormat.RGBA32,false);var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++){float dx=Mathf.Max(radius-x,x-(size-radius-1)),dy=Mathf.Max(radius-y,y-(size-radius-1));float d=Mathf.Sqrt(Mathf.Max(0,dx)*Mathf.Max(0,dx)+Mathf.Max(0,dy)*Mathf.Max(0,dy));Color c=rim.HasValue?Color.Lerp(rim.Value,color,Mathf.Clamp01(radius-rimWidth+.5f-d)):color;pixels[y*size+x]=new Color(c.r,c.g,c.b,c.a*Mathf.Clamp01(radius+.5f-d));}
            t.SetPixels(pixels);t.Apply();t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;return t;
        }
        void Text(Rect r,string text,int size=18,TextAnchor align=TextAnchor.UpperLeft,Color? color=null,bool title=false,Color? shadow=null,float shadowY=1,float blur=0,float outline=0)
        {
            var s=title?heading:label;s.fontSize=size;s.alignment=align;
            if(shadow.HasValue){
                Color tint=shadow.Value;GuguTypography.SetLabelColor(s,tint);
                if(outline>0)for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)if(dx!=0||dy!=0)GUI.Label(new Rect(r.x+dx*outline,r.y+dy*outline,r.width,r.height),text,s);
                if(blur>0){tint.a*=.12f;GuguTypography.SetLabelColor(s,tint);for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)GUI.Label(new Rect(r.x+dx*blur*.5f,r.y+shadowY+dy*blur*.5f,r.width,r.height),text,s);}
                tint=shadow.Value;tint.a*=blur>0?.6f:1;GuguTypography.SetLabelColor(s,tint);GUI.Label(new Rect(r.x,r.y+shadowY,r.width,r.height),text,s);
            }
            GuguTypography.SetLabelColor(s,color??Ink);GUI.Label(r,text,s);
        }
        void Box(Rect r,Texture2D tex=null){panelStyle.normal.background=tex?tex:panel;GUI.Box(r,GUIContent.none,panelStyle);}
        bool Button(Rect r,string text,GUIStyle style=null)
        {
            if(modal=="setup"){
                if(style==null||style==action||style==tuningAction)style=setupAction;
                else if(style==primaryAction)style=setupPrimary;
            }
            desktop.Interactive(r);uiArt.DrawButtonShadow(r,desktop.PointerOver(r),GUI.enabled,reduced);
            if(GUI.Button(r,text,style??action)){sound?.Sample("button",.25f);return true;}return false;
        }
        bool Ice(Rect r,string asset)
        {
            desktop.Interactive(r);
            bool hover=desktop.PointerOver(r);
            #if UNITY_EDITOR || GUGU_QA
            if(IsPreview)hover=previewIceHover==asset;
#endif
            bool active=hover&&Event.current.type==EventType.Repaint&&Mouse.current!=null&&Mouse.current.leftButton.isPressed;
            var im=Art("floe/"+asset+(active?"-Click":""));if(!im)im=Art("floe/"+asset);
            Rect artworkBounds=r;
            if(asset=="Back-Botton"&&active&&!reduced)artworkBounds.y+=3;
            uiArt.Draw(artworkBounds,im,hover,GUI.enabled,reduced);
            if(GUI.Button(r,GUIContent.none,GUIStyle.none)){sound.Sample("button",.25f);return true;}return false;
        }
        bool Tap(Rect r,string text,string lane,bool bite=true)
        {
            desktop.Interactive(r);uiArt.DrawButtonShadow(r,desktop.PointerOver(r),GUI.enabled,reduced);
            if(GUI.enabled&&Event.current.type==EventType.MouseDown&&Event.current.button==0&&r.Contains(Event.current.mousePosition)){Press(lane,Time.realtimeSinceStartupAsDouble,bite);Event.current.Use();}
            GUI.Button(r,text,action);return false;
        }
        void LateUpdate()
        {
            if(!stage||run==null)return;
            float scale=Mathf.Min(Screen.width/W,Screen.height/H),x=(Screen.width-W*scale)*.5f,y=(Screen.height-H*scale)*.5f;
            var bounds=new Rect(-x/scale,-y/scale,Screen.width/scale,Screen.height/scale);
            float t=(float)(run.openingBreath?run.breathElapsed:run.status=="ready"?visual:run.time);
            stage.Begin(Scene,bounds);view.DrawBackdrop(run,Scene,bounds.width,bounds.height);
            view.Draw(run,Scene,t,run.status=="paused"||IsPreview?0:lastDt,W,H,bounds);stage.End();
        }
        void OnGUI()
        {
            Styles();GUI.skin=theme;float scale=Mathf.Min(Screen.width/W,Screen.height/H);float x=(Screen.width-W*scale)*.5f,y=(Screen.height-H*scale)*.5f;
            if(!stage&&Event.current.type==EventType.Repaint)view.DrawBackdrop(run,Scene,Screen.width,Screen.height);
            canvasBounds=new Rect(-x/scale,-y/scale,Screen.width/scale,Screen.height/scale);
            GUI.matrix=Matrix4x4.TRS(new Vector3(x,y,0),Quaternion.identity,new Vector3(scale,scale,1));
            desktop.BeginGui();
            if(stage)stage.DrawLabels(sans);
            if(run!=null){
                float t=(float)(run.openingBreath?run.breathElapsed:run.status=="ready"?visual:run.time);
                if(!stage&&Event.current.type==EventType.Repaint)view.Draw(run,Scene,t,run.status=="paused"||IsPreview?0:lastDt,W,H,canvasBounds);
                // The dimmer is only paint. Disable every underlying control before
                // processing the foreground window, including manual mouse-down taps.
                bool backgroundEnabled=GUI.enabled;GUI.enabled=backgroundEnabled&&modal=="";
                try {
                    Header();
                    if(Scene=="menu")Menu();else if(Scene=="breath")Breathing();else if(Scene=="play")Hud();else Ending();
                    if(Scene!="menu")Footer();
                } finally { GUI.enabled=backgroundEnabled; }
                if(modal!=""){
                    Modal();
                    if(Event.current.isMouse||Event.current.type==EventType.ScrollWheel)Event.current.Use();
                }
                if(IsPreview&&!hidePreviewBadge){Box(new Rect(440,671,400,39));Text(new Rect(450,679,230,24),"美術預覽 · 非實際成績",16);if(Button(new Rect(690,674,130,33),"返回"))Back();}
            }else{Box(new Rect(180,180,920,300));Text(new Rect(210,220,850,200),error,24);}
            if(desktop.FpsVisible){
                Box(new Rect(12,9,146,48));
                Text(new Rect(18,17,134,31),desktop.FramesPerSecond+" FPS",22,TextAnchor.MiddleCenter);
            }
            desktop.EndGui();GUI.matrix=Matrix4x4.identity;
        }
        void Header()
        {
            Text(new Rect(desktop.FpsVisible?174:39,9,330,38),"≈ 海豹呼呼",28,TextAnchor.UpperLeft,DiveUi?Css(0xecfaff):Ink,true,DiveUi?Css(0x235c77):(Color?)null,1,3);
            Text(new Rect(desktop.FpsVisible?184:49,47,250,25),"一口氣的旅程",14,TextAnchor.UpperLeft,DiveUi?Css(0xecfaff):Muted);
            Text(new Rect(1020,25,132,28),Version,18,TextAnchor.MiddleRight,DiveUi?Css(0xe9f9ff):Muted);
            if(Button(new Rect(1160,15,47,45),sound.muted?"♪":"♫")){sound.Mute(!sound.muted);if(!sound.muted&&run.status=="ready")sound.Menu(tracks[song]);}
            if((run.status=="playing"||run.status=="breathing")&&!IsPreview&&Button(new Rect(1214,15,47,45),"Ⅱ"))Pause();
            if(Scene!="menu")Text(new Rect(50,88,600,27),Scene=="breath"?"02 / 給自己一口氣":Scene=="play"?"03 / 循著音樂，游向海底":"04 / 旅程的另一端",16,TextAnchor.UpperLeft,DiveUi?Css(0xd6f0f7,.8f):Muted);
        }
        void Menu()
        {

            if(!stage){var logo=Art("huhu/title");if(logo)GUI.DrawTexture(GuguUiArt.Fit(new Rect(145,185,530,175),logo),logo);}
            Text(new Rect(195,320,500,42),"一口氣，游向你。",27,TextAnchor.UpperLeft,Ink,true);
            if(Ice(HuhuStage.AnchorRect(stage?stage.startAnchor:null,new Rect(717,158,461,192)),"Start-Botton")){modal="setup";setupScroll=Vector2.zero;}
            if(Ice(HuhuStage.AnchorRect(stage?stage.creditsAnchor:null,new Rect(806,331,448,186)),"Credits-Botton"))modal="credits";
            if(Ice(HuhuStage.AnchorRect(stage?stage.exitAnchor:null,new Rect(781,518,422,176)),"Exit-Botton")){sound.Silence();Application.Quit();}
        }
        string Cue(out double remaining)
        {
            remaining=0;if(run.status!="playing"&&!(run.status=="paused"&&run.beforePause=="playing"))return "none";
            if(run.phase=="surface"){remaining=run.breathRemaining;return "refill";}
            if(run.phase!="underwater")return "none";
            if(run.time-run.missedGateAt<1.5)return "missed";
            var note=run.notes.Skip(run.cursor).FirstOrDefault(n=>n.result==null&&(n.kind=="surface"||n.kind=="exit"));
            remaining=note!=null?run.noteTime(note)-run.time:double.PositiveInfinity;
            return remaining>=-run.window&&remaining<=3.5?"approach":"none";
        }
        void GateAlert()
        {
            string cue=Cue(out double left);if(cue=="none")return;
            Color c=new Color(1,.91f,.81f,.96f);var old=GUI.color;GUI.color=c;Box(new Rect(345,121,590,94));GUI.color=old;
            Text(new Rect(355,130,570,47),cue=="approach"?"換氣冰洞！":cue=="missed"?"錯過換氣！":run.air>=90?"繼續！多吸一點":"快！連打補氣",34,TextAnchor.MiddleCenter,new Color(.56f,.23f,.19f),true);
            Text(new Rect(355,178,570,28),cue=="approach"?"等洞口到海豹面前，準拍按 D / F / ↑":cue=="missed"?"屏住最後一口氣，抓住下一個冰洞！":run.air>=90?"越接近滿氣越難補，再多按幾下！":"SPACE 或上下排連打 · 氧氣就是生命",16,TextAnchor.MiddleCenter,new Color(.56f,.23f,.19f));
        }
        void Meter(Rect r,string name,string value,float fraction,Color fill,int valueSize=24)
        {
            Text(new Rect(r.x,r.y,r.width*.55f,32),name,17,TextAnchor.UpperLeft,WorldInk,false,DiveUi?(Color?)SongShadow:null,1,4);Text(new Rect(r.x+r.width*.5f,r.y-2,r.width*.5f,35),value,valueSize,TextAnchor.UpperRight,WorldInk,false,DiveUi?(Color?)SongShadow:null,1,4);
            string kind=name=="肺活量"?"air":name=="節奏穩定"?"stability":"food";
            meters.Draw(new Rect(r.x,r.y+34,r.width,kind=="stability"?11:17),fraction,kind,fraction<.35f&&(!run.openingBreath||kind!="air"),reduced||run.openingBreath||IsPreview);
        }
        void Breathing()
        {
            GateAlert();bool opening=run.openingBreath;
            Text(new Rect(160,514,960,48),opening?"慢慢吸一口氣。":"連打吸氣！"+run.breathTaps+" 下",29,TextAnchor.MiddleCenter,Ink,true);
            Text(new Rect(228,562,824,48),opening?"看肚子鼓起、氣流收攏，達到 90% 以上時按一下 SPACE 或上下排按鍵收氣。":"持續連打 SPACE 或上下排按鍵！越接近滿氣，每下補得越少，倒數結束自動下海。",17,TextAnchor.UpperCenter);
            Meter(new Rect(294,612,610,50),"肺活量",GuguRun.airDisplay(run.air)+"%",(float)run.air/100,new Color(.39f,.71f,.79f));
            if(opening)GUI.DrawTexture(new Rect(294+610*.9f,643,2,25),Texture2D.whiteTexture,ScaleMode.StretchToFill,false,0,new Color(.55f,.44f,.24f),0,0);
            if(!IsPreview)Tap(new Rect(950,602,182,67),opening?"收氣\nSPACE":"連打補氣\nSPACE","lower",false);
        }
        void Hud()
        {
            Text(new Rect(51,126,235,23),"SCORE",15,TextAnchor.UpperLeft,ScoreInk,false,Css(0x204b68),2,3);
            Text(new Rect(51,152,270,39),run.score.ToString("0000000"),30,TextAnchor.UpperLeft,ScoreInk,false,Css(0x204b68),2,3);
            Text(new Rect(51,195,260,28),"ACCURACY  "+run.accuracy.ToString("F2")+"%",17,TextAnchor.UpperLeft,ScoreInk,false,Css(0x204b68),2,3);
            string cue=Cue(out double remain);if(cue=="none"){
                var matrix=GUI.matrix;GUIUtility.ScaleAroundPivot(Vector2.one*(reduced?1:comboScale),new Vector2(640,165));
                Text(new Rect(510,117,260,85),run.combo.ToString(),70,TextAnchor.MiddleCenter,Css(0xfff0a5),false,Css(0x34667f),3,1,2);GUI.matrix=matrix;
                Text(new Rect(540,201,200,26),"C O M B O",18,TextAnchor.MiddleCenter,Css(0xfffdf0),false,Css(0x255a77),3,2);
            }
            GateAlert();
            Meter(new Rect(948,126,281,50),"肺活量",GuguRun.airDisplay(run.air)+"%",(float)run.air/100,run.air<35?new Color(.88f,.5f,.45f):new Color(.41f,.73f,.79f));
            Meter(new Rect(948,193,281,50),"節奏穩定",GuguRun.airDisplay(run.stability)+"%",(float)run.stability/100,new Color(.72f,.75f,.88f),21);
            if(Button(new Rect(948,251,281,75),"遊玩調整  ☷\n"+speed.ToString("F1")+"× · "+delay+"ms · ±"+window+"ms",tuningAction))OpenTuning();
            int stage=0;for(int i=0;i<3;i++)if(run.time>=run.track.stageBounds[i])stage=i;
            Text(new Rect(51,229,305,27),(run.practice?"練習 · ":"")+"0"+(stage+1)+" / "+new[]{"晨光淺海","藍色冰廊","極光歸途"}[stage]+" · "+run.stageFood[stage]+"/"+run.stageTotals[stage],15,TextAnchor.UpperLeft,DiveInk,false,SongShadow,1,4);
            Text(new Rect(51,259,280,31),run.track.title,24,TextAnchor.UpperLeft,DiveInk,false,SongShadow,1,4);
            Text(new Rect(51,291,300,28),new[]{"小海豹","圓滾滾","飽飽海豹"}[run.form]+" · 體型 "+Math.Round(run.growth*100)+"% · "+run.depth+" m",15,TextAnchor.UpperLeft,DiveInk,false,SongShadow,1,4);
            if(judgeAge<.7f){var matrix=GUI.matrix;GUIUtility.ScaleAroundPivot(Vector2.one*(reduced?1:judgeScale),new Vector2(666,280));var color=grade=="MISS"?Css(0xffc1b6):grade=="GOOD"?Css(0xbceefa):Css(0xffe99b);color.a=Mathf.Clamp01(1-(judgeAge-.45f)*4);Text(new Rect(486,244,360,66),grade,56,TextAnchor.MiddleCenter,color,false,Css(0x345976,color.a),3,1,1.5f);Text(new Rect(446,312,440,33),timing,17,TextAnchor.MiddleCenter,grade=="MISS"?Css(0xffe2da,color.a):Css(0xedffff,color.a),false,Css(0x204564,color.a),2,4);GUI.matrix=matrix;}
            if(run.danger>.05)Text(new Rect(948,341,284,90),run.air<10?"最後一口氣！準拍衝向釣魚人的洞口。":run.air<25?"氧氣不足，下一個冰洞一定要抓準。":"呼吸漸急。看好釣魚人，準備上岸。",18,TextAnchor.UpperLeft,Css(0xffe7c6),false,Css(0x204e65),2,4);
            Text(new Rect(562,520,356,43),run.phase=="underwater"?"跟著鼓點，咬住小魚。":run.called?"朋友聽見了，旅程即將完成。":"跟著音樂，準備下一段旅程。",21,TextAnchor.MiddleCenter,Css(0xe8faff),false,Css(0x234b67),2,4);
            Text(new Rect(562,563,356,38),run.time<run.track.introEnd?"前奏 · 放鬆準備，魚群正在靠近":double.IsInfinity(remain)?"準備上岸，呼喚同伴":(blind?"純聽練習 · ":"")+"冰洞倒數 "+Math.Max(0,remain).ToString("F1")+" 秒",16,TextAnchor.MiddleCenter,Css(0xe8faff),false,Css(0x234b67),2,4);
            Meter(new Rect(64,601,844,50),"飽食度",run.food+" / "+run.targets.full,(float)run.food/run.targets.full,new Color(.93f,.77f,.40f),22);
            Text(new Rect(64,654,844,25),run.targets.grow+" · 長大",16,TextAnchor.UpperLeft,Css(0xe7faff));Text(new Rect(64,654,844,25),run.targets.fat+" · 進化",16,TextAnchor.UpperCenter,Css(0xe7faff));Text(new Rect(64,654,844,25),run.targets.full+" · 吃飽",16,TextAnchor.UpperRight,Css(0xe7faff));
            if(!IsPreview){Tap(new Rect(978,602,116,78),"上排\nD F ↑","upper");Tap(new Rect(1107,602,116,78),"下排\nJ K ↓","lower");}
        }
        void Footer(){Text(new Rect(30,692,350,22),run.track.title+" · "+run.totalFish+" 隻魚",14,TextAnchor.UpperLeft,new Color(WorldInk.r,WorldInk.g,WorldInk.b,.72f));Text(new Rect(350,692,590,22),"D F ↑ 上排  ·  J K ↓ 下排  ·  ESC 暫停",14,TextAnchor.UpperCenter,new Color(WorldInk.r,WorldInk.g,WorldInk.b,.72f));Text(new Rect(980,692,267,22),Fmt(run.time)+" / "+Fmt(run.track.duration),14,TextAnchor.UpperRight,new Color(WorldInk.r,WorldInk.g,WorldInk.b,.72f));if(!run.openingBreath)GUI.DrawTexture(new Rect(0,716,W*(float)run.progress,4),Texture2D.whiteTexture,ScaleMode.StretchToFill,false,0,new Color(.38f,.67f,.76f),0,0);}
        void Ending()
        {
            Box(new Rect(230,387,820,288));string title,copy;
            switch(run.outcome){case "friends":title="吃飽了，也交到朋友了。";copy="你跨過冰洞、呼喚了同伴。下一次，一起游吧。";break;case "rest":title="沒吃飽，先攤一下。";copy="平安回到岸上。休息一下，再去找小魚吧。";break;case "angel":title="有翅膀的小海豹。";copy="肚子暖暖的，旅程化成了一雙小翅膀。";break;default:title="還餓著的小幽靈。";copy="還沒吃飽的海豹，變成了餓死鬼。";break;}
            Text(new Rect(252,408,776,26),(run.practice?"PRACTICE / ":"")+(run.status=="won"?"旅程完成":"旅程結束"),16,TextAnchor.MiddleCenter,Muted);
            Text(new Rect(252,446,776,48),title,33,TextAnchor.MiddleCenter,Ink,true);
            string reason=run.reason=="oxygen"?"氧氣耗盡。":run.reason=="rhythm"?"節奏穩定度歸零。":run.reason=="leap"?"錯過了大吸氣前的上岸機會。":run.reason=="route"?"沒有完成跨洞、上岸與呼喚。":"";
            Text(new Rect(258,502,764,107),reason+copy+" 吃到 "+run.food+" / "+run.totalFish+" 隻魚；吃飽目標 "+run.targets.full+" 隻。\n準確率 "+run.accuracy.ToString("F1")+"% · 最高 "+run.maxCombo+" COMBO · PERFECT "+run.counts.perfect+" / GOOD "+run.counts.good+" / MISS "+run.counts.miss+"\n時機／錯軌 "+run.overpresses+" 次 · 魚速 "+speed.ToString("F1")+"× · 延遲 "+delay+" ms · 判定 ±"+window+" ms",17,TextAnchor.UpperCenter);
            if(Button(new Rect(439,617,200,44),"再一次 ↗",primaryAction))StartRun();if(Button(new Rect(661,617,180,44),"回到開始"))Back();
        }
        void Modal()
        {
            GUI.DrawTexture(canvasBounds,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.12f,.27f,.34f,.42f),0,0);
            if(modal=="setup"){Setup();return;}
            if(modal=="credits"){
                Box(new Rect(300,125,680,490));Text(new Rect(330,159,620,50),"CREDITS",34,TextAnchor.MiddleCenter,Ink,true);
                Text(new Rect(330,222,620,52),credits?credits.group:"第二組",34,TextAnchor.MiddleCenter);
                string lines=credits&&credits.entries!=null?string.Join("\n",credits.entries):"內容待補";float contentHeight=Mathf.Max(200,label.CalcHeight(new GUIContent(lines),570)+32);
                creditsScroll=GUI.BeginScrollView(new Rect(342,291,596,207),creditsScroll,new Rect(0,0,570,contentHeight));Text(new Rect(0,0,570,contentHeight),lines,22,TextAnchor.UpperCenter);GUI.EndScrollView();
                if(Ice(new Rect(580,513,120,85),"Back-Botton"))CloseModal();return;
            }
            Box(new Rect(332,85,616,565));Text(new Rect(365,111,550,48),modal=="pause"?"海會等你。":"找到舒服的手感。",32,TextAnchor.MiddleCenter,Ink,true);
            Text(new Rect(365,166,550,39),modal=="pause"?"氧氣、音樂與旅程都停在這一刻。":"設定會自動保存，下次遊玩仍然適用。",18,TextAnchor.MiddleCenter);
            Tuning(374,223,532);if(Button(new Rect(428,554,235,54),modal=="pause"?"繼續旅行 ↗":"完成調整",primaryAction)){if(modal=="pause")Resume();else CloseModal();}
            if(Button(new Rect(678,554,182,54),"回到開始"))Back();
        }
        void Setup()
        {
            Box(new Rect(244,25,792,670));Text(new Rect(273,45,640,48),"選一首歌，準備出發。",30,TextAnchor.UpperLeft,Ink,true);
            if(Ice(new Rect(946,32,70,64),"Back-Botton")){CloseModal();return;}
            desktop.BeginPointerClip(new Rect(271,100,738,570));
            setupScroll=GUI.BeginScrollView(new Rect(271,100,738,570),setupScroll,new Rect(0,0,714,1100));
            Text(new Rect(0,0,690,26),"選擇音樂",18);
            for(int i=0;i<tracks.Length;i++){
                Rect r=new Rect(i*237,37,225,92);
                if(Button(r,tracks[i].title+"\n"+Math.Round(tracks[i].bpm)+" BPM · "+Fmt(tracks[i].duration),song==i?choiceSelected:choice)){if(song!=i){song=i;StopCalibration();sound.Silence();NewPreview();sound.Menu(tracks[song]);}}
            }
            Text(new Rect(0,146,690,26),"選擇難度",18);
            string[] levels={"beginner","intermediate","expert"},names={"新手\n每 4 拍一次","中階\n每 2 拍一次","高手\n全拍＋八分連打"};
            for(int i=0;i<3;i++){
                Rect r=new Rect(i*237,182,225,83);
                if(Button(r,names[i],level==levels[i]?choiceSelected:choice)){if(level!=levels[i]){level=levels[i];window=150;SaveTuning();StopCalibration();NewPreview();sound.Menu(tracks[song]);}}
            }
            Text(new Rect(0,276,714,28),"吃飽目標 "+run.targets.full+" / "+run.totalFish+" 隻 · 耗氧 "+Math.Round(run.balance.oxygen*100)+"% · Miss −"+run.balance.missPenalty+" 穩定",16,TextAnchor.MiddleCenter,Muted);
            if(Button(new Rect(222,310,270,47),"開始吸氣 →",primaryAction))StartRun();
            bool p=Toggle(new Rect(0,361,330,51),practice,"輕鬆練習 · 不會死亡",toggle);bool b=Toggle(new Rect(354,361,355,51),blind,"純聽練習 · 隱藏魚群，不會死亡",toggle);
            if(p!=practice||b!=blind){practice=p;blind=b;NewPreview();}
            Text(new Rect(0,431,712,124),"D／F／↑ 打上排，J／K／↓ 打下排。\n開場看肚子鼓起、氣流收攏時，按一下收氣。途中上岸才需連打補氣，時間到自動下海。\n空拍可自由上下移動。魚靠近判定點時，太早、太晚或按錯軌會 Miss，該魚不能再補按。節奏穩定度歸零則旅程結束。",17);
            Tuning(0,570,710);
            Text(new Rect(0,904,710,35),"拍點試聽與耳機校正",22,TextAnchor.UpperLeft,Ink,true);
            Text(new Rect(0,946,710,49),"聽到清脆輕響時按 SPACE 或點「跟拍」，收集 6 次以上再套用。校正含個人反應時間，可手動微調。",16);
            if(Button(new Rect(0,1007,212,45),calibration?"停止試聽":"試聽 16 秒")){if(calibration)StopCalibration();else StartCalibration();}
            if(Button(new Rect(240,1007,212,45),"跟拍"))CalTap(Time.realtimeSinceStartupAsDouble);
            GUI.enabled=calibrationSamples.Count>=6;if(Button(new Rect(480,1007,212,45),"套用校正")){var values=calibrationSamples.OrderBy(n=>n).ToArray();delay=(float)values[values.Length/2];SaveTuning();StopCalibration();}GUI.enabled=true;
            Text(new Rect(0,1062,710,30),calibrationSamples.Count>0?"已收集 "+calibrationSamples.Count+" 次 · 建議 "+Math.Round(calibrationSamples.OrderBy(n=>n).ElementAt(calibrationSamples.Count/2))+" ms":"可依耳機與個人反應時間校正剩餘誤差。",16,TextAnchor.UpperLeft,Muted);
            GUI.EndScrollView();desktop.EndPointerClip();
        }
        bool Toggle(Rect r,bool value,string text,GUIStyle style)
        {
            if(modal=="setup"){
                r.height=Mathf.Max(r.height,38);style=setupToggle;text=(value?"✓  ":"□  ")+text;
                uiArt.DrawButtonShadow(r,desktop.PointerOver(r),GUI.enabled,reduced);
            }
            desktop.Interactive(r);bool next=GUI.Toggle(r,value,text,style);
            if(next!=value)sound?.Sample("button",.25f);return next;
        }
        float Slider(Rect r,float value,float min,float max,GUIStyle track,GUIStyle knob)
        {
            if(modal=="setup"){track=setupTrack;knob=setupThumb;}
            desktop.Interactive(r);return GUI.HorizontalSlider(r,value,min,max,track,knob);
        }
        void Tuning(float x,float y,float width)
        {
            float oldSpeed=speed,oldDelay=delay,oldWindow=window;
            Text(new Rect(x,y,width,31),"魚群速度",19);Text(new Rect(x,y,width,31),speed.ToString("F1")+"×",22,TextAnchor.UpperRight);
            speed=Slider(new Rect(x,y+37,width,25),speed,.1f,2f,scrollbar,thumb);Text(new Rect(x,y+61,width,24),"0.1× — 2.0× · 只調整魚群前進速度",15,TextAnchor.UpperLeft,Muted);
            Text(new Rect(x,y+96,width,31),"判定延遲",19);Text(new Rect(x,y+96,width,31),(delay>0?"+":"")+delay+" ms",22,TextAnchor.UpperRight);
            delay=Slider(new Rect(x,y+133,width,25),delay,-200,200,scrollbar,thumb);Text(new Rect(x,y+157,width,26),"−200 — +200 ms · 正值晚按，負值提早按",15,TextAnchor.UpperLeft,Muted);
            Text(new Rect(x,y+190,width,31),"判定範圍",19);Text(new Rect(x,y+190,width,31),"±"+window+" ms",22,TextAnchor.UpperRight);
            window=Slider(new Rect(x,y+227,width,25),window,40,200,scrollbar,thumb);Text(new Rect(x,y+251,width,24),"前後各 40 — 200 ms · 數值越小，判定越嚴格",15,TextAnchor.UpperLeft,Muted);
            if(Button(new Rect(x,y+284,220,38),"重設遊玩調整")){speed=1;delay=0;window=150;SaveTuning();}
            bool next=Toggle(new Rect(x+250,y+290,width-250,30),reduced,"減少動態效果",toggle);if(next!=reduced){reduced=next;SaveTuning();}
            if(oldSpeed!=speed||oldDelay!=delay||oldWindow!=window)SaveTuning();
        }
        void StartCalibration(){sound.Silence();calibrationClicks.Clear();calibrationSamples.Clear();lastCalibration=-1;foreach(var n in run.notes)if(n.time>=7&&n.time<23)calibrationClicks.Add(n.time);sound.Play(tracks[song],6,false);foreach(var time in calibrationClicks)sound.ClickAt(time);calibration=true;}
        void StopCalibration(){if(calibration)sound.Pause();calibration=false;}
        void CalTap(double stamp){if(!calibration||calibrationClicks.Count==0)return;double t=sound.TimeAt(stamp);int best=0;for(int i=1;i<calibrationClicks.Count;i++)if(Math.Abs(calibrationClicks[i]-t)<Math.Abs(calibrationClicks[best]-t))best=i;if(best==lastCalibration||Math.Abs(calibrationClicks[best]-t)>.3)return;lastCalibration=best;calibrationSamples.Add((t-calibrationClicks[best])*1000);}

#if UNITY_EDITOR || GUGU_QA
        /// <summary>Editor/dev-build artwork review only; never compiled into the release player.</summary>
        public void PreviewScene(string mode)
        {
            sound.Silence();modal="";NewPreview();IsPreview=true;
            if(mode=="menu")return;
            if(mode=="setup"||mode=="credits"||mode=="tuning"){modal=mode;setupScroll=Vector2.zero;return;}
            if(mode=="opening"||mode=="opening-full"){run.startBreath();run.advanceBreath(mode=="opening-full"?2.7:.24);return;}
            if(mode.StartsWith("swim")||mode.StartsWith("low")||mode=="bite"){
                run.status="playing";run.phase="underwater";run.time=19.1;run.air=mode.StartsWith("low")?8:73;
                run.food=mode.Contains("fat")?run.targets.fat:mode.Contains("medium")?run.targets.grow:0;run.depth=24;
                foreach(var n in run.notes)if(n.time<19.1)n.result="hit";run.cursor=run.notes.FindIndex(n=>n.result==null);
                view.Reset(run);if(mode=="bite"){var note=run.notes.First(n=>n.kind=="fish");view.Emit(new GameEvent{type="hit",note=note,result="perfect"},run);run.time+=.08;}return;
            }
            if(mode=="gate"||mode=="surface"){
                var hole=run.notes.First(n=>n.kind=="surface");run.status="playing";run.time=run.noteTime(hole)-(mode=="gate"?1.5:0);run.phase=mode=="gate"?"underwater":"surface";run.air=18;run.depth=18;
                foreach(var n in run.notes)if(n.time<run.time)n.result="hit";if(mode=="surface")hole.result="hit";run.cursor=run.notes.FindIndex(n=>n.result==null);return;
            }
            run.status=mode=="friends"||mode=="rest"?"won":"lost";run.outcome=mode;run.food=mode=="friends"?run.targets.full:mode=="rest"?run.targets.full-10:mode=="angel"?run.targets.fat:run.targets.fat-10;run.phase="shore";run.leaped=run.called=true;
        }
#endif
    }
}
