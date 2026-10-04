using System;
using System.Collections.Generic;
using System.Globalization;

namespace SealGugu
{
    // Native deterministic port of the shipped HTML edition 13 (1665a124).
    // Time is audible song time in seconds. Rendering and animation never gate input.
    [Serializable] public sealed class ChartDocument { public int version; public Track[] tracks; }
    [Serializable] public sealed class Track
    {
        public string id, title, src;
        public double duration, bpm, introEnd, beatOffset;
        public double[] beats, stageBounds;
        public Charts charts;
    }
    [Serializable] public sealed class Charts
    {
        public Note[] beginner, intermediate, expert;
        public Note[] Get(string level)
        {
            if (level == "beginner") return beginner;
            if (level == "intermediate") return intermediate;
            return expert;
        }
    }
    [Serializable] public sealed class Note
    {
        public double time, depth;
        public int step, stage, fish, id;
        public string kind, result, lane;
        public bool accent;
        public Note Copy() { return (Note)MemberwiseClone(); }
    }
    [Serializable] public sealed class RunSettings { public bool legacyBalance; public double delay = 0, window = 150, oxygenDrainMultiplier = GuguRun.OXYGEN_DRAIN_MULTIPLIER; }
    [Serializable] public sealed class Profile
    {
        public string label;
        public double window, perfect, approach;
        public int stride;
        public static Profile For(string level)
        {
            if (level == "beginner") return new Profile { label = "新手", window = .17, perfect = .065, approach = 1.8, stride = 4 };
            if (level == "intermediate") return new Profile { label = "中階", window = .125, perfect = .05, approach = 1.25, stride = 2 };
            return new Profile { label = "高手", window = .085, perfect = .035, approach = .82, stride = 1 };
        }
    }
    [Serializable] public sealed class Counts { public int perfect, good, miss; }
    [Serializable] public sealed class FoodTargets { public int grow, fat, full; }
    [Serializable] public sealed class BreathVisual
    {
        public double phase, fill, ringScale, mist, expansion, pulse;
        public bool inhaling, ready;
    }
    [Serializable] public sealed class GameEvent
    {
        public string type, result, reason, lane;
        public Note note;
        public double at, error, food, growth, air, gain;
        public int taps;
        public bool big, automatic;
    }

    public sealed class GuguRun
    {
        public const double OXYGEN_DRAIN_MULTIPLIER = .5;
        public readonly double oxygenDrainMultiplier;
        readonly bool drainDuringSurface;
        public readonly DifficultyBalance balance;
        public const double DEFAULT_WINDOW = 150, REFILL_FRACTION = .12, REFILL_LIMIT = 100 - 1e-9;
        static readonly string[] Phrase = { "lower", "lower", "upper", "upper", "upper", "upper", "lower", "lower", "lower", "upper", "upper", "lower", "upper", "lower", "lower", "upper" };
        public Track track;
        public string difficulty, status = "ready", phase = "shore", beforePause, outcome, reason;
        public bool practice, leaped, called;
        public double time, air, initialAir, depth, window, delayMs, windowMs, surfaceAt;
        public double stability = 100, inputAt = -100, lastPress = -100, breathElapsed, lastBreathTap = -100, missedGateAt = -100;
        public int food, totalFish, cursor, combo, maxCombo, hits, misses, score, overpresses, departureCursor, breathTaps;
        public int[] stageFood = new int[3], stageTotals = new int[3];
        public string inputLane = "lower";
        public readonly List<Note> notes = new List<Note>(), departures = new List<Note>();
        public readonly List<GameEvent> events = new List<GameEvent>();
        public readonly List<double> errors = new List<double>();
        public readonly Counts counts = new Counts();
        public Profile profile;
        public FoodTargets targets;
        public GameEvent feedback;

