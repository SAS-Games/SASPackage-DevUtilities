using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SAS.Utilities.Presentation;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.UI;
using UnityEditor.PackageManager;

namespace SAS.Utilities.RemoteDevUtilities.Tests
{
    public sealed class RemoteAssemblyBoundaryTests
    {
        private const string RuntimeRemoteAssemblyGuid = "a891377085b3492d8872e92be37704e7";
        private const string EditorRemoteAssemblyGuid = "b0de41f68f8f40f5a0d4bd685eb7625e";
        private const string CoreAssemblyGuid = "cfbcb856ed571e04480857f2fe81e499";
        private const string SceneInspectorProtocolAssembly = "DevUtilities.RemoteDevUtilities.SceneInspector.Protocol";
        private const string SceneInspectorRuntimeAssembly = "DevUtilities.RemoteDevUtilities.SceneInspector.Runtime";
        private const string SceneInspectorEditorAssembly = "DevUtilities.RemoteDevUtilities.SceneInspector.Editor";
        private const string DebugHostRuntimeAssembly = "DevUtilities.RemoteDevUtilities.DebugHost.Runtime";
        private const string DebugHostEditorAssembly = "DevUtilities.RemoteDevUtilities.DebugHost.Editor";
        private const string DebugHostRuntimeAssemblyGuid = "5865764a68b53d549ab03f428bf6914d";
        private const string PlayerConnectionRuntimeAssembly = "DevUtilities.RemoteDevUtilities.Transport.PlayerConnection.Runtime";
        private const string PlayerConnectionEditorAssembly = "DevUtilities.RemoteDevUtilities.Transport.PlayerConnection.Editor";
        private const string TcpRuntimeAssembly = "DevUtilities.RemoteDevUtilities.Transport.Tcp.Runtime";
        private const string TcpEditorAssembly = "DevUtilities.RemoteDevUtilities.Transport.Tcp.Editor";
        private const string LanDiscoveryRuntimeAssembly = "DevUtilities.RemoteDevUtilities.Transport.LanDiscovery.Runtime";
        private const string LanDiscoveryEditorAssembly = "DevUtilities.RemoteDevUtilities.Transport.LanDiscovery.Editor";
        private static readonly string[] OptionalFeatureAssemblies =
        {
            "DevUtilities.RemoteDevUtilities.Commands.Protocol",
            "DevUtilities.RemoteDevUtilities.Commands.Runtime",
            "DevUtilities.RemoteDevUtilities.Commands.Editor",
            "DevUtilities.RemoteDevUtilities.Logging.Protocol",
            "DevUtilities.RemoteDevUtilities.Logging.Runtime",
            "DevUtilities.RemoteDevUtilities.Logging.Editor",
            "DevUtilities.RemoteDevUtilities.MiniTools.Protocol",
            "DevUtilities.RemoteDevUtilities.MiniTools.Runtime",
            "DevUtilities.RemoteDevUtilities.MiniTools.Editor"
        };

        [Test]
        public void BaseAssemblies_DoNotReferenceOptionalRemoteAssemblies()
        {
            string runtime = ReadAssemblyDefinition("Runtime/DevUtilities.asmdef");
            string editor = ReadAssemblyDefinition("Editor/DevUtilitiesEditor.asmdef");

            Assert.That(runtime, Does.Not.Contain(RuntimeRemoteAssemblyGuid));
            Assert.That(runtime, Does.Not.Contain(EditorRemoteAssemblyGuid));
            Assert.That(editor, Does.Not.Contain(RuntimeRemoteAssemblyGuid));
            Assert.That(editor, Does.Not.Contain(EditorRemoteAssemblyGuid));
        }

        [Test]
        public void RemoteAssemblies_DependOnCoreInOneDirection()
        {
            string runtime = ReadAssemblyDefinition("RemoteDevUtilities/Core/Runtime/DevUtilities.RemoteDevUtilities.asmdef");
            string editor = ReadAssemblyDefinition("RemoteDevUtilities/Core/Editor/DevUtilities.RemoteDevUtilities.Editor.asmdef");

            Assert.That(runtime, Does.Contain(CoreAssemblyGuid));
            Assert.That(editor, Does.Contain(CoreAssemblyGuid));
            Assert.That(editor, Does.Contain(RuntimeRemoteAssemblyGuid));
        }

