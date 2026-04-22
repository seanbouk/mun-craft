using MunCraft.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MunCraft.UI
{
    /// <summary>
    /// Two slide-in panels (Q = left, E = right). While a panel is open
    /// the game is paused and the mouse cursor is visible. Opening one
    /// panel auto-closes the other.
    ///
    /// The left panel renders its own placeholder content. The right panel
    /// exposes its state (RightSlide, IsRightOpen, RightContentRect) so
    /// MachinesMenuUI can draw inside it.
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
        public Color PanelBackground = new Color(0.043f, 0.118f, 0.173f, 0.92f); // blueprint bg
        public Color HintBackground = new Color(0.12f, 0.12f, 0.15f, 0.85f);
        public Color HintText = new Color(0.85f, 0.85f, 0.90f, 1f);
        public Color CloseButtonColor = new Color(0.91f, 0.96f, 1f, 1f);

        float _leftSlide;
        float _rightSlide;
        bool _leftOpen;
        bool _rightOpen;

        Texture2D _whitePixel;
        GUIStyle _hintStyle;
        GUIStyle _closeStyle;
        GUIStyle _titleStyle;

        // Public state for MachinesMenuUI / GameMenuUI to read
        public bool IsRightOpen => _rightOpen;
        public float RightSlide => _rightSlide;
        public Rect RightPanelRect { get; private set; }
        public Rect RightContentRect { get; private set; }

        public bool IsLeftOpen => _leftOpen;
        public float LeftSlide => _leftSlide;
        public Rect LeftPanelRect { get; private set; }

        public static SideMenuManager Instance { get; private set; }

        void Awake() { Instance = this; }

        void Start()
        {
            _whitePixel = new Texture2D(1, 1);
            _whitePixel.SetPixel(0, 0, Color.white);
            _whitePixel.Apply();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
                if (_leftOpen) CloseAll();
                else OpenLeft();
            }
            else if (kb.eKey.wasPressedThisFrame)
            {
                if (_rightOpen) CloseAll();
                else OpenRight();
            }

            float dt = Time.unscaledDeltaTime * SlideSpeed;
            _leftSlide = Mathf.MoveTowards(_leftSlide, _leftOpen ? 1f : 0f, dt);
            _rightSlide = Mathf.MoveTowards(_rightSlide, _rightOpen ? 1f : 0f, dt);

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

        void OpenLeft() { _leftOpen = true; _rightOpen = false; }
        void OpenRight() { _rightOpen = true; _leftOpen = false; }
        public void CloseAll() { _leftOpen = false; _rightOpen = false; }
        public void CloseLeft() { _leftOpen = false; }
        public void CloseRight() { _rightOpen = false; }

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
            if (_leftSlide > 0.001f) DrawLeftPanel(_leftSlide);
            if (_rightSlide > 0.001f) DrawRightPanelChrome(_rightSlide);
        }

        void DrawHints()
        {
            if (_leftOpen || _rightOpen) return;

            float hintH = 48, margin = 12;

            float lHintW = 100;
            var leftRect = new Rect(margin, 90, lHintW, hintH);
            DrawSolidRect(leftRect, HintBackground);
            GUI.Label(leftRect, "Q \u25C1", _hintStyle);
            var lLabelRect = new Rect(margin, leftRect.yMax + 2, lHintW, 14);
            var lSmallStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 9,
            };
            lSmallStyle.normal.textColor = new Color(0.7f, 0.8f, 0.9f, 0.6f);
            GUI.Label(lLabelRect, "GAME", lSmallStyle);

            // Right hint shows "Machines"
            float rHintW = 100;
            var rightRect = new Rect(Screen.width - rHintW - margin, 90, rHintW, hintH);
            DrawSolidRect(rightRect, HintBackground);
            GUI.Label(rightRect, "\u25B7 E", _hintStyle);
            // Small label below
            var labelRect = new Rect(rightRect.x, rightRect.yMax + 2, rHintW, 14);
            var smallStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
            };
            smallStyle.normal.textColor = new Color(0.7f, 0.8f, 0.9f, 0.6f);
            GUI.Label(labelRect, "MACHINES", smallStyle);
        }

        /// <summary>
        /// Computes the left panel rect but draws NOTHING.
        /// GameMenuUI handles all rendering for the left panel.
        /// </summary>
        void DrawLeftPanel(float slide)
        {
            float panelW = Screen.width * PanelWidthFraction;
            float panelH = Screen.height - PanelTopMargin - PanelBottomMargin;
            float panelY = PanelTopMargin;
            float panelX = Mathf.Lerp(-panelW, 0, slide);

            LeftPanelRect = new Rect(panelX, panelY, panelW, panelH);
        }

        /// <summary>
        /// Draws the right panel background, header, and close button.
        /// Exposes RightPanelRect and RightContentRect so MachinesMenuUI
        /// can draw inside on the same frame.
        /// </summary>
        /// <summary>
        /// Computes the right panel rects but draws NOTHING.
        /// MachinesMenuUI handles all rendering for the right panel
        /// to avoid IMGUI ordering issues.
        /// </summary>
        void DrawRightPanelChrome(float slide)
        {
            float panelW = Screen.width * PanelWidthFraction;
            float panelH = Screen.height - PanelTopMargin - PanelBottomMargin;
            float panelY = PanelTopMargin;
            float targetX = Screen.width - panelW;
            float panelX = Mathf.Lerp(Screen.width, targetX, slide);

            RightPanelRect = new Rect(panelX, panelY, panelW, panelH);

            float headerH = 48;
            float pad = 12;
            RightContentRect = new Rect(
                panelX + pad,
                panelY + headerH + pad,
                panelW - pad * 2,
                panelH - headerH - pad * 2
            );
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
