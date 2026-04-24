using System.Collections.Generic;
using MunCraft.Crafting;
using MunCraft.InventorySystem;
using UnityEngine;

namespace MunCraft.UI
{
    /// <summary>
    /// IMGUI rendering of the Machines skill tree inside the right panel.
    /// Blueprint colour scheme. Handles slot interaction, recipe matching,
    /// crafting execution, and error feedback.
    /// </summary>
    public class MachinesMenuUI : MonoBehaviour
    {
        public Inventory Inventory;

        // Blueprint palette — high contrast against the dark panel
        static readonly Color Bg = new Color(0.02f, 0.06f, 0.10f, 1f);
        static readonly Color BgLight = new Color(0.06f, 0.14f, 0.22f, 1f);
        static readonly Color Ink = new Color(0.92f, 0.96f, 1f, 1f);
        static readonly Color InkDim = new Color(0.65f, 0.75f, 0.85f, 1f);
        static readonly Color InkFaint = new Color(0.45f, 0.55f, 0.65f, 1f);
        static readonly Color Accent = new Color(1f, 0.81f, 0.29f, 1f);
        static readonly Color Danger = new Color(1f, 0.36f, 0.38f, 1f);
        static readonly Color SlotEmpty = new Color(0.08f, 0.18f, 0.28f, 1f);
        static readonly Color SlotBorder = new Color(0.75f, 0.89f, 1f, 0.7f);
        static readonly Color Locked = new Color(0.3f, 0.35f, 0.4f, 0.6f);

        // State
        Machine? _pickingMachine;
        int _pickingSlotIndex;
        Machine? _errorMachine;
        float _errorTimer;

        // Cached
        Texture2D _pixel;
        Vector2 _scrollPos;
        GUIStyle _machineNameStyle;
        GUIStyle _smallLabelStyle;
        GUIStyle _slotLabelStyle;
        GUIStyle _btnStyle;
        GUIStyle _pickerItemStyle;

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
        }

        void OnDestroy()
        {
            if (_pixel != null) Destroy(_pixel);
        }

        void Update()
        {
            if (_errorTimer > 0)
                _errorTimer -= Time.unscaledDeltaTime;
        }

        void EnsureStyles()
        {
            if (_machineNameStyle != null) return;

            _machineNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            _machineNameStyle.normal.textColor = Ink;

            _smallLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10, alignment = TextAnchor.MiddleCenter,
            };
            _smallLabelStyle.normal.textColor = InkDim;

