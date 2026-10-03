/* Regenerate against the immutable HTML v13 source, not the C# implementation. */
const fs=require('node:fs'),path=require('node:path');
const source=process.argv[2]||'C:/Users/user/Documents/GitHub/rintrint.github.io/gamejam';
const output=process.argv[3]||path.join(__dirname,'core-golden.json');
const {GuguRun}=require(path.join(source,'gugu-core.js'));
const {tracks}=require(path.join(source,'assets/floe/chart.json'));
const suite={sourceCommit:'1665a124f2a60c380989dd53af80a943386e2d7e',scenarios:[]};
function snapshot(g,from){
 const e=g.feedback||{},b=g.breathVisual();
 return {
  numbers:[g.time,g.air,g.initialAir||0,g.depth,g.window,g.delayMs,g.windowMs,g.surfaceAt,g.stability,g.inputAt,g.lastPress,g.breathElapsed,g.lastBreathTap,g.missedGateAt,g.food,g.totalFish,g.cursor,g.combo,g.maxCombo,g.hits,g.misses,g.score,g.overpresses,g.departureCursor,g.breathTaps,...g.stageFood,...g.stageTotals,g.targets.grow,g.targets.fat,g.targets.full,g.form,g.growth,g.progress,g.danger,g.accuracy,g.breathRemaining,b.phase||0,b.fill,b.ringScale,b.mist,b.expansion,b.pulse||0,g.events.length,g.errors.length,g.counts.perfect,g.counts.good,g.counts.miss],
  text:[g.status,g.phase,g.beforePause||'',g.outcome||'',g.reason||'',g.inputLane,e.type||'',e.result||'',e.reason||''],
  flags:[g.leaped,g.called,g.openingBreath,g.breathingActive,b.inhaling,b.ready],
  noteResults:g.notes.map(n=>n.result==='hit'?'H':n.result==='miss'?'M':'.').join(''),
  events:g.events.slice(from).map(e=>({type:e.type||'',result:e.result||'',reason:e.reason||'',lane:e.lane||'',noteId:e.note?.id??-1,noteKind:e.note?.kind||'',noteLane:e.note?.lane||'',at:e.at||0,error:e.error||0,food:e.food||0,growth:e.growth||0,air:e.air||0,gain:e.gain||0,taps:e.taps||0,big:!!e.big,automatic:!!e.automatic}))
 };
}
function scenario(name,track,level='expert',practice=false,settings={}){
 const s={name,trackId:track.id,level,practice,delay:settings.delay||0,window:settings.window??150,steps:[]};
 const g=new GuguRun(track,level,practice,settings);suite.scenarios.push(s);
 function act(op,value=0,lane='',flag=false){
  const from=g.events.length;
  if(op==='start')g.startBreath();
  else if(op==='advance')g.advanceBreath(value);
  else if(op==='inhale')g.inhale();
  else if(op==='tap')g.tapBreath();
  else if(op==='pause')g.pause();
  else if(op==='resume')g.resume();
  else if(op==='update')g.update(value);
  else if(op==='press')g.press(lane,value);
  else if(op==='food')g.food=value;
  else if(op==='lose')g.lose(lane);
  else if(op==='finish'){g.phase='shore';g.leaped=true;const n=g.notes.find(n=>n.kind==='call');g.time=n.time;g.judge(n,true);}
  else if(op==='note'){
   const n=g.notes.find(n=>n.id===value),at=g.noteTime(n),fps=Number(lane);
   for(let t=g.time+1/fps;t<at;t+=1/fps)g.update(t);
   g.press(n.lane,at);
  }
  else if(op==='refill'){for(let i=0;i<value&&g.phase==='surface';i++)g.press('lower',g.time+.03);}
  s.steps.push({op,value,lane,flag,expected:snapshot(g,from)});
 }
 return {g,act};
}
function launch(act,t=2.7){act('start');act('advance',t);act('inhale');}
for(const track of tracks)for(const level of ['beginner','intermediate','expert'])for(const fps of [30,144]){
 const {g,act}=scenario(`${track.id}/${level}/perfect/${fps}fps`,track,level);launch(act);
 for(const n of g.notes){if(g.phase==='surface')act('refill',20);act('note',n.id,String(fps));}
 if(g.status!=='won'||g.food!==g.totalFish)throw Error('Baseline perfect replay failed '+track.id);
}
for(const [ti,track] of tracks.entries())for(const [li,level] of ['beginner','intermediate','expert'].entries()){
 const {g,act}=scenario(`${track.id}/${level}/mixed-practice`,track,level,true,{delay:[-200,0,200][ti],window:[40,95,200][li]});launch(act,1.35);
 let index=0;
 for(const n of g.notes){
  if(g.phase==='surface')act('refill',7+index%20);
  const at=g.noteTime(n),mode=index++%9;
  if(n.kind!=='fish'||mode<3)act('press',at,n.lane);
  else if(mode===3)act('press',at-g.window*.8,n.lane);
  else if(mode===4)act('press',at+g.window*.75,n.lane);
  else if(mode===5)act('press',at,n.lane==='upper'?'lower':'upper');
  else if(mode===6)act('press',at-g.approachWindow+.0001,n.lane);
  else act('update',at+g.window+.031);
 }
}
for(const track of tracks){
 const {g,act}=scenario(`${track.id}/mash-loses`,track);launch(act);
 for(let t=track.introEnd;g.status==='playing'&&t<40;t+=.05){act('press',t,'upper');act('press',t,'lower');}
 if(g.status!=='lost'||g.reason!=='rhythm')throw Error('Baseline mash should lose');
}
for(const window of [40,150,200])for(const delta of [-.000001,0,.000001])for(const sign of [-1,1]){
 const {g,act}=scenario(`boundary/${window}/${sign}/${delta}`,tracks[0],'expert',true,{delay:55,window});launch(act);
 const n=g.notes.find(n=>n.kind==='fish');act('press',g.noteTime(n)+sign*(window/1000+delta),n.lane);
 act('press',g.noteTime(n)+Math.max(0,sign*(window/1000+delta)),n.lane);
}
{
 const {g,act}=scenario('opening-cycle-and-pause',tracks[0]);
 act('start');act('advance',2.43);act('pause');act('advance',10);act('tap');act('inhale');act('resume');act('advance',.27);act('advance',.9);act('advance',2.7);act('inhale');
 for(const n of g.notes){act('press',g.noteTime(n),n.lane);if(n.kind==='surface')break;}
 act('pause');act('tap');act('update',g.time+1);act('resume');act('refill',1000);
 act('update',g.noteTime(g.departures[g.departureCursor]));act('tap');
}
for(const track of tracks)for(const delta of [-1,0]){
 const loss=scenario(`${track.id}/ending-loss/${delta}`,track);launch(loss.act);loss.act('food',loss.g.targets.fat+delta);loss.act('lose',0,'oxygen');
 const win=scenario(`${track.id}/ending-win/${delta}`,track);launch(win.act);win.act('food',win.g.targets.full+delta);win.act('finish');
}
for(const track of tracks){
 const {g,act}=scenario(`${track.id}/no-input`,track);launch(act,.6);act('update',track.duration);
}
fs.writeFileSync(output,JSON.stringify(suite));
console.log(JSON.stringify({scenarios:suite.scenarios.length,checkpoints:suite.scenarios.reduce((n,s)=>n+s.steps.length,0),output,bytes:fs.statSync(output).size}));
