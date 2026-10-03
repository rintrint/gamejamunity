using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SealGugu.Editor
{
    /// <summary>V14.1.1 intentionally differs from HTML oxygen consumption; preserve the historical golden separately.</summary>
    public static class OxygenBalanceParity
    {
        [Serializable] sealed class Report {
            public string version=GuguGame.Version;
            public bool success;
            public double runtimeMultiplier=GuguRun.OXYGEN_DRAIN_MULTIPLIER;
            public int assertions, rateCases, perfectRoutes;
            public string note="Original V13 golden uses 1x for historical parity; release default is 0.5x. Only time/depth consumption changes; refill, grace, pause, judgement and stability do not.";
        }
        static Report report;
        static void Require(bool value,string message){report.assertions++;if(!value)throw new InvalidOperationException("Oxygen balance: "+message);}
        static GuguRun Ready(Track track,string level,double multiplier) {
            var run=new GuguRun(track,level,false,new RunSettings{oxygenDrainMultiplier=multiplier});
            run.startBreath();run.advanceBreath(2.7);run.inhale();return run;
        }
        [UnityEditor.MenuItem("Tools/海豹咕咕/驗證半速氧氣消耗")]
        public static void Verify() {
            report=new Report();
            var charts=JsonUtility.FromJson<ChartDocument>(Resources.Load<TextAsset>("Data/chart").text);
            foreach(var track in charts.tracks)foreach(string level in new[]{"beginner","intermediate","expert"}) {
                var normal=new GuguRun(track,level);Require(normal.oxygenDrainMultiplier==.5,"default must be half speed for "+track.id+level);
                foreach(double depth in new[]{0.0,18,60})foreach(int fps in new[]{30,60,144}) {
                    var old=Ready(track,level,1);var current=Ready(track,level,.5);
                    old.time=current.time=track.introEnd;old.depth=current.depth=depth;
                    for(int frame=1;frame<=fps*2;frame++){double t=track.introEnd+(double)frame/fps;old.consume(t);current.consume(t);}
                    Require(Math.Abs((100-current.air)*2-(100-old.air))<1e-8,"exact half drain at every depth/frame rate");
                    Require(Math.Abs(100-current.air-(4.15+depth*.012))<1e-8,"two-second independent rate check");report.rateCases++;
                }
                normal=Ready(track,level,.5);normal.consume(track.introEnd*.5);Require(normal.air==100,"intro remains free");
                normal.consume(track.introEnd+1);Require(Math.Abs(normal.air-(100-(4.15+18*.012)*.5))<1e-8,"intro boundary charges only underwater elapsed time");
                normal.phase="surface";normal.surfaceAt=normal.time;normal.air=40;double at=normal.time;
                normal.consume(at+.1);Require(normal.air==40,"no shore drain");normal.tapBreath();Require(Math.Abs(normal.air-47.2)<1e-9,"refill remains 12 percent of missing air");
                normal.pause();at=normal.time;double air=normal.air;normal.update(at+20);Require(normal.time==at&&normal.air==air,"pause freezes clock and oxygen");
            }
            foreach(var track in charts.tracks)foreach(string level in new[]{"beginner","intermediate","expert"})foreach(int fps in new[]{30,144}) {
                var run=Ready(track,level,GuguRun.OXYGEN_DRAIN_MULTIPLIER);
                foreach(var note in run.notes){
                    if(run.phase=="surface")for(int i=0;i<20&&run.phase=="surface";i++)run.press("lower",run.time+.03);
                    double at=run.noteTime(note);
                    for(double time=run.time+1.0/fps;time<at;time+=1.0/fps)run.update(time);
                    run.press(note.lane,at);
                }
                Require(run.status=="won"&&run.outcome=="friends"&&run.food==run.totalFish&&run.misses==0,"new-default perfect route "+track.id+level+fps);
                report.perfectRoutes++;
            }
            var empty=new Track{id="oxygen-boundary",duration=200,introEnd=0,charts=new Charts{expert=Array.Empty<Note>()}};
            var fast=Ready(empty,"expert",1);var slow=Ready(empty,"expert",.5);
            double oldLimit=100/(4.15+18*.012);fast.consume(oldLimit+.00001);slow.consume(oldLimit+.00001);
            Require(fast.status=="lost"&&fast.reason=="oxygen","old exhaustion boundary");
            Require(slow.status=="playing"&&Math.Abs(slow.air-50)<.001,"half-speed run still has half its oxygen at old limit");
            slow.consume(oldLimit*2+.00001);Require(slow.status=="lost"&&slow.reason=="oxygen","oxygen still kills at doubled elapsed time");
            report.success=true;File.WriteAllText("Validation/oxygen-balance.json",JsonUtility.ToJson(report,true));
            Debug.Log("GUGU_OXYGEN_BALANCE PASS: "+report.assertions+" assertions; "+report.rateCases+" song/difficulty/depth/frame-rate cases; "+report.perfectRoutes+" full routes; 50% consumption.");
        }
    }
}
