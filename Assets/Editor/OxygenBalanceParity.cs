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
            public int assertions, rateCases, perfectRoutes, surfaceCases;
            public string note="Original V13 golden uses 1x for historical parity; release default is 0.5x. Difficulty oxygen multipliers are applied on top of the 0.5 base; Surface refill now consumes oxygen at depth-zero rate while retaining 12% refill, opening/intro grace and pause behavior. Legacy HTML fixtures explicitly keep the old surface exemption.";
        }
        static Report report;
        static void Require(bool value,string message){report.assertions++;if(!value)throw new InvalidOperationException("Oxygen balance: "+message);}
        static GuguRun Ready(Track track,string level,double multiplier) {
            var run=new GuguRun(track,level,false,new RunSettings{oxygenDrainMultiplier=multiplier});
            run.startBreath();run.advanceBreath(2.7);run.inhale();return run;
        }
        static Track SurfaceTrack() {
            var notes=new[]{new Note{kind="surface",time=2},new Note{kind="leap",time=6},new Note{kind="exit",time=8},new Note{kind="call",time=9}};
            return new Track{id="surface-regression",duration=10,introEnd=0,charts=new Charts{beginner=notes,intermediate=notes,expert=notes}};
        }
        static GuguRun SurfaceReady(string level,double delay=0,bool practice=false,bool legacy=false) {
            var g=new GuguRun(SurfaceTrack(),level,practice,new RunSettings{delay=delay,legacyBalance=legacy});
            g.startBreath();g.advanceBreath(2.7);g.inhale();g.press("upper",2+delay/1000);
            Require(g.phase=="surface"&&g.status=="playing","enter refill through the real gate input");
            g.air=40;return g;
        }
        static void VerifySurfaceBreathing() {
            foreach(string level in new[]{"beginner","intermediate","expert"}) {
                // Independent expected rates in percentage points per second.
                double rate=level=="beginner"?1.14125:level=="intermediate"?1.55625:2.075;
                foreach(int fps in new[]{30,60,144})foreach(double delay in new[]{-200.0,0,200}) {
                    var idle=SurfaceReady(level,delay);double start=idle.time;
                    for(int frame=1;frame<=2*fps;frame++)idle.update(start+(double)frame/fps);
                    Require(idle.phase=="surface"&&Math.Abs(idle.air-(40-2*rate))<1e-8,"idle surface drain independent of FPS/calibration");
                    var tapped=SurfaceReady(level,delay);double expected=40;int frameIndex=1;
                    for(int tap=1;tap<=8;tap++) {
                        double at=start+tap*.25;
                        while(start+(double)frameIndex/fps<at){tapped.update(start+(double)frameIndex/fps);frameIndex++;}
                        double before=expected-rate*.25;expected=before+(100-before)*.12;
                        tapped.press(tap%2==0?"upper":"lower",at);
                        Require(Math.Abs(tapped.air-expected)<1e-8,"timestamped taps pay elapsed drain before nonlinear refill");
                        Require(tapped.breathTaps==tap&&tapped.air<100,"one refill per tap, never full");
                    }
                    tapped.update(start+2.5);expected-=rate*.5;
                    Require(Math.Abs(tapped.air-expected)<1e-8,"air declines immediately after tapping stops");
                    tapped.pause();double paused=tapped.time,air=tapped.air;tapped.update(paused+50);tapped.press("lower",paused+50);
                    Require(tapped.time==paused&&tapped.air==air&&tapped.breathTaps==8,"pause freezes drain and rejects refill input");
                    tapped.resume();tapped.update(paused+.2);
                    Require(Math.Abs(tapped.air-(air-rate*.2))<1e-8,"resume only charges active song time");
                    var boundary=SurfaceReady(level,delay);double departure=6+delay/1000;
                    boundary.update(departure+.125);
                    double underwaterRate=(4.15+12*.012)*.5*(level=="beginner"?.55:level=="intermediate"?.75:1);
                    Require(boundary.phase=="underwater"&&boundary.leaped&&Math.Abs(boundary.air-(40-4*rate-.125*underwaterRate))<1e-8,"frame crossing auto-leap charges both intervals exactly once");
                    Require(boundary.events.FindAll(e=>e.type=="bigBreath").Count==1,"one automatic departure event");
                    report.surfaceCases++;
                }
                var dying=SurfaceReady(level);dying.air=.01;dying.press("lower",dying.time+.1);
                Require(dying.status=="lost"&&dying.reason=="oxygen"&&dying.air==0&&dying.breathTaps==0,"late refill cannot revive exhausted oxygen");
                var practice=SurfaceReady(level,0,true);practice.air=.01;practice.update(practice.time+.1);
                Require(practice.status=="playing"&&practice.air==1,"practice retains its nonfatal oxygen floor");
                practice.press("lower",practice.time+.1);Require(practice.air>1&&practice.breathTaps==1,"practice can still refill");
                var shore=SurfaceReady(level);shore.press("upper",8);double shoreAir=shore.air;shore.update(8.5);
                Require(shore.phase=="shore"&&shore.air==shoreAir,"final shore call is not a refill stop and remains safe");
            }
            var legacyRun=SurfaceReady("expert",0,false,true);legacyRun.update(4);
            Require(legacyRun.air==40,"explicit historical HTML balance preserves original surface behavior");
            var opening=new GuguRun(SurfaceTrack());opening.startBreath();opening.advanceBreath(2.7);opening.update(10);
            Require(opening.status=="breathing"&&opening.time==0&&opening.air==100,"opening timed inhale unaffected");
        }
        [UnityEditor.MenuItem("Tools/海豹呼呼/驗證半速氧氣消耗")]
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
                    Require(Math.Abs(100-current.air-(4.15+depth*.012)*current.balance.oxygen)<1e-8,"two-second independent rate check");report.rateCases++;
                }
                normal=Ready(track,level,.5);normal.consume(track.introEnd*.5);Require(normal.air==100,"intro remains free");
                normal.consume(track.introEnd+1);Require(Math.Abs(normal.air-(100-(4.15+18*.012)*.5*normal.balance.oxygen))<1e-8,"intro boundary charges only underwater elapsed time");
                normal.phase="surface";normal.surfaceAt=normal.time;normal.depth=0;normal.air=40;double at=normal.time;
                normal.consume(at+.1);double depleted=40-.1*4.15*.5*normal.balance.oxygen;
                Require(Math.Abs(normal.air-depleted)<1e-9,"surface keeps depth-zero oxygen drain");normal.tapBreath();Require(Math.Abs(normal.air-(depleted+(100-depleted)*.12))<1e-9,"refill remains 12 percent of missing air after elapsed drain");
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
            VerifySurfaceBreathing();
            report.success=true;File.WriteAllText("Validation/oxygen-balance.json",JsonUtility.ToJson(report,true));
            Debug.Log("GUGU_OXYGEN_BALANCE PASS: "+report.assertions+" assertions; "+report.rateCases+" song/difficulty/depth/frame-rate cases; "+report.perfectRoutes+" full routes; "+report.surfaceCases+" surface timing cases; 50% base consumption.");
        }
    }
}
