#if UNITY_EDITOR || GUGU_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SealGugu.Diagnostics
{
    public static class PresentationAudit
    {
        [Serializable] sealed class Report {
            public string version=GuguGame.Version,font;
            public bool success,hungerReturnedToNormal;
            public int assertions;
            public List<string> captures=new List<string>();
            public List<int> hungerFrames=new List<int>();
        }
        static Report report;
        static void Check(bool value,string message){report.assertions++;if(!value)throw new Exception("Presentation audit: "+message);}
        static T Field<T>(GuguGame game,string name){return (T)typeof(GuguGame).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);}
        static void Capture(string directory,string name,bool checkEdges=false){
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            if(checkEdges){
                int dark=0,count=0;
                for(int x=2;x<texture.width-2;x+=16)foreach(int y in new[]{2,texture.height-3}){Color p=texture.GetPixel(x,y);if(p.maxColorComponent<.12f)dark++;count++;}
                for(int y=2;y<texture.height-2;y+=16)foreach(int x in new[]{2,texture.width-3}){Color p=texture.GetPixel(x,y);if(p.maxColorComponent<.12f)dark++;count++;}
                Check(dark==0,"no dark letterbox edges: "+name+" "+dark+"/"+count);
            }
            File.WriteAllBytes(Path.Combine(directory,name+".png"),texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);report.captures.Add(name+".png");
        }
        public static IEnumerator Run(GuguGame game,string directory){
            report=new Report{font=GuguTypography.Family};
            var sound=Field<GuguAudio>(game,"sound");var view=Field<GuguRenderer>(game,"view");bool muted=sound.muted;float listener=AudioListener.volume;
            try {
                Check(report.font=="Microsoft JhengHei"||report.font=="Microsoft JhengHei UI","Microsoft JhengHei selected from installed system fonts");
                foreach(var resolution in new[]{new Vector2Int(1280,720),new Vector2Int(1280,800),new Vector2Int(1680,720)}){
                    Screen.SetResolution(resolution.x,resolution.y,FullScreenMode.Windowed);
                    for(int i=0;i<60&&(Screen.width!=resolution.x||Screen.height!=resolution.y);i++)yield return null;
                    Check(Screen.width==resolution.x&&Screen.height==resolution.y,"requested screenshot size");
                    foreach(string scene in new[]{"menu","swim-fat","opening-full","friends"}){
                        game.PreviewScene(scene);yield return null;yield return new WaitForEndOfFrame();
                        Capture(directory,"aspect-"+resolution.x+"x"+resolution.y+"-"+scene,true);
                    }
                }
                Screen.SetResolution(1280,720,FullScreenMode.Windowed);
                for(int i=0;i<60&&(Screen.width!=1280||Screen.height!=720);i++)yield return null;
                var gif=Resources.Load<Texture2D>("Art/v15-1/hunger-gif");
                var bg=Resources.Load<Texture2D>("Art/v15-1/menu-background");
                Check(gif&&gif.width==2475&&gif.height==464&&gif.GetPixel(0,0).a==0,"original GIF frames and transparency imported without resizing");
                Check(bg&&bg.width==1794&&bg.height==1010,"artist's clean background imported at its original dimensions");
                foreach(double elapsed in new[]{0.0,.079,.08,.159,.16,.239,.24,.32,.40})
                    Check(view.MenuFrameAt(elapsed)==(int)Math.Floor((elapsed+1e-9)/.08)%3,"GIF keeps 80 ms timing at "+elapsed);
                game.PreviewScene("menu");sound.Mute(false);AudioListener.volume=0;sound.MenuHunger();
                double deadline=Time.realtimeSinceStartupAsDouble+15;
                while(sound.MenuHungerElapsed>=0){
                    Check(Time.realtimeSinceStartupAsDouble<deadline,"hunger audio must end");
                    yield return null;yield return new WaitForEndOfFrame();
                    int frame=view.MenuHungerFrame;
                    if(frame>=0&&!report.hungerFrames.Contains(frame)){
                        report.hungerFrames.Add(frame);Capture(directory,"hunger-gif-"+frame);
                    }
                }
                yield return null;yield return new WaitForEndOfFrame();Capture(directory,"hunger-idle");
                Check(report.hungerFrames.Contains(0)&&report.hungerFrames.Contains(1)&&report.hungerFrames.Contains(2),"all three supplied poses appear while the actual audio is playing");
                Check(view.MenuHungerFrame==-1&&sound.MenuHungerElapsed<0,"hunger ends in relaxed idle");report.hungerReturnedToNormal=true;
                game.PreviewScene("opening");yield return null;yield return new WaitForEndOfFrame();float before=view.SealWidth;
                game.PreviewScene("opening-full");yield return null;yield return new WaitForEndOfFrame();
                Check(view.SealWidth>before*1.06f&&view.BellyScale>1.25f,"full inhale enlarges whole body a little and belly more");
                report.success=true;
            } finally {
                sound.StopEffects();sound.Mute(muted);AudioListener.volume=listener;
                File.WriteAllText(Path.Combine(directory,"presentation.json"),JsonUtility.ToJson(report,true));
            }
            Debug.Log("GUGU_PRESENTATION PASS: "+report.assertions+" assertions; "+report.captures.Count+" extra captures; Microsoft JhengHei, borderless aspect fill and synchronized hunger.");
        }
    }
}
#endif
