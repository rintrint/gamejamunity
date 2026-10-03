#if UNITY_EDITOR || GUGU_QA
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SealGugu.Diagnostics
{
    public static class NativeDesktopParity
    {
        static int assertions;
        static void Require(bool value, string message) {
            if (!value) throw new InvalidOperationException("Desktop controls: " + message);
            assertions++;
        }
        static void State(Keyboard keyboard, params Key[] keys) {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update();
        }
        static IEnumerator WaitForMode(bool fullscreen) {
            double timeout = Time.realtimeSinceStartupAsDouble + 5;
            do { yield return null; } while (Screen.fullScreen != fullscreen && Time.realtimeSinceStartupAsDouble < timeout);
            Require(Screen.fullScreen == fullscreen, "actual native window mode did not change");
            yield return null; yield return null;
        }
        public static IEnumerator Run(GuguGame game, string output) {
            var desktop = game.GetComponent<GuguDesktop>();
            var previous = Keyboard.current; var keyboard = InputSystem.AddDevice<Keyboard>("Desktop validation keyboard");
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var modal = typeof(GuguGame).GetField("modal", flags);
            var calibration = typeof(GuguGame).GetField("calibration", flags);
            var start = typeof(GuguGame).GetMethod("StartRun", flags);
            var back = typeof(GuguGame).GetMethod("Back", flags);
            bool automated = game.automatedTest; game.automatedTest = true;
            assertions = 0;
            try {
                Require(!Screen.fullScreen && !desktop.RequestedFullscreen, "test must begin windowed");
                Require(desktop.SystemCursorsLoaded, "distinct Windows system arrow and hand must load");
                Require(!desktop.FpsVisible, "FPS defaults to hidden");
                State(keyboard, Key.F1); Require(desktop.FpsVisible, "F1 enables FPS on menu");
                State(keyboard, Key.F1); Require(desktop.FpsVisible, "held F1 must not toggle again");
                State(keyboard); State(keyboard, Key.F1); State(keyboard);
                Require(!desktop.FpsVisible && game.run.status == "ready", "F1 off must leave menu alone");
                modal.SetValue(game, "setup"); calibration.SetValue(game, true);
                State(keyboard, Key.F1); State(keyboard);
                Require(desktop.FpsVisible && (string)modal.GetValue(game) == "setup" && (bool)calibration.GetValue(game), "F1 must work during setup/calibration without consuming a tap");
                calibration.SetValue(game, false);
                int width = Screen.width, height = Screen.height;
                State(keyboard, Key.Enter); State(keyboard);
                Require(!desktop.RequestedFullscreen, "Enter alone must not switch modes");
                State(keyboard, Key.LeftAlt, Key.Enter);
                Require(desktop.RequestedFullscreen, "left Alt+Enter requests fullscreen");
                State(keyboard, Key.LeftAlt, Key.Enter);
                Require(desktop.RequestedFullscreen, "holding Alt+Enter must not repeat");
                State(keyboard);
                var wait = WaitForMode(true); while (wait.MoveNext()) yield return wait.Current;
                Require((string)modal.GetValue(game) == "setup", "mode switching preserves foreground setup");
                State(keyboard, Key.RightAlt, Key.NumpadEnter); State(keyboard);
                wait = WaitForMode(false); while (wait.MoveNext()) yield return wait.Current;
                Require(!desktop.RequestedFullscreen && Screen.width == width && Screen.height == height, "right Alt+numpad Enter restores previous window dimensions");
                start.Invoke(game, null); double air = game.run.air;
                State(keyboard, Key.F1); State(keyboard);
                Require(!desktop.FpsVisible && game.run.status == "breathing" && game.run.air == air, "F1 cannot inhale or alter oxygen");
                State(keyboard, Key.Escape); State(keyboard); State(keyboard, Key.F1); State(keyboard);
                Require(desktop.FpsVisible && game.run.status == "paused", "FPS works while paused without resuming");
                yield return new WaitForSecondsRealtime(.6f);
                Require(desktop.FramesPerSecond > 0, "unscaled FPS continues updating while paused");
                yield return new WaitForEndOfFrame();
                var image = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(output, "desktop-fps.png"), image.EncodeToPNG()); UnityEngine.Object.Destroy(image);
                State(keyboard, Key.F1); State(keyboard);
                string result = "PASS: " + assertions + " assertions; actual window/fullscreen round trip; restore " + width + "x" + height + "; global key isolation, held-key suppression, paused FPS, system cursor handles.";
                File.WriteAllText(Path.Combine(output, "desktop-parity.txt"), result); Debug.Log("GUGU_DESKTOP_PARITY " + result);
            } finally {
                InputSystem.RemoveDevice(keyboard); if(previous!=null && previous.added) previous.MakeCurrent();
                calibration.SetValue(game, false); back.Invoke(game, null); game.automatedTest = automated;
            }
        }
    }
}
#endif