        [Test]
        public void RemoteCoreAssemblies_DoNotDependOnSceneInspectorModule()
        {
            string runtime = ReadAssemblyDefinition("RemoteDevUtilities/Core/Runtime/DevUtilities.RemoteDevUtilities.asmdef");
            string editor = ReadAssemblyDefinition("RemoteDevUtilities/Core/Editor/DevUtilities.RemoteDevUtilities.Editor.asmdef");

            Assert.That(runtime, Does.Not.Contain(SceneInspectorProtocolAssembly));
            Assert.That(runtime, Does.Not.Contain(SceneInspectorRuntimeAssembly));
            Assert.That(runtime, Does.Not.Contain(SceneInspectorEditorAssembly));
            Assert.That(editor, Does.Not.Contain(SceneInspectorProtocolAssembly));
            Assert.That(editor, Does.Not.Contain(SceneInspectorRuntimeAssembly));
            Assert.That(editor, Does.Not.Contain(SceneInspectorEditorAssembly));
        }

        [Test]
        public void SceneInspectorModule_DependsOnRemoteCoreInOneDirection()
        {
            string protocol = ReadAssemblyDefinition(
                "RemoteDevUtilities/Features/SceneInspector/Shared/DevUtilities.RemoteDevUtilities.SceneInspector.Protocol.asmdef");
            string runtime = ReadAssemblyDefinition(
                "RemoteDevUtilities/Features/SceneInspector/Runtime/DevUtilities.RemoteDevUtilities.SceneInspector.Runtime.asmdef");
            string editor = ReadAssemblyDefinition(
                "RemoteDevUtilities/Features/SceneInspector/Editor/DevUtilities.RemoteDevUtilities.SceneInspector.Editor.asmdef");

            Assert.That(protocol, Does.Contain("\"references\": []"));
            Assert.That(runtime, Does.Contain("\"DevUtilities.RemoteDevUtilities\""));
            Assert.That(runtime, Does.Contain("\"" + SceneInspectorProtocolAssembly + "\""));
            Assert.That(editor, Does.Contain("\"DevUtilities.RemoteDevUtilities.Editor\""));
            Assert.That(editor, Does.Contain("\"" + SceneInspectorRuntimeAssembly + "\""));
        }

        [Test]
        public void RemoteCoreAssemblies_DoNotDependOnDebugHostModule()
        {
            string runtime = ReadAssemblyDefinition("RemoteDevUtilities/Core/Runtime/DevUtilities.RemoteDevUtilities.asmdef");
            string editor = ReadAssemblyDefinition("RemoteDevUtilities/Core/Editor/DevUtilities.RemoteDevUtilities.Editor.asmdef");

            Assert.That(runtime, Does.Not.Contain(DebugHostRuntimeAssembly));
            Assert.That(runtime, Does.Not.Contain(DebugHostEditorAssembly));
            Assert.That(editor, Does.Not.Contain(DebugHostRuntimeAssembly));
            Assert.That(editor, Does.Not.Contain(DebugHostEditorAssembly));
        }

        [Test]
        public void DebugHostModule_DependsOnRemoteCoreInOneDirection()
        {
            string runtime = ReadAssemblyDefinition(
                "RemoteDevUtilities/Features/DebugHost/Runtime/DevUtilities.RemoteDevUtilities.DebugHost.Runtime.asmdef");
            string editor = ReadAssemblyDefinition(
                "RemoteDevUtilities/Features/DebugHost/Editor/DevUtilities.RemoteDevUtilities.DebugHost.Editor.asmdef");

            Assert.That(runtime, Does.Contain(RuntimeRemoteAssemblyGuid));
            Assert.That(editor, Does.Contain(EditorRemoteAssemblyGuid));
            Assert.That(editor, Does.Contain(DebugHostRuntimeAssemblyGuid));
        }

