using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration
{
    internal sealed class RemoteMiniToolConfigurationWindow : EditorWindow
    {
        private readonly List<RemoteMiniToolDescriptor> _tools = new();
        private readonly HashSet<string> _availableToolIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _installedToolIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _presentationErrors = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _commandErrors = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _overrideFoldouts = new(StringComparer.OrdinalIgnoreCase);

        private RemoteDevUtilitiesClient _client;
        private Vector2 _scroll;
        private string _search = string.Empty;
        private string _initializationError;

        internal static void Open()
        {
            var window = GetWindow<RemoteMiniToolConfigurationWindow>(true, "Configure Remote Mini Tools", true);
            window.minSize = new Vector2(560f, 440f);
            window.Show();
        }

        private void OnEnable()
        {
            TryAttachClient();
            RemoteMiniToolVisibilitySettings.Changed += Repaint;
            RemoteMiniToolPresentationSettings.Changed += Repaint;
            RemoteCommandPresentationRegistry.Changed += Repaint;
            MiniToolRegistry.Changed += Repaint;
        }

        private void OnDisable()
        {
            RemoteMiniToolVisibilitySettings.Changed -= Repaint;
            RemoteMiniToolPresentationSettings.Changed -= Repaint;
            RemoteCommandPresentationRegistry.Changed -= Repaint;
            MiniToolRegistry.Changed -= Repaint;
            if (_client != null)
                _client.StateChanged -= Repaint;
            _client = null;
        }

        private void OnGUI()
        {
            RemoteMiniToolVisibilitySettings settings = RemoteMiniToolVisibilitySettings.instance;
            RemoteMiniToolPresentationSettings presentationSettings = RemoteMiniToolPresentationSettings.instance;
            RemoteMiniToolCommandSettings commandSettings = RemoteMiniToolCommandSettings.instance;
            if (_client == null)
                TryAttachClient();

            if (_client != null)
                settings.RegisterCatalog(_client.MiniTools.Tools);
            // Installed definitions are the source of truth for build-time
            // configuration. A connected older Player may expose the same
            // tool ID with an earlier descriptor shape.
            settings.RegisterCatalog(RemoteMiniToolEditorDiscovery.Descriptors);

            BuildToolList(settings);
            DrawHeader(settings);
            EditorGUILayout.Space(6f);
            DrawSearch();
            EditorGUILayout.Space(4f);
            DrawToolList(settings, presentationSettings, commandSettings);
            DrawFooter(settings);
        }

        private void DrawHeader(RemoteMiniToolVisibilitySettings settings)
        {
            EditorGUILayout.LabelField("Remote Mini Tool Configuration", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Every installed definition automatically feeds the Player, Native Workspace, Debug Host, and command routing. " + "This window controls visibility and optional project overrides.", EditorStyles.wordWrappedLabel);

            if (!string.IsNullOrWhiteSpace(_initializationError))
                EditorGUILayout.HelpBox(_initializationError, MessageType.Warning);
            else if (_client == null || !_client.IsConnected)
                EditorGUILayout.HelpBox("Installed definitions are available without a Player connection. Connect only to verify a specific build or discover a legacy runtime-only provider.", MessageType.Info);

            foreach (string error in MiniToolRegistry.ValidationErrors)
                EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (string warning in MiniToolRegistry.ValidationWarnings)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);

            EditorGUILayout.Space(5f);
            EditorGUI.BeginChangeCheck();
            bool showNew = EditorGUILayout.ToggleLeft("Automatically show newly discovered mini-tools", settings.Configuration.ShowNewToolsByDefault);
            if (EditorGUI.EndChangeCheck())
            {
                settings.SetShowNewToolsByDefault(showNew);
                UnsubscribeHiddenTools(settings);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Show All", GUILayout.Width(90f)))
                settings.ShowAll();
            if (GUILayout.Button("Hide All", GUILayout.Width(90f)))
            {
                settings.HideAll();
                UnsubscribeHiddenTools(settings);
            }

            if (GUILayout.Button("Reset Visibility", GUILayout.Width(120f)))
            {
                settings.ResetOverrides();
                UnsubscribeHiddenTools(settings);
            }

            if (GUILayout.Button("Register Mini Tool", GUILayout.Width(130f)))
                MiniToolRegistrationWindow.OpenWindow();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSearch()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUIStyle searchStyle = GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField;
            _search = GUILayout.TextField(_search, searchStyle, GUILayout.ExpandWidth(true));
            if (!string.IsNullOrEmpty(_search) && GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(45f)))
                _search = string.Empty;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolList(RemoteMiniToolVisibilitySettings settings, RemoteMiniToolPresentationSettings presentationSettings, RemoteMiniToolCommandSettings commandSettings)
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            int matchingTools = 0;
            foreach (RemoteMiniToolDescriptor descriptor in _tools)
            {
                if (!MatchesSearch(descriptor))
                    continue;
                matchingTools++;
                DrawTool(settings, presentationSettings, commandSettings, descriptor);
            }

            if (matchingTools == 0)
            {
                EditorGUILayout.HelpBox(_tools.Count == 0 ? "No mini-tools have been discovered yet." : "No mini-tools match the current search.", MessageType.None);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTool(RemoteMiniToolVisibilitySettings settings, RemoteMiniToolPresentationSettings presentationSettings, RemoteMiniToolCommandSettings commandSettings, RemoteMiniToolDescriptor descriptor)
        {
            bool available = _availableToolIds.Contains(descriptor.Id);
            bool installed = _installedToolIds.Contains(descriptor.Id);
            bool rememberedOnly = !available && !installed;
            bool visible = settings.IsVisible(descriptor.Id);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(descriptor.DisplayName) ? descriptor.Id : descriptor.DisplayName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(available ? "In Target" : installed ? "Installed" : "Remembered", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(72f));
            bool forget = rememberedOnly && GUILayout.Button(new GUIContent("Forget", "Remove this unavailable mini-tool and its project overrides from the remembered catalog."), EditorStyles.miniButton, GUILayout.Width(72f));
            if (!rememberedOnly && GUILayout.Button(visible ? "Remove" : "Add", EditorStyles.miniButton, GUILayout.Width(72f)))
            {
                settings.SetVisible(descriptor.Id, !visible);
                if (visible)
                    Unsubscribe(descriptor.Id);
            }

            EditorGUILayout.EndHorizontal();

            if (forget)
            {
                Unsubscribe(descriptor.Id);
                settings.Forget(descriptor.Id);
                presentationSettings.ClearOverride(descriptor.Id);
                commandSettings.ClearOverride(descriptor.Id);
                _presentationErrors.Remove(descriptor.Id);
                _commandErrors.Remove(descriptor.Id);
                _overrideFoldouts.Remove(descriptor.Id);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField(descriptor.Id, EditorStyles.miniLabel);
            if (!string.IsNullOrWhiteSpace(descriptor.Description))
                EditorGUILayout.LabelField(descriptor.Description, EditorStyles.wordWrappedMiniLabel);

            if (MiniToolRegistry.TryGet(descriptor.Id, out MiniToolRegistration registration))
            {
                DrawDefinition(registration);
                bool expanded = _overrideFoldouts.TryGetValue(descriptor.Id, out bool stored) && stored;
                bool nextExpanded = EditorGUILayout.Foldout(expanded, "Project Overrides", true);
                _overrideFoldouts[descriptor.Id] = nextExpanded;
                if (nextExpanded)
                {
                    DrawHostPrefab(presentationSettings, descriptor.Id);
                    DrawCommandRouting(commandSettings, descriptor);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Legacy provider: create a Mini Tool Definition to use the unified registration pipeline.", MessageType.Warning);
                DrawHostPrefab(presentationSettings, descriptor.Id);
                DrawCommandRouting(commandSettings, descriptor);
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawDefinition(MiniToolRegistration registration)
        {
            EditorGUILayout.Space(3f);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Definition", registration.Definition, typeof(MiniToolDefinition), false);
            }

            if (GUILayout.Button(registration.IsProjectOwned ? "Edit" : "View", EditorStyles.miniButton, GUILayout.Width(45f)))
            {
                Selection.activeObject = registration.Definition;
                EditorGUIUtility.PingObject(registration.Definition);
            }

            EditorGUILayout.EndHorizontal();

            string commandName = registration.Descriptor.Command?.Name;
            GameObject prefab = registration.LoadDebugHostPrefab();
            RemoteMiniToolCapabilities capabilities = registration.Descriptor.Capabilities;
            string nativeWorkspace = (capabilities & RemoteMiniToolCapabilities.NativeWorkspaceFields) != 0 ? "Fields" : "Not used";
            string debugHost = (capabilities & RemoteMiniToolCapabilities.TypedDebugHostSnapshot) != 0 ? prefab == null ? "Typed snapshot (generic fallback)" : "Typed snapshot" : prefab == null ? "Generic fields" : prefab.name;
            if ((capabilities & RemoteMiniToolCapabilities.EventStream) != 0)
            {
                debugHost += " + live events";
            }

            string summary = $"Native Workspace: {nativeWorkspace}  |  " + $"Debug Host: {debugHost}  |  " + $"Command: {(string.IsNullOrWhiteSpace(commandName) ? "None" : commandName)}";
            EditorGUILayout.LabelField(summary, EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawHostPrefab(RemoteMiniToolPresentationSettings settings, string toolId)
        {
            bool hasOverride = settings.TryGetOverride(toolId, out GameObject overridePrefab);
            bool hasDefault = MiniToolRegistry.TryGetDebugHostPrefab(toolId, out GameObject defaultPrefab);
            GameObject effectivePrefab = hasOverride && overridePrefab != null ? overridePrefab : defaultPrefab;

            EditorGUILayout.Space(2f);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            GameObject selected = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Host Prefab", "Optional prefab instantiated in the Play Mode Debug Host."), effectivePrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (selected == null || selected == defaultPrefab)
                {
                    settings.ClearOverride(toolId);
                    _presentationErrors.Remove(toolId);
                }
                else if (!settings.SetOverride(toolId, selected, out string error))
                {
                    _presentationErrors[toolId] = error;
                }
                else
                {
                    _presentationErrors.Remove(toolId);
                }
            }

            using (new EditorGUI.DisabledScope(!hasOverride))
            {
                if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(52f)))
                {
                    settings.ClearOverride(toolId);
                    _presentationErrors.Remove(toolId);
                }
            }

            EditorGUILayout.EndHorizontal();

            string source = hasOverride ? overridePrefab != null ? "Project override" : "Project override is missing" : hasDefault ? "Package default" : "No Host prefab assigned";
            EditorGUILayout.LabelField(source, EditorStyles.centeredGreyMiniLabel);

            if (_presentationErrors.TryGetValue(toolId, out string errorMessage))
                EditorGUILayout.HelpBox(errorMessage, MessageType.Warning);
        }

        private void DrawCommandRouting(RemoteMiniToolCommandSettings settings, RemoteMiniToolDescriptor descriptor)
        {
            string toolId = descriptor.Id;
            bool hasOverride = settings.TryGetOverride(toolId, out RemoteMiniToolCommandOverride commandOverride);
            bool hasMiniToolDefault = RemoteMiniToolCommandManifestResolver.TryCreateBinding(descriptor, out RemoteCommandPresentationBinding miniToolBinding);
            bool hasPackageDefault = RemoteCommandPresentationRegistry.TryGetDefinitionDefaultForMiniTool(toolId, out RemoteCommandPresentationBinding packageBinding);
            RemoteCommandPresentationBinding defaultBinding = hasMiniToolDefault ? miniToolBinding : hasPackageDefault ? packageBinding : null;

            string commandName = hasOverride ? commandOverride.CommandName : defaultBinding != null ? defaultBinding.CommandName : string.Empty;
            RemoteCommandRouting routing = hasOverride ? commandOverride.Routing : defaultBinding != null ? defaultBinding.Routing : RemoteCommandRouting.ControlEditorToolOnly;

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Command", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Optional command that starts or stops this Editor mini-tool. Leave it empty to disable command control.", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            string selectedCommand = EditorGUILayout.DelayedTextField(new GUIContent("Command Name", "Existing console command name, without its prefix or On/Off argument."), commandName);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyCommandConfiguration(settings, toolId, selectedCommand, routing, defaultBinding);
            }

            using (new EditorGUI.DisabledScope(!hasOverride))
            {
                if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(52f)))
                {
                    settings.ClearOverride(toolId);
                    _commandErrors.Remove(toolId);
                }
            }

            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(commandName)))
            {
                EditorGUI.BeginChangeCheck();
                RemoteCommandRouting selectedRouting = (RemoteCommandRouting)EditorGUILayout.EnumPopup(new GUIContent("When Command Runs", "Choose whether the command executes in the build, controls the Editor tool, or does both."), routing);
                if (EditorGUI.EndChangeCheck())
                {
                    ApplyCommandConfiguration(settings, toolId, commandName, selectedRouting, defaultBinding);
                }
            }

            string source = hasOverride ? string.IsNullOrWhiteSpace(commandOverride.CommandName) ? "Project override: command disabled" : "Project override" : hasMiniToolDefault ? "Mini-tool default" : hasPackageDefault ? "Package default" : "No command assigned";
            EditorGUILayout.LabelField(source, EditorStyles.centeredGreyMiniLabel);

            if (_commandErrors.TryGetValue(toolId, out string errorMessage))
                EditorGUILayout.HelpBox(errorMessage, MessageType.Warning);
        }

        private void ApplyCommandConfiguration(RemoteMiniToolCommandSettings settings, string toolId, string commandName, RemoteCommandRouting routing, RemoteCommandPresentationBinding defaultBinding)
        {
            string normalizedCommandName = commandName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(normalizedCommandName) && defaultBinding == null)
            {
                settings.ClearOverride(toolId);
                _commandErrors.Remove(toolId);
                return;
            }

            if (defaultBinding != null && string.Equals(normalizedCommandName, defaultBinding.CommandName, StringComparison.Ordinal) && routing == defaultBinding.Routing)
            {
                settings.ClearOverride(toolId);
                _commandErrors.Remove(toolId);
                return;
            }

            foreach (RemoteCommandPresentationBinding registration in RemoteCommandPresentationRegistry.GetRegistrations())
            {
                if (string.IsNullOrEmpty(normalizedCommandName) || !string.Equals(registration.CommandName, normalizedCommandName, StringComparison.OrdinalIgnoreCase) || string.Equals(registration.MiniToolId, toolId, StringComparison.OrdinalIgnoreCase))
                    continue;

                _commandErrors[toolId] = $"Command '{normalizedCommandName}' is already assigned to mini-tool " + $"'{registration.MiniToolId}'.";
                return;
            }

            foreach (RemoteMiniToolDescriptor candidate in _tools)
            {
                if (string.IsNullOrEmpty(normalizedCommandName) || string.Equals(candidate?.Id, toolId, StringComparison.OrdinalIgnoreCase) || RemoteMiniToolCommandSettings.instance.TryGetOverride(candidate?.Id, out _) || !RemoteMiniToolCommandManifestResolver.TryCreateBinding(candidate, out RemoteCommandPresentationBinding manifestBinding) || !string.Equals(manifestBinding.CommandName, normalizedCommandName, StringComparison.OrdinalIgnoreCase))
                    continue;

                _commandErrors[toolId] = $"Command '{normalizedCommandName}' is already recommended by mini-tool " + $"'{manifestBinding.MiniToolId}'.";
                return;
            }

            if (!settings.SetOverride(toolId, normalizedCommandName, routing, out string error))
            {
                _commandErrors[toolId] = error;
                return;
            }

            _commandErrors.Remove(toolId);
        }

        private void DrawFooter(RemoteMiniToolVisibilitySettings settings)
        {
            int visible = 0;
            foreach (RemoteMiniToolDescriptor descriptor in _tools)
            {
                if (settings.IsVisible(descriptor.Id))
                    visible++;
            }

            EditorGUILayout.Space(5f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{visible} of {_tools.Count} remembered mini-tools shown", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Done", GUILayout.Width(80f)))
                Close();
            EditorGUILayout.EndHorizontal();
        }

        private void BuildToolList(RemoteMiniToolVisibilitySettings settings)
        {
            _tools.Clear();
            _availableToolIds.Clear();
            _installedToolIds.Clear();
            var rememberedToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (RemoteMiniToolDescriptor descriptor in settings.Configuration.KnownTools)
            {
                if (descriptor != null && !string.IsNullOrWhiteSpace(descriptor.Id))
                {
                    _tools.Add(descriptor);
                    rememberedToolIds.Add(descriptor.Id);
                }
            }

            foreach (RemoteMiniToolDescriptor descriptor in _client?.MiniTools.Tools ?? Array.Empty<RemoteMiniToolDescriptor>())
            {
                if (descriptor != null && !string.IsNullOrWhiteSpace(descriptor.Id))
                    _availableToolIds.Add(descriptor.Id);
            }

            foreach (RemoteMiniToolDescriptor descriptor in RemoteMiniToolEditorDiscovery.Descriptors)
            {
                if (descriptor != null && !string.IsNullOrWhiteSpace(descriptor.Id))
                    _installedToolIds.Add(descriptor.Id);
            }

            foreach (MiniToolRegistration registration in MiniToolRegistry.Registrations)
            {
                AddRememberedTool(registration.Descriptor.Id, rememberedToolIds);
            }

            foreach (RemoteCommandPresentationBinding registration in RemoteCommandPresentationRegistry.GetRegistrations())
            {
                AddRememberedTool(registration.MiniToolId, rememberedToolIds);
            }

            _tools.Sort((left, right) => string.Compare(left.DisplayName ?? left.Id, right.DisplayName ?? right.Id, StringComparison.OrdinalIgnoreCase));
        }

        private bool MatchesSearch(RemoteMiniToolDescriptor descriptor)
        {
            if (string.IsNullOrWhiteSpace(_search))
                return true;
            if (Contains(descriptor.Id, _search) || Contains(descriptor.DisplayName, _search) || Contains(descriptor.Description, _search))
                return true;

            if (RemoteMiniToolCommandSettings.instance.TryGetOverride(descriptor.Id, out RemoteMiniToolCommandOverride commandOverride))
                return Contains(commandOverride.CommandName, _search);

            if (Contains(descriptor.Command?.Name, _search))
                return true;

            return RemoteCommandPresentationRegistry.TryGetDefinitionDefaultForMiniTool(descriptor.Id, out RemoteCommandPresentationBinding defaultBinding) && Contains(defaultBinding.CommandName, _search);
        }

        private void AddRememberedTool(string toolId, ISet<string> rememberedToolIds)
        {
            if (string.IsNullOrWhiteSpace(toolId) || !rememberedToolIds.Add(toolId))
                return;

            _tools.Add(new RemoteMiniToolDescriptor
            {
                Id = toolId,
                DisplayName = toolId,
                Description = "Connect to a Player to load this mini-tool's runtime description."
            });
        }

        private void UnsubscribeHiddenTools(RemoteMiniToolVisibilitySettings settings)
        {
            if (_client == null)
                return;
            foreach (RemoteMiniToolDescriptor descriptor in _client.MiniTools.Tools)
            {
                if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id))
                    continue;
                if (!settings.IsVisible(descriptor.Id))
                    Unsubscribe(descriptor.Id);
            }
        }

        private void Unsubscribe(string toolId)
        {
            if (_client?.MiniTools.IsSubscribed(toolId) == true)
                _client.MiniTools.SetSubscription(toolId, false, 0f);
        }

        private void TryAttachClient()
        {
            if (_client != null)
                return;
            try
            {
                _client = RemoteDevUtilitiesEditorService.Client;
                _client.StateChanged += Repaint;
                _initializationError = null;
            }
            catch (Exception exception)
            {
                _initializationError = exception.GetType().Name + ": " + exception.Message;
            }
        }

        private static bool Contains(string value, string search)
        {
            return !string.IsNullOrWhiteSpace(value) && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
