using System;
using System.Collections.Generic;
using UnityEngine;

namespace SAS.Utilities.RuntimeDebugger
{
    internal sealed class RuntimeDebuggerTheme : IDisposable
    {
        private static readonly Color WindowColor = new(0.055f, 0.065f, 0.085f, 0.86f);
        private static readonly Color CardColor = new(0.09f, 0.11f, 0.145f, 0.72f);
        private static readonly Color TextColor = new(0.92f, 0.95f, 1f);
        private static readonly Color HeaderColor = new(0.72f, 0.78f, 0.88f);
        private static readonly Color MutedColor = new(0.55f, 0.61f, 0.7f);
        private static readonly Color FocusColor = new(0.22f, 0.75f, 1f);
        private static readonly Color SuccessColor = new(0.45f, 0.9f, 0.55f);
        private static readonly Color WarningColor = new(1f, 0.67f, 0.28f);

        private readonly RuntimeDebuggerSettings _settings;
        private readonly List<Texture2D> _textures = new();

        internal RuntimeDebuggerTheme(RuntimeDebuggerSettings settings) => _settings = settings;

        internal Font Font { get; private set; }
        internal GUIStyle Window { get; private set; }
        internal GUIStyle Title { get; private set; }
        internal GUIStyle Section { get; private set; }
        internal GUIStyle Body { get; private set; }
        internal GUIStyle Muted { get; private set; }
        internal GUIStyle Footer { get; private set; }
        internal GUIStyle Badge { get; private set; }
        internal GUIStyle Panel { get; private set; }
        internal GUIStyle Toolbar { get; private set; }
        internal GUIStyle Summary { get; private set; }
        internal GUIStyle SearchField { get; private set; }
        internal GUIStyle ValueField { get; private set; }
        internal GUIStyle Button { get; private set; }
        internal GUIStyle PrimaryButton { get; private set; }
        internal GUIStyle SuccessButton { get; private set; }
        internal GUIStyle WarningButton { get; private set; }
        internal GUIStyle IconButton { get; private set; }
        internal GUIStyle Row { get; private set; }
        internal GUIStyle SelectedRow { get; private set; }
        internal GUIStyle SceneRow { get; private set; }
        internal GUIStyle InactiveRow { get; private set; }
        internal GUIStyle Component { get; private set; }
        internal GUIStyle Message { get; private set; }
        internal GUIStyle ResizeHandle { get; private set; }

