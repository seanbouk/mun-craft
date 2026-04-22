using MunCraft.Crafting;
using UnityEngine;

namespace MunCraft.UI
{
    /// <summary>
    /// Left panel (Q): "GAME" menu showing earned achievements as emoji
    /// badges, grouped by machine, with counts.
    /// </summary>
    public class GameMenuUI : MonoBehaviour
    {
        // Blueprint palette (same as MachinesMenuUI)
        static readonly Color Bg = new Color(0.02f, 0.06f, 0.10f, 1f);
        static readonly Color Ink = new Color(0.92f, 0.96f, 1f, 1f);
        static readonly Color InkDim = new Color(0.65f, 0.75f, 0.85f, 1f);
        static readonly Color InkFaint = new Color(0.45f, 0.55f, 0.65f, 1f);
        static readonly Color Accent = new Color(1f, 0.81f, 0.29f, 1f);

        Texture2D _pixel;
        Vector2 _scrollPos;
        Font _emojiFont;
        GUIStyle _emojiStyle;
        GUIStyle _headerStyle;
        GUIStyle _countStyle;
        GUIStyle _nameStyle;
        GUIStyle _totalStyle;

        static readonly Machine[] MachineOrder =
        {
            Machine.Hands, Machine.Fire, Machine.Furnace,
            Machine.Forge, Machine.Lathe, Machine.MokaPot
        };

        void Start()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();

            // Try to load an emoji-capable font from the OS
            string[] emojiFontNames = {
                "Segoe UI Emoji",      // Windows
                "Apple Color Emoji",   // macOS / iOS
                "Noto Color Emoji",    // Linux / Android
            };
            foreach (var name in emojiFontNames)
            {
                _emojiFont = Font.CreateDynamicFontFromOSFont(name, 28);
                if (_emojiFont != null) break;
            }
        }

        void OnDestroy()
        {
            if (_pixel != null) Destroy(_pixel);
        }

        void EnsureStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            _headerStyle.normal.textColor = Ink;

