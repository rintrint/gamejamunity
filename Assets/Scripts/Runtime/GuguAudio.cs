using System;
using System.Collections.Generic;
using UnityEngine;

namespace SealGugu
{
    /// <summary>Scheduled sample clock shared by music, input judgement and chart rendering.</summary>
    public sealed class GuguAudio : MonoBehaviour
    {
        AudioSource music, menu;
        AudioLowPassFilter lowpass;
        readonly List<Voice> voices = new List<Voice>();
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, double> cooldowns = new Dictionary<string, double>();
        AudioClip click, heart;
        public const float MusicVolume=.93f*.75f, MenuHungerVolume=.6f*.75f;
        double hungerAt=-100,hungerDuration;
        public float MenuHungerProgress => muted||hungerDuration<=0||AudioSettings.dspTime<hungerAt||AudioSettings.dspTime>=hungerAt+hungerDuration ? -1 : (float)((AudioSettings.dspTime-hungerAt)/hungerDuration);
        double anchor, position, pausedAt, duration, lastHeart = -100, lastMistake = -100;
        bool running;
        public bool muted { get; private set; }
        public bool isRunning => running;
        public double outputEstimate { get; private set; }
        public double time => running ? Math.Max(position, Math.Min(duration, AudioSettings.dspTime - anchor - outputEstimate)) : pausedAt;
        public double TimeAt(double realtime) => running ? Math.Max(position, Math.Min(duration,
            AudioSettings.dspTime - anchor - outputEstimate + realtime - Time.realtimeSinceStartupAsDouble)) : pausedAt;
        sealed class Voice { public AudioSource source; public double start, end; public float volume; }
        struct SampleInfo { public float lead, peak; public SampleInfo(float l,float p){lead=l;peak=p;} }
        static readonly Dictionary<string, SampleInfo> metadata = new Dictionary<string, SampleInfo> {
            {"dive",new SampleInfo(.118f,.7324f)},{"breathBad",new SampleInfo(.062f,.6566f)},
            {"surface",new SampleInfo(.120f,1.24f)},{"eat",new SampleInfo(.068f,1.1977f)},
            {"inhale",new SampleInfo(.082f,.2495f)},{"hungry",new SampleInfo(.033f,.374f)},
            {"button",new SampleInfo(.013f,1.109f)},{"happy",new SampleInfo(.013f,.986f)},
            {"sad",new SampleInfo(.049f,.9634f)},{"belly",new SampleInfo(.016f,.9762f)},
            {"start",new SampleInfo(.034f,.294f)},{"breathGood",new SampleInfo(.04f,1.0968f)},
            {"ice",new SampleInfo(.079f,.9491f)},{"fisher",new SampleInfo(.09f,1.2861f)},
            {"angel",new SampleInfo(.011f,1.4094f)},{"ghost",new SampleInfo(.125f,1.0526f)}
        };
        AudioSource Source(string name) { var go=new GameObject(name);go.transform.SetParent(transform);var s=go.AddComponent<AudioSource>();s.playOnAwake=false;s.spatialBlend=0;s.dopplerLevel=0;return s; }
        void Awake()
        {
            music=Source("Music · DSP clock");menu=Source("Menu music");menu.loop=true;
            lowpass=music.gameObject.AddComponent<AudioLowPassFilter>();lowpass.cutoffFrequency=18000;
            AudioSettings.GetDSPBufferSize(out int length,out int buffers);
            outputEstimate=(double)length*Math.Max(0,buffers-1)/AudioSettings.outputSampleRate;
            for(int i=0;i<32;i++)voices.Add(new Voice{source=Source("SFX "+i),end=-1});
            foreach(var pair in metadata) clips[pair.Key]=Resources.Load<AudioClip>("Audio/scenes/audio/"+pair.Key);
            click=Tone("Count in",1500,1500,.04f);heart=Tone("Quiet heartbeat",78,45,.16f);
        }
        AudioClip Tone(string name,float from,float to,float seconds)
        {
            const int rate=48000;var data=new float[(int)(seconds*rate)];double phase=0;
            for(int i=0;i<data.Length;i++){float x=(float)i/data.Length;phase+=2*Math.PI*Mathf.Lerp(from,to,x)/rate;data[i]=(float)Math.Sin(phase)*Mathf.Exp(-x*8)*Mathf.Min(1,x*40);}
            var c=AudioClip.Create(name,data.Length,1,rate,false);c.SetData(data,0);return c;
        }
        public AudioClip TrackClip(Track track)
        {
            string path=track.src.Replace("assets/","Audio/");path=path.Substring(0,path.LastIndexOf('.'));
            if(!clips.TryGetValue(path,out AudioClip clip)){clip=Resources.Load<AudioClip>(path);if(!clip)throw new InvalidOperationException("缺少音樂："+path);clips[path]=clip;}
            return clip;
        }
        public void Menu(Track track){if(running||muted)return;var clip=TrackClip(track);if(menu.isPlaying&&menu.clip==clip)return;menu.clip=clip;menu.volume=MusicVolume;menu.Play();}
        public void StopMenu(){menu.Stop();}
        public void Play(Track track,double from=0,bool countIn=true)
        {
            StopMenu();music.Stop();music.clip=TrackClip(track);duration=track.duration;position=Math.Max(0,from);pausedAt=position;
            music.timeSamples=Math.Min(music.clip.samples-1,(int)(position*music.clip.frequency));
            anchor=AudioSettings.dspTime+.10-position;running=true;music.volume=muted?0:MusicVolume;lowpass.cutoffFrequency=18000;if(from==0){lastHeart=lastMistake=-100;}
            music.PlayScheduled(anchor+position);
            if(countIn&&from==0&&track.beats.Length>1){double step=track.beats[1]-track.beats[0];for(int i=4;i>0;i--)ClickAt(track.beats[0]-step*i);}
        }
        public void Pause(){if(running)pausedAt=time;running=false;music.Stop();StopEffects();}
        public void Resume(Track track){Play(track,pausedAt,false);}
        public void Silence(){Pause();StopMenu();StopEffects();}
        public void Mute(bool value){muted=value;music.volume=value?0:MusicVolume;menu.volume=value?0:MusicVolume;foreach(var v in voices) v.source.mute=value;}
        public void Won(){music.volume=muted?0:.32f*.75f;}
        Voice Available(){foreach(var v in voices)if(v.end<AudioSettings.dspTime)return v;var extra=new Voice{source=Source("SFX "+voices.Count),end=-1};voices.Add(extra);return extra;}
        void Schedule(AudioClip clip,float volume,double at,float offset=0,float length=-1)
        {
            if(!clip||muted)return;var v=Available();v.source.clip=clip;v.source.timeSamples=Math.Min(clip.samples-1,(int)(offset*clip.frequency));
            v.start=Math.Max(AudioSettings.dspTime,at);v.end=v.start+(length<0?clip.length-offset:Math.Min(length,clip.length-offset));v.volume=volume;
            // Audio already has its attack envelope. Do not defer a scheduled onset to Update.
            v.source.volume=volume;v.source.mute=false;v.source.PlayScheduled(v.start);v.source.SetScheduledEndTime(v.end);
        }
        public void Sample(string key,float volume=.45f,double delay=0,float length=-1,bool immediateInput=false)
        {
            if(!metadata.TryGetValue(key,out SampleInfo m)||!clips.TryGetValue(key,out AudioClip clip))return;
            double at=AudioSettings.dspTime+delay;double cooldown=key=="eat"?.085:key=="button"?.06:0;
            if(!immediateInput&&cooldowns.TryGetValue(key,out double last)&&at-last<cooldown)return;cooldowns[key]=at;
            Schedule(clip,volume*Mathf.Min(2.5f,.85f/Mathf.Max(.1f,m.peak))*.75f*.8f,at,m.lead,length);
        }
        // Input feedback belongs to each physical edge, including empty beats.
        // Keep the recognizable chew transient and do not gate rapid alternation.
        public void MenuHunger()
        {
            if(muted)return;
            hungerAt=AudioSettings.dspTime;
            hungerDuration=clips["hungry"].length-metadata["hungry"].lead;
            Sample("hungry",MenuHungerVolume);
        }
        public void Bite(){Sample("eat",.28f,0,.30f,true);}
        public void ClickAt(double songTime){double at=anchor+songTime;if(at>=AudioSettings.dspTime)Schedule(click,.075f,at);}
        public void StopEffects(){hungerDuration=0;foreach(var v in voices){v.source.Stop();v.end=-1;}cooldowns.Clear();}
        public void Danger(double amount,double songTime)
        {
            lowpass.cutoffFrequency=Mathf.Lerp(lowpass.cutoffFrequency,18000-(float)amount*5000,Time.unscaledDeltaTime*5);
            if(amount>0&&songTime-lastHeart>1.1-amount*.6){lastHeart=songTime;Schedule(heart,(float)(.035+amount*.05)*.15f,AudioSettings.dspTime);}
        }
        void Update(){double now=AudioSettings.dspTime;foreach(var v in voices)if(v.end>=now){float fadeOut=Mathf.Clamp01((float)(v.end-now)/.04f);v.source.volume=v.volume*fadeOut;}}
        public void Event(GameEvent e,GuguRun g)
        {
            if(e.type=="won"||e.type=="lost")StopEffects();
            switch(e.type){
                case "breathTap":Sample("button",.22f,0,.09f);break;
                case "breath":if(e.big)Sample(g.initialAir>=90?"breathGood":"breathBad",.5f);else Sample("inhale",.35f,0,1.5f);break;
                case "bigBreath":Sample("inhale",.85f,0,1.7f);Sample("dive",.55f,.7);break;
                case "splash":Sample("dive",.5f);break;
                case "call":Sample("happy",.4f);break;
                case "hit":if(e.note.kind=="surface"||e.note.kind=="exit"){Sample("surface",.42f);Sample("fisher",.4f,.05);if(e.note.kind=="surface")Sample(e.result=="perfect"?"breathGood":"breathBad",.35f,.3);}break;
                case "miss":if(AudioSettings.dspTime-lastMistake>.16){Sample("ice",.12f,0,.16f);lastMistake=AudioSettings.dspTime;}break;
                case "lost":if(g.reason=="entry"||g.reason=="leap")Sample("ice",.4f);Sample(g.outcome=="angel"?"angel":"ghost",.5f,.12);break;
                case "won":if(g.outcome=="friends"){Sample("happy",.4f);Sample("belly",.45f,1.4);}else{Sample("sad",.3f);Sample("hungry",.5f,1.8);}break;
            }
        }
    }
}