        [Test]
        public void RemoteCoreAssemblies_DoNotDependOnOptionalTransportModules()
        {
            string runtime = ReadAssemblyDefinition("RemoteDevUtilities/Core/Runtime/DevUtilities.RemoteDevUtilities.asmdef");
            string editor = ReadAssemblyDefinition("RemoteDevUtilities/Core/Editor/DevUtilities.RemoteDevUtilities.Editor.asmdef");
            string[] optionalAssemblies =
            {
                PlayerConnectionRuntimeAssembly, PlayerConnectionEditorAssembly,
                TcpRuntimeAssembly, TcpEditorAssembly,
                LanDiscoveryRuntimeAssembly, LanDiscoveryEditorAssembly
            };

            foreach (string optionalAssembly in optionalAssemblies)
            {
                Assert.That(runtime, Does.Not.Contain(optionalAssembly));
                Assert.That(editor, Does.Not.Contain(optionalAssembly));
            }
        }

        [Test]
        public void OptionalTransportModules_DependOnCoreWithoutSiblingDependencies()
        {
            AssertTransportModule("PlayerConnection", PlayerConnectionRuntimeAssembly,
                PlayerConnectionEditorAssembly, TcpRuntimeAssembly, LanDiscoveryRuntimeAssembly);
            AssertTransportModule("Tcp", TcpRuntimeAssembly,
                TcpEditorAssembly, PlayerConnectionRuntimeAssembly, LanDiscoveryRuntimeAssembly);
            AssertTransportModule("LanDiscovery", LanDiscoveryRuntimeAssembly,
                LanDiscoveryEditorAssembly, PlayerConnectionRuntimeAssembly, TcpRuntimeAssembly);
        }

        [Test]
        public void RemoteCoreAssemblies_DoNotDependOnOptionalFeatureModules()
        {
            string runtime = ReadAssemblyDefinition("RemoteDevUtilities/Core/Runtime/DevUtilities.RemoteDevUtilities.asmdef");
            string editor = ReadAssemblyDefinition("RemoteDevUtilities/Core/Editor/DevUtilities.RemoteDevUtilities.Editor.asmdef");

            foreach (string optionalAssembly in OptionalFeatureAssemblies)
            {
                Assert.That(runtime, Does.Not.Contain(optionalAssembly));
                Assert.That(editor, Does.Not.Contain(optionalAssembly));
            }
        }

        [Test]
        public void OptionalFeatureModules_ReferenceCoreWithoutSiblingImplementations()
        {
            AssertFeatureModule("Commands");
            AssertFeatureModule("Logging");
            AssertFeatureModule("MiniTools");
        }

        private static string ReadAssemblyDefinition(string relativePath)
        {
            PackageInfo package = PackageInfo.FindForAssembly(typeof(DevUtilityPresentation).Assembly);
            Assert.That(package?.assetPath, Is.Not.Null.And.Not.Empty);
            string root = package.assetPath.TrimEnd('/');
            return File.ReadAllText(root + "/" + relativePath);
        }

        private static void AssertTransportModule(string folder, string runtimeAssembly,
            string editorAssembly, params string[] forbiddenSiblingAssemblies)
        {
            string runtime = ReadAssemblyDefinition(
                $"RemoteDevUtilities/Features/Transports/{folder}/Runtime/{runtimeAssembly}.asmdef");
            string editor = ReadAssemblyDefinition(
                $"RemoteDevUtilities/Features/Transports/{folder}/Editor/{editorAssembly}.asmdef");

            Assert.That(runtime, Does.Contain("\"DevUtilities.RemoteDevUtilities\""));
            Assert.That(editor, Does.Contain("\"DevUtilities.RemoteDevUtilities.Editor\""));
            Assert.That(editor, Does.Contain("\"" + runtimeAssembly + "\""));
            foreach (string forbidden in forbiddenSiblingAssemblies)
            {
                Assert.That(runtime, Does.Not.Contain(forbidden));
                Assert.That(editor, Does.Not.Contain(forbidden));
            }
        }

