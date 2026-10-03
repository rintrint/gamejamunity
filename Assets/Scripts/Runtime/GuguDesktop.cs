using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SealGugu
{
    /// <summary>Desktop conveniences are independent of music, judgement and pause state.</summary>
    public sealed class GuguDesktop : MonoBehaviour
    {
        public bool FpsVisible { get; private set; }
        public int FramesPerSecond { get; private set; }
        public bool RequestedFullscreen { get; private set; }
        public bool HandRequested { get; private set; }
        readonly List<InputAction> shortcuts = new List<InputAction>();
        readonly Stack<bool> pointerClips = new Stack<bool>();
        bool pointerAllowed = true;
        int windowWidth, windowHeight, frames;
        double sampleStart;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Shared system resources retain the user's Windows cursor theme and DPI.
        // Do not DestroyCursor on handles returned by LoadCursor with a null instance.
        [DllImport("user32.dll", EntryPoint="LoadCursorW")]
        static extern IntPtr LoadCursor(IntPtr instance, IntPtr name);
        [DllImport("user32.dll")] static extern IntPtr SetCursor(IntPtr cursor);
        IntPtr arrow, hand;
#endif
        void Awake()
        {
            RequestedFullscreen = Screen.fullScreen;
            windowWidth = Mathf.Min(1600, Screen.currentResolution.width);
            windowHeight = Mathf.Min(900, Screen.currentResolution.height);
            sampleStart = Time.realtimeSinceStartupAsDouble;
            Bind("f1", _ => FpsVisible = !FpsVisible);
            foreach (string key in new[] { "enter", "numpadEnter" })
                Bind(key, ctx => {
                    var keyboard = ctx.control.device as Keyboard;
                    if (keyboard != null && keyboard.altKey.isPressed) ToggleFullscreen();
                });
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            arrow = LoadCursor(IntPtr.Zero, new IntPtr(32512)); // IDC_ARROW
            hand = LoadCursor(IntPtr.Zero, new IntPtr(32649));  // IDC_HAND
#endif
        }
        void Bind(string key, Action<InputAction.CallbackContext> pressed)
        {
            var action = new InputAction("Desktop " + key, InputActionType.Button, "<Keyboard>/" + key);
            action.performed += ctx => pressed(ctx);
            action.Enable(); shortcuts.Add(action);
        }
        void ToggleFullscreen()
        {
            if (!RequestedFullscreen) {
                windowWidth = Screen.width; windowHeight = Screen.height;
                var monitor = Screen.currentResolution;
                Screen.SetResolution(monitor.width, monitor.height, FullScreenMode.FullScreenWindow);
            } else {
                Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
            }
            RequestedFullscreen = !RequestedFullscreen;
        }
        void Update()
        {
            frames++;
            double now = Time.realtimeSinceStartupAsDouble, elapsed = now - sampleStart;
            if (elapsed >= .5) {
                FramesPerSecond = Mathf.RoundToInt((float)(frames / elapsed));
                frames = 0; sampleStart = now;
            }
        }
        void LateUpdate() { ApplyCursor(); }
        public void BeginGui()
        {
            if (Event.current.type == EventType.Repaint) HandRequested = false;
            pointerClips.Clear(); pointerAllowed = true;
        }
        // Call before entering / after leaving IMGUI scroll coordinate spaces.
        public void BeginPointerClip(Rect viewport)
        {
            pointerClips.Push(pointerAllowed);
            pointerAllowed &= viewport.Contains(Event.current.mousePosition);
        }
        public void EndPointerClip() { pointerAllowed = pointerClips.Pop(); }
        public void Interactive(Rect rect)
        {
            if (Event.current.type != EventType.Repaint || !GUI.enabled || !pointerAllowed) return;
            if (rect.Contains(Event.current.mousePosition)) HandRequested = true;
#if UNITY_EDITOR
            UnityEditor.EditorGUIUtility.AddCursorRect(rect, UnityEditor.MouseCursor.Link);
#endif
        }
        public void EndGui() { if (Event.current.type == EventType.Repaint) ApplyCursor(); }
        void ApplyCursor()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // Never change another application's cursor, the title bar or window borders.
            var mouse = Mouse.current;
            if (!Application.isFocused || mouse == null) return;
            Vector2 position = mouse.position.ReadValue();
            if (position.x < 0 || position.y < 0 || position.x >= Screen.width || position.y >= Screen.height) return;
            IntPtr cursor = HandRequested ? hand : arrow;
            if (cursor != IntPtr.Zero) SetCursor(cursor);
#endif
        }
        void OnApplicationFocus(bool focused) { if (!focused) HandRequested = false; }
        void OnDisable() { HandRequested = false; ApplyCursor(); }
        void OnDestroy() { foreach (var action in shortcuts) action.Dispose(); }
#if UNITY_EDITOR || GUGU_QA
        public bool SystemCursorsLoaded {
            get {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                return arrow != IntPtr.Zero && hand != IntPtr.Zero && arrow != hand;
#else
                return true;
#endif
            }
        }
#endif
    }
}
