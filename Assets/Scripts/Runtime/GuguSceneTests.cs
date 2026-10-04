using System;
using System.Linq;
using UnityEngine;

namespace SealGugu
{
    // Intentional player feature. Unlike QA capture hooks, this ships in release builds.
    public sealed partial class GuguGame
    {
        bool sceneTestActive, testMenuResume, savedTestResume, savedTestPreview, savedTestMusic;
        string sceneTestName="", testMenuReturn="", savedTestModal="";
        double savedTestMusicTime;
        GuguRun savedTestRun;
        public bool SceneTestActive => sceneTestActive;
        public bool SceneTestsOpen => modal=="sceneTests";
        static readonly string[] testIds={"opening","surface","leap","exit","swim-thin","swim-medium","swim-fat","low-thin","low-medium","low-fat","friends","rest","hungryGhost","angel","hungry"};
        static readonly string[] testLabels={"開場吸一口氣","撞飛垂釣者 → 換氣","大吸氣 → 跨洞","最後上岸 → 呼喚","游泳 · 瘦","游泳 · 中","游泳 · 胖","缺氧 · 瘦","缺氧 · 中","缺氧 · 胖","吃飽 · 交到朋友","沒吃飽 · 攤在岸上","失敗 · 餓死鬼","失敗 · 變天使","主畫面 · 肚子餓"};

        void ToggleSceneTests(){if(SceneTestsOpen)CloseSceneTests();else OpenSceneTests();}
        public void OpenSceneTests()
        {
            if(run==null||SceneTestsOpen)return;
            StopCalibration();testMenuReturn=modal;
            testMenuResume=!IsPreview&&(run.status=="playing"||run.status=="breathing");
            if(testMenuResume){if(run.status=="playing")sound.Pause();sound.StopEffects();run.pause();}
            modal="sceneTests";
        }
        public void CloseSceneTests()
        {
            if(!SceneTestsOpen)return;
            modal=testMenuReturn;
            if(testMenuResume){run.resume();if(run.status=="playing")sound.Play(run.track,run.time,false);else lastBreathCycle=-1;}
            else if(run.status=="ready"&&!IsPreview)sound.Menu(tracks[song]);
            testMenuResume=false;
        }
        void ClearSceneTests(){sceneTestActive=false;savedTestRun=null;testMenuResume=false;sceneTestName="";}

