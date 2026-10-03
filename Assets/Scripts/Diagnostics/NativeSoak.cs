#if UNITY_EDITOR || GUGU_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SealGugu.Diagnostics
{
    /// <summary>
    /// Real-time, normal-pitch end-to-end playthrough. It drives the installed
    /// InputActions with timestamped virtual keyboard events, never calls judge,
    /// press, update or inhale directly, and allows Unity's real DSP clock to run.
    /// Only the test's choices are automated; no run state or PlayerPrefs is edited.
    /// </summary>
    public static class NativeSoak
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        [Serializable] public sealed class ClockSample
        {
            public double wallSeconds, audibleSongSeconds, decoderSongSeconds, differenceMs;
        }
        [Serializable] public sealed class SoakReport
        {
            public string version = GuguGame.Version, unityVersion, startedUtc, completedUtc, trackId, difficulty = "expert", status, outcome;
            public bool success, normalPitch, mutedDuringTest, pausePassed;
            public double trackDuration, nativeClipDuration, wallSeconds, audibleSeconds, songTimeAtWin, initialAir;
            public double pauseClockBefore, pauseClockAfter, pauseAirBefore, pauseAirAfter, pauseWallSeconds;
            public double maxFrameTimeMs, maxClockDifferenceMs, meanClockDifferenceMs, outputEstimateMs, maximumJudgementErrorMs, accuracy;
            public int queuedNotes, noteCount, hits, misses, perfect, good, food, totalFish, queuedRefillTaps, clockSampleCount;
            public List<ClockSample> clockSamples = new List<ClockSample>();
            public List<string> errors = new List<string>();
            public List<string> missedNoteDetails = new List<string>();
            public double oxygenDrainMultiplier, finalAir, finalSongTime, finalInputAt, finalLastPress;
            public string finalPhase, feedbackReason;
            public bool isolatedGameplayKeyboard;
            public int ignoredExternalTestInputs;
            public int backgroundFrames, disabledTestKeyboardFrames;
        }
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
        static void Require(bool condition, string message, SoakReport report)
        {
            if (condition) return;
            report.errors.Add(message); throw new InvalidOperationException("Native real-time soak: " + message);
        }
        static void QueueTap(Keyboard keyboard, Key key, double eventTime)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key), eventTime);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(), eventTime + .000001);
        }
        static void Frame(SoakReport report)
        {
            report.maxFrameTimeMs = Math.Max(report.maxFrameTimeMs, Time.unscaledDeltaTime * 1000);
        }
        public static IEnumerator Run(GuguGame game, string reportPath)
        {
            if (!game || game.run == null) throw new ArgumentException("NativeSoak needs an initialized game instance.");
            var report = new SoakReport { unityVersion = Application.unityVersion, startedUtc = DateTime.UtcNow.ToString("O"), mutedDuringTest = true };
            var sound = (GuguAudio)Field(game, "sound");
            int previousSong = (int)Field(game, "song"); string previousLevel = (string)Field(game, "level");
            bool previousPractice = (bool)Field(game, "practice"), previousBlind = (bool)Field(game, "blind"), previousMute = sound.muted;
            float previousSpeed = (float)Field(game, "speed"), previousDelay = (float)Field(game, "delay"), previousWindow = (float)Field(game, "window");
            bool previousBackground = Application.runInBackground, previousAutomated = (bool)Field(game, "automatedTest");
            var previousInputBackground = InputSystem.settings.backgroundBehavior;
            Keyboard previousKeyboard = Keyboard.current, keyboard = null;
            double began = Time.realtimeSinceStartupAsDouble, deadline = began + 270, lastClockSample = -100, lastResume = -100, clockDifferenceSum = 0;
            GuguRun run = null;
            try
            {
                Set(game, "automatedTest", true); Application.runInBackground = true;
                // Native keyboards are normally disabled on focus loss even with runInBackground.
                // This QA-only keyboard must keep receiving its scheduled events. The shell
                // filters other keyboards above, and normal focus behavior is restored below.
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                Set(game, "song", 0); Set(game, "level", "expert"); Set(game, "practice", false); Set(game, "blind", false);
                Set(game, "speed", 1f); Set(game, "delay", 0f); Set(game, "window", 150f);
                Invoke(game, "Back"); sound.Mute(true); Invoke(game, "StartRun");
                run = game.run; report.oxygenDrainMultiplier=run.oxygenDrainMultiplier; report.trackId = run.track.id; report.trackDuration = run.track.duration; report.noteCount = run.notes.Count;
                report.totalFish = run.totalFish; report.outputEstimateMs = sound.outputEstimate * 1000;
                AudioClip clip = sound.TrackClip(run.track); report.nativeClipDuration = (double)clip.samples / clip.frequency;
                AudioSource music = (AudioSource)Field(sound, "music");
                keyboard = InputSystem.AddDevice<Keyboard>("Gugu real-time DSP soak");
                game.automatedKeyboard=keyboard;game.ignoredExternalTestInputs=0;report.isolatedGameplayKeyboard=true;
                deadline = began + run.track.duration + 45;

                // Observe the actual opening animation clock and press inside its hold.
                bool openingQueued = false;
                while (run.status == "breathing")
                {
                    Frame(report); double now = Time.realtimeSinceStartupAsDouble;
                    Require(now < deadline, "timed out during opening breath", report);
                    BreathVisual breath = run.breathVisual();
                    if (!openingQueued && breath.phase >= 2.7 && breath.phase <= 3.0)
                    {
                        QueueTap(keyboard, Key.Space, now); openingQueued = true;
                    }
                    yield return null;
                }
                Require(run.status == "playing" && run.initialAir == 100, "timed opening input must begin the real song with 100% air", report);
                report.initialAir = run.initialAir; report.normalPitch = Math.Abs(music.pitch - 1) < .00001;
                Require(report.normalPitch, "real-time soak must not change music pitch or playback speed", report);
                int cursor = 0; bool pausedOnce = false, won = false;
                double surfaceSeen = -1, nextTap = double.PositiveInfinity, nextLog = 20;
                while (true)
                {
                    Frame(report); double now = Time.realtimeSinceStartupAsDouble;
                    Require(now < deadline, "song did not complete within its real-time deadline", report);
                    if(!Application.isFocused)report.backgroundFrames++;
                    if(!keyboard.enabled)report.disabledTestKeyboardFrames++;
                    Require(run.status != "lost", "real input playthrough lost: " + run.reason + ", food=" + run.food + ", misses=" + run.misses, report);
                    double audible = sound.time;
                    if (!pausedOnce && run.status == "playing" && audible >= 12)
                    {
                        QueueTap(keyboard, Key.Escape, now);
                        double waitUntil = now + 2;
                        while (run.status != "paused")
                        {
                            Require(Time.realtimeSinceStartupAsDouble < waitUntil, "Escape did not pause the actual shell", report);
                            Frame(report); yield return null;
                        }
                        report.pauseClockBefore = sound.time; report.pauseAirBefore = run.air;
                        double pauseBegan = Time.realtimeSinceStartupAsDouble;
                        while (Time.realtimeSinceStartupAsDouble - pauseBegan < .5) { Frame(report); yield return null; }
                        report.pauseWallSeconds = Time.realtimeSinceStartupAsDouble - pauseBegan;
                        report.pauseClockAfter = sound.time; report.pauseAirAfter = run.air;
                        Require(Math.Abs(report.pauseClockAfter - report.pauseClockBefore) < 1e-8 && report.pauseAirAfter == report.pauseAirBefore,
                            "music clock or oxygen advanced during real half-second pause", report);
                        report.pausePassed = true; QueueTap(keyboard, Key.Escape, Time.realtimeSinceStartupAsDouble);
                        waitUntil = Time.realtimeSinceStartupAsDouble + 2;
                        while (run.status == "paused")
                        {
                            Require(Time.realtimeSinceStartupAsDouble < waitUntil, "Escape did not resume the actual shell", report);
                            Frame(report); yield return null;
                        }
                        pausedOnce = true; lastResume = Time.realtimeSinceStartupAsDouble; continue;
                    }
                    Require(run.status != "paused", "unexpected background or focus pause during automated play", report);
                    if (run.status == "playing")
                    {
                        // Retain the musical timestamp if this frame arrives a few ms later.
                        // Dispatch remains via Unity's next real InputSystem update.
                        while (cursor < run.notes.Count && run.noteTime(run.notes[cursor]) <= audible)
                        {
                            Note note = run.notes[cursor++];
                            Require(note.result != "miss", "note expired before its queued physical input: " + note.id, report);
                            if (note.result != null) continue;
                            double stamp = now + run.noteTime(note) - audible;
                            QueueTap(keyboard, note.lane == "upper" ? Key.D : Key.J, stamp); report.queuedNotes++;
                        }
                        if (run.phase == "surface")
                        {
                            if (surfaceSeen != run.surfaceAt) { surfaceSeen = run.surfaceAt; nextTap = audible + .02; }
                            while (audible >= nextTap && run.breathRemaining > .04)
                            {
                                QueueTap(keyboard, Key.Space, now + nextTap - audible); report.queuedRefillTaps++; nextTap += .125;
                            }
                        }
                        else nextTap = double.PositiveInfinity;
                    }
                    if (run.status == "won" && !won)
                    {
                        won = true; report.songTimeAtWin = run.time;
                        Require(run.outcome == "friends" && run.food == run.totalFish && run.misses == 0,
                            "complete input route must yield friends, every fish and zero misses", report);
                        Debug.Log("GUGU_SOAK_ROUTE_COMPLETE " + run.food + "/" + run.totalFish + " fish; " + run.hits + " hits. Listening through final song tail.");
                    }
                    // Decoder position is a coarse independent sanity check. Its buffer
                    // can lead the audible clock; do not claim sample-perfect device sync.
                    if (now - lastClockSample >= .2 && now - lastResume > .3 && music.isPlaying && audible > .3 && audible < run.track.duration - .3)
                    {
                        double decoder = (double)music.timeSamples / music.clip.frequency;
                        double difference = Math.Abs(decoder - audible) * 1000;
                        report.clockSamples.Add(new ClockSample { wallSeconds = now - began, audibleSongSeconds = audible,
                            decoderSongSeconds = decoder, differenceMs = difference });
                        report.maxClockDifferenceMs = Math.Max(report.maxClockDifferenceMs, difference); clockDifferenceSum += difference;
                        lastClockSample = now;
                    }
                    if (audible >= nextLog)
                    {
                        Debug.Log("GUGU_SOAK_PROGRESS " + audible.ToString("F1") + "s; fish=" + run.food + "; air=" + run.air.ToString("F1") + "; misses=" + run.misses);
                        nextLog += 20;
                    }
                    if (won && audible >= run.track.duration) break;
                    yield return null;
                }
                report.audibleSeconds = sound.time; report.clockSampleCount = report.clockSamples.Count;
                report.meanClockDifferenceMs = clockDifferenceSum / Math.Max(1, report.clockSampleCount);
                foreach (double error in run.errors) report.maximumJudgementErrorMs = Math.Max(report.maximumJudgementErrorMs, Math.Abs(error));
                Require(report.pausePassed && report.clockSampleCount > 100, "soak needs pause evidence and independent samples throughout the song", report);
                Require(report.maxClockDifferenceMs < 200, "DSP audible clock diverged more than 200 ms from the native audio decoder", report);
                Require(run.status == "won" && run.outcome == "friends" && run.food == run.totalFish && run.misses == 0,
                    "final real-time result is not a complete perfect-fish route", report);
                report.success = true;
            }
            finally
            {
                report.wallSeconds = Time.realtimeSinceStartupAsDouble - began; report.completedUtc = DateTime.UtcNow.ToString("O");
                if (run != null)
                {
                    report.finalAir=run.air;report.finalSongTime=run.time;report.finalInputAt=run.inputAt;report.finalLastPress=run.lastPress;report.finalPhase=run.phase;report.feedbackReason=run.feedback?.reason;
                    foreach(var note in run.notes)if(note.result=="miss")report.missedNoteDetails.Add("id="+note.id+" time="+note.time+" kind="+note.kind+" lane="+note.lane);
                    report.status = run.status; report.outcome = run.outcome; report.food = run.food; report.hits = run.hits; report.misses = run.misses;
                    report.perfect = run.counts.perfect; report.good = run.counts.good; report.accuracy = run.accuracy;
                }
                if (!report.success && report.errors.Count == 0) report.errors.Add("Real-time soak was interrupted or threw before completion; see the player log for the exception.");
                report.ignoredExternalTestInputs=game.ignoredExternalTestInputs;game.automatedKeyboard=null;
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior=previousInputBackground;
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                sound.Silence(); Set(game, "song", previousSong); Set(game, "level", previousLevel);
                Set(game, "practice", previousPractice); Set(game, "blind", previousBlind);
                Set(game, "speed", previousSpeed); Set(game, "delay", previousDelay); Set(game, "window", previousWindow);
                sound.Mute(previousMute); Invoke(game, "Back");
                Set(game, "automatedTest", previousAutomated); Application.runInBackground = previousBackground;
                string fullPath = Path.GetFullPath(reportPath); Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, JsonUtility.ToJson(report, true));
                Debug.Log((report.success ? "GUGU_SOAK_SUCCESS " : "GUGU_SOAK_FAILED ") + report.food + "/" + report.totalFish +
                    " fish; " + report.wallSeconds.ToString("F1") + " real seconds; " + report.clockSamples.Count + " clock samples; max decoder/audible difference=" + report.maxClockDifferenceMs.ToString("F2") + "ms; " + fullPath);
            }
        }
    }
}
#endif
