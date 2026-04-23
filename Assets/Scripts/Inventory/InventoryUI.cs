using System;
using MunCraft.Core;
using MunCraft.Crafting;
using UnityEngine;

namespace MunCraft.InventorySystem
{
    /// <summary>
    /// IMGUI inventory bar across the top of the screen. Shows raw materials
    /// (block types) — one slot per type. Empty slots are dark; once you've
    /// collected at least one, the slot fills with the item's colour.
    /// Labels + count badges.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("Layout")]
        public int SlotSize = 56;
        public int SlotGap = 8;
        public int TopMargin = 16;
        public int BorderWidth = 2;
        public int LabelHeight = 18;
        public int LabelGap = 4;

        [Header("Colours")]
        public Color EmptySlotColor = new Color(0.10f, 0.10f, 0.12f, 0.85f);
        public Color BorderColor = new Color(0.55f, 0.55f, 0.60f, 1f);
        public Color LabelColor = new Color(0.85f, 0.85f, 0.90f, 1f);
        public Color CountColor = Color.white;
        public Color TextShadowColor = new Color(0f, 0f, 0f, 0.85f);

        public Inventory Inventory;

        // Raw material items to display (derived from BlockType, skipping Air)
        CraftingItem[] _rawItems;
        Texture2D _whitePixel;
        GUIStyle _labelStyle;
        GUIStyle _countStyle;

        void Start()
        {
            BuildItemList();
            _whitePixel = new Texture2D(1, 1);
            _whitePixel.SetPixel(0, 0, Color.white);
            _whitePixel.Apply();
        }

        void OnDestroy()
        {
            if (_whitePixel != null) Destroy(_whitePixel);
        }

        void BuildItemList()
        {
            // Show one slot per non-Air BlockType, mapped to CraftingItem
            var blockTypes = (BlockType[])Enum.GetValues(typeof(BlockType));
            int count = 0;
            foreach (var bt in blockTypes) if (bt != BlockType.Air) count++;
            _rawItems = new CraftingItem[count];
            int i = 0;
            foreach (var bt in blockTypes)
                if (bt != BlockType.Air) _rawItems[i++] = bt.ToCraftingItem();
        }

        void EnsureStyles()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    fontStyle = FontStyle.Normal,
                };
                _labelStyle.normal.textColor = LabelColor;
            }

            if (_countStyle == null)
            {
                _countStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.LowerRight,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(0, 4, 0, 2),
                };
                _countStyle.normal.textColor = CountColor;
            }
        }

        void OnGUI()
        {
            if (Core.GameState.CurrentFlow != Core.FlowState.Playing) return;
            if (_rawItems == null || _rawItems.Length == 0) return;
            EnsureStyles();

            int totalWidth = _rawItems.Length * SlotSize + (_rawItems.Length - 1) * SlotGap;
            int startX = (Screen.width - totalWidth) / 2;
            int y = TopMargin;

            for (int i = 0; i < _rawItems.Length; i++)
            {
                int x = startX + i * (SlotSize + SlotGap);
                DrawSlot(x, y, _rawItems[i]);
            }
        }

        void DrawSlot(int x, int y, CraftingItem item)
        {
            DrawSolidRect(new Rect(x, y, SlotSize, SlotSize), BorderColor);

            var inner = new Rect(
                x + BorderWidth,
                y + BorderWidth,
                SlotSize - BorderWidth * 2,
                SlotSize - BorderWidth * 2);

            int count = Inventory != null ? Inventory.GetCount(item) : 0;
            bool owned = count > 0;
            DrawSolidRect(inner, owned ? item.GetColor() : EmptySlotColor);

            if (owned)
            {
                var countRect = new Rect(x, y, SlotSize, SlotSize);
                DrawTextWithShadow(countRect, count.ToString(), _countStyle);
            }

            var labelRect = new Rect(
                x - SlotGap / 2,
                y + SlotSize + LabelGap,
                SlotSize + SlotGap,
                LabelHeight);
            DrawTextWithShadow(labelRect, item.DisplayName(), _labelStyle);
        }

        void DrawTextWithShadow(Rect rect, string text, GUIStyle style)
        {
            var prevColor = style.normal.textColor;

            style.normal.textColor = TextShadowColor;
            var shadowRect = new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height);
            GUI.Label(shadowRect, text, style);

            style.normal.textColor = prevColor;
            GUI.Label(rect, text, style);
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
