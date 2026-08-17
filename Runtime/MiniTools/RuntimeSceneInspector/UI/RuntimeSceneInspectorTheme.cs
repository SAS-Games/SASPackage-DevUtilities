using System;
using UnityEngine;

namespace HP.Utilities.RuntimeSceneInspector
{
    internal sealed class RuntimeSceneInspectorTheme : IDisposable
    {
        private readonly RuntimeSceneInspectorSettings _settings;

        private Texture2D _windowTexture;
        private Texture2D _panelTexture;
        private Texture2D _toolbarTexture;
        private Texture2D _rowTexture;
        private Texture2D _selectedTexture;
        private Texture2D _componentTexture;
        private Texture2D _summaryTexture;
        private Texture2D _buttonTexture;
        private Texture2D _primaryButtonTexture;
        private Texture2D _successButtonTexture;
        private Texture2D _warningButtonTexture;
        private Texture2D _messageTexture;
        private Texture2D _fieldTexture;

        private bool _created;

        internal Font Font => _settings != null ? _settings.RegularFont : null;
        internal Font BoldFont => _settings != null ? _settings.BoldFont : null;

        internal GUIStyle Window { get; private set; }
        internal GUIStyle Panel { get; private set; }
        internal GUIStyle Toolbar { get; private set; }
        internal GUIStyle Title { get; private set; }
        internal GUIStyle Section { get; private set; }
        internal GUIStyle Body { get; private set; }
        internal GUIStyle Muted { get; private set; }
        internal GUIStyle Badge { get; private set; }
        internal GUIStyle Button { get; private set; }
        internal GUIStyle PrimaryButton { get; private set; }
        internal GUIStyle SuccessButton { get; private set; }
        internal GUIStyle WarningButton { get; private set; }
        internal GUIStyle Row { get; private set; }
        internal GUIStyle SelectedRow { get; private set; }
        internal GUIStyle SceneRow { get; private set; }
        internal GUIStyle InactiveRow { get; private set; }
        internal GUIStyle Component { get; private set; }
        internal GUIStyle Summary { get; private set; }
        internal GUIStyle IconButton { get; private set; }
        internal GUIStyle SearchField { get; private set; }
        internal GUIStyle ValueField { get; private set; }
        internal GUIStyle Message { get; private set; }
        internal GUIStyle Footer { get; private set; }
        internal GUIStyle ResizeHandle { get; private set; }

        internal RuntimeSceneInspectorTheme(RuntimeSceneInspectorSettings settings)
        {
            _settings = settings;
        }

        internal void EnsureCreated()
        {
            if (_created)
                return;

            if (_settings == null)
            {
                Debug.LogWarning("[Runtime Scene Inspector] No RuntimeSceneInspectorSettings asset was assigned. " + "Expected Assets/Resources/RuntimeSceneInspectorSettings.asset.");
            }
            else if (_settings.RegularFont == null)
            {
                Debug.LogWarning("[Runtime Scene Inspector] No regular font has been assigned to RuntimeSceneInspectorSettings. " + "The inspector will fall back to the default GUI font.");
            }

            CreateTextures();
            CreateStyles();

            _created = true;
        }

        private void CreateTextures()
        {
            _windowTexture = CreateTexture(new Color32(18, 21, 28, 245));
            _panelTexture = CreateTexture(new Color32(24, 28, 37, 245));
            _toolbarTexture = CreateTexture(new Color32(29, 34, 45, 245));
            _rowTexture = CreateTexture(new Color32(32, 37, 48, 235));
            _selectedTexture = CreateTexture(new Color32(51, 94, 155, 255));
            _componentTexture = CreateTexture(new Color32(27, 32, 42, 245));
            _summaryTexture = CreateTexture(new Color32(31, 37, 49, 245));
            _buttonTexture = CreateTexture(new Color32(49, 56, 72, 255));
            _primaryButtonTexture = CreateTexture(new Color32(48, 104, 178, 255));
            _successButtonTexture = CreateTexture(new Color32(45, 126, 79, 255));
            _warningButtonTexture = CreateTexture(new Color32(151, 89, 38, 255));
            _messageTexture = CreateTexture(new Color32(117, 47, 47, 255));
            _fieldTexture = CreateTexture(new Color32(15, 18, 24, 255));
        }