            _countStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, alignment = TextAnchor.MiddleRight,
            };
            _countStyle.normal.textColor = Accent;

            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 8, alignment = TextAnchor.MiddleCenter,
            };
            _nameStyle.normal.textColor = InkDim;

            _totalStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _totalStyle.normal.textColor = Accent;

            _emojiStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, alignment = TextAnchor.MiddleCenter,
                contentOffset = new Vector2(0, 1), // push glyph down to centre in row
            };
            if (_emojiFont != null)
                _emojiStyle.font = _emojiFont;
            _emojiStyle.normal.textColor = Ink;
        }

        void OnGUI()
        {
            var mgr = SideMenuManager.Instance;
            if (mgr == null || mgr.LeftSlide < 0.01f) return;

            var state = CraftingState.Instance;
            if (state == null) return;

            EnsureStyles();

            // Draw the full left panel (background, header, close, content)
            var panelRect = mgr.LeftPanelRect;
            Solid(panelRect, new Color(0.043f, 0.118f, 0.173f, 0.94f));

            // Header
            float headerH = 48;
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            titleStyle.normal.textColor = Ink;
            GUI.Label(new Rect(panelRect.x, panelRect.y, panelRect.width, headerH), "GAME", titleStyle);

            // Close button
            float closeSize = 36;
            var closeRect = new Rect(panelRect.x + panelRect.width - closeSize - 8,
                panelRect.y + 6, closeSize, closeSize);
            if (closeRect.Contains(Event.current.mousePosition))
                Solid(closeRect, new Color(1f, 1f, 1f, 0.15f));
            var closeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            closeStyle.normal.textColor = Ink;
            GUI.Label(closeRect, "\u2715", closeStyle);
            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && closeRect.Contains(Event.current.mousePosition))
            {
                mgr.CloseAll();
                Event.current.Use();
                return;
            }

            // Content area
            float pad = 12;
            var content = new Rect(
                panelRect.x + pad,
                panelRect.y + headerH + pad,
                panelRect.width - pad * 2,
                panelRect.height - headerH - pad * 2);

            GUILayout.BeginArea(content);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, false,
                GUIStyle.none, GUIStyle.none, GUIStyle.none);

            // Total count
            int grandTotal = 0;
            int grandEarned = state.TotalAchievements;
            for (int i = 0; i < MachineOrder.Length; i++)
                grandTotal += RecipeDatabase.AchievementTotal(MachineOrder[i]);

            GUILayout.Label($"ACHIEVEMENTS", _totalStyle, GUILayout.Height(30));
            _countStyle.fontSize = 14;
            _countStyle.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label($"{grandEarned} / {grandTotal}", _countStyle, GUILayout.Height(24));
            _countStyle.fontSize = 11;
            _countStyle.alignment = TextAnchor.MiddleRight;

            GUILayout.Space(16);

            // Per-machine sections
            for (int m = 0; m < MachineOrder.Length; m++)
            {
                Machine machine = MachineOrder[m];
                int earned = state.GetAchievementCount(machine);
                int total = RecipeDatabase.AchievementTotal(machine);
                if (total == 0) continue;

                // Machine header
                GUILayout.BeginHorizontal();
                GUILayout.Label(RecipeDatabase.DisplayName(machine), _headerStyle, GUILayout.Height(22));
                GUILayout.Label($"{earned}/{total}", _countStyle, GUILayout.Height(22));
                GUILayout.EndHorizontal();

                // Divider
                var div = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
                Solid(div, new Color(Ink.r, Ink.g, Ink.b, 0.15f));
                GUILayout.Space(6);

                // Achievement grid
                DrawAchievementGrid(machine, state);
                GUILayout.Space(16);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawAchievementGrid(Machine machine, CraftingState state)
        {
            var recipes = RecipeDatabase.AllRecipes;
            float rowH = 28;

            for (int r = 0; r < recipes.Length; r++)
            {
                if (recipes[r].Machine != machine) continue;
                if (recipes[r].OutputType != RecipeOutputType.Achievement) continue;

                bool earned = state.HasAchievement(recipes[r].AchievementName);

                var rowRect = GUILayoutUtility.GetRect(0, rowH, GUILayout.ExpandWidth(true));

                if (earned)
                {
                    // Subtle background
                    Solid(rowRect, new Color(Accent.r, Accent.g, Accent.b, 0.08f));

                    // Emoji (left) — nudged down to vertically centre with the text
                    var emojiRect = new Rect(rowRect.x + 4, rowRect.y, 30, rowH);
                    GUI.Label(emojiRect, recipes[r].AchievementEmoji, _emojiStyle);

                    // Name + recipe
                    string recipe = string.Join(" + ",
                        System.Array.ConvertAll(recipes[r].Inputs, i => i.DisplayName()));
                    string label = $"{recipes[r].AchievementName}  —  {recipe}";

                    var labelRect = new Rect(rowRect.x + 38, rowRect.y, rowRect.width - 42, rowH);
                    _nameStyle.alignment = TextAnchor.MiddleLeft;
                    _nameStyle.fontSize = 10;
                    GUI.Label(labelRect, label, _nameStyle);
                    _nameStyle.alignment = TextAnchor.MiddleCenter;
                    _nameStyle.fontSize = 8;
                }
                else
                {
                    // Unearned — dim row with "?"
                    Solid(rowRect, new Color(Ink.r, Ink.g, Ink.b, 0.03f));

                    var qRect = new Rect(rowRect.x + 4, rowRect.y, 30, rowH);
                    var qStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 14, alignment = TextAnchor.MiddleCenter,
                    };
                    qStyle.normal.textColor = InkFaint;
                    GUI.Label(qRect, "?", qStyle);

                    var hiddenRect = new Rect(rowRect.x + 38, rowRect.y, rowRect.width - 42, rowH);
                    _nameStyle.alignment = TextAnchor.MiddleLeft;
                    _nameStyle.fontSize = 10;
                    _nameStyle.normal.textColor = InkFaint;
                    GUI.Label(hiddenRect, "???", _nameStyle);
                    _nameStyle.normal.textColor = InkDim;
                    _nameStyle.alignment = TextAnchor.MiddleCenter;
                    _nameStyle.fontSize = 8;
                }

                GUILayout.Space(2);
            }
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
