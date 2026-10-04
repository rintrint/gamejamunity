using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SealGugu.Editor
{
    public static class DifficultyBalanceParity
    {
        [Serializable] sealed class Row { public string track,difficulty;public int total,grow,fat,full;public double oxygen,missPenalty,perfectRecovery,goodRecovery; }
        [Serializable] sealed class Report { public string version=GuguGame.Version;public bool success;public int assertions,imperfectRoutes;public List<Row> rows=new List<Row>(); }
        static Report report;
        static void Check(bool result,string message){report.assertions++;if(!result)throw new Exception("Difficulty balance: "+message);}
        static GuguRun Ready(Track track,string level) { var g=new GuguRun(track,level);g.startBreath();g.advanceBreath(2.7);g.inhale();return g; }
        public static void Verify() {
            report=new Report();var charts=JsonUtility.FromJson<ChartDocument>(Resources.Load<TextAsset>("Data/chart").text);
            foreach(var track in charts.tracks)foreach(string level in new[]{"beginner","intermediate","expert"}){
                var g=Ready(track,level);int old=GuguRun.foodTargets(g.totalFish).full;
                Check(g.targets.grow%10==0&&g.targets.fat%10==0&&g.targets.full%10==0,"whole ten targets");
                Check(g.targets.grow<g.targets.fat&&g.targets.fat<g.targets.full&&g.targets.full<=g.totalFish,"ordered growth targets");
                Check(level=="expert"?g.targets.full==old:g.targets.full<old,"beginner/intermediate easier, expert unchanged");
                report.rows.Add(new Row{track=track.id,difficulty=level,total=g.totalFish,grow=g.targets.grow,fat=g.targets.fat,full=g.targets.full,oxygen=g.balance.oxygen,missPenalty=g.balance.missPenalty,perfectRecovery=g.balance.perfectRecovery,goodRecovery=g.balance.goodRecovery});
                g.judge(new Note{kind="fish"},false);Check(g.stability==100-g.balance.missPenalty,"real Miss penalty");
                g.judge(new Note{kind="fish"},true);Check(Math.Abs(g.stability-(100-g.balance.missPenalty+g.balance.perfectRecovery))<1e-8,"real Perfect recovery");
                g.judge(new Note{kind="fish"},true,.10);Check(g.stability<=100&&g.stability>100-g.balance.missPenalty,"Good recovers stability");
                var death=Ready(track,level);int misses=(int)Math.Ceiling(100/g.balance.missPenalty);
                for(int i=0;i<misses-1;i++)death.judge(new Note{kind="fish"},false);
                Check(death.status=="playing","survive intended consecutive Miss allowance");death.judge(new Note{kind="fish"},false);
                Check(death.status=="lost"&&death.reason=="rhythm","continued misses still lose");
                foreach(int delta in new[]{-1,0}){
                    var end=Ready(track,level);end.food=end.targets.full+delta;end.phase="shore";end.leaped=true;end.judge(new Note{kind="call"},true);
                    Check(end.outcome==(delta==0?"friends":"rest"),"food success boundary");
                    var lost=Ready(track,level);lost.food=lost.targets.fat+delta;lost.lose("oxygen");Check(lost.outcome==(delta==0?"angel":"hungryGhost"),"death form boundary");
                }
                if(level!="expert"){
                    var route=Ready(track,level);int fishIndex=0,skip=level=="beginner"?3:4;
                    foreach(var note in route.notes){
                        // Refill only once the shared landing animation has completed.
                        double at=route.noteTime(note);for(double t=route.time+1.0/60;t<at;t+=1.0/60){route.update(t);if(route.breathingActive&&route.time-route.lastBreathTap>=.125)route.press("lower",route.time);}
                        if(note.kind=="fish"&&++fishIndex%skip==0)continue;
                        route.press(note.lane,at);
                    }
                    Check(route.status=="won"&&route.outcome=="friends"&&route.misses>0,"imperfect route can still finish fed: "+track.id+level);
                    report.imperfectRoutes++;
                }
            }
            var fish=new[]{new Note{kind="fish",time=2,stage=0}};var probe=new Track{id="early-input",introEnd=0,duration=10,charts=new Charts{beginner=fish,intermediate=fish,expert=fish}};
            foreach(string level in new[]{"beginner","intermediate","expert"}){
                var g=Ready(probe,level);string lane=g.notes[0].lane;g.press(lane,1.8);g.press(lane,2);
                Check(g.misses==1&&g.hits==0&&g.food==0&&g.overpresses==1,"early mash cannot reclaim missed fish");
            }
            foreach(double percent in new[]{0,.1,12.3,99.999999999,100})Check(!GuguRun.airDisplay(percent).Contains("."),"integer UI percent");
            Check(GuguRun.airDisplay(99.999999999)=="99","nonlinear refill must not show false 100");
            Check(Math.Abs(GuguRenderer.InhaleScale(1)-1.075f)<.00001&&GuguRenderer.InhaleScale(0)==1,"uniform opening growth");
            Check(GuguRenderer.HungerFrame(-1)==0&&GuguRenderer.HungerFrame(.45f)==3&&GuguRenderer.HungerFrame(1)==0,"hunger animation returns to normal");
            report.success=true;File.WriteAllText("Validation/difficulty-balance.json",JsonUtility.ToJson(report,true));
            Debug.Log("GUGU_DIFFICULTY_BALANCE PASS: "+report.assertions+" assertions; "+report.imperfectRoutes+" imperfect full routes.");
        }
    }
}
