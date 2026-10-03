using System;
using System.Collections.Generic;
using System.Text;

namespace SealGugu
{
    [Serializable] public sealed class GoldenSuite { public string sourceCommit; public GoldenScenario[] scenarios; }
    [Serializable] public sealed class GoldenScenario
    {
        public string name, trackId, level;
        public bool practice;
        public double delay, window;
        public GoldenStep[] steps;
    }
    [Serializable] public sealed class GoldenStep
    {
        public string op, lane;
        public double value;
        public bool flag;
        public GoldenSnapshot expected;
    }
    [Serializable] public sealed class GoldenSnapshot
    {
        public double[] numbers;
        public string[] text;
        public bool[] flags;
        public string noteResults;
        public GoldenEvent[] events;
    }
    [Serializable] public sealed class GoldenEvent
    {
        public string type, result, reason, lane, noteKind, noteLane;
        public int noteId, taps;
        public double at, error, food, growth, air, gain;
        public bool big, automatic;
    }
    // Historical V13 parity explicitly uses the original drain rate (1x). Current default balance has separate tests.
    // Golden traces are produced by unmodified shipped JavaScript, independently
    // of this port. Any discrepancy throws, causing the Editor build gate to fail.
    public static class CoreParity
    {
        static int checks;
        static string context;
        static void Eq(double actual, double expected, string label)
        {
            checks++;
            if (double.IsNaN(actual) || Math.Abs(actual - expected) > 1e-7)
                throw new InvalidOperationException(context + " / " + label + ": expected " + expected.ToString("R") + ", actual " + actual.ToString("R"));
        }
        static void Eq(string actual, string expected, string label)
        {
            checks++; if ((actual ?? "") != (expected ?? "")) throw new InvalidOperationException(context + " / " + label + ": expected " + expected + ", actual " + actual);
        }
        static void Eq(bool actual, bool expected, string label) { Eq(actual ? 1 : 0, expected ? 1 : 0, label); }
        public static string Run(ChartDocument charts, GoldenSuite suite)
        {
            checks = 0; int checkpoints = 0;
            foreach (var scenario in suite.scenarios)
            {
                Track track = Array.Find(charts.tracks, t => t.id == scenario.trackId);
                if (track == null) throw new ArgumentException("Golden track missing " + scenario.trackId);
                var run = new GuguRun(track, scenario.level, scenario.practice, new RunSettings { delay = scenario.delay, window = scenario.window, oxygenDrainMultiplier = 1, legacyBalance = true });
                for (int index = 0; index < scenario.steps.Length; index++)
                {
                    GoldenStep step = scenario.steps[index]; int before = run.events.Count;
                    context = scenario.name + "/" + index + "/" + step.op;
                    Apply(run, step); Compare(run, step.expected, before); checkpoints++;
                }
            }
            // Nonlinear saturation must never produce 100%, even under extreme tapping.
            double air = 0, gain = double.PositiveInfinity;
            for (int i = 0; i < 10000; i++)
            {
                double next = GuguRun.refillAir(air), delta = next - air;
                if (!(next >= air && next < 100 && delta <= gain + 1e-12)) throw new InvalidOperationException("Nonlinear refill invariant");
                if (GuguRun.airDisplay(next) == "100") throw new InvalidOperationException("Refill display rounded to 100");
                gain = delta; air = next; checks += 2;
            }
            return "PASS: " + suite.scenarios.Length + " JavaScript golden scenarios, " + checkpoints + " checkpoints, " + checks + " assertions. Source " + suite.sourceCommit;
        }
        static void Apply(GuguRun g, GoldenStep a)
        {
            switch (a.op)
            {
                case "start": g.startBreath(); break;
                case "advance": g.advanceBreath(a.value); break;
                case "inhale": g.inhale(); break;
                case "tap": g.tapBreath(); break;
                case "pause": g.pause(); break;
                case "resume": g.resume(); break;
                case "update": g.update(a.value); break;
                case "press": g.press(a.lane, a.value); break;
                case "food": g.food = (int)a.value; break;
                case "lose": g.lose(a.lane); break;
                case "finish":
                    g.phase = "shore"; g.leaped = true; Note call = g.notes.Find(n => n.kind == "call");
                    g.time = call.time; g.judge(call, true); break;
                case "note":
                    Note note = g.notes.Find(n => n.id == (int)a.value); double at = g.noteTime(note);
                    double fps = double.Parse(a.lane, System.Globalization.CultureInfo.InvariantCulture);
                    for (double t = g.time + 1 / fps; t < at; t += 1 / fps) g.update(t);
                    g.press(note.lane, at); break;
                case "refill":
                    for (int i = 0; i < a.value && g.phase == "surface"; i++) g.press("lower", g.time + .03); break;
                default: throw new ArgumentException("Unknown golden operation " + a.op);
            }
        }
        static void Compare(GuguRun g, GoldenSnapshot s, int eventFrom)
        {
            BreathVisual b = g.breathVisual(); GameEvent e = g.feedback ?? new GameEvent();
            double[] numbers = {
                g.time,g.air,g.initialAir,g.depth,g.window,g.delayMs,g.windowMs,g.surfaceAt,g.stability,g.inputAt,g.lastPress,g.breathElapsed,g.lastBreathTap,g.missedGateAt,
                g.food,g.totalFish,g.cursor,g.combo,g.maxCombo,g.hits,g.misses,g.score,g.overpresses,g.departureCursor,g.breathTaps,
                g.stageFood[0],g.stageFood[1],g.stageFood[2],g.stageTotals[0],g.stageTotals[1],g.stageTotals[2],
                g.targets.grow,g.targets.fat,g.targets.full,g.form,g.growth,g.progress,g.danger,g.accuracy,g.breathRemaining,
                b.phase,b.fill,b.ringScale,b.mist,b.expansion,b.pulse,g.events.Count,g.errors.Count,g.counts.perfect,g.counts.good,g.counts.miss
            };
            for (int i = 0; i < numbers.Length; i++) Eq(numbers[i], s.numbers[i], "number[" + i + "]");
            string[] text = { g.status,g.phase,g.beforePause,g.outcome,g.reason,g.inputLane,e.type,e.result,e.reason };
            for (int i = 0; i < text.Length; i++) Eq(text[i], s.text[i], "text[" + i + "]");
            bool[] flags = { g.leaped,g.called,g.openingBreath,g.breathingActive,b.inhaling,b.ready };
            for (int i = 0; i < flags.Length; i++) Eq(flags[i], s.flags[i], "flag[" + i + "]");
            var results = new StringBuilder(); foreach (var n in g.notes) results.Append(n.result == "hit" ? 'H' : n.result == "miss" ? 'M' : '.');
            Eq(results.ToString(), s.noteResults, "note results"); Eq(g.events.Count - eventFrom, s.events.Length, "event count");
            for (int i = 0; i < s.events.Length; i++)
            {
                GameEvent actual = g.events[eventFrom + i]; GoldenEvent expected = s.events[i]; string label = "event[" + i + "]";
                Eq(actual.type, expected.type, label + ".type"); Eq(actual.result, expected.result, label + ".result");
                Eq(actual.reason, expected.reason, label + ".reason"); Eq(actual.lane, expected.lane, label + ".lane");
                Eq(actual.note != null ? actual.note.id : -1, expected.noteId, label + ".noteId");
                Eq(actual.note != null ? actual.note.kind : null, expected.noteKind, label + ".kind");
                Eq(actual.note != null ? actual.note.lane : null, expected.noteLane, label + ".noteLane");
                Eq(actual.at, expected.at, label + ".at"); Eq(actual.error, expected.error, label + ".error"); Eq(actual.food, expected.food, label + ".food");
                Eq(actual.growth, expected.growth, label + ".growth"); Eq(actual.air, expected.air, label + ".air"); Eq(actual.gain, expected.gain, label + ".gain");
                Eq(actual.taps, expected.taps, label + ".taps"); Eq(actual.big, expected.big, label + ".big"); Eq(actual.automatic, expected.automatic, label + ".automatic");
            }
        }
    }
}
