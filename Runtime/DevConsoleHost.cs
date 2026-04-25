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
            AppendLine("DevConsole ready. Press Ctrl+G to toggle. Type 'help' for commands.");
        }

        private void Update()
        {
            var ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl && Input.GetKeyDown(ToggleKey))
            {
                _visible = !_visible;
                GameContext.ConsoleHasFocus = _visible;
                if (_visible)
                    _focusNextFrame = true;
                Log.Warn($"{LogPrefix} toggle -> visible={_visible}");
            }
        }

        private void OnGUI()
        {
            GUI.depth = -1000;
            if (!_visible)
                return;

            var ev = Event.current;

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

            // Strip stray backtick that the fallback toggle may leave in the input field.
            if (ev.type == EventType.KeyDown && ev.character == '`')
            {
                _input = _input.TrimEnd('`');
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
            Log.Warn($"{LogPrefix} DevConsoleHost destroyed");
        }
    }
}