        public GuguRun(Track track, string level = "expert", bool practice = false, RunSettings settings = null)
        {
            if (track == null || track.charts == null) throw new ArgumentNullException("track");
            this.track = track; difficulty = level; this.practice = practice;
            profile = Profile.For(level);
            settings = settings ?? new RunSettings();
            balance = DifficultyBalance.For(settings.legacyBalance ? "expert" : level);
            // Historical HTML fixtures retain their original balance; normal play keeps breathing effort active.
            drainDuringSurface = !settings.legacyBalance;
            oxygenDrainMultiplier = settings.oxygenDrainMultiplier; setTiming(settings.delay, settings.window);
            Note[] source = track.charts.Get(level);
            if (source == null) throw new ArgumentException("Missing difficulty chart: " + level);
            var chart = new List<Note>();
            for (int i = 0; i < source.Length; i++)
            {
                var note = source[i].Copy(); note.id = i; note.result = null; note.fish = 0;
                note.lane = laneFor(note); chart.Add(note);
            }
            // Bundle skipped fish at retained timestamps, preserving each song's total.
            Note[] expert = track.charts.expert ?? track.charts.beginner ?? track.charts.intermediate;
            var gates = new List<Note>();
            foreach (var n in expert) if (n.kind != "fish") gates.Add(n);
            foreach (var n in expert)
            {
                if (n.kind != "fish") continue;
                int group = gateGroup(gates, n); Note nearest = null; double distance = double.PositiveInfinity;
                foreach (var candidate in chart)
                {
                    if (candidate.kind != "fish" || candidate.stage != n.stage || gateGroup(gates, candidate) != group) continue;
                    double d = Math.Abs(candidate.time - n.time);
                    if (d < distance) { nearest = candidate; distance = d; }
                }
                if (nearest == null)
                    foreach (var candidate in chart)
                    {
                        if (candidate.kind != "fish" || candidate.stage != n.stage) continue;
                        double d = Math.Abs(candidate.time - n.time);
                        if (d < distance) { nearest = candidate; distance = d; }
                    }
                if (nearest == null) throw new ArgumentException("此段譜面沒有可分配的魚群");
                nearest.fish++;
            }
            foreach (var n in chart)
            {
                if (n.kind == "dive" || n.kind == "leap") departures.Add(n);
                else { notes.Add(n); totalFish += n.fish; stageTotals[n.stage] += n.fish; }
            }
            targets = foodTargets(totalFish, balance.fullRatio);
        }

