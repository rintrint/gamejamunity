#if UNITY_EDITOR || GUGU_QA
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SealGugu.Diagnostics
{
    public static class SceneTestAudit
    {
        static int assertions;
        static void Check(bool value,string message){assertions++;if(!value)throw new Exception("Scene test audit: "+message);}
        static void State(Keyboard keyboard,params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();}
        static void Tap(Keyboard keyboard,Key key){State(keyboard,key);State(keyboard);}
        static void Shot(string directory,string name){var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(directory,name+".png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);}
        public static IEnumerator Run(GuguGame game,string directory)
        {
            assertions=0;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var type=typeof(GuguGame);var modal=type.GetField("modal",flags);
            var sound=(GuguAudio)type.GetField("sound",flags).GetValue(game);
            var originalKeyboard=Keyboard.current;var previousTestKeyboard=game.automatedKeyboard;
            bool automated=game.automatedTest,muted=sound.muted;float volume=AudioListener.volume;
            var background=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard=InputSystem.AddDevice<Keyboard>("F2 scene testing validation");game.automatedKeyboard=keyboard;game.automatedTest=true;
            try{
                sound.Mute(false);AudioListener.volume=0;
                type.GetMethod("Back",flags).Invoke(game,null);
                var original=game.run;
                State(keyboard,Key.F2);Check(game.SceneTestsOpen,"F2 opens on main menu");
                State(keyboard,Key.F2);Check(game.SceneTestsOpen,"holding F2 cannot toggle repeatedly");State(keyboard);
                Tap(keyboard,Key.Space);Check(ReferenceEquals(original,game.run)&&game.run.status=="ready","dialog blocks gameplay keys");
                yield return null;yield return new WaitForEndOfFrame();Shot(directory,"f2-menu");
                Tap(keyboard,Key.Escape);Check(!game.SceneTestsOpen&&!game.SceneTestActive,"Escape closes the F2 menu");
                modal.SetValue(game,"credits");Tap(keyboard,Key.F2);Tap(keyboard,Key.F2);
                Check((string)modal.GetValue(game)=="credits","F2 restores the previous foreground dialog");
                type.GetMethod("StartRun",flags).Invoke(game,null);game.run.advanceBreath(2.7);Tap(keyboard,Key.Space);
                yield return null;
                original=game.run;Tap(keyboard,Key.F2);double at=original.time,air=original.air;int score=original.score;
                Check(original.status=="paused","F2 pauses original gameplay");
                game.SelectSceneTest("surface");
                Check(game.SceneTestActive&&game.run!=original&&game.run.arriving,"surface selection begins the real rise animation");
                Check(game.run.practice&&!game.IsPreview&&sound.isRunning,"interactive test uses music clock and cannot die");
                yield return new WaitForSecondsRealtime(.38f);yield return new WaitForEndOfFrame();Shot(directory,"f2-impact");
                Check(game.run.arriving&&game.run.time>game.run.arrivalAt,"arrival animates rather than showing a frozen snapshot");
                double deadline=Time.realtimeSinceStartupAsDouble+3;
                while(game.run.arriving&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Check(game.run.breathingActive&&!game.run.arriving,"arrival leads to actual refill controls");
                double before=game.run.air;int taps=game.run.breathTaps;
                for(int i=0;i<10;i++)Tap(keyboard,Key.Space);
                Check(game.run.breathTaps==taps+10&&game.run.air>before&&game.run.air<100,"refill is interactive and retains diminishing returns");
                yield return new WaitForEndOfFrame();Shot(directory,"f2-refill");
                game.ReturnFromSceneTest();
                Check(ReferenceEquals(original,game.run)&&game.run.status=="playing","return restores the original run object and resumes");
                Check(game.run.time==at&&game.run.air==air&&game.run.score==score,"test does not alter original time, oxygen or score");
                Check(Math.Abs(sound.time-at)<.1,"restored music seeks to original game time");
                Tap(keyboard,Key.Escape);Check(original.status=="paused","original gameplay can still pause");
                Tap(keyboard,Key.F2);game.SelectSceneTest("angel");game.ReturnFromSceneTest();
                Check(original.status=="paused"&&(string)modal.GetValue(game)=="pause","a previously paused run remains paused after tests");
                // Every scene is selectable at every track/difficulty, including the leap's preceding hole.
                var song=type.GetField("song",flags);var level=type.GetField("level",flags);
                int oldSong=(int)song.GetValue(game);string oldLevel=(string)level.GetValue(game);
                try{
                    for(int s=0;s<3;s++)foreach(string l in new[]{"beginner","intermediate","expert"}){
                        song.SetValue(game,s);level.SetValue(game,l);
                        foreach(string mode in new[]{"opening","surface","leap","exit","swim-thin","swim-medium","swim-fat","low-thin","low-medium","low-fat","friends","rest","hungryGhost","angel","hungry"}){
                            game.SelectSceneTest(mode);
                            Check(game.SceneTestActive&&game.run.practice&&!game.SceneTestsOpen,"select "+s+"/"+l+"/"+mode);
                            if(mode=="surface"||mode=="leap"||mode=="exit")Check(game.run.arriving,"shared arrival: "+mode);
                            if(mode=="leap")Check(game.run.departures[game.run.departureCursor].kind=="leap","leap starts at the correct preceding refill");
                            if(mode.StartsWith("swim")||mode.StartsWith("low"))Check(game.run.form==(mode.EndsWith("fat")?2:mode.EndsWith("medium")?1:0),"requested body size");
                            if(mode.StartsWith("low"))Check(game.run.air==8,"critical oxygen");
                            if(mode=="friends"||mode=="rest"||mode=="angel"||mode=="hungryGhost")Check(game.run.outcome==mode&&(game.run.status=="won"||game.run.status=="lost"),"requested ending");
                        }
                    }
                }finally{song.SetValue(game,oldSong);level.SetValue(game,oldLevel);}
                game.SelectSceneTest("friends");yield return null;yield return new WaitForEndOfFrame();Shot(directory,"f2-ending");
                game.ReturnFromSceneTest();
                Check(ReferenceEquals(original,game.run)&&original.time==at&&original.score==score,"repeated switches preserve original progress");
                Check(Resources.Load<Shader>("Shaders/HuhuInkOutline")?.isSupported==true,"outline shader supported by actual player GPU");
                string report="PASS: "+assertions+" assertions; real F2 input, held-key suppression, modal isolation, interactive impact/refill, all 135 track/difficulty/scene selections, original progress and DSP position restored.";
                File.WriteAllText(Path.Combine(directory,"scene-tests.txt"),report);Debug.Log("HUHU_SCENE_TESTS "+report);
            }finally{
                game.ReturnFromSceneTest();type.GetMethod("Back",flags).Invoke(game,null);sound.Mute(muted);AudioListener.volume=volume;
                InputSystem.RemoveDevice(keyboard);if(originalKeyboard!=null&&originalKeyboard.added)originalKeyboard.MakeCurrent();
                game.automatedKeyboard=previousTestKeyboard;game.automatedTest=automated;InputSystem.settings.backgroundBehavior=background;
            }
        }
    }
}
#endif