            _slotLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9, alignment = TextAnchor.MiddleCenter,
            };
            _slotLabelStyle.normal.textColor = InkDim;

            _btnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _btnStyle.normal.textColor = Ink;

            _pickerItemStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, alignment = TextAnchor.MiddleLeft,
            };
            _pickerItemStyle.normal.textColor = Ink;
        }

        void OnGUI()
        {
            var mgr = SideMenuManager.Instance;
            if (mgr == null || mgr.RightSlide < 0.01f) return;

            var state = CraftingState.Instance;
            if (state == null || Inventory == null) return;

            EnsureStyles();

            // Draw the entire right panel (background, header, close, content)
            // so we control the full rendering order — no IMGUI layering fights.
            var panelRect = mgr.RightPanelRect;
            Solid(panelRect, new Color(0.043f, 0.118f, 0.173f, 0.94f));

            // Header
            float headerH = 48;
            var headerRect = new Rect(panelRect.x, panelRect.y, panelRect.width, headerH);
            _machineNameStyle.fontSize = 18;
            _machineNameStyle.alignment = TextAnchor.MiddleCenter;
            _machineNameStyle.normal.textColor = Ink;
            GUI.Label(headerRect, "MACHINES", _machineNameStyle);
            _machineNameStyle.fontSize = 16;
            _machineNameStyle.alignment = TextAnchor.MiddleLeft;

            // Close button
            float closeSize = 36;
            var closeRect = new Rect(panelRect.x + 8, panelRect.y + 6, closeSize, closeSize);
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
                mgr.CloseRight();
                Event.current.Use();
                return;
            }

            // Content area
            var content = mgr.RightContentRect;
            GUILayout.BeginArea(content);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, false, GUIStyle.none, GUIStyle.none, GUIStyle.none);

            for (int i = 0; i < MachineOrder.Length; i++)
            {
                Machine machine = MachineOrder[i];
                bool unlocked = state.IsUnlocked(machine);

                DrawMachine(machine, state, unlocked);

                // Bridge to next machine
                if (i < MachineOrder.Length - 1)
                    DrawBridge(machine, MachineOrder[i + 1], state);
            }

            GUILayout.Space(20);
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Draw picker overlay on top (outside scroll to avoid clipping)
            if (_pickingMachine.HasValue)
                DrawPickerOverlay(content);
        }

        void DrawMachine(Machine machine, CraftingState state, bool unlocked)
        {
            bool isError = _errorMachine == machine && _errorTimer > 0;
            Color borderCol = isError ? Danger : (unlocked ? SlotBorder : Locked);
            float alpha = unlocked ? 1f : 0.4f;

            // Machine panel background
            GUILayout.BeginVertical();

            // Header
            GUILayout.BeginHorizontal();
            var prevColor = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);

            string label = RecipeDatabase.DisplayName(machine);
            if (!unlocked) label += "  [LOCKED]";
            GUILayout.Label(label, _machineNameStyle, GUILayout.Height(28));

            // Moka pot progress
            if (machine == Machine.MokaPot && !unlocked)
            {
                int prog = state.MokaProgress;
                GUILayout.Label($"{prog}/4", _smallLabelStyle, GUILayout.Width(40), GUILayout.Height(28));
            }

            GUI.color = prevColor;
            GUILayout.EndHorizontal();

            // Divider line
            var divRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            Solid(divRect, new Color(Ink.r, Ink.g, Ink.b, 0.2f * alpha));

            GUILayout.Space(8);

            if (unlocked)
            {
                DrawSlots(machine, state);
                GUILayout.Space(6);
                DrawCraftButton(machine, state);
            }
            else
            {
                GUILayout.Label("Unlock by crafting in the machine above", _smallLabelStyle,
                    GUILayout.Height(40));
            }

            GUILayout.Space(4);

            // Discovered outputs from this machine
            DrawOutputs(machine, state);

            GUILayout.EndVertical();
            GUILayout.Space(8);
        }

        void DrawSlots(Machine machine, CraftingState state)
        {
            int slotCount = RecipeDatabase.SlotCount(machine);
            var slots = state.GetSlots(machine);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (int i = 0; i < slotCount; i++)
            {
                var item = slots[i];
                float slotSize = 64;

                var slotRect = GUILayoutUtility.GetRect(slotSize, slotSize,
                    GUILayout.Width(slotSize), GUILayout.Height(slotSize));

                // Border
                Solid(slotRect, SlotBorder);

                // Inner
                var inner = new Rect(slotRect.x + 2, slotRect.y + 2,
                    slotRect.width - 4, slotRect.height - 4);

                if (item.HasValue)
                {
                    Solid(inner, item.Value.GetColor());
                    // Item name
                    _slotLabelStyle.normal.textColor = Ink;
                    GUI.Label(inner, item.Value.DisplayName(), _slotLabelStyle);
                }
                else
                {
                    Solid(inner, SlotEmpty);
                    _slotLabelStyle.normal.textColor = InkFaint;
                    GUI.Label(inner, "empty", _slotLabelStyle);
                }

                // Click handling (disabled while picker overlay is open)
                if (!_pickingMachine.HasValue
                    && Event.current.type == EventType.MouseDown
                    && Event.current.button == 0
                    && slotRect.Contains(Event.current.mousePosition))
                {
                    if (item.HasValue)
                    {
                        // Return item to inventory
                        Inventory.Add(item.Value);
                        state.SetSlot(machine, i, null);
                        _pickingMachine = null;
                    }
                    else
                    {
                        // Open picker for this slot
                        _pickingMachine = machine;
                        _pickingSlotIndex = i;
                    }
                    Event.current.Use();
                }

                GUILayout.Space(8);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void DrawCraftButton(Machine machine, CraftingState state)
        {
            var filledSlots = state.GetFilledSlots(machine);
            bool allFilled = state.AllSlotsFilled(machine);
            var recipe = allFilled ? RecipeDatabase.FindRecipe(machine, filledSlots) : null;
            bool isReady = recipe.HasValue;
            bool isError = _errorMachine == machine && _errorTimer > 0;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            float btnW = 140, btnH = 36;
            var btnRect = GUILayoutUtility.GetRect(btnW, btnH,
                GUILayout.Width(btnW), GUILayout.Height(btnH));

            Color btnBg = isError ? Danger : (isReady ? Accent : BgLight);
            Color btnFg = isReady ? Bg : Ink;
            Solid(btnRect, btnBg);

            string btnText = isReady ? "EXECUTE" : "RUN";
            _btnStyle.normal.textColor = isError ? Ink : btnFg;
            GUI.Label(btnRect, btnText, _btnStyle);

            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && btnRect.Contains(Event.current.mousePosition))
            {
                if (isReady)
                    ExecuteRecipe(machine, recipe.Value, state);
                else if (allFilled)
                    TriggerError(machine);
                Event.current.Use();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void ExecuteRecipe(Machine machine, Recipe recipe, CraftingState state)
        {
            // Clear slots (inputs are consumed)
            state.ClearSlots(machine);

            switch (recipe.OutputType)
            {
                case RecipeOutputType.Material:
                    Inventory.Add(recipe.Produces);
                    break;

                case RecipeOutputType.Tool:
                    state.AddTool(recipe.Produces);
                    // Also add to inventory so the UI shows it
                    Inventory.Add(recipe.Produces);
                    break;

                case RecipeOutputType.Unlock:
                    state.UnlockMachine(recipe.UnlocksTarget);
                    break;

                case RecipeOutputType.Partial:
                    state.AddMokaProgress();
                    break;

                case RecipeOutputType.Achievement:
                    if (!state.HasAchievement(recipe.AchievementName))
                        state.EarnAchievement(recipe.AchievementName);
                    break;
            }

            _pickingMachine = null;
        }

        void TriggerError(Machine machine)
        {
            _errorMachine = machine;
            _errorTimer = 0.5f;
        }

        void DrawOutputs(Machine machine, CraftingState state)
        {
            // Show recipes for this machine (as discovered hints)
            var recipes = RecipeDatabase.AllRecipes;
            bool any = false;

            GUILayout.BeginHorizontal();
            for (int r = 0; r < recipes.Length; r++)
            {
                if (recipes[r].Machine != machine) continue;
                if (recipes[r].OutputType == RecipeOutputType.Unlock
                    || recipes[r].OutputType == RecipeOutputType.Partial
                    || recipes[r].OutputType == RecipeOutputType.Achievement) continue;

                var item = recipes[r].Produces;
                bool owned = Inventory.Has(item) || state.HasTool(item);
                if (!owned) continue;

                any = true;

                // Small output card
                float cardW = 50, cardH = 50;
                var cardRect = GUILayoutUtility.GetRect(cardW, cardH,
                    GUILayout.Width(cardW), GUILayout.Height(cardH));

                Color cardBg = item.IsTool() ? new Color(Accent.r, Accent.g, Accent.b, 0.3f)
                                             : new Color(Ink.r, Ink.g, Ink.b, 0.1f);
                Solid(cardRect, cardBg);

                var innerCard = new Rect(cardRect.x + 1, cardRect.y + 1,
                    cardRect.width - 2, cardRect.height - 2);
                Solid(innerCard, new Color(Bg.r, Bg.g, Bg.b, 0.8f));

                // Color dot
                var dotRect = new Rect(innerCard.x + innerCard.width / 2 - 8,
                    innerCard.y + 6, 16, 16);
                Solid(dotRect, item.GetColor());

                // Name
                var nameRect = new Rect(innerCard.x, innerCard.y + 24,
                    innerCard.width, innerCard.height - 24);
                var tinyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 7, alignment = TextAnchor.MiddleCenter,
                };
                tinyStyle.normal.textColor = InkDim;
                GUI.Label(nameRect, item.DisplayName(), tinyStyle);

                GUILayout.Space(4);
            }

            // Badges card (achievements for this machine)
            int earned = state.GetAchievementCount(machine);
            int total = RecipeDatabase.AchievementTotal(machine);
            if (earned > 0 && total > 0)
            {
                any = true;
                float cardW = 56, cardH = 50;
                var cardRect = GUILayoutUtility.GetRect(cardW, cardH,
                    GUILayout.Width(cardW), GUILayout.Height(cardH));

                Solid(cardRect, new Color(Accent.r, Accent.g, Accent.b, 0.2f));
                var innerCard = new Rect(cardRect.x + 1, cardRect.y + 1,
                    cardRect.width - 2, cardRect.height - 2);
                Solid(innerCard, new Color(Bg.r, Bg.g, Bg.b, 0.8f));

                var badgeLabel = new GUIStyle(GUI.skin.label)
                    { fontSize = 9, alignment = TextAnchor.MiddleCenter };
                badgeLabel.normal.textColor = Accent;
                var topRect = new Rect(innerCard.x, innerCard.y + 4, innerCard.width, 20);
                GUI.Label(topRect, "Badges", badgeLabel);
                var countRect = new Rect(innerCard.x, innerCard.y + 24, innerCard.width, 20);
                badgeLabel.fontSize = 12;
                badgeLabel.fontStyle = FontStyle.Bold;
                GUI.Label(countRect, $"{earned}/{total}", badgeLabel);
            }

            if (!any)
            {
                GUILayout.Label("", GUILayout.Height(1));
            }

            GUILayout.EndHorizontal();
        }

        void DrawBridge(Machine from, Machine to, CraftingState state)
        {
            GUILayout.Space(4);

            // Simple bridge: vertical line + arrow
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            bool toUnlocked = state.IsUnlocked(to);
            Color bridgeCol = toUnlocked ? Accent : InkFaint;

            string arrow = toUnlocked ? "\u25BC" : "\u25BD"; // ▼ or ▽
            var arrowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, alignment = TextAnchor.MiddleCenter,
            };
            arrowStyle.normal.textColor = bridgeCol;
            GUILayout.Label(arrow, arrowStyle, GUILayout.Height(24));

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
        }

        void DrawPickerOverlay(Rect contentArea)
        {
            if (!_pickingMachine.HasValue) return;

            // Darken background
            Solid(contentArea, new Color(0, 0, 0, 0.5f));

            // Picker list centered in the content area
            float pickerW = 200, maxH = 300;
            float pickerX = contentArea.x + (contentArea.width - pickerW) / 2;
            float pickerY = contentArea.y + (contentArea.height - maxH) / 2;
            var pickerRect = new Rect(pickerX, pickerY, pickerW, maxH);

            Solid(pickerRect, new Color(Bg.r, Bg.g, Bg.b, 0.98f));
            Solid(new Rect(pickerRect.x, pickerRect.y, pickerRect.width, 1), SlotBorder);
            Solid(new Rect(pickerRect.x, pickerRect.yMax - 1, pickerRect.width, 1), SlotBorder);

            // Title
            var titleRect = new Rect(pickerRect.x, pickerRect.y + 4, pickerRect.width, 24);
            _smallLabelStyle.normal.textColor = Accent;
            GUI.Label(titleRect, "SELECT ITEM", _smallLabelStyle);
            _smallLabelStyle.normal.textColor = InkDim;

            // List items with count > 0
            float itemY = pickerRect.y + 32;
            float itemH = 28;
            bool anyItems = false;

            foreach (var kvp in Inventory.AllItems)
            {
                if (kvp.Value <= 0) continue;
                if (kvp.Key.IsTool()) continue; // tools aren't spendable materials
                if (itemY + itemH > pickerRect.yMax - 4) break;

                anyItems = true;
                var itemRect = new Rect(pickerRect.x + 8, itemY, pickerRect.width - 16, itemH);

                // Hover highlight
                bool hover = itemRect.Contains(Event.current.mousePosition);
                if (hover)
                    Solid(itemRect, new Color(1, 1, 1, 0.08f));

                // Color swatch
                var swatchRect = new Rect(itemRect.x + 2, itemRect.y + 4, 20, 20);
                Solid(swatchRect, kvp.Key.GetColor());

                // Name + count
                var labelRect = new Rect(swatchRect.xMax + 8, itemRect.y, itemRect.width - 40, itemH);
                _pickerItemStyle.normal.textColor = hover ? Accent : Ink;
                GUI.Label(labelRect, $"{kvp.Key.DisplayName()} ({kvp.Value})", _pickerItemStyle);

                // Click to select
                if (Event.current.type == EventType.MouseDown
                    && Event.current.button == 0
                    && itemRect.Contains(Event.current.mousePosition))
                {
                    // Deduct from inventory and place in slot
                    if (Inventory.Remove(kvp.Key))
                    {
                        CraftingState.Instance.SetSlot(
                            _pickingMachine.Value, _pickingSlotIndex, kvp.Key);
                    }
                    _pickingMachine = null;
                    Event.current.Use();
                    return;
                }

                itemY += itemH;
            }

            if (!anyItems)
            {
                var emptyRect = new Rect(pickerRect.x, pickerRect.y + 60,
                    pickerRect.width, 24);
                _smallLabelStyle.normal.textColor = InkFaint;
                GUI.Label(emptyRect, "No items in inventory", _smallLabelStyle);
            }

            // Click outside picker to cancel
            if (Event.current.type == EventType.MouseDown
                && !pickerRect.Contains(Event.current.mousePosition))
            {
                _pickingMachine = null;
                Event.current.Use();
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
