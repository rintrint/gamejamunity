#if UNITY_EDITOR || GUGU_QA
using System;
using System.Reflection;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SealGugu
{
    /// <summary>
    /// Runtime smoke test of the actual InputAction -> GuguGame -> renderer/core path.
    /// Invoke on a freshly loaded ready game. A temporary virtual keyboard is removed
    /// afterward and the menu restored. Never compiled into the release player.
    /// </summary>
    public static class NativeInputParity
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Native input parity: " + message);
        }
        static void State(Keyboard keyboard, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }
        public static string Run(GuguGame game)
        {
            if (game == null || game.run == null || game.run.status != "ready")
                throw new ArgumentException("NativeInputParity requires a freshly loaded ready game.");
            var renderer = (GuguRenderer)typeof(GuguGame).GetField("view", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
            var back = typeof(GuguGame).GetMethod("Back", BindingFlags.Instance | BindingFlags.NonPublic);
            var sound=(GuguAudio)typeof(GuguGame).GetField("sound",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
            bool wasMuted=sound.muted;float listenerVolume=AudioListener.volume;AudioListener.volume=0;sound.Mute(false);sound.StopEffects();
            Func<int> bites=()=>((IEnumerable)typeof(GuguAudio).GetField("voices",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sound)).Cast<object>().Count(v=>{
                var type=v.GetType();var source=(AudioSource)type.GetField("source").GetValue(v);
                return source.clip!=null&&source.clip.name=="eat"&&(double)type.GetField("end").GetValue(v)>=AudioSettings.dspTime;
            });
            var background = InputSystem.settings.backgroundBehavior;
            bool automated = game.automatedTest; var oldKeyboard = game.automatedKeyboard;
            game.automatedTest = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Keyboard previous = Keyboard.current, keyboard = InputSystem.AddDevice<Keyboard>("Gugu validation keyboard");
            game.automatedKeyboard = keyboard;
            int assertions = 0;
            try
            {
                GuguRun run = game.run;
                run.startBreath(); run.advanceBreath(2.7); run.inhale(); run.events.Clear(); renderer.Reset(run);
                int input = renderer.InputCount;
                State(keyboard, Key.D);
                Require(renderer.InputCount == ++input && run.inputLane == "upper", "D must move immediately"); assertions++;
                Require(bites()==1,"an upper empty-beat input plays a bite immediately");assertions++;
                State(keyboard, Key.D, Key.F);
                Require(renderer.InputCount == ++input && run.inputLane == "upper", "F must trigger even while D remains held"); assertions++;
                State(keyboard, Key.D, Key.F, Key.J);
                Require(renderer.InputCount == ++input && run.inputLane == "lower", "J must move immediately across lanes"); assertions++;
                State(keyboard, Key.D, Key.F, Key.J, Key.K);
                Require(renderer.InputCount == ++input && run.inputLane == "lower", "K must trigger even while J remains held"); assertions++;
                Require(bites()==4,"all four lane-key edges make a bite even with no fish");assertions++;
                State(keyboard, Key.D, Key.F, Key.J, Key.K);
                Require(renderer.InputCount == input, "an unchanged held state must not auto-repeat"); assertions++;
                State(keyboard);
                Require(renderer.InputCount == input, "key releases must never count as hits"); assertions++;

                // Many separate physical edges within one InputSystem update must all
                // arrive; aggregating keys or imposing an animation cooldown loses them.
                sound.StopEffects();
                for (int i = 0; i < 50; i++)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(i % 2 == 0 ? Key.UpArrow : Key.DownArrow));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                }
                InputSystem.Update(); input += 50;
                Require(renderer.InputCount == input && run.inputLane == "lower", "all 50 same-frame press edges must arrive without a cooldown"); assertions++;
                Require(bites()==50,"all 50 rapid inputs schedule feedback without an eat cooldown");assertions++;
                Require(run.misses == 0 && run.overpresses == 0 && run.stability == 100, "empty beats must allow movement without penalties"); assertions++;
                State(keyboard, Key.Space); State(keyboard);
                Require(renderer.InputCount == input && run.misses == 0, "Space must be ignored underwater"); assertions++;

                run.phase = "surface"; run.air = 50;
                double expectedAir = 50;
                foreach (var key in new[] { Key.Space, Key.D, Key.J, Key.UpArrow, Key.DownArrow })
                {
                    State(keyboard, key); State(keyboard); expectedAir = GuguRun.refillAir(expectedAir);
                    Require(Math.Abs(run.air - expectedAir) < 1e-7 && renderer.InputCount == input,
                        "surface input must refill without lane movement: " + key); assertions++;
                }
                Require(run.breathTaps == 5, "five physical refill presses must count exactly five taps"); assertions++;
                double air = run.air;
                State(keyboard, Key.Escape); State(keyboard);
                Require(run.status == "paused", "Escape must pause the actual game shell"); assertions++;
                State(keyboard, Key.D); State(keyboard); State(keyboard, Key.Space); State(keyboard);
                Require(run.air == air && renderer.InputCount == input && run.breathTaps == 5,
                    "paused input must not move or refill"); assertions++;
                State(keyboard, Key.Escape); State(keyboard);
                Require(run.status == "playing", "Escape must resume the actual game shell"); assertions++;
                return "PASS: native InputSystem integration, " + assertions + " assertions, 50 independent press edges in one input update.";
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                game.automatedKeyboard = oldKeyboard; game.automatedTest = automated;
                InputSystem.settings.backgroundBehavior = background;
                if (previous != null && previous.added) previous.MakeCurrent();
                sound.Mute(wasMuted);back.Invoke(game, null);AudioListener.volume=listenerVolume;
            }
        }
    }
}
#endif
