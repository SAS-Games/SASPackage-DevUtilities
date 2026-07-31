using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector
{
    internal sealed class RemoteInspectorView
    {
        private readonly RemoteComponentInspectorView _components = new();
        private readonly RemoteMaterialInspectorView _materials = new();

        public void Draw(RemoteRuntimeSceneInspectorClient client)
        {
            RemoteSceneInspectorInspectResponse inspection = client.Inspection;
            if (inspection == null)
            {
                EditorGUILayout.LabelField("Select a runtime GameObject.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            if (!inspection.Found || inspection.Details == null)
            {
                EditorGUILayout.HelpBox(inspection.Error ?? "The runtime object is unavailable.", MessageType.Warning);
                return;
            }

            DrawObjectHeader(client, inspection.Details);
            _components.Draw(client, inspection.Details.Components);
            _materials.Draw(client, inspection.Details.MaterialsAndShaders);

            if (client.LastCommandResult != null && !string.IsNullOrWhiteSpace(client.LastCommandResult.Message))
            {
                EditorGUILayout.HelpBox(client.LastCommandResult.Message, client.LastCommandResult.Success ? MessageType.Info : MessageType.Error);
            }
        }

        private static void DrawObjectHeader(RemoteRuntimeSceneInspectorClient client, RemoteObjectDetails details)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(details.Name, EditorStyles.boldLabel);
            bool active = EditorGUILayout.Toggle("Active", details.Active);
            if (active != details.Active)
            {
                client.Execute(new RemoteSceneInspectorCommandRequest
                {
                    Kind = RemoteSceneInspectorCommandKind.SetGameObjectActive,
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
