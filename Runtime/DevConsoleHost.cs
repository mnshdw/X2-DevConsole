using System.Collections.Generic;
using UnityEngine;
using static DevConsole.ModConstants;

namespace DevConsole.Runtime
{
    [DefaultExecutionOrder(int.MinValue)]
    public class DevConsoleHost : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.G;
        private const int MaxLines = 200;
        private const string InputControlName = "DevConsoleInput";

        private bool _visible;
        private string _input = "";
        private readonly Queue<string> _output = new();
        private Vector2 _scroll;
        private bool _focusNextFrame;
        private bool _registered;

        private void Awake()
        {
            if (!_registered)
            {
                CommandRegistry.RegisterBuiltins();
                _registered = true;
            }
            AppendLine(
                "DevConsole ready. Press Ctrl+G to toggle, Esc to close. Type 'help' for commands."
            );
        }

        private void OnEnable()
        {
            Log.Info($"{LogPrefix} DevConsoleHost OnEnable");
        }

        private void OnDisable()
        {
            Log.Info($"{LogPrefix} DevConsoleHost OnDisable");
        }

        private void OnGUI()
        {
            GUI.depth = -1000;
            var ev = Event.current;

            // Toggle handled here (not in Update) so it works even when the game's
            // Input.GetKeyDown poll is suppressed by GroundCombat input handling.
            // Ctrl+G toggles. Esc closes only.
            if (ev.type == EventType.KeyDown)
            {
                var isToggle = ev.control && ev.keyCode == ToggleKey;
                var isClose = _visible && ev.keyCode == KeyCode.Escape;
                if (isToggle || isClose)
                {
                    SetVisible(!_visible);
                    Log.Info($"{LogPrefix} toggle (OnGUI {ev.keyCode}) -> visible={_visible}");
                    ev.Use();
                    return;
                }
            }

            if (!_visible)
                return;

            // Submit on Enter (consume before TextField sees it, regardless of focus reporting).
            var enterPressed =
                ev.type == EventType.KeyDown
                && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter);
            if (enterPressed)
            {
                if (_input.Length > 0)
                    Submit();
                ev.Use();
            }

            var screenW = Screen.width;
            var screenH = Screen.height;
            var panelH = Mathf.RoundToInt(screenH * 0.33f);
            var rect = new Rect(0, screenH - panelH, screenW, panelH);

            GUI.Box(rect, GUIContent.none);

            const int pad = 8;
            const int lineH = 22;
            const int inputH = 28;
            const int boxBorder = 4;
            var inner = new Rect(
                rect.x + boxBorder,
                rect.y + boxBorder,
                rect.width - boxBorder * 2,
                rect.height - boxBorder * 2
            );
            var outputRect = new Rect(
                inner.x + pad,
                inner.y + pad,
                inner.width - pad * 2,
                inner.height - inputH - pad * 3
            );
            var inputRect = new Rect(
                inner.x + pad,
                inner.yMax - inputH - pad,
                inner.width - pad * 2,
                inputH
            );

            var contentH = _output.Count * lineH;
            _scroll = GUI.BeginScrollView(
                outputRect,
                _scroll,
                new Rect(0, 0, outputRect.width - 16, contentH)
            );
            var y = 0;
            foreach (var line in _output)
            {
                GUI.Label(new Rect(0, y, outputRect.width - 16, lineH), line);
                y += lineH;
            }
            GUI.EndScrollView();

            _scroll.y = float.MaxValue;

            GUI.SetNextControlName(InputControlName);
            _input = GUI.TextField(inputRect, _input);

            if (_focusNextFrame)
            {
                GUI.FocusControl(InputControlName);
                _focusNextFrame = false;
            }
        }

        private void Submit()
        {
            var line = _input;
            _input = "";
            AppendLine($"> {line}");
            CommandRegistry.Execute(line, this);
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            GameContext.ConsoleHasFocus = visible;
            if (visible)
            {
                _focusNextFrame = true;
                TryClearUguiFocus();
            }
        }

        // Best-effort: drop any UGUI EventSystem selection so the game's UI doesn't keep
        // swallowing keyboard/mouse focus away from the IMGUI text field. Reflection avoids
        // a hard reference to UnityEngine.UI; if the assembly isn't loaded this silently no-ops.
        private static void TryClearUguiFocus()
        {
            try
            {
                var esType = System.Type.GetType(
                    "UnityEngine.EventSystems.EventSystem, UnityEngine.UI"
                );
                if (esType == null)
                    return;
                var current = esType.GetProperty("current")?.GetValue(null);
                if (current == null)
                    return;
                esType
                    .GetMethod("SetSelectedGameObject", new[] { typeof(GameObject) })
                    ?.Invoke(current, new object[] { null });
            }
            catch
            {
                /* best-effort */
            }
        }

        public void AppendLine(string line)
        {
            _output.Enqueue(line);
            while (_output.Count > MaxLines)
                _output.Dequeue();
        }

        public void Clear()
        {
            _output.Clear();
        }

        private void OnDestroy()
        {
            GameContext.ConsoleHasFocus = false;
            Log.Info($"{LogPrefix} DevConsoleHost destroyed");
        }
    }
}
