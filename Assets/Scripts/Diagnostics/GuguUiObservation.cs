#if UNITY_EDITOR || GUGU_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SealGugu.Diagnostics
{
    /// <summary>Read-only evidence of real desktop input. Does not inject events or change game state.</summary>
    public sealed class GuguUiObservation : MonoBehaviour
    {
        [Serializable] sealed class Record {
            public string modal, level, status, cursor;
            public int song, width, height, fps;
            public bool fullscreen, fpsVisible, hand, focused;
            public float mouseX, mouseY;
        }
        [Serializable] sealed class Report { public string version = GuguGame.Version; public List<Record> states = new List<Record>(); }
#if UNITY_STANDALONE_WIN
        [StructLayout(LayoutKind.Sequential)] struct CursorInfo { public int size, flags; public IntPtr cursor; public int x, y; }
        [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CursorInfo info);
        [DllImport("user32.dll", EntryPoint="LoadCursorW")] static extern IntPtr LoadCursor(IntPtr instance, IntPtr name);
#endif
        string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch() {
            string[] args = Environment.GetCommandLineArgs(); int flag = Array.IndexOf(args, "-guguUiObserve");
            if(flag < 0 || flag + 1 >= args.Length) return;
            new GameObject("Read-only desktop QA observer").AddComponent<GuguUiObservation>().output = Path.GetFullPath(args[flag+1]);
        }
        IEnumerator Start() {
            var game = FindAnyObjectByType<GuguGame>(); var desktop = game.GetComponent<GuguDesktop>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var modal = typeof(GuguGame).GetField("modal", flags); var song = typeof(GuguGame).GetField("song", flags); var level = typeof(GuguGame).GetField("level", flags);
            var report = new Report(); string previous = "";
            while(true) {
                yield return new WaitForEndOfFrame();
                var mouse = Mouse.current == null ? Vector2.zero : Mouse.current.position.ReadValue();
                string cursor = "unknown";
#if UNITY_STANDALONE_WIN
                var info = new CursorInfo { size = Marshal.SizeOf<CursorInfo>() };
                if(GetCursorInfo(ref info)) cursor = info.cursor == LoadCursor(IntPtr.Zero,new IntPtr(32649)) ? "IDC_HAND" : info.cursor == LoadCursor(IntPtr.Zero,new IntPtr(32512)) ? "IDC_ARROW" : "other";
#endif
                var state = new Record { modal=(string)modal.GetValue(game), level=(string)level.GetValue(game), song=(int)song.GetValue(game), status=game.run.status, cursor=cursor,
                    width=Screen.width, height=Screen.height, fullscreen=Screen.fullScreen, fps=desktop.FramesPerSecond, fpsVisible=desktop.FpsVisible,
                    hand=desktop.HandRequested, focused=Application.isFocused, mouseX=mouse.x, mouseY=mouse.y };
                string signature=state.modal+state.level+state.song+state.status+state.cursor+state.width+state.height+state.fullscreen+state.fpsVisible+state.hand+state.focused;
                if(signature != previous && report.states.Count < 500) {
                    previous=signature; report.states.Add(state); Directory.CreateDirectory(Path.GetDirectoryName(output));
                    File.WriteAllText(output, JsonUtility.ToJson(report,true));
                }
            }
        }
    }
}
#endif
