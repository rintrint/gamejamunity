using System;
using System.Linq;

namespace SealGugu
{
    public static class ShoreSequenceChecks
    {
        static int assertions,stops;
        static void Check(bool value,string why){assertions++;if(!value)throw new Exception("Shore sequence: "+why);}
        public static string Run(ChartDocument charts)
        {
            assertions=stops=0;
            foreach(var track in charts.tracks)foreach(string level in new[]{"beginner","intermediate","expert"})
            foreach(int fps in new[]{30,60,144})foreach(double delay in new[]{-200.0,0,200})
            {
                var indices=track.charts.Get(level).Select((n,i)=>new {n,i}).Where(x=>x.n.kind=="surface"||x.n.kind=="exit");
                foreach(var candidate in indices)
                {
                    var g=new GuguRun(track,level,false,new RunSettings{delay=delay});
                    g.startBreath();g.advanceBreath(2.7);g.inhale();g.events.Clear();
                    var gate=g.notes.First(n=>n.id==candidate.i);double at=g.noteTime(gate);
                    g.cursor=g.notes.IndexOf(gate);g.time=at-.001;g.departureCursor=g.departures.Count(n=>g.noteTime(n)<at);g.air=90;
                    g.press("upper",at);
                    Check(gate.result=="hit"&&g.arriving,"all gates use arrival after a real timing hit");
                    Check(!g.breathingActive,"inhale must not start before the seal takes the seat");
                    double air=g.air,rate=4.15*(level=="expert"?1:.5);
                    g.press("lower",at+.1);
                    Check(g.breathTaps==0,"early mash cannot refill during the collision");
                    if(gate.kind=="surface")Check(Math.Abs(g.air-(air-.1*rate))<1e-7,"oxygen drains during the collision");
                    for(int i=1;(double)i/fps<GuguRun.SURFACE_ARRIVAL_SECONDS;i++)g.update(at+(double)i/fps);
                    double landing=at+GuguRun.SURFACE_ARRIVAL_SECONDS;
                    g.update(landing);
                    Check(!g.arriving,"identical landing deadline at every frame rate");
                    Check(g.events.Count(e=>e.type=="surfaceRise")==1,"one collision per gate");
                    if(gate.kind=="surface"){
                        Check(g.breathingActive,"refill begins after landing");
                        Check(g.events.Count(e=>e.type=="breath")==1,"one inhalation after landing");
                        air=g.air;g.press("lower",landing);
                        Check(g.breathTaps==1&&Math.Abs(g.air-(air+(100-air)*.12))<1e-7,"first seated press uses nonlinear refill");
                        g.pause();air=g.air;g.update(landing+1);g.press("lower",landing+1);
                        Check(g.time==landing&&g.air==air,"pause freezes the shared sequence");g.resume();
                        g.update(landing+.1);Check(g.events.Count(e=>e.type=="breath")==1,"resume cannot replay inhalation");
                    }
                    stops++;
                }
                var balance=DifficultyBalance.For(level);
                Check(balance.oxygen==(level=="expert"?1:.5),"HTML expert / half-rate easy and intermediate");
            }
            foreach(var track in charts.tracks)foreach(string level in new[]{"beginner","intermediate","expert"})
            foreach(int boundary in new[]{-1,0}){
                var g=new GuguRun(track,level);g.status="playing";g.food=g.targets.fat+boundary;
                var hole=g.notes.First(n=>n.kind=="surface");g.time=g.noteTime(hole);g.phase="underwater";g.air=.01;g.judge(hole,true);
                g.cursor=g.notes.Count;g.departureCursor=g.departures.Count(n=>g.noteTime(n)<=g.time);
                g.update(g.time+.1);
                Check(g.status=="lost"&&!g.arriving&&!g.breathingActive,"oxygen death during collision must immediately select the ending");
                Check(!g.events.Any(e=>e.type=="breath"),"no refill cue after dying before landing");
                Check(g.outcome==(boundary<0?"hungryGhost":"angel"),"death ending uses fish growth, not song progress");
                g=new GuguRun(track,level);g.status="playing";g.phase="shore";g.leaped=true;g.food=g.targets.full+boundary;g.judge(new Note{kind="call"},true);
                Check(g.outcome==(boundary<0?"rest":"friends"),"safe arrival separates resting from friends");
            }
            return "PASS: "+stops+" actual chart stops at 30/60/144 FPS and -200/0/+200 ms; "+assertions+" assertions; all four ending boundaries.";
        }
    }
}