        public void SelectSceneTest(string mode)
        {
            if(Array.IndexOf(testIds,mode)<0)return;
            if(!SceneTestsOpen)OpenSceneTests();
            if(!sceneTestActive){
                savedTestRun=run;savedTestPreview=IsPreview;savedTestResume=testMenuResume;savedTestModal=testMenuReturn;
                savedTestMusic=sound.isRunning||savedTestResume&&run.beforePause=="playing";
                savedTestMusicTime=sound.time;
            }
            sound.Silence();calibration=false;modal="";IsPreview=false;
            sceneTestActive=true;sceneTestName=mode;testMenuResume=false;
            NewPreview();run.practice=true;view.hideNotes=false;lastBreathCycle=-1;lastHungerCycle=-1;
            if(mode=="hungry"){
                visual=0;lastHungerCycle=0;sound.Menu(run.track);sound.MenuHunger();return;
            }
            if(mode=="opening"){run.startBreath();return;}
            if(mode=="friends"||mode=="rest"||mode=="angel"||mode=="hungryGhost"){
                run.status=mode=="friends"||mode=="rest"?"won":"lost";run.outcome=mode;run.phase="shore";
                run.time=run.track.duration;run.leaped=run.called=true;
                run.food=mode=="friends"?run.targets.full:mode=="rest"?run.targets.full-10:mode=="angel"?run.targets.fat:Math.Max(0,run.targets.fat-10);
                run.air=run.status=="lost"?0:35;run.reason=run.status=="lost"?"oxygen":"";
                view.Reset(run);run.events.Add(new GameEvent{type=run.status,at=run.time});Events();return;
            }
            run.status="playing";run.phase="underwater";run.initialAir=100;
            run.air=mode.StartsWith("low")?8:73;run.depth=24;run.time=Math.Min(19.1,run.track.duration/4);
            run.food=mode.Contains("fat")?run.targets.fat:mode.Contains("medium")?run.targets.grow:0;
            Note hole=null;
            if(mode=="surface"||mode=="leap"||mode=="exit"){
                hole=run.notes.First(n=>n.kind==(mode=="exit"?"exit":"surface")&&(mode!="leap"||run.departures.FirstOrDefault(d=>run.noteTime(d)>run.noteTime(n))?.kind=="leap"));
                run.time=run.noteTime(hole);run.air=24;run.food=run.targets.grow;
                if(mode=="exit")run.leaped=true;
            }
            foreach(var note in run.notes)if(run.noteTime(note)<run.time)note.result="hit";
            run.cursor=run.notes.FindIndex(n=>n.result==null);if(run.cursor<0)run.cursor=run.notes.Count;
            run.departureCursor=run.departures.Count(n=>run.noteTime(n)<=run.time);
            view.Reset(run);sound.Play(run.track,run.time,false);
            // Use the real judgement/arrival path, including the scheduled fisherman impact sound.
            if(hole!=null){run.judge(hole,true);Events();}
        }
        public void ReturnFromSceneTest()
        {
            if(!sceneTestActive){CloseSceneTests();return;}
            sound.Silence();run=savedTestRun;IsPreview=savedTestPreview;modal=savedTestModal;
            bool resume=savedTestResume, music=savedTestMusic;double musicTime=savedTestMusicTime;
            ClearSceneTests();view.Reset(run);view.hideNotes=blind;
            if(resume)run.resume();
            if(!IsPreview){
                if(run.status=="playing")sound.Play(run.track,run.time,false);
                else if(run.status=="paused"&&run.beforePause=="playing"){sound.Play(run.track,run.time,false);sound.Pause();}
                else if(run.status=="ready")sound.Menu(run.track);
                else if(music&&!run.openingBreath){sound.Play(run.track,musicTime,false);if(run.status=="won")sound.Won();}
            }
            lastBreathCycle=-1;lastHungerCycle=-1;
        }
        void SceneTestToolbar()
        {
            Box(new Rect(407,10,590,53));Text(new Rect(421,19,160,33),"場景測試 · F2",20,TextAnchor.MiddleLeft,Ink,true);
            if(Button(new Rect(582,17,104,38),"重播",setupAction))SelectSceneTest(sceneTestName);
            if(Button(new Rect(696,17,135,38),"選擇場景",setupAction))OpenSceneTests();
            if(Button(new Rect(841,17,145,38),"離開測試",setupAction))ReturnFromSceneTest();
        }
        void SceneTestDialog()
        {
            Box(new Rect(173,65,934,591));
            Text(new Rect(205,88,870,46),"F2 · 場景與結局測試",32,TextAnchor.MiddleCenter,Ink,true);
            Text(new Rect(205,137,870,35),"使用目前歌曲與難度 · 可直接操作 · 測試不會改動原本進度",18,TextAnchor.MiddleCenter,Muted);
            for(int i=0;i<testIds.Length;i++){
                if(Button(new Rect(209+(i%3)*291,187+(i/3)*70,280,58),testLabels[i],sceneTestActive&&sceneTestName==testIds[i]?choiceSelected:setupAction))SelectSceneTest(testIds[i]);
            }
            if(Button(new Rect(229,565,255,53),"關閉選單 · F2 / ESC",setupAction))CloseSceneTests();
            if(sceneTestActive&&Button(new Rect(508,565,255,53),"返回原本遊戲",setupPrimary))ReturnFromSceneTest();
            if(Button(new Rect(787,565,255,53),"回到主畫面",setupAction))Back();
        }
    }
}
