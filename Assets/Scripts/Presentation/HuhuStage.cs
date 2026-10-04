using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace SealGugu
{
    public enum HuhuScene { Menu, Breath, Underwater, Ending }
    [Serializable] public sealed class HuhuSceneBinding { public HuhuScene scene; public Transform root; }
    [ExecuteAlways]
    public sealed class HuhuStage : MonoBehaviour
    {
        public Camera sceneCamera;
        public Material spriteLit,spriteUnlit;
        public HuhuSealAnimation sealAnimation;
        public HuhuSealArt sealArt;
        public HuhuSceneBinding[] scenes;
        [Header("Scene editing preview (no Play required)")]
        public HuhuScene previewScene;
        public string previewPose="swim-thin";
        public bool previewAnimation;
        [Range(0,10)] public float previewTime=1;
        [Header("Main menu anchors: move/scale these in Scene view")]
        public Transform titleAnchor,startAnchor,creditsAnchor,exitAnchor;
        public Texture2D titleArtwork;
        readonly Dictionary<Transform,List<HuhuGraphic>> pools=new Dictionary<Transform,List<HuhuGraphic>>();
        readonly Dictionary<Transform,int> cursors=new Dictionary<Transform,int>();
        readonly List<Label> labels=new List<Label>();
        struct Label {public string text;public float x,y;public int size;public Color color;}
        GuguRenderer editorView;
        GuguRun editorRun;
        HuhuScene shown;
        string shownPose;
        Transform activeRoot,group;
        int order;
        HuhuBelly belly;
        bool bellyDrawn;
        GUIStyle labelStyle;
        Font labelFont;
        bool painting;
        public bool IsPainting=>painting;
        public HuhuScene ActiveScene {get;private set;}
        public Rect Viewport {get;private set;}
        public void Begin(string state,Rect viewport)
        {
            ActiveScene=state=="menu"?HuhuScene.Menu:state=="breath"?HuhuScene.Breath:state=="end"?HuhuScene.Ending:HuhuScene.Underwater;
            Viewport=viewport;order=-1000;labels.Clear();cursors.Clear();painting=true;bellyDrawn=false;
            foreach(var binding in scenes){bool active=binding.scene==ActiveScene;binding.root.gameObject.SetActive(active);if(active)activeRoot=binding.root;}
            Group("Background");
            if(sceneCamera){float aspect=Application.isPlaying?(float)Screen.width/Math.Max(1,Screen.height):16f/9;sceneCamera.orthographic=true;sceneCamera.orthographicSize=Mathf.Max(3.6f,6.4f/aspect);}
        }
        public void Group(string name)
        {
            group=activeRoot.Find(name);
            if(!group){var go=new GameObject(name);group=go.transform;group.SetParent(activeRoot,false);}
        }
        HuhuGraphic Next()
        {
            if(!pools.TryGetValue(group,out var list)){
                list=new List<HuhuGraphic>();foreach(Transform offset in group){var g=offset.GetComponentInChildren<HuhuGraphic>(true);if(g)list.Add(g);}pools.Add(group,list);
            }
            int i=cursors.TryGetValue(group,out var count)?count:0;cursors[group]=i+1;
            if(i>=list.Count){
                var offset=new GameObject("Art "+(i+1).ToString("00")+" · edit offset");offset.transform.SetParent(group,false);
                var driven=new GameObject("Visual · animated");driven.transform.SetParent(offset.transform,false);list.Add(driven.AddComponent<HuhuGraphic>());
            }
            var result=list[i];result.gameObject.SetActive(true);return result;
        }
        public void Slice(Texture2D image,Rect crop,Rect destination,float alpha=1,float rotation=0,bool flip=false,Vector2? pivot=null)
        {
            if(!image||alpha<=0)return;
            var graphic=Next();var anchor=pivot??destination.center;
            Vector2 delta=destination.center-anchor;
            if(flip)delta.x=-delta.x;
            float c=Mathf.Cos(rotation),s=Mathf.Sin(rotation);delta=new Vector2(c*delta.x-s*delta.y,s*delta.x+c*delta.y);
            Vector2 center=anchor+delta;
            graphic.transform.localPosition=new Vector3((center.x-640)/100,(360-center.y)/100,0);
            graphic.transform.localRotation=Quaternion.Euler(0,0,-rotation*Mathf.Rad2Deg);
            graphic.Paint(image,crop,new Vector2(destination.width*(flip?-1:1),destination.height),alpha,spriteLit,spriteUnlit,order++);
        }
        public void Belly(Texture2D image,Rect destination,float expansion)
        {
            Transform offset=group.Find("Abdomen · edit offset");
            if(!offset){offset=new GameObject("Abdomen · edit offset").transform;offset.SetParent(group,false);}
            var body=offset.GetComponentInChildren<HuhuBelly>(true);
            if(!body){var go=new GameObject("Breathing mesh · animated");go.transform.SetParent(offset,false);body=go.AddComponent<HuhuBelly>();}
            body.gameObject.SetActive(true);body.Paint(image,destination,expansion,spriteLit,order++,sealAnimation?sealAnimation.bellyExpansion:.32f);
            belly=body;bellyDrawn=true;
        }
        public void LabelText(string text,float x,float y,int size,Color color){labels.Add(new Label{text=text,x=x,y=y,size=size,color=color});}
        public void DrawLabels(Font font)
        {
            if(labelStyle==null)labelStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,clipping=TextClipping.Overflow,wordWrap=false};
            foreach(var l in labels){labelStyle.font=font;labelStyle.fontSize=Math.Max(16,l.size);GuguTypography.SetLabelColor(labelStyle,l.color);GUI.Label(new Rect(l.x-260,l.y-l.size*1.05f,520,l.size*1.4f),l.text,labelStyle);}
        }
        public static Rect AnchorRect(Transform anchor,Rect fallback)
        {
            if(!anchor)return fallback;var p=anchor.position;var s=anchor.lossyScale;
            return new Rect(640+p.x*100-s.x*50,360-p.y*100-s.y*50,s.x*100,s.y*100);
        }
        public void End()
        {
            if(belly&&!bellyDrawn)belly.gameObject.SetActive(false);
            foreach(var pair in pools){if(!pair.Key)continue;int count=cursors.TryGetValue(pair.Key,out var used)?used:0;for(int i=count;i<pair.Value.Count;i++)if(pair.Value[i])pair.Value[i].gameObject.SetActive(false);}
            if(ActiveScene==HuhuScene.Menu){
                Group("Title");Rect r=AnchorRect(titleAnchor,new Rect(145,195,530,170));if(titleArtwork)Slice(titleArtwork,new Rect(0,0,titleArtwork.width,titleArtwork.height),GuguUiArt.Fit(r,titleArtwork));
                if(!Application.isPlaying){PreviewButton(startAnchor,"Start-Botton");PreviewButton(creditsAnchor,"Credits-Botton");PreviewButton(exitAnchor,"Exit-Botton");}
            }
            painting=false;
        }
        void PreviewButton(Transform anchor,string name){Group("Button previews");var im=Resources.Load<Texture2D>("Art/floe/"+name);if(!im)return;var r=GuguUiArt.Fit(AnchorRect(anchor,default),im);Slice(im,new Rect(0,0,im.width,im.height),r);}
        void Update(){
            if(Application.isPlaying||!sceneCamera||scenes==null||scenes.Length!=4)return;
            RefreshPreview();
        }
        public void RefreshPreview()
        {
            if(Application.isPlaying)return;
            if(editorView==null){editorView=new GuguRenderer{stage=this};editorView.Preload();}
            if(editorRun==null||shown!=previewScene||shownPose!=previewPose){
                var tracks=JsonUtility.FromJson<ChartDocument>(Resources.Load<TextAsset>("Data/chart").text).tracks;
                editorRun=new GuguRun(tracks[0]);editorRun.startBreath();editorRun.advanceBreath(2.7);editorRun.inhale();
                if(previewScene==HuhuScene.Menu){editorRun=new GuguRun(tracks[0]);}
                else if(previewScene==HuhuScene.Breath){
                    if(previewPose.StartsWith("surface")){
                        var hole=editorRun.notes.First(n=>n.kind=="surface");editorRun.time=editorRun.noteTime(hole);editorRun.air=40;
                        editorRun.judge(hole,true);editorRun.events.Clear();
                    }else{editorRun.startBreath();editorRun.advanceBreath(2.3);}
                }
                else if(previewScene==HuhuScene.Ending){editorRun.status="won";editorRun.outcome=previewPose=="angel"?"angel":previewPose=="rest"?"rest":previewPose=="hungryGhost"?"hungryGhost":"friends";editorRun.status=editorRun.outcome=="angel"||editorRun.outcome=="hungryGhost"?"lost":"won";}
                else{editorRun.time=18;editorRun.food=previewPose.Contains("fat")?editorRun.targets.fat:previewPose.Contains("medium")?editorRun.targets.grow:0;editorRun.air=previewPose.StartsWith("low")?14:95;}
                editorView.Reset(editorRun);shown=previewScene;shownPose=previewPose;
            }
            float t=previewAnimation?Time.realtimeSinceStartup:previewTime;
            string state=previewScene==HuhuScene.Menu?"menu":previewScene==HuhuScene.Breath?"breath":previewScene==HuhuScene.Ending?"end":"play";
            if(previewScene==HuhuScene.Underwater)editorRun.time=18+t;
            if(previewScene==HuhuScene.Breath&&previewPose.StartsWith("surface")){
                float age=previewAnimation?t%5:previewTime;editorRun.time=editorRun.arrivalAt+age;
                editorRun.air=40;editorRun.lastBreathTap=age>=GuguRun.SURFACE_ARRIVAL_SECONDS?editorRun.time-(age%.125f):-100;
                editorRun.departureCursor=editorRun.departures.Count(n=>editorRun.noteTime(n)<=editorRun.time);
            }
            editorView.menuHungerProgress=previewPose=="hungry"?(t%3)/3:-1;editorView.menuHungerElapsed=previewPose=="hungry"?t:-1;
            Begin(state,new Rect(0,0,1280,720));editorView.DrawBackdrop(editorRun,state,1280,720);editorView.Draw(editorRun,state,t,0,1280,720,Viewport);End();
        }
        void OnDisable(){if(editorView!=null){editorView.Dispose();editorView=null;}}
    }
}
