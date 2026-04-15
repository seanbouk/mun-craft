using System;
using MunCraft.Core;
using UnityEngine;

namespace MunCraft.InventorySystem
{
    /// <summary>
    /// IMGUI inventory bar across the top of the screen. One slot per
    /// non-Air block type, in enum order. Empty slots show as dark
    /// squares; once you've collected at least one of a type, the slot
    /// fills with that block's colour. Each slot has a name label
    /// underneath and a count badge in the bottom-right while owned.
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

        BlockType[] _types;
        Texture2D _whitePixel;
        GUIStyle _labelStyle;
        GUIStyle _countStyle;

        void Start()
        {
            BuildTypeList();
            _whitePixel = new Texture2D(1, 1);
            _whitePixel.SetPixel(0, 0, Color.white);
            _whitePixel.Apply();
        }

        void OnDestroy()
        {
            if (_whitePixel != null) Destroy(_whitePixel);
        }

        void BuildTypeList()
        {
            var values = (BlockType[])Enum.GetValues(typeof(BlockType));
            int count = 0;
            foreach (var v in values) if (v != BlockType.Air) count++;
            _types = new BlockType[count];
            int i = 0;
            foreach (var v in values) if (v != BlockType.Air) _types[i++] = v;
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
            if (_types == null || _types.Length == 0) return;
            EnsureStyles();

            int totalWidth = _types.Length * SlotSize + (_types.Length - 1) * SlotGap;
            int startX = (Screen.width - totalWidth) / 2;
            int y = TopMargin;

            for (int i = 0; i < _types.Length; i++)
            {
                int x = startX + i * (SlotSize + SlotGap);
                DrawSlot(x, y, _types[i]);
            }
        }

        void DrawSlot(int x, int y, BlockType type)
        {
            // Border
            DrawSolidRect(new Rect(x, y, SlotSize, SlotSize), BorderColor);

            // Inner fill — block colour if owned, dark grey otherwise
            var inner = new Rect(
                x + BorderWidth,
                y + BorderWidth,
                SlotSize - BorderWidth * 2,
                SlotSize - BorderWidth * 2);

            int count = Inventory != null ? Inventory.GetCount(type) : 0;
            bool owned = count > 0;
            DrawSolidRect(inner, owned ? type.GetColor() : EmptySlotColor);

            // Count badge in bottom-right corner (with subtle shadow for legibility)
            if (owned)
            {
                var countRect = new Rect(x, y, SlotSize, SlotSize);
                DrawTextWithShadow(countRect, count.ToString(), _countStyle);
            }

            // Label underneath
            var labelRect = new Rect(
                x - SlotGap / 2,
                y + SlotSize + LabelGap,
                SlotSize + SlotGap,
                LabelHeight);
            DrawTextWithShadow(labelRect, type.ToString(), _labelStyle);
        }

        void DrawTextWithShadow(Rect rect, string text, GUIStyle style)
        {
            var prevColor = style.normal.textColor;

            // Shadow (offset 1px down-right)
            style.normal.textColor = TextShadowColor;
            var shadowRect = new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height);
            GUI.Label(shadowRect, text, style);

            // Main text
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
