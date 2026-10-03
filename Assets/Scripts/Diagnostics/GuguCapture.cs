#if UNITY_EDITOR || GUGU_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SealGugu.Diagnostics
{
    /// <summary>Development-only deterministic visual audit, excluded from release players.</summary>
    public sealed class GuguCapture : MonoBehaviour
    {
        [Serializable] private sealed class CaptureRecord
        {
            public string mode, file, status, phase, outcome;
            public int width, height, food, form, totalFish;
            public double songTime, air, growth, breathElapsed;
            public int sampledPixels;
            public double meanLuminance, luminanceDeviation, colourRange, blackCoverage, oceanPaletteCoverage;
            public bool renderedContentValid;
        }

        [Serializable] private sealed class CaptureReport
        {
            public string version = GuguGame.Version;
            public string unityVersion, capturedUtc, graphicsDevice;
            public bool success;
            public List<CaptureRecord> captures = new List<CaptureRecord>();
            public List<string> errors = new List<string>();
        }

        private static readonly string[] Modes = {
            "menu", "setup", "credits", "tuning", "opening", "opening-full", "swim-thin", "swim-medium", "swim-fat",
            "low-thin", "low-medium", "low-fat", "bite", "gate", "surface",
            "friends", "rest", "hungryGhost", "angel"
        };
        private string outputDirectory;
        private readonly CaptureReport report = new CaptureReport();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            string[] args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "-guguCapture");
            if (flag < 0) return;
            if (flag + 1 >= args.Length || args[flag + 1].StartsWith("-"))
            {
                Debug.LogError("-guguCapture requires an output directory.");
                Application.Quit(2);
                return;
            }
            var audit = new GameObject("Development screenshot audit").AddComponent<GuguCapture>();
            audit.outputDirectory = Path.GetFullPath(args[flag + 1]);
            DontDestroyOnLoad(audit.gameObject);
        }

        private void ErrorLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                report.errors.Add(message + "\n" + trace);
        }

        private IEnumerator Start()
        {
            var sequence = CaptureScenes();
            while (true)
            {
                bool moved = false;
                object next = null;
                Exception failure = null;
                try { moved = sequence.MoveNext(); next = sequence.Current; }
                catch (Exception e) { failure = e; }
                if (failure != null)
                {
                    report.errors.Add(failure.ToString());
                    Complete();
                    yield break;
                }
                if (!moved) yield break;
                yield return next;
            }
        }

        private IEnumerator CaptureScenes()
        {
            Application.logMessageReceived += ErrorLog;
            Application.runInBackground = true;
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            report.unityVersion = Application.unityVersion;
            report.capturedUtc = DateTime.UtcNow.ToString("O");
            report.graphicsDevice = SystemInfo.graphicsDeviceName;
            Directory.CreateDirectory(outputDirectory);
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            for (int frame = 0; frame < 30 && (Screen.width != 1280 || Screen.height != 720); frame++) yield return null;
            yield return null;
            var game = FindAnyObjectByType<GuguGame>();
            if (!game || game.run == null)
            {
                report.errors.Add("GuguGame or its chart run failed to initialize.");
                Complete();
                yield break;
            }

            string inputResult = NativeInputParity.Run(game);
            File.WriteAllText(Path.Combine(outputDirectory, "input-parity.txt"), inputResult);
            Debug.Log("GUGU_INPUT_PARITY " + inputResult);
            string audioResult = NativeAudioParity.Run(game);
            File.WriteAllText(Path.Combine(outputDirectory, "audio-parity.txt"), audioResult);
            Debug.Log("GUGU_AUDIO_PARITY " + audioResult);
            var desktopChecks = NativeDesktopParity.Run(game, outputDirectory);
            while (desktopChecks.MoveNext()) yield return desktopChecks.Current;
            game.hidePreviewBadge = true;

            foreach (string mode in Modes)
            {
                game.PreviewScene(mode);
                // Give native IMGUI and uploaded texture changes complete render frames.
                yield return null;
                yield return null;
                yield return new WaitForEndOfFrame();
                Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
                if (!capture)
                {
                    report.errors.Add("No rendered screenshot for " + mode);
                    continue;
                }
                string name = mode + ".png";
                File.WriteAllBytes(Path.Combine(outputDirectory, name), capture.EncodeToPNG());
                var run = game.run;
                var record = new CaptureRecord {
                    mode = mode, file = name, width = capture.width, height = capture.height,
                    status = run.status, phase = run.phase, outcome = run.outcome,
                    food = run.food, form = run.form, totalFish = run.totalFish,
                    songTime = run.time, air = run.air, growth = run.growth, breathElapsed = run.breathElapsed
                };
                InspectRenderedContent(capture, record);
                report.captures.Add(record);
                if (capture.width != 1280 || capture.height != 720)
                    report.errors.Add(mode + " resolution was " + capture.width + "x" + capture.height + ", expected 1280x720.");
                if (!record.renderedContentValid)
                    report.errors.Add(mode + " has no reliable rendered game content: mean=" + record.meanLuminance.ToString("F2") +
                        ", deviation=" + record.luminanceDeviation.ToString("F2") + ", colour range=" + record.colourRange.ToString("F0") +
                        ", black coverage=" + record.blackCoverage.ToString("P1") + ", ocean palette=" + record.oceanPaletteCoverage.ToString("P1") +
                        ". A hidden/minimized window or unavailable graphics surface must not pass visual QA.");
                ValidateSceneState(mode, run);
                Destroy(capture);
                Debug.Log("GUGU_CAPTURE " + mode + " " + name);
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-guguSoak") >= 0)
            {
                var soak = NativeSoak.Run(game, Path.Combine(outputDirectory, "realtime-soak.json"));
                while (soak.MoveNext()) yield return soak.Current;
            }
            Complete();
        }

        private static void InspectRenderedContent(Texture2D texture, CaptureRecord record)
        {
            Color32[] pixels = texture.GetPixels32();
            int stepX = Math.Max(1, texture.width / 64), stepY = Math.Max(1, texture.height / 36);
            int count = 0, black = 0, palette = 0;
            int minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0;
            double sum = 0, squared = 0;
            for (int y = stepY / 2; y < texture.height; y += stepY)
            for (int x = stepX / 2; x < texture.width; x += stepX)
            {
                Color32 p = pixels[y * texture.width + x];
                double luminance = .2126 * p.r + .7152 * p.g + .0722 * p.b;
                sum += luminance; squared += luminance * luminance; count++;
                minR = Math.Min(minR, p.r); minG = Math.Min(minG, p.g); minB = Math.Min(minB, p.b);
                maxR = Math.Max(maxR, p.r); maxG = Math.Max(maxG, p.g); maxB = Math.Max(maxB, p.b);
                if (p.r < 8 && p.g < 8 && p.b < 8) black++;
                // Broad shared ice/water/paper palette, deliberately tolerant of danger grading.
                if (p.r > 12 && p.g > 60 && p.b > 75 && p.b >= p.r * .8) palette++;
            }
            record.sampledPixels = count;
            record.meanLuminance = sum / Math.Max(1, count);
            record.luminanceDeviation = Math.Sqrt(Math.Max(0, squared / Math.Max(1, count) - record.meanLuminance * record.meanLuminance));
            record.colourRange = Math.Max(maxR - minR, Math.Max(maxG - minG, maxB - minB));
            record.blackCoverage = (double)black / Math.Max(1, count);
            record.oceanPaletteCoverage = (double)palette / Math.Max(1, count);
            record.renderedContentValid = count > 500 && record.meanLuminance > 12 && record.luminanceDeviation > 4 &&
                record.colourRange > 30 && record.blackCoverage < .80 && record.oceanPaletteCoverage > .10;
        }

        private void ValidateSceneState(string mode, GuguRun run)
        {
            if (run.totalFish <= 0 || run.air < 0 || run.air > 100 || double.IsNaN(run.air))
                report.errors.Add(mode + " has invalid chart or oxygen state.");
            if (mode == "menu" && run.status != "ready") report.errors.Add("Menu preview is not ready.");
            if (mode.StartsWith("opening") && (!run.openingBreath || run.status != "breathing"))
                report.errors.Add(mode + " is not opening breathing.");
            if (mode.StartsWith("swim") || mode.StartsWith("low") || mode == "bite")
            {
                int expectedForm = mode.EndsWith("fat") ? 2 : mode.EndsWith("medium") ? 1 : 0;
                if (run.status != "playing" || run.phase != "underwater" || run.form != expectedForm)
                    report.errors.Add(mode + " does not match requested underwater body form.");
                if (mode.StartsWith("low") && run.air > 10) report.errors.Add(mode + " does not show critical oxygen.");
            }
            if (mode == "surface" && (run.status != "playing" || run.phase != "surface"))
                report.errors.Add("Surface preview is not active breathing on shore.");
            if (mode == "friends" || mode == "rest" || mode == "hungryGhost" || mode == "angel")
            {
                string expectedStatus = mode == "friends" || mode == "rest" ? "won" : "lost";
                if (run.outcome != mode || run.status != expectedStatus)
                    report.errors.Add(mode + " does not match requested ending.");
            }
        }

        private void Complete()
        {
            report.success = report.errors.Count == 0 && report.captures.Count == Modes.Length;
            File.WriteAllText(Path.Combine(outputDirectory, "capture-report.json"), JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= ErrorLog;
            Debug.Log((report.success ? "GUGU_CAPTURE_SUCCESS " : "GUGU_CAPTURE_FAILED ") + report.captures.Count + " scenes; " + outputDirectory);
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(report.success ? 0 : 1);
#else
            Application.Quit(report.success ? 0 : 1);
#endif
        }

        private void OnDestroy() { Application.logMessageReceived -= ErrorLog; }
    }
}
#endif