        internal void EnsureCreated()
        {
            if (Window != null)
                return;

            Color windowColor = _settings != null ? _settings.BackgroundColor : WindowColor;
            Color focusColor = _settings != null ? _settings.FocusColor : FocusColor;
            Texture2D windowTexture = MakeTexture(windowColor);
            Texture2D cardTexture = MakeTexture(CardColor);
            Texture2D subtleTexture = MakeTexture(Color.Lerp(CardColor, windowColor, 0.38f));
            Texture2D inputTexture = MakeTexture(new Color(0.065f, 0.08f, 0.11f, 1f));
            Texture2D inputFocusTexture = MakeTexture(Color.Lerp(new Color(0.065f, 0.08f, 0.11f, 1f), focusColor, 0.22f));
            Texture2D transparentTexture = MakeTexture(new Color(0f, 0f, 0f, 0f));
            Texture2D hoverTexture = MakeTexture(Color.Lerp(CardColor, focusColor, 0.14f));
            Texture2D selectedTexture = MakeTexture(Color.Lerp(CardColor, focusColor, 0.34f));
            Texture2D selectedHoverTexture = MakeTexture(Color.Lerp(CardColor, focusColor, 0.46f));
            Texture2D primaryTexture = MakeTexture(Color.Lerp(windowColor, focusColor, 0.54f));
            Texture2D primaryHoverTexture = MakeTexture(Color.Lerp(windowColor, focusColor, 0.7f));
            Texture2D primaryActiveTexture = MakeTexture(Color.Lerp(windowColor, focusColor, 0.38f));
            Texture2D successTexture = MakeTexture(Color.Lerp(windowColor, SuccessColor, 0.36f));
            Texture2D successHoverTexture = MakeTexture(Color.Lerp(windowColor, SuccessColor, 0.52f));
            Texture2D warningTexture = MakeTexture(Color.Lerp(windowColor, WarningColor, 0.34f));
            Texture2D warningHoverTexture = MakeTexture(Color.Lerp(windowColor, WarningColor, 0.5f));
            Texture2D messageTexture = MakeTexture(Color.Lerp(windowColor, WarningColor, 0.15f));

            Window = new GUIStyle(GUI.skin.window) { padding = new RectOffset(16, 16, 14, 14), normal = { background = windowTexture } };
            Title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = TextColor } };
            Section = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = HeaderColor } };
            Body = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = TextColor }, clipping = TextClipping.Ellipsis };
            Muted = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = MutedColor }, clipping = TextClipping.Ellipsis };
            Footer = new GUIStyle(Muted) { wordWrap = true };
            
            Badge = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 3, 3), normal = { background = subtleTexture, textColor = HeaderColor }
            };
            
            Panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8), normal = { background = cardTexture } };
            Toolbar = new GUIStyle(Panel) { padding = new RectOffset(10, 10, 6, 6) };
            Summary = new GUIStyle(Panel) { padding = new RectOffset(8, 8, 6, 6), normal = { background = subtleTexture } };
            SearchField = CreateTextFieldStyle(inputTexture, inputFocusTexture, 12);
            ValueField = CreateTextFieldStyle(inputTexture, inputFocusTexture, 11);
            Button = CreateButtonStyle(subtleTexture, hoverTexture, selectedTexture, HeaderColor, 11);
            PrimaryButton = CreateButtonStyle(primaryTexture, primaryHoverTexture, primaryActiveTexture, TextColor, 11);
            SuccessButton = CreateButtonStyle(successTexture, successHoverTexture, selectedTexture, TextColor, 10);
            WarningButton = CreateButtonStyle(warningTexture, warningHoverTexture, selectedTexture, TextColor, 10);
            IconButton = CreateButtonStyle(subtleTexture, hoverTexture, selectedTexture, HeaderColor, 12);
            IconButton.padding = new RectOffset(2, 2, 1, 1);
            
            Row = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, padding = new RectOffset(6, 6, 2, 2), clipping = TextClipping.Ellipsis,
                normal = { background = transparentTexture, textColor = TextColor },
                hover = { background = hoverTexture, textColor = TextColor },
                active = { background = selectedTexture, textColor = TextColor },
                focused = { background = hoverTexture, textColor = TextColor }
            };
            SelectedRow = new GUIStyle(Row)
            {
                fontStyle = FontStyle.Bold, normal = { background = selectedTexture, textColor = TextColor },
                hover = { background = selectedHoverTexture, textColor = TextColor },
                active = { background = primaryActiveTexture, textColor = TextColor },
                focused = { background = selectedTexture, textColor = TextColor }
            };
            
            SceneRow = new GUIStyle(Row) { fontStyle = FontStyle.Bold, normal = { background = transparentTexture, textColor = HeaderColor } };
            InactiveRow = new GUIStyle(Row) { normal = { background = transparentTexture, textColor = MutedColor } };
            
            Component = new GUIStyle(Panel)
            {
                padding = new RectOffset(6, 6, 4, 4), margin = new RectOffset(0, 0, 0, 2),
                normal = { background = subtleTexture }
            };
            Message = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10, wordWrap = true, padding = new RectOffset(8, 8, 5, 5),
                normal = { background = messageTexture, textColor = WarningColor }
            };
            ResizeHandle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleCenter,
                normal = { background = subtleTexture, textColor = MutedColor }
            };

            Font = GUI.skin.label.font != null ? GUI.skin.label.font : GUI.skin.font;
            ApplyFont();
        }

        public void Dispose()
        {
            foreach (Texture2D texture in _textures)
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);

            _textures.Clear();
            Window = null;
            Font = null;
        }

        private void ApplyFont()
        {
            if (Font == null)
                return;

            GUIStyle[] styles =
            {
                Window, Title, Section, Body, Muted, Footer, Badge, Panel, Toolbar, Summary, SearchField, ValueField,
                Button, PrimaryButton, SuccessButton, WarningButton, IconButton, Row, SelectedRow, SceneRow,
                InactiveRow, Component, Message, ResizeHandle
            };

            foreach (GUIStyle style in styles)
                style.font = Font;
        }

        private static GUIStyle CreateTextFieldStyle(Texture2D normalTexture, Texture2D focusedTexture, int fontSize)
        {
            return new GUIStyle(GUI.skin.textField)
            {
                fontSize = fontSize,
                padding = new RectOffset(8, 8, 5, 5),
                normal = { background = normalTexture, textColor = TextColor },
                hover = { background = focusedTexture, textColor = TextColor },
                active = { background = focusedTexture, textColor = TextColor },
                focused = { background = focusedTexture, textColor = TextColor }
            };
        }

        private static GUIStyle CreateButtonStyle(Texture2D normalTexture, Texture2D hoverTexture, Texture2D activeTexture, Color textColor, int fontSize)
        {
            return new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 4, 4),
                normal = { background = normalTexture, textColor = textColor },
                hover = { background = hoverTexture, textColor = TextColor },
                active = { background = activeTexture, textColor = TextColor },
                focused = { background = hoverTexture, textColor = TextColor }
            };
        }

        private Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            
            texture.SetPixel(0, 0, color);
            texture.Apply();
            _textures.Add(texture);
            return texture;
        }
    }
}
