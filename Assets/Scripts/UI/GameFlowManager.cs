using MunCraft.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MunCraft.UI
{
    /// <summary>
    /// Single-scene state machine: Title → LevelSelect → Playing.
    /// Draws the title screen and level select UI. During Playing,
    /// draws nothing — existing systems handle gameplay.
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        // Blueprint palette
        static readonly Color Bg = new Color(0.043f, 0.118f, 0.173f, 1f);
        static readonly Color Ink = new Color(0.92f, 0.96f, 1f, 1f);
        static readonly Color InkDim = new Color(0.65f, 0.75f, 0.85f, 1f);
        static readonly Color Accent = new Color(1f, 0.81f, 0.29f, 1f);
        static readonly Color BtnBg = new Color(0.06f, 0.16f, 0.24f, 1f);
        static readonly Color BtnHover = new Color(0.08f, 0.22f, 0.32f, 1f);

        Texture2D _pixel;
        GUIStyle _titleStyle;
        GUIStyle _subtitleStyle;
        GUIStyle _promptStyle;
        GUIStyle _btnStyle;
        GUIStyle _backStyle;
        GUIStyle _mapLabelStyle;
        float _promptPulse;

        public static GameFlowManager Instance { get; private set; }

        void Awake() { Instance = this; }

        void Start()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();

            GameState.CurrentFlow = FlowState.Title;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_pixel != null) Destroy(_pixel);
        }

        void Update()
        {
            _promptPulse += Time.unscaledDeltaTime * 2f;

            if (GameState.CurrentFlow == FlowState.Title)
            {
                var kb = Keyboard.current;
                var mouse = Mouse.current;
                if ((kb != null && kb.anyKey.wasPressedThisFrame) ||
                    (mouse != null && mouse.leftButton.wasPressedThisFrame))
                {
                    GameState.CurrentFlow = FlowState.LevelSelect;
                }
            }
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 64, fontStyle = FontStyle.Normal,
                alignment = TextAnchor.LowerCenter,
            };
            _titleStyle.normal.textColor = InkDim;

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 80, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
            };
            _subtitleStyle.normal.textColor = Ink;

            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, alignment = TextAnchor.MiddleCenter,
            };
            _promptStyle.normal.textColor = InkDim;

            _btnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _btnStyle.normal.textColor = Ink;

            _backStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, alignment = TextAnchor.MiddleLeft,
            };
            _backStyle.normal.textColor = InkDim;

            _mapLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, alignment = TextAnchor.MiddleCenter,
            };
            _mapLabelStyle.normal.textColor = InkDim;
        }

        void OnGUI()
        {
            if (GameState.CurrentFlow == FlowState.Playing) return;

            EnsureStyles();

            // Full-screen background
            Solid(new Rect(0, 0, Screen.width, Screen.height), Bg);

            if (GameState.CurrentFlow == FlowState.Title)
                DrawTitle();
            else if (GameState.CurrentFlow == FlowState.LevelSelect)
                DrawLevelSelect();
        }

        void DrawTitle()
        {
            float cx = Screen.width / 2f;
            float cy = Screen.height / 2f;

            // mün
            var munRect = new Rect(0, cy - 90, Screen.width, 90);
            GUI.Label(munRect, "m\u00FCn", _titleStyle);

            // CRAFT
            var craftRect = new Rect(0, cy - 10, Screen.width, 100);
            GUI.Label(craftRect, "CRAFT", _subtitleStyle);

            // (press any key) — pulsing opacity
            float alpha = 0.3f + 0.4f * (0.5f + 0.5f * Mathf.Sin(_promptPulse));
            _promptStyle.normal.textColor = new Color(InkDim.r, InkDim.g, InkDim.b, alpha);
            var promptRect = new Rect(0, cy + 120, Screen.width, 30);
            GUI.Label(promptRect, "(press any key)", _promptStyle);
        }

        void DrawLevelSelect()
        {
            float cx = Screen.width / 2f;

            // Back button (top-left)
            var backRect = new Rect(30, 30, 100, 36);
            bool backHover = backRect.Contains(Event.current.mousePosition);
            if (backHover)
                Solid(backRect, new Color(1, 1, 1, 0.06f));
            _backStyle.normal.textColor = backHover ? Ink : InkDim;
            GUI.Label(backRect, "\u25C1 Back", _backStyle);
            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && backRect.Contains(Event.current.mousePosition))
            {
                GameState.CurrentFlow = FlowState.Title;
                Event.current.Use();
            }

            // Title
            var headerRect = new Rect(0, 60, Screen.width, 40);
            _btnStyle.fontSize = 24;
            GUI.Label(headerRect, "SELECT MAP", _btnStyle);
            _btnStyle.fontSize = 18;

            // 2×2 grid of map buttons
            float btnW = 200, btnH = 140, gap = 24;
            float gridW = btnW * 2 + gap;
            float gridH = btnH * 2 + gap;
            float startX = cx - gridW / 2;
            float startY = (Screen.height - gridH) / 2 + 20;

            string[] mapNames = { "Verdant", "Frostpeak", "Rustlands", "Deep Core" };

            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 2; col++)
                {
                    int idx = row * 2 + col;
                    float x = startX + col * (btnW + gap);
                    float y = startY + row * (btnH + gap);
                    var rect = new Rect(x, y, btnW, btnH);

                    bool hover = rect.Contains(Event.current.mousePosition);
                    Solid(rect, hover ? BtnHover : BtnBg);

                    // Border
                    float b = 2;
                    Color borderCol = hover ? Accent : new Color(Ink.r, Ink.g, Ink.b, 0.25f);
                    Solid(new Rect(x, y, btnW, b), borderCol);
                    Solid(new Rect(x, y + btnH - b, btnW, b), borderCol);
                    Solid(new Rect(x, y, b, btnH), borderCol);
                    Solid(new Rect(x + btnW - b, y, b, btnH), borderCol);

                    // Map name
                    var nameRect = new Rect(x, y + btnH / 2 - 16, btnW, 32);
                    _btnStyle.normal.textColor = hover ? Accent : Ink;
                    GUI.Label(nameRect, mapNames[idx], _btnStyle);

                    // Subtitle
                    var subRect = new Rect(x, y + btnH / 2 + 16, btnW, 20);
                    _mapLabelStyle.normal.textColor = InkDim;
                    GUI.Label(subRect, $"Map {idx + 1}", _mapLabelStyle);

                    // Click
                    if (Event.current.type == EventType.MouseDown
                        && Event.current.button == 0
                        && rect.Contains(Event.current.mousePosition))
                    {
                        LaunchMap(idx);
                        Event.current.Use();
                    }
                }
            }
        }

        void LaunchMap(int mapId)
        {
            GameState.CurrentFlow = FlowState.Playing;
            var bootstrap = MunCraft.Debug.GameBootstrap.Instance;
            if (bootstrap != null)
                bootstrap.LoadMap(mapId);
        }

        public void GoToLevelSelect()
        {
            var bootstrap = MunCraft.Debug.GameBootstrap.Instance;
            if (bootstrap != null)
                bootstrap.UnloadMap();

            GameState.CurrentFlow = FlowState.LevelSelect;
            GameState.MenuOpen = false;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Solid(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _pixel);
            GUI.color = prev;
        }
    }
}
