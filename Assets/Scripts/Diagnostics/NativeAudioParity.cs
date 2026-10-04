#if UNITY_EDITOR || GUGU_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace SealGugu.Diagnostics
{
    /// <summary>
    /// Checks imported native audio and the real calibration scheduling path.
    /// Run on a ready menu instance; temporarily selects the densest expert song
    /// and restores selection/mute/menu without writing PlayerPrefs.
    /// Excluded from release players.
    /// </summary>
    public static class NativeAudioParity
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static readonly string[] RequiredEffects = {
            "dive", "breathBad", "surface", "eat", "inhale", "hungry", "button", "happy",
            "sad", "belly", "start", "breathGood", "ice", "fisher", "angel", "ghost"
        };
        static object Field(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, Members);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            return field.GetValue(target);
        }
        static void Set(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, Members);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            field.SetValue(target, value);
        }
        static void Invoke(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(name, Members);
            if (method == null) throw new MissingMethodException(target.GetType().Name, name);
            method.Invoke(target, null);
        }
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Native audio parity: " + message);
        }
        static List<double> ScheduledClicks(GuguAudio sound, AudioClip click, out int distinctSources)
        {
            var times = new List<double>(); var sources = new HashSet<AudioSource>();
            double now = AudioSettings.dspTime;
            foreach (object voice in (IEnumerable)Field(sound, "voices"))
            {
                var source = (AudioSource)Field(voice, "source");
                if (source.clip != click || (double)Field(voice, "end") < now) continue;
                times.Add((double)Field(voice, "start")); sources.Add(source);
            }
            times.Sort(); distinctSources = sources.Count; return times;
        }
        static List<string> EffectKeys(GuguAudio sound)
        {
            return ((IEnumerable)Field(sound,"voices")).Cast<object>()
                .Where(v=>(double)Field(v,"end")>=AudioSettings.dspTime)
                .Select(v=>((AudioSource)Field(v,"source")).clip.name).OrderBy(n=>n).ToList();
        }
        static int CheckEffectRoutes(GuguAudio sound,GuguRun run,StringBuilder details)
        {
            int count=0;AudioListener.volume=0;
            // Expected routes come from V13 scenes-media.js + PulseAudio/DuetAudio,
            // including the extra breathTap/inhale wiring in gugu.js.
            Action<string,GameEvent,string[]> check=(name,e,expected)=>{
                sound.StopEffects();Set(sound,"lastMistake",-100.0);sound.Event(e,run);
                var actual=EffectKeys(sound);Require(actual.SequenceEqual(expected.OrderBy(n=>n)),name+": expected "+string.Join(",",expected)+"; got "+string.Join(",",actual));count++;
                foreach(object v in (IEnumerable)Field(sound,"voices")){
                    if((double)Field(v,"end")<AudioSettings.dspTime)continue;
                    var source=(AudioSource)Field(v,"source");Require((float)Field(v,"volume")>0&&!source.mute,name+" must schedule audible non-muted audio");count++;
                }
                details.AppendLine("Route "+name+": "+string.Join(", ",actual)+".");
            };
            run.initialAir=100;check("opening >=90%",new GameEvent{type="breath",big=true},new[]{"breathGood"});
            run.initialAir=70;check("opening <90%",new GameEvent{type="breath",big=true},new[]{"breathBad"});
            check("surface inhale",new GameEvent{type="breath"},new[]{"inhale"});
            check("surface roll",new GameEvent{type="breathTap"},new[]{"button"});
            check("big inhale and dive",new GameEvent{type="bigBreath"},new[]{"inhale","dive"});
            check("water splash",new GameEvent{type="splash"},new[]{"dive"});
            check("call friends",new GameEvent{type="call"},new[]{"happy"});
            check("every ice-hole arrival",new GameEvent{type="surfaceRise"},new[]{"surface","fisher"});
            check("perfect seated inhale",new GameEvent{type="breath",result="perfect"},new[]{"inhale","breathGood"});
            check("good seated inhale",new GameEvent{type="breath",result="good"},new[]{"inhale","breathBad"});
            check("arrival hit does not duplicate collision audio",new GameEvent{type="hit",note=new Note{kind="exit"},result="perfect"},Array.Empty<string>());
            check("miss",new GameEvent{type="miss",note=new Note{kind="fish"}},new[]{"ice"});
            run.reason="oxygen";run.outcome="hungryGhost";check("hungry ghost",new GameEvent{type="lost"},new[]{"ghost"});
            run.outcome="angel";check("angel",new GameEvent{type="lost"},new[]{"angel"});
            run.reason="leap";check("missed leap",new GameEvent{type="lost"},new[]{"ice","angel"});
            run.outcome="friends";check("friends ending",new GameEvent{type="won"},new[]{"happy","belly"});
            run.outcome="rest";check("rest ending",new GameEvent{type="won"},new[]{"sad","hungry"});
            check("fish hit does not double input bite",new GameEvent{type="hit",note=new Note{kind="fish"},result="perfect"},Array.Empty<string>());
            foreach(string key in new[]{"start","hungry","button"}){
                sound.StopEffects();sound.Sample(key);
                Require(EffectKeys(sound).SequenceEqual(new[]{key}),key+" direct UI cue schedules its sample");count++;
            }
            sound.StopEffects();sound.Bite();sound.Bite();
            Require(EffectKeys(sound).SequenceEqual(new[]{"eat","eat"}),"two immediate inputs schedule two bite voices without a cooldown");count++;
            foreach(object v in (IEnumerable)Field(sound,"voices")){
                if((double)Field(v,"end")<AudioSettings.dspTime)continue;
                Require(Math.Abs((double)Field(v,"end")-(double)Field(v,"start")-.3)<.0001,"bite retains the complete 300 ms attack");count++;
                Require((float)Field(v,"volume")>.11f,"bite level is clearly above the former .045 linear level");count++;
            }
            details.AppendLine("Bite uses 300 ms and audible input gain; rapid edges are not cooled down; a fish hit adds no duplicate.");
            return count;
        }
        public static string Run(GuguGame game)
        {
            if (game == null || game.run == null || game.run.status != "ready")
                throw new ArgumentException("NativeAudioParity requires a freshly loaded ready game.");
            var sound = (GuguAudio)Field(game, "sound");
            var tracks = (Track[])Field(game, "tracks");
            int previousSong = (int)Field(game, "song");
            string previousLevel = (string)Field(game, "level");
            bool previousMute = sound.muted;
            float previousListenerVolume=AudioListener.volume;
            var details = new StringBuilder(); int assertions = 0;
            try
            {
                Require(tracks.Length == 3, "expected exactly three version-13 songs"); assertions++;
                int denseSong = -1, largest = 0;
                for (int i = 0; i < tracks.Length; i++)
                {
                    AudioClip clip = sound.TrackClip(tracks[i]);
                    Require(clip && clip.loadState == AudioDataLoadState.Loaded && clip.samples > 0 && clip.frequency > 0,
                        tracks[i].id + " must have imported and loaded PCM audio"); assertions++;
                    double duration = (double)clip.samples / clip.frequency, difference = duration - tracks[i].duration;
                    Require(Math.Abs(difference) * clip.frequency <= 1.0, tracks[i].id + " decoded duration " + duration.ToString("F6", CultureInfo.InvariantCulture) +
                        " differs from chart " + tracks[i].duration.ToString("F6", CultureInfo.InvariantCulture) + " by more than one sample (" +
                        (1000.0 / clip.frequency).ToString("F6", CultureInfo.InvariantCulture) + " ms)"); assertions++;
                    details.AppendLine(tracks[i].id + ": native " + duration.ToString("F6", CultureInfo.InvariantCulture) + " s; chart " +
                        tracks[i].duration.ToString("F6", CultureInfo.InvariantCulture) + " s; difference " + (difference * 1000).ToString("F3", CultureInfo.InvariantCulture) +
                        " ms; " + clip.frequency + " Hz; " + clip.channels + " channels.");
                    var expert = new GuguRun(tracks[i], "expert");
                    int count = expert.notes.Count(n => n.time >= 7 && n.time < 23);
                    if (count > largest) { largest = count; denseSong = i; }
                }
                foreach (string key in RequiredEffects)
                {
                    AudioClip clip = Resources.Load<AudioClip>("Audio/scenes/audio/" + key);
                    Require(clip && clip.loadState == AudioDataLoadState.Loaded && clip.samples > 0 && clip.frequency > 0,
                        "missing or unloaded team sound effect " + key); assertions++;
                    var samples=new float[Math.Min(clip.samples,clip.frequency)*clip.channels];
                    Require(clip.GetData(samples,0)&&samples.Any(s=>Math.Abs(s)>.005f),key+" contains audible PCM, not a silent placeholder");assertions++;
                }
                details.AppendLine("All 16 team sound effects loaded.");
                Require(largest > 32, "calibration fixture must exceed the original 32-voice pool"); assertions++;
                Set(game, "song", denseSong); Set(game, "level", "expert"); Invoke(game, "NewPreview");
                sound.Mute(false); Invoke(game, "StartCalibration");
                var clicks = (List<double>)Field(game, "calibrationClicks");
                var clickClip = (AudioClip)Field(sound, "click");
                double anchor = (double)Field(sound, "anchor");
                List<double> scheduled = ScheduledClicks(sound, clickClip, out int distinct);
                Require(clicks.Count == largest && scheduled.Count == clicks.Count && distinct == clicks.Count,
                    "all " + clicks.Count + " calibration clicks need independent scheduled voices; got " + scheduled.Count + " events on " + distinct + " sources"); assertions++;
                Require(AudioSettings.dspTime < scheduled[0], "inspect calibration before the first audible click begins"); assertions++;
                for (int i = 0; i < clicks.Count; i++)
                {
                    Require(Math.Abs(scheduled[i] - (anchor + clicks[i])) < 1e-7, "click " + i + " lost its chart timestamp"); assertions++;
                }
                // Interacting with the calibration UI must not steal an already queued beat.
                sound.Sample("button", .25f);
                var afterButton = ScheduledClicks(sound, clickClip, out int afterSources);
                Require(afterButton.Count == clicks.Count && afterSources == distinct,
                    "button feedback must preserve every future calibration click"); assertions++;
                for (int i = 0; i < scheduled.Count; i++)
                {
                    Require(afterButton[i] == scheduled[i], "button feedback replaced a scheduled beat"); assertions++;
                }
                details.AppendLine(tracks[denseSong].id + ": " + scheduled.Count + " distinct calibration click voices retain exact chart timestamps, including after button feedback.");
                Invoke(game, "StopCalibration");
                Set(sound, "lastHeart", 170.0); Set(sound, "lastMistake", 170.0);
                sound.Play(tracks[denseSong], 0, false);
                Require((double)Field(sound, "lastHeart") == -100 && (double)Field(sound, "lastMistake") == -100,
                    "new-song playback must reset heartbeat and mistake cooldown history"); assertions++;
                Require(!double.IsNaN(sound.outputEstimate) && sound.outputEstimate >= 0,
                    "DSP output latency estimate must be finite and nonnegative"); assertions++;
                details.AppendLine("New-song heartbeat/mistake history resets. DSP queued-output estimate: " +
                    (sound.outputEstimate * 1000).ToString("F3", CultureInfo.InvariantCulture) + " ms (device calibration may still be needed).");
                sound.Silence();sound.Mute(false);sound.Menu(tracks[0]);
                float menuGain=((AudioSource)Field(sound,"menu")).volume;
                sound.Play(tracks[0]);float playGain=((AudioSource)Field(sound,"music")).volume;
                Require(Math.Abs(menuGain-playGain)<1e-7,"menu BGM equals gameplay gain");assertions++;
                sound.Mute(true);sound.Mute(false);
                Require(Math.Abs(((AudioSource)Field(sound,"menu")).volume-playGain)<1e-7,"unmute retains matching BGM gain");assertions++;
                sound.Silence();sound.Sample("hungry",.6f);
                float oldHunger=((IEnumerable)Field(sound,"voices")).Cast<object>().Where(v=>(double)Field(v,"end")>=AudioSettings.dspTime).Sum(v=>(float)Field(v,"volume"));
                sound.StopEffects();sound.MenuHunger();
                float newHunger=((IEnumerable)Field(sound,"voices")).Cast<object>().Where(v=>(double)Field(v,"end")>=AudioSettings.dspTime).Sum(v=>(float)Field(v,"volume"));
                Require(Math.Abs(newHunger-oldHunger*.75f)<1e-6,"menu hunger gain is exactly 75 percent");assertions++;
                Require(sound.MenuHungerProgress>=0&&sound.MenuHungerProgress<.1f,"animation starts on the sound DSP clock");assertions++;
                sound.StopEffects();Require(sound.MenuHungerProgress<0,"cancelling sound cancels hunger animation");assertions++;
                details.AppendLine("Menu BGM equals gameplay gain; menu hunger gain 75%; animation shares sample DSP duration and cancellation.");
                assertions+=CheckEffectRoutes(sound,new GuguRun(tracks[0],"expert"),details);
                return "PASS: native audio integration, " + assertions + " assertions.\n" + details;
            }
            finally
            {
                Invoke(game, "StopCalibration"); sound.Silence();
                Set(game, "song", previousSong); Set(game, "level", previousLevel);
                sound.Mute(previousMute); Invoke(game, "Back");
                AudioListener.volume=previousListenerVolume;
            }
        }
    }
}
#endif