        private static void AssertFeatureModule(string folder)
        {
            string prefix = "DevUtilities.RemoteDevUtilities." + folder;
            string protocol = ReadAssemblyDefinition(
                $"RemoteDevUtilities/Features/{folder}/Shared/{prefix}.Protocol.asmdef");
            string runtime = ReadAssemblyDefinition(
                $"RemoteDevUtilities/Features/{folder}/Runtime/{prefix}.Runtime.asmdef");
            string editor = ReadAssemblyDefinition(
                $"RemoteDevUtilities/Features/{folder}/Editor/{prefix}.Editor.asmdef");

            Assert.That(runtime, Does.Contain("\"DevUtilities.RemoteDevUtilities\""));
            Assert.That(runtime, Does.Contain("\"" + prefix + ".Protocol\""));
            Assert.That(editor, Does.Contain("\"DevUtilities.RemoteDevUtilities.Editor\""));
            Assert.That(editor, Does.Contain("\"" + prefix + ".Protocol\""));

            foreach (string sibling in OptionalFeatureAssemblies.Where(name =>
                         !name.StartsWith(prefix + ".", System.StringComparison.Ordinal)))
            {
                Assert.That(protocol, Does.Not.Contain(sibling));
                Assert.That(runtime, Does.Not.Contain(sibling));
                Assert.That(editor, Does.Not.Contain(sibling));
            }
        }
    }

    public sealed class RemoteCompositionRegistryTests
    {
        private sealed class RecordingSession : IRemoteEditorSession
        {
            public bool IsConnected => true;
            public readonly List<string> SentMessageTypes = new();

            public long Send<T>(string messageType, T payload)
            {
                SentMessageTypes.Add(messageType);
                return SentMessageTypes.Count;
            }

            public void NotifyStateChanged()
            {
            }
        }

        [Test]
        public void RuntimeRegistry_DiscoversEndpointsInStableOrder()
        {
            IReadOnlyList<IRuntimeRemoteEndpoint> endpoints = RuntimeRemoteEndpointRegistry.CreateEndpoints();
            try
            {
                int previousOrder = int.MinValue;
                foreach (IRuntimeRemoteEndpoint endpoint in endpoints)
                {
                    RuntimeRemoteEndpointAttribute registration = endpoint.GetType()
                        .GetCustomAttribute<RuntimeRemoteEndpointAttribute>();
                    Assert.That(registration, Is.Not.Null);
                    Assert.That(registration.Order, Is.GreaterThanOrEqualTo(previousOrder));
                    previousOrder = registration.Order;
                }
            }
            finally
            {
                for (int i = endpoints.Count - 1; i >= 0; i--)
                    endpoints[i].Dispose();
            }
        }

        [Test]
        public void EditorRegistry_DiscoversFeaturesInStableOrder()
        {
            var session = new RecordingSession();
            IReadOnlyList<IRemoteEditorFeatureClient> features = RemoteEditorFeatureRegistry.CreateFeatures(session);

            int previousOrder = int.MinValue;
            foreach (IRemoteEditorFeatureClient feature in features)
            {
                RemoteEditorFeatureAttribute registration = feature.GetType()
                    .GetCustomAttribute<RemoteEditorFeatureAttribute>();
                Assert.That(registration, Is.Not.Null);
                Assert.That(registration.Order, Is.GreaterThanOrEqualTo(previousOrder));
                previousOrder = registration.Order;
            }
        }

        [Test]
        public void WorkspaceRegistry_DiscoversPanelsInStableOrder()
        {
            IReadOnlyList<RemoteWorkspacePanelInstance> panels = RemoteWorkspacePanelRegistry.CreatePanels(() => { });
            try
            {
                int previousOrder = int.MinValue;
                foreach (RemoteWorkspacePanelInstance panel in panels)
                {
                    Assert.That(panel.Registration.Order, Is.GreaterThanOrEqualTo(previousOrder));
                    previousOrder = panel.Registration.Order;
                }
            }
            finally
            {
                for (int i = panels.Count - 1; i >= 0; i--)
                    panels[i].Panel.Dispose();
            }
        }
    }
}