        private void CreateStyles()
        {
            Window = CreateRegularStyle(GUI.skin.window);
            Window.normal.background = _windowTexture;
            Window.padding = new RectOffset(14, 14, 14, 14);

            Panel = CreateRegularStyle(GUI.skin.box);
            Panel.normal.background = _panelTexture;
            Panel.padding = new RectOffset(8, 8, 8, 8);

            Toolbar = CreateRegularStyle(GUI.skin.box);
            Toolbar.normal.background = _toolbarTexture;
            Toolbar.padding = new RectOffset(6, 6, 5, 5);

            Title = CreateBoldStyle(GUI.skin.label);
            Title.fontSize = 16;
            Title.normal.textColor = Color.white;

            Section = CreateBoldStyle(GUI.skin.label);
            Section.fontSize = 12;
            Section.normal.textColor = Color.white;
            Section.alignment = TextAnchor.MiddleLeft;

            Body = CreateRegularStyle(GUI.skin.label);
            Body.fontSize = 12;
            Body.normal.textColor = new Color32(230, 233, 239, 255);
            Body.alignment = TextAnchor.MiddleLeft;
            Body.wordWrap = false;

            Muted = CreateRegularStyle(GUI.skin.label);
            Muted.fontSize = 11;
            Muted.normal.textColor = new Color32(157, 165, 181, 255);
            Muted.alignment = TextAnchor.MiddleLeft;
            Muted.wordWrap = false;

            Badge = CreateBoldStyle(GUI.skin.label);
            Badge.fontSize = 10;
            Badge.normal.textColor = new Color32(177, 206, 255, 255);
            Badge.alignment = TextAnchor.MiddleCenter;

            Button = CreateButtonStyle(_buttonTexture);
            PrimaryButton = CreateButtonStyle(_primaryButtonTexture);
            SuccessButton = CreateButtonStyle(_successButtonTexture);
            WarningButton = CreateButtonStyle(_warningButtonTexture);

            Row = CreateRegularButtonStyle(_rowTexture);
            Row.alignment = TextAnchor.MiddleLeft;
            Row.padding = new RectOffset(6, 6, 0, 0);

            SelectedRow = CreateRegularButtonStyle(_selectedTexture);
            SelectedRow.alignment = TextAnchor.MiddleLeft;
            SelectedRow.padding = new RectOffset(6, 6, 0, 0);

            SceneRow = CreateBoldStyle(Row);
            SceneRow.normal.textColor = new Color32(121, 187, 255, 255);

            InactiveRow = new GUIStyle(Row);
            InactiveRow.normal.textColor = new Color32(126, 130, 141, 255);

            Component = CreateRegularStyle(GUI.skin.box);
            Component.normal.background = _componentTexture;
            Component.padding = new RectOffset(5, 5, 5, 5);

            Summary = CreateRegularStyle(GUI.skin.box);
            Summary.normal.background = _summaryTexture;
            Summary.padding = new RectOffset(8, 8, 7, 7);

            IconButton = CreateButtonStyle(_buttonTexture);
            IconButton.alignment = TextAnchor.MiddleCenter;
            IconButton.padding = new RectOffset(0, 0, 0, 0);

            SearchField = CreateTextFieldStyle();
            ValueField = CreateTextFieldStyle();

            Message = CreateRegularStyle(GUI.skin.label);
            Message.normal.background = _messageTexture;
            Message.normal.textColor = new Color32(255, 216, 216, 255);
            Message.padding = new RectOffset(7, 7, 4, 4);
            Message.wordWrap = true;

            Footer = CreateRegularStyle(GUI.skin.label);
            Footer.fontSize = 10;
            Footer.normal.textColor = new Color32(139, 149, 168, 255);
            Footer.alignment = TextAnchor.MiddleLeft;

            ResizeHandle = CreateBoldStyle(GUI.skin.box);
            ResizeHandle.normal.background = _buttonTexture;
            ResizeHandle.normal.textColor = Color.white;
            ResizeHandle.alignment = TextAnchor.MiddleCenter;
            ResizeHandle.padding = new RectOffset(0, 0, 0, 0);
        }

        private GUIStyle CreateButtonStyle(Texture2D background)
        {
            GUIStyle style = CreateBoldStyle(GUI.skin.button);

            style.fontSize = 11;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.background = background;
            style.hover.background = background;
            style.active.background = background;
            style.focused.background = background;

            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.focused.textColor = Color.white;

            return style;
        }

        private GUIStyle CreateRegularButtonStyle(Texture2D background)
        {
            GUIStyle style = CreateRegularStyle(GUI.skin.button);

            style.fontSize = 11;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.background = background;
            style.hover.background = background;
            style.active.background = background;
            style.focused.background = background;

            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.focused.textColor = Color.white;

            return style;
        }

        private GUIStyle CreateTextFieldStyle()
        {
            GUIStyle style = CreateRegularStyle(GUI.skin.textField);

            style.fontSize = 12;
            style.normal.background = _fieldTexture;
            style.focused.background = _fieldTexture;
            style.normal.textColor = Color.white;
            style.focused.textColor = Color.white;
            style.padding = new RectOffset(7, 7, 4, 4);

            return style;
        }

        private GUIStyle CreateRegularStyle(GUIStyle source)
        {
            return new GUIStyle(source)
            {
                font = _settings != null ? _settings.RegularFont : null,
                fontStyle = FontStyle.Normal
            };
        }

        private GUIStyle CreateBoldStyle(GUIStyle source)
        {
            return new GUIStyle(source)
            {
                font = _settings != null ? _settings.BoldFont : null,
                fontStyle = FontStyle.Normal
            };
        }

        private static Texture2D CreateTexture(Color colour)
        {
            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            texture.SetPixel(0, 0, colour);
            texture.Apply(false, true);

            return texture;
        }

        public void Dispose()
        {
            DestroyTexture(_windowTexture);
            DestroyTexture(_panelTexture);
            DestroyTexture(_toolbarTexture);
            DestroyTexture(_rowTexture);
            DestroyTexture(_selectedTexture);
            DestroyTexture(_componentTexture);
            DestroyTexture(_summaryTexture);
            DestroyTexture(_buttonTexture);
            DestroyTexture(_primaryButtonTexture);
            DestroyTexture(_successButtonTexture);
            DestroyTexture(_warningButtonTexture);
            DestroyTexture(_messageTexture);
            DestroyTexture(_fieldTexture);

            _created = false;
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(texture);
            else
                UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
