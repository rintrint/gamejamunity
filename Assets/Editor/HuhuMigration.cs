using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SealGugu.Editor {
    public static class HuhuMigration {
        const string Folder="Assets/Settings/";
        [MenuItem("Tools/海豹呼呼/建立 URP 2D 可編輯場景")]
        public static void Upgrade(){
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before migrating the scene.");
            AssetDatabase.Refresh();
            var renderer=AssetDatabase.LoadAssetAtPath<Renderer2DData>(Folder+"HuhuRenderer2D.asset");
            if(!renderer){renderer=ScriptableObject.CreateInstance<Renderer2DData>();AssetDatabase.CreateAsset(renderer,Folder+"HuhuRenderer2D.asset");}
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Folder+"HuhuURP.asset");
            if(!pipeline){pipeline=UniversalRenderPipelineAsset.Create(renderer);pipeline.supportsHDR=false;pipeline.msaaSampleCount=1;AssetDatabase.CreateAsset(pipeline,Folder+"HuhuURP.asset");}
            GraphicsSettings.defaultRenderPipeline=pipeline;
            int previous=QualitySettings.GetQualityLevel();
            for(int i=0;i<QualitySettings.names.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=pipeline;}
            QualitySettings.SetQualityLevel(previous,false);
            PlayerSettings.productName="海豹呼呼";
            var lit=Material("HuhuSpriteLit","Universal Render Pipeline/2D/Sprite-Lit-Default");
            var unlit=Material("HuhuSpriteUnlit","Universal Render Pipeline/2D/Sprite-Unlit-Default");
            var scene=EditorSceneManager.OpenScene(ProjectBuild.ScenePath);
            var game=UnityEngine.Object.FindAnyObjectByType<GuguGame>();if(!game)throw new InvalidOperationException("Missing game root");
            game.name="海豹呼呼 · "+GuguGame.Version;
            var stage=UnityEngine.Object.FindAnyObjectByType<HuhuStage>();
            if(!stage){var root=new GameObject("Presentation · editable 2D scenes");stage=root.AddComponent<HuhuStage>();}
            game.stage=stage;stage.sceneCamera=Camera.main;
            if(!stage.sceneCamera)stage.sceneCamera=UnityEngine.Object.FindAnyObjectByType<Camera>();
            stage.sceneCamera.tag="MainCamera";stage.sceneCamera.orthographic=true;stage.sceneCamera.orthographicSize=3.6f;stage.sceneCamera.transform.position=new Vector3(0,0,-10);
            var data=stage.sceneCamera.GetUniversalAdditionalCameraData();data.SetRenderer(0);data.renderPostProcessing=false;
            stage.spriteLit=lit;stage.spriteUnlit=unlit;stage.titleArtwork=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Art/huhu/title.png");
            if(stage.scenes==null||stage.scenes.Length!=4){
                stage.scenes=new HuhuSceneBinding[4];
                string[] names={"01 · 主畫面 Menu","02 · 吸氣 Breathing","03 · 海底 Underwater","04 · 結局 Ending"};
                for(int i=0;i<4;i++){var root=new GameObject(names[i]);root.transform.SetParent(stage.transform,false);stage.scenes[i]=new HuhuSceneBinding{scene=(HuhuScene)i,root=root.transform};}
            }
            var light=stage.GetComponentInChildren<Light2D>(true);
            if(!light){var go=new GameObject("Global Light 2D · neutral paper light");go.transform.SetParent(stage.transform,false);light=go.AddComponent<Light2D>();light.lightType=Light2D.LightType.Global;light.intensity=1;light.color=Color.white;}
            var art=AssetDatabase.LoadAssetAtPath<HuhuSealArt>("Assets/Animation/Seal/SealArt.asset");
            if(!art){
                art=ScriptableObject.CreateInstance<HuhuSealArt>();
                string[] names={"thin","medium","fat"};Rect[] crops={new Rect(185,670,552,158),new Rect(165,579,501,175),new Rect(164,541,497,214)};float[] ys={.58f,.61f,.72f};
                for(int i=0;i<3;i++)art.swimming[i]=new HuhuSealArt.SwimForm{image=Resources.Load<Texture2D>("Art/floe/swim-"+names[i]),crop=crops[i],mouthAnchor=new Vector2(.99f,ys[i])};
                art.portrait=Resources.Load<Texture2D>("Art/tide/seal");art.eatingAtlas=Resources.Load<Texture2D>("Art/duet/seal-eating");art.menuHungerAtlas=Resources.Load<Texture2D>("Art/v14-2/menu-hunger");AssetDatabase.CreateAsset(art,"Assets/Animation/Seal/SealArt.asset");
            }
            if(!art.menuBackground)art.menuBackground=Resources.Load<Texture2D>("Art/v15-1/menu-background");
            if(!art.menuGifAtlas)art.menuGifAtlas=Resources.Load<Texture2D>("Art/v15-1/hunger-gif");
            if(!art.menuGifTiming)art.menuGifTiming=Resources.Load<TextAsset>("Art/v15-1/hunger-gif");
            if(!art.surfaceFisher)art.surfaceFisher=Resources.Load<Texture2D>("Art/huhu/fisher-poses");
            if(!art.resting)art.resting=Resources.Load<Texture2D>("Art/huhu/resting");
            if(!art.angel)art.angel=Resources.Load<Texture2D>("Art/huhu/angel-new");
            EditorUtility.SetDirty(art);stage.sealArt=art;
            var controller=Animations();
            if(!stage.sealAnimation){
                string path="Assets/Prefabs/SealActor.prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(!prefab){var temp=new GameObject("SealActor · clock-sampled animation");temp.AddComponent<Animator>().runtimeAnimatorController=controller;temp.AddComponent<HuhuSealAnimation>();prefab=PrefabUtility.SaveAsPrefabAsset(temp,path);UnityEngine.Object.DestroyImmediate(temp);}
                var actor=(GameObject)PrefabUtility.InstantiatePrefab(prefab,stage.transform);stage.sealAnimation=actor.GetComponent<HuhuSealAnimation>();
            }
            stage.sealAnimation.GetComponent<Animator>().runtimeAnimatorController=controller;
            stage.titleAnchor=Anchor(stage,"Title · position and size",stage.titleAnchor,new Rect(145,140,530,196));
            stage.startAnchor=Anchor(stage,"Start · position and size",stage.startAnchor,new Rect(717,158,461,192));
            stage.creditsAnchor=Anchor(stage,"Credits · position and size",stage.creditsAnchor,new Rect(806,331,448,186));
            stage.exitAnchor=Anchor(stage,"Exit · position and size",stage.exitAnchor,new Rect(781,518,422,176));
            for(int i=0;i<4;i++){stage.previewScene=(HuhuScene)i;stage.previewPose=i==3?"friends":"swim-thin";stage.RefreshPreview();}
            stage.previewScene=HuhuScene.Menu;stage.previewPose="swim-thin";stage.RefreshPreview();
            EditorUtility.SetDirty(stage);EditorUtility.SetDirty(game);EditorUtility.SetDirty(pipeline);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Selection.activeGameObject=stage.gameObject;
            if(SceneView.lastActiveSceneView){SceneView.lastActiveSceneView.in2DMode=true;SceneView.lastActiveSceneView.LookAt(Vector3.zero,Quaternion.identity,7);}
            Debug.Log("HUHU_URP_READY: URP 17.6 / Renderer2D / four editable stages / animation clips; core clock unchanged.");
        }
        [MenuItem("Tools/海豹呼呼/驗證 URP 場景與動畫")]
        public static void Verify(){
            var stage=UnityEngine.Object.FindAnyObjectByType<HuhuStage>();
            if(!stage||!stage.sealArt)throw new InvalidOperationException("Missing authored stage or seal art");
            if(!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset pipeline)||pipeline.scriptableRenderer.GetType().Name!="Renderer2D")throw new InvalidOperationException("URP Renderer2D is not active");
            int count=0;var old=stage.previewScene;string pose=stage.previewPose;
            foreach(HuhuScene scene in Enum.GetValues(typeof(HuhuScene))){stage.previewScene=scene;stage.RefreshPreview();foreach(var sprite in stage.GetComponentsInChildren<SpriteRenderer>())if(sprite.sprite)count++;}
            if(count<30)throw new InvalidOperationException("Not enough real scene sprites: "+count);
            var anchor=stage.startAnchor;var position=anchor.localPosition;anchor.localPosition+=Vector3.right*.25f;stage.RefreshPreview();
            if(Math.Abs(HuhuStage.AnchorRect(anchor,default).center.x-(640+position.x*100+25))>.01f)throw new InvalidOperationException("Artist anchor offset was overwritten");anchor.localPosition=position;
            var rig=stage.sealAnimation;rig.Sample("Swim",Mathf.PI/(2*3.65f));if(rig.bodyScale<1.02f)throw new InvalidOperationException("Editable Swim clip not driving body scale");
            rig.Sample("Eat",.08,false);if(rig.frame<3||rig.frame>4)throw new InvalidOperationException("Eat clip frame sampling failed");
            rig.Sample("Inhale",1,false);if(rig.bodyScale<1.074f)throw new InvalidOperationException("Inhale clip is not enlarging body");
            for(int i=0;i<3;i++)if(!stage.sealArt.swimming[i].image)throw new InvalidOperationException("Missing body form "+i);
            stage.previewScene=old;stage.previewPose=pose;stage.RefreshPreview();
            File.WriteAllText("Validation/URP/editor-checks.json","{\"success\":true,\"pipeline\":\"URP 17.6 Renderer2D\",\"nativeSpriteObservations\":"+count+",\"editableAnchorPreserved\":true,\"animationClipsSampled\":true,\"swimForms\":3}");
            Debug.Log("HUHU_EDITOR_CHECKS PASS: native sprites, editable anchors, three body forms and sampled clips.");
        }
        static Transform Anchor(HuhuStage stage,string name,Transform existing,Rect r){
            if(existing){if(name.StartsWith("Title")&&Mathf.Abs(existing.localPosition.y-.82f)<.001f)existing.localPosition+=Vector3.up*.4f;return existing;}
            var t=new GameObject(name).transform;t.SetParent(stage.transform,false);t.localPosition=new Vector3((r.center.x-640)/100,(360-r.center.y)/100,0);t.localScale=new Vector3(r.width/100,r.height/100,1);return t;
        }
        static Material Material(string name,string shader){var path=Folder+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){var s=Shader.Find(shader);if(!s)throw new InvalidOperationException("Missing shader "+shader);m=new Material(s);AssetDatabase.CreateAsset(m,path);}return m;}
        static AnimationClip Clip(string name,float duration,bool loop,params (string field,AnimationCurve curve)[] curves){
            var path="Assets/Animation/Seal/"+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(clip)return clip;
            clip=new AnimationClip{name=name,frameRate=60};
            foreach(var item in curves)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(HuhuSealAnimation),item.field),item.curve);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);AssetDatabase.CreateAsset(clip,path);return clip;
        }
        static AnimationCurve Constant(float v,float duration){return AnimationCurve.Linear(0,v,duration,v);}
        static AnimationCurve Wave(float middle,float amount,float duration){var k=new Keyframe[17];for(int i=0;i<k.Length;i++){float t=duration*i/16;k[i]=new Keyframe(t,middle+Mathf.Sin(i/16f*Mathf.PI*2)*amount);}return new AnimationCurve(k);}
        static AnimationCurve Steps(float duration,params float[] values){var keys=new Keyframe[values.Length+1];for(int i=0;i<values.Length;i++)keys[i]=new Keyframe(duration*i/values.Length,values[i],float.PositiveInfinity,float.PositiveInfinity);keys[values.Length]=new Keyframe(duration,values[values.Length-1],float.PositiveInfinity,float.PositiveInfinity);return new AnimationCurve(keys);}
        static AnimatorController Animations(){
            string path="Assets/Animation/Seal/Seal.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);if(controller)return controller;
            controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            var clips=new[]{
                Clip("Idle",1,true,("bodyScale",Constant(1,1)),("frame",Constant(0,1))),
                Clip("Hungry",1,false,("frame",Steps(1,0,1,2,3,2,3,4,5))),
                Clip("Swim",2*Mathf.PI/3.65f,true,("bodyScale",Wave(1,.025f,2*Mathf.PI/3.65f)),("roll",Wave(0,.013f,2*Mathf.PI/3.65f))),
                Clip("Eat",.19f,false,("frame",Steps(.19f,2,3,4,5))),
                Clip("Inhale",1,false,("bodyScale",AnimationCurve.EaseInOut(0,1,1,1.075f))),
                Clip("Rest",1,true,("bodyScale",Constant(1,1)),("bob",Constant(0,1))),
                Clip("Friends",Mathf.PI,true,("bob",Wave(0,5,Mathf.PI)))};
            foreach(var clip in clips){var state=controller.layers[0].stateMachine.AddState(clip.name);state.motion=clip;state.writeDefaultValues=true;if(clip.name=="Idle")controller.layers[0].stateMachine.defaultState=state;}
            return controller;
        }
    }
    [CustomEditor(typeof(HuhuStage))]
    [InitializeOnLoad]
    public sealed class HuhuStageEditor:UnityEditor.Editor {
        static double repaintAt;
        static HuhuStageEditor(){EditorApplication.update+=()=>{
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<repaintAt)return;
            var selected=Selection.activeGameObject;var stage=selected?selected.GetComponentInParent<HuhuStage>():null;
            if(stage&&stage.previewAnimation){repaintAt=EditorApplication.timeSinceStartup+1.0/30;EditorApplication.QueuePlayerLoopUpdate();SceneView.RepaintAll();}
        };}
        public override void OnInspectorGUI(){
            EditorGUILayout.HelpBox("不需 Play：切換 previewScene 查看四幕。展開場景群組，移動 'edit offset' 物件調整位置；Visual 子物件由音樂驅動。換圖請使用 HuhuGraphic.replacement。動畫在 Assets/Animation/Seal。",MessageType.Info);
            DrawDefaultInspector();var stage=(HuhuStage)target;
            EditorGUILayout.LabelField("快速預覽",EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            foreach(var preset in new[]{"swim-thin","swim-medium","swim-fat"})if(GUILayout.Button(preset.Replace("swim-",""))){Undo.RecordObject(stage,"Preview seal form");stage.previewScene=HuhuScene.Underwater;stage.previewPose=preset;stage.RefreshPreview();SceneView.RepaintAll();}
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("撞飛 → 接替 → 吸氣")){Undo.RecordObject(stage,"Preview shore arrival");stage.previewScene=HuhuScene.Breath;stage.previewPose="surface";stage.previewTime=.42f;stage.RefreshPreview();SceneView.RepaintAll();}
            foreach(var pose in new[]{"friends","rest","hungryGhost","angel"})if(GUILayout.Button(pose=="friends"?"朋友":pose=="rest"?"攤平":pose=="angel"?"天使":"餓死鬼")){Undo.RecordObject(stage,"Preview ending");stage.previewScene=HuhuScene.Ending;stage.previewPose=pose;stage.RefreshPreview();SceneView.RepaintAll();}
            EditorGUILayout.EndHorizontal();
            if(GUILayout.Button("更新預覽並框選畫面")){stage.RefreshPreview();SceneView.lastActiveSceneView?.LookAt(Vector3.zero,Quaternion.identity,7);SceneView.RepaintAll();}
            if(GUILayout.Button("儲存場景調整")){EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);EditorSceneManager.SaveScene(stage.gameObject.scene);}
        }
    }
}
