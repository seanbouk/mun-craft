using MunCraft.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MunCraft.UI
{
    /// <summary>
    /// Two slide-in panels (Q = left, E = right). While a panel is open
    /// the game is paused and the mouse cursor is visible. Opening one
    /// panel auto-closes the other. Panels have no content yet — just a
    /// dark background, a title area, and a close button.
    /// </summary>
    public class SideMenuManager : MonoBehaviour
    {
        [Header("Layout")]
        [Range(0.2f, 0.5f)]
        public float PanelWidthFraction = 0.33f;
        public float PanelTopMargin = 40f;
        public float PanelBottomMargin = 40f;
        public float SlideSpeed = 6f;

        [Header("Colours")]
        public Color PanelBackground = new Color(0.08f, 0.08f, 0.10f, 0.92f);
        public Color HintBackground = new Color(0.12f, 0.12f, 0.15f, 0.85f);
        public Color HintText = new Color(0.85f, 0.85f, 0.90f, 1f);
        public Color CloseButtonColor = new Color(0.85f, 0.85f, 0.90f, 1f);

        // 0 = fully closed, 1 = fully open
        float _leftSlide;
        float _rightSlide;
        bool _leftOpen;
        bool _rightOpen;

        Texture2D _whitePixel;
        GUIStyle _hintStyle;
        GUIStyle _closeStyle;
        GUIStyle _titleStyle;

        void Start()
        {
            _whitePixel = new Texture2D(1, 1);
            _whitePixel.SetPixel(0, 0, Color.white);
            _whitePixel.Apply();
        }

        void OnDestroy()
        {
            // Restore timeScale if destroyed while open
            if (GameState.MenuOpen)
            {
                Time.timeScale = 1f;
                GameState.MenuOpen = false;
            }
            if (_whitePixel != null) Destroy(_whitePixel);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.qKey.wasPressedThisFrame)
            {
                if (_leftOpen)
                    CloseAll();
                else
                    OpenLeft();
            }
            else if (kb.eKey.wasPressedThisFrame)
            {
                if (_rightOpen)
                    CloseAll();
                else
                    OpenRight();
            }

            // Animate slides using unscaledDeltaTime (timeScale may be 0)
            float dt = Time.unscaledDeltaTime * SlideSpeed;
            _leftSlide = Mathf.MoveTowards(_leftSlide, _leftOpen ? 1f : 0f, dt);
            _rightSlide = Mathf.MoveTowards(_rightSlide, _rightOpen ? 1f : 0f, dt);

            // Update global state
            bool anyOpen = _leftOpen || _rightOpen;
            if (anyOpen && !GameState.MenuOpen)
            {
                GameState.MenuOpen = true;
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (!anyOpen && _leftSlide <= 0f && _rightSlide <= 0f && GameState.MenuOpen)
            {
                GameState.MenuOpen = false;
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void OpenLeft()
        {
            _leftOpen = true;
            _rightOpen = false;
        }

        void OpenRight()
        {
            _rightOpen = true;
            _leftOpen = false;
        }

        void CloseAll()
        {
            _leftOpen = false;
            _rightOpen = false;
        }

        void EnsureStyles()
        {
            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                };
                _hintStyle.normal.textColor = HintText;
            }

            if (_closeStyle == null)
            {
                _closeStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                };
                _closeStyle.normal.textColor = CloseButtonColor;
            }

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                };
                _titleStyle.normal.textColor = HintText;
            }
        }

        void OnGUI()
        {
            EnsureStyles();
            DrawHints();
            if (_leftSlide > 0.001f) DrawPanel(isLeft: true, _leftSlide);
            if (_rightSlide > 0.001f) DrawPanel(isLeft: false, _rightSlide);
        }

        void DrawHints()
        {
            // Don't draw hints while a panel is open — the panel itself is visible
            if (_leftOpen || _rightOpen) return;

            float hintW = 48, hintH = 48;
            float margin = 12;

            // Left hint — top-left, below inventory bar area
            var leftRect = new Rect(margin, 90, hintW, hintH);
            DrawSolidRect(leftRect, HintBackground);
            GUI.Label(leftRect, "Q \u25C1", _hintStyle); // Q ◁

            // Right hint — top-right
            var rightRect = new Rect(Screen.width - hintW - margin, 90, hintW, hintH);
            DrawSolidRect(rightRect, HintBackground);
            GUI.Label(rightRect, "\u25B7 E", _hintStyle); // ▷ E
        }

        void DrawPanel(bool isLeft, float slide)
        {
            float panelW = Screen.width * PanelWidthFraction;
            float panelH = Screen.height - PanelTopMargin - PanelBottomMargin;
            float panelY = PanelTopMargin;

            // Slide from off-screen to on-screen
            float targetX = isLeft ? 0 : Screen.width - panelW;
            float offscreenX = isLeft ? -panelW : Screen.width;
            float panelX = Mathf.Lerp(offscreenX, targetX, slide);

            var panelRect = new Rect(panelX, panelY, panelW, panelH);
            DrawSolidRect(panelRect, PanelBackground);

            // Header bar
            float headerH = 48;
            var headerRect = new Rect(panelX, panelY, panelW, headerH);

            // Title (placeholder)
            string title = isLeft ? "Menu" : "Menu";
            GUI.Label(headerRect, title, _titleStyle);

            // Close button — top corner of panel (right for left panel, left for right panel)
            float closeSize = 36;
            float closeX = isLeft
                ? panelX + panelW - closeSize - 8
                : panelX + 8;
            var closeRect = new Rect(closeX, panelY + 6, closeSize, closeSize);

            // Draw close button background on hover
            if (closeRect.Contains(Event.current.mousePosition))
                DrawSolidRect(closeRect, new Color(1f, 1f, 1f, 0.1f));

            GUI.Label(closeRect, "\u2715", _closeStyle); // ✕

            // Click to close
            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && closeRect.Contains(Event.current.mousePosition))
            {
                CloseAll();
                Event.current.Use();
            }
        }

        void DrawSolidRect(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _whitePixel);
            GUI.color = prev;
        }
    }
}
