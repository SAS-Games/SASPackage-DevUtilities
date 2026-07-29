using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeDebugger;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.RuntimeDebugger
{
    internal sealed class RemoteInspectorView
    {
        private readonly RemoteComponentInspectorView _components = new();
        private readonly RemoteMaterialInspectorView _materials = new();
        private Vector2 _scroll;

        public void Draw(RemoteRuntimeDebuggerClient client)
        {
            RemoteDebuggerInspectResponse inspection = client.Inspection;
            if (inspection == null)
            {
                EditorGUILayout.LabelField(
                    "Select a runtime GameObject.",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }

            if (!inspection.Found || inspection.Details == null)
            {
                EditorGUILayout.HelpBox(
                    inspection.Error ?? "The runtime object is unavailable.",
                    MessageType.Warning);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawObjectHeader(client, inspection.Details);
            _components.Draw(client, inspection.Details.Components);
            _materials.Draw(client, inspection.Details.MaterialsAndShaders);

            if (client.LastCommandResult != null &&
                !string.IsNullOrWhiteSpace(client.LastCommandResult.Message))
            {
                EditorGUILayout.HelpBox(
                    client.LastCommandResult.Message,
                    client.LastCommandResult.Success ? MessageType.Info : MessageType.Error);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawObjectHeader(
            RemoteRuntimeDebuggerClient client,
            RemoteObjectDetails details)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(details.Name, EditorStyles.boldLabel);
            bool active = EditorGUILayout.Toggle("Active", details.Active);
            if (active != details.Active)
            {
                client.Execute(new RemoteDebuggerCommandRequest
                {
                    Kind = RemoteDebuggerCommandKind.SetGameObjectActive,
                    ObjectId = details.Id,
                    BooleanValue = active
                });
            }

            EditorGUILayout.LabelField("Tag", details.Tag ?? string.Empty);
            EditorGUILayout.LabelField("Layer", details.Layer.ToString());
            EditorGUILayout.EndVertical();
        }
    }
}
