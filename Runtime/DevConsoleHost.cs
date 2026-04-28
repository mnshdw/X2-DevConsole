using System.Collections.Generic;
using System.Linq;
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

        private const int FontSize = 14;
        private const int MaxHistory = 100;

        private bool _visible;
        private string _input = "";
        private readonly Queue<string> _output = new();
        private Vector2 _scroll;
        private bool _focusNextFrame;
        private bool _registered;
        private GUIStyle? _labelStyle;
        private GUIStyle? _inputStyle;
        private GUIStyle? _panelStyle;
        private static Texture2D? _panelTex;
        private static Texture2D? _inputTex;

        // History cursor: ranges over [0, _history.Count]. _history.Count means
        // "fresh draft, not navigating yet"; navigating up walks toward 0.
        private readonly List<string> _history = new();
        private int _historyCursor;
        private string _draft = "";
        private bool _moveCaretToEnd;

        private void Awake()
        {
            if (!_registered)
            {
                CommandRegistry.RegisterBuiltins();
                _registered = true;
            }
            // Alt avoids Ctrl (used by free-aim) and Shift (held for sprint).
            AppendLine(
                $"<color=#a0a0a0>DevConsole ready. Press</color> {Key("Alt+G")} <color=#a0a0a0>to toggle,</color> {Key("Esc")} <color=#a0a0a0>to close,</color> {Key("Up/Down")} <color=#a0a0a0>for history. Type</color> {Cmd("help")} <color=#a0a0a0>for commands.</color>"
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
            // Alt+G toggles. Esc closes only.
            if (ev.type == EventType.KeyDown)
            {
                var isToggle = ev.alt && ev.keyCode == ToggleKey;
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

            EnsureStyles();

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

            // Up/Down history. Consume before the TextField sees it (otherwise IMGUI
            // moves the caret line-wise and steals focus).
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.UpArrow)
            {
                HistoryPrev();
                ev.Use();
            }
            else if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.DownArrow)
            {
                HistoryNext();
                ev.Use();
            }

            var screenW = Screen.width;
            var screenH = Screen.height;
            var panelH = Mathf.RoundToInt(screenH * 0.33f);
            var rect = new Rect(0, screenH - panelH, screenW, panelH);

            GUI.Box(rect, GUIContent.none, _panelStyle);

            const int pad = 8;
            const int lineH = 26;
            const int inputH = 32;
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
                GUI.Label(new Rect(0, y, outputRect.width - 16, lineH), line, _labelStyle);
                y += lineH;
            }
            GUI.EndScrollView();

            _scroll.y = float.MaxValue;

            GUI.SetNextControlName(InputControlName);
            _input = GUI.TextField(inputRect, _input, _inputStyle);

            if (_focusNextFrame)
            {
                GUI.FocusControl(InputControlName);
                _focusNextFrame = false;
            }

            if (_moveCaretToEnd && ev.type == EventType.Repaint)
            {
                _moveCaretToEnd = false;
                if (
                    GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl)
                    is TextEditor te
                )
                {
                    te.text = _input;
                    te.MoveTextEnd();
                }
            }
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null)
                return;

            _panelTex ??= MakeTex(new Color(0.05f, 0.05f, 0.05f, 0.72f));
            _inputTex ??= MakeTex(new Color(0.10f, 0.10f, 0.10f, 0.80f));

            Font? gameFont = null;
            try
            {
                var fonts = Resources.FindObjectsOfTypeAll<Font>();
                gameFont =
                    fonts.FirstOrDefault(f => f.name == "LegacyRuntime")
                    ?? fonts.FirstOrDefault(f => f.dynamic)
                    ?? fonts.FirstOrDefault();
            }
            catch
            {
                /* best-effort */
            }

            var textColor = new Color(0.95f, 0.95f, 0.95f);
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = FontSize,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                richText = true,
            };
            _labelStyle.normal.textColor = textColor;

            _inputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = FontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 4, 4),
            };
            _inputStyle.normal.background = _inputTex;
            _inputStyle.focused.background = _inputTex;
            _inputStyle.hover.background = _inputTex;
            _inputStyle.active.background = _inputTex;
            _inputStyle.normal.textColor = textColor;
            _inputStyle.focused.textColor = textColor;

            _panelStyle = new GUIStyle();
            _panelStyle.normal.background = _panelTex;

            if (gameFont != null)
            {
                _labelStyle.font = gameFont;
                _inputStyle.font = gameFont;
            }

            _historyCursor = _history.Count;
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        private void HistoryPrev()
        {
            if (_history.Count == 0)
                return;
            if (_historyCursor == _history.Count)
                _draft = _input;
            if (_historyCursor > 0)
                _historyCursor--;
            _input = _history[_historyCursor];
            _moveCaretToEnd = true;
        }

        private void HistoryNext()
        {
            if (_history.Count == 0)
                return;
            if (_historyCursor >= _history.Count)
                return;
            _historyCursor++;
            _input = _historyCursor == _history.Count ? _draft : _history[_historyCursor];
            _moveCaretToEnd = true;
        }

        private void Submit()
        {
            var line = _input;
            _input = "";
            _draft = "";
            if (_history.Count == 0 || _history[_history.Count - 1] != line)
            {
                _history.Add(line);
                while (_history.Count > MaxHistory)
                    _history.RemoveAt(0);
            }
            _historyCursor = _history.Count;
            AppendLine($"<color=#7ecfff>> {line}</color>");
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