        static int gateGroup(List<Note> gates, Note n) { int result = 0; foreach (var gate in gates) if (gate.time < n.time) result++; return result; }
        public static double clamp(double n, double lo, double hi) { return Math.Max(lo, Math.Min(hi, n)); }
        // JS Math.round chooses +infinity at ties, unlike System.Math.Round's bankers rounding.
        public static double jsRound(double value) { return Math.Floor(value + .5); }
        public static double speedValue(double n) { return double.IsNaN(n) || double.IsInfinity(n) ? 1 : clamp(jsRound(n * 10) / 10, .1, 2); }
        static double bounded(double v, double lo, double hi, double fallback) { return double.IsNaN(v) || double.IsInfinity(v) ? fallback : clamp(jsRound(v / 5) * 5, lo, hi); }
        public static double delayValue(double v) { return bounded(v, -200, 200, 0); }
        public static double windowValue(double v) { return bounded(v, 40, 200, 85); }
        public static double refillAir(double value) { return Math.Min(REFILL_LIMIT, value + (100 - value) * REFILL_FRACTION); }
        public static string airDisplay(double value) { return Math.Floor(clamp(value, 0, 100)).ToString("0", CultureInfo.InvariantCulture); }
        public static FoodTargets foodTargets(int total, double fullRatio = 360.0 / 438)
        {
            int tenFloor = (int)Math.Floor(total / 10.0) * 10;
            int maximum = Math.Max(1, tenFloor != 0 ? tenFloor : total);
            Func<double, int> target = ratio => Math.Min(maximum, Math.Max(Math.Min(10, maximum), (int)jsRound(total * ratio / 10) * 10));
            return new FoodTargets { grow = target(fullRatio / 3), fat = target(fullRatio * 2 / 3), full = target(fullRatio) };
        }
        public static string laneFor(Note note)
        {
            if (note.kind == "surface" || note.kind == "exit" || note.kind == "call") return "upper";
            if (note.kind != "fish") return "lower";
            string lane = Phrase[(int)Math.Floor(note.step / 2.0) % 16];
            return note.step % 2 != 0 ? (lane == "upper" ? "lower" : "upper") : lane;
        }
        public void setTiming(double delay, double window)
        {
            delayMs = delayValue(delay); windowMs = windowValue(window); this.window = windowMs / 1000;
            profile.perfect = Math.Min(.065, this.window * .42);
        }
        public double noteTime(Note note) { return note.time + delayMs / 1000; }
        public int form { get { return food >= targets.fat ? 2 : food >= targets.grow ? 1 : 0; } }
        public double growth { get { return 1 + clamp((double)food / targets.full, 0, 1) * .6; } }
        public double progress { get { return clamp(time / track.duration, 0, 1); } }
        public double danger { get { return phase == "underwater" ? clamp((55 - air) / 55, 0, 1) : 0; } }
        public double accuracy { get { int total = hits + misses; return total != 0 ? jsRound((counts.perfect + counts.good * .5) / total * 10000) / 100 : 100; } }
        public Note target { get { for (int i = cursor; i < notes.Count; i++) if (notes[i].result == null) return notes[i]; return null; } }
        public bool openingBreath { get { return status == "breathing" || status == "paused" && beforePause == "breathing"; } }
        public bool breathingActive { get { string s = status == "paused" ? beforePause : status; return s == "breathing" || s == "playing" && phase == "surface"; } }
        public double breathRemaining
        {
            get
            {
                if (openingBreath) return 3.6 - breathElapsed % 3.6;
                return phase == "surface" && departureCursor < departures.Count ? Math.Max(0, noteTime(departures[departureCursor]) - time) : 0;
            }
        }
        public double approachWindow { get { return Math.Max(.24, window + .1); } }
        static double smooth(double value) { return value * value * (3 - 2 * value); }
        public static BreathVisual breathState(double time)
        {
            double phase = ((time % 3.6) + 3.6) % 3.6, inhale = Math.Min(1, phase / 2.7);
            double release = clamp((phase - 3.05) / .55, 0, 1), fill = inhale * (1 - smooth(release));
            return new BreathVisual { phase = phase, fill = fill, inhaling = phase < 2.7, ready = fill >= .9,
                ringScale = 1 - .86 * smooth(inhale), mist = Math.Min(1, phase / .18) * (1 - smooth(release)), expansion = smooth(fill) };
        }
        public double breathSize() { return openingBreath ? breathState(breathElapsed).fill : air / 100; }
        public double breathSize(double at) { return openingBreath ? breathState(at).fill : air / 100; }
        public BreathVisual breathVisual()
        {
            if (openingBreath) return breathState(breathElapsed);
            double fill = air / 100, pulse = Math.Max(0, 1 - (time - lastBreathTap) / .18);
            return new BreathVisual { fill = fill, expansion = Math.Min(1, smooth(fill) + pulse * .06), ringScale = 1 - .86 * smooth(fill),
                mist = .2 + pulse * .8, inhaling = true, ready = fill >= .9, pulse = pulse };
        }
        public void startBreath() { status = "breathing"; time = 0; air = 0; breathElapsed = 0; breathTaps = 0; lastBreathTap = -100; }
        public void advanceBreath(double dt)
        {
            if (status != "breathing") return;
            breathElapsed += Math.Max(0, dt); air = breathSize() * 100;
        }
        public void inhale()
        {
            if (status != "breathing") return;
            air = jsRound(clamp(breathSize(breathElapsed), 0, 1) * 100); initialAir = air; status = "playing"; time = 0;
            events.Add(new GameEvent { type = "breath", big = true, at = 0 });
            phase = "underwater"; depth = 18; events.Add(new GameEvent { type = "splash", at = 0, automatic = true });
        }
        public void tapBreath()
        {
            if (status == "breathing") inhale();
            else if (status == "playing" && phase == "surface" && breathRemaining > 0)
            {
                double before = air; air = refillAir(air); breathTaps++; lastBreathTap = time;
                events.Add(new GameEvent { type = "breathTap", at = time, air = air, gain = air - before, taps = breathTaps });
            }
        }
        public void pause() { if (status == "playing" || status == "breathing") { beforePause = status; status = "paused"; } }
        public void resume() { if (status == "paused") status = beforePause; }
        public void lose(string reason)
        {
            if (practice) { air = Math.Max(1, air); return; }
            status = "lost"; this.reason = reason; outcome = food >= targets.fat ? "angel" : "hungryGhost";
            events.Add(new GameEvent { type = "lost", at = time });
        }
        void syncOutcome() { if (status == "won") outcome = food >= targets.full ? "friends" : "rest"; }
        void consumeSegment(double to)
        {
            if (phase == "surface" && !drainDuringSurface) { time = Math.Max(time, to); return; }
            // Intro grace is for the initial underwater lead-in, not mid-song refill stops.
            if (phase == "underwater" && time < track.introEnd) time = Math.Min(to, track.introEnd);
            double dt = Math.Max(0, to - time);
            if (phase == "underwater" || phase == "surface")
            {
                double oxygenDepth = phase == "surface" ? 0 : depth;
                air = Math.Max(0, air - dt * (4.15 + oxygenDepth * .012) * oxygenDrainMultiplier * balance.oxygen);
                if (air <= 0 && !practice) { time = to; lose("oxygen"); return; }
                if (practice) air = Math.Max(1, air);
            }
            time = to;
        }
        public void consume(double to)
        {
            while (departureCursor < departures.Count && noteTime(departures[departureCursor]) <= to)
            {
                Note departure = departures[departureCursor++]; double at = Math.Max(time, noteTime(departure));
                consumeSegment(at); if (status != "playing") return;
                if (departure.kind == "leap" && phase != "surface" && !practice) { lose("leap"); return; }
                if (phase == "surface" || practice)
                {
                    phase = "underwater"; depth = 12; if (departure.kind == "leap") leaped = true;
                    events.Add(new GameEvent { type = departure.kind == "leap" ? "bigBreath" : "splash", at = at, automatic = true });
                }
            }
            consumeSegment(to);
        }
        public void update(double to)
        {
            if (status == "playing" && to >= time)
            {
                while (cursor < notes.Count && noteTime(notes[cursor]) + window + .03 < to)
                {
                    Note note = notes[cursor++]; consume(Math.Max(time, noteTime(note) + window + .03));
                    if (status != "playing") { syncOutcome(); return; }
                    if (note.result == null) judge(note, false);
                    if (status != "playing") { syncOutcome(); return; }
                }
                consume(to);
                if (status == "playing" && to >= track.duration)
                {
                    if (leaped && phase == "shore" && called)
                    {
                        status = "won"; outcome = food >= targets.full ? "friends" : "rest";
                        events.Add(new GameEvent { type = "won", at = to });
                    }
                    else lose("route");
                }
            }
            syncOutcome();
        }
        public void judge(Note note, bool hit, double error = 0)
        {
            if (note.result != null) return;
            double airBefore = air;
            bool allowed = note.kind == "fish" || note.kind == "surface" || note.kind == "exit" ? phase == "underwater" : note.kind == "call" ? phase == "shore" : phase != "underwater";
            hit = hit && allowed; note.result = hit ? "hit" : "miss";
            if (hit)
            {
                hits++; combo++;
                if (note.kind == "fish") { food += note.fish; stageFood[note.stage] += note.fish; depth = note.depth; }
                if (note.kind == "surface") { phase = "surface"; surfaceAt = time; depth = 0; events.Add(new GameEvent { type = "breath", at = time }); }
                if (note.kind == "dive" || note.kind == "leap")
                {
                    phase = "underwater"; depth = 12; if (note.kind == "leap") leaped = true;
                    events.Add(new GameEvent { type = note.kind == "leap" ? "bigBreath" : "splash", at = time });
                }
                if (note.kind == "exit") { phase = "shore"; depth = 0; }
                if (note.kind == "call") { called = true; events.Add(new GameEvent { type = "call", at = time }); }
            }
            else
            {
                misses++; combo = 0;
                if (note.kind == "leap") { if (practice) { leaped = true; phase = "underwater"; } else lose("leap"); }
                if (practice && note.kind == "dive") phase = "underwater";
                if (practice && note.kind == "surface") { phase = "surface"; air = 100; }
                if (practice && note.kind == "exit") phase = "shore";
                if (practice && note.kind == "call") called = true;
                if (note.kind == "dive" && phase == "shore" && !practice) lose("entry");
            }
            var e = new GameEvent { type = hit ? "hit" : "miss", note = note, at = time, error = error,
                result = hit ? (Math.Abs(error) < window * .45 ? "perfect" : "good") : "miss", food = food, growth = growth };
            events.Add(e); feedback = e;
            // Preserve superclass ordering: score, call completion, stability, shore reset.
            if (e.type == "hit")
            {
                e.result = Math.Abs(error) <= profile.perfect ? "perfect" : "good";
                if (e.result == "perfect") counts.perfect++; else counts.good++;
                errors.Add(error * 1000); score += e.result == "perfect" ? 1000 : 500; maxCombo = Math.Max(maxCombo, combo);
            }
            else counts.miss++;
            if (note.kind == "call" && called && leaped && phase == "shore")
            {
                status = "won"; outcome = food >= targets.full ? "friends" : "rest";
                events.Add(new GameEvent { type = "won", at = time });
            }
            if (note.result == "hit") stability = Math.Min(100, stability + (Math.Abs(error) <= profile.perfect ? balance.perfectRecovery : balance.goodRecovery));
            else { stability = Math.Max(0, stability - balance.missPenalty); if (stability == 0 && status == "playing" && !practice) lose("rhythm"); }
            if (note.kind == "surface" && phase == "surface")
            {
                if (note.result == "miss") air = airBefore;
                breathTaps = 0; lastBreathTap = -100;
            }
            if ((note.kind == "surface" || note.kind == "exit") && note.result == "miss") missedGateAt = time;
            syncOutcome();
        }
        public void press(string lane, double at)
        {
            if (status != "playing" || lane != "upper" && lane != "lower") return;
            update(at); if (status != "playing") return;
            if (phase == "surface") { tapBreath(); return; }
            inputLane = lane; inputAt = at;
            Note nearest = null, wrong = null; double distance = double.PositiveInfinity;
            for (int i = cursor; i < notes.Count; i++)
            {
                Note n = notes[i]; if (n.result != null) continue;
                double d = Math.Abs(at - noteTime(n));
                if (n.lane == lane && d < distance) { nearest = n; distance = d; }
                if (wrong == null && n.lane != lane && d <= window + 1e-8) wrong = n;
            }
            if (nearest != null && distance <= window + 1e-8) { judge(nearest, true, at - noteTime(nearest)); return; }
            Note nearby = nearest != null && distance <= approachWindow + 1e-8 ? nearest : null;
            Note missed = nearby ?? wrong;
            if (missed != null)
            {
                overpresses++; double delta = at - noteTime(missed); judge(missed, false, delta);
                feedback.reason = nearby != null ? (delta < 0 ? "early" : "late") : "lane";
            }
        }
    }
}
