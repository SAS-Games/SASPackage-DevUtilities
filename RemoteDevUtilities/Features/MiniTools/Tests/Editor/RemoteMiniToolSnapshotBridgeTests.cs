using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SAS.DevUtilities;
using SAS.DevUtilities.Stats;
using SAS.Utilities.DeveloperConsole;
using SAS.Utilities.RemoteDevUtilities.Editor.DebugHost.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Scaffolding;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using SAS.Utilities.RemoteDevUtilities.MiniTools.Providers;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SAS.Utilities.RemoteDevUtilities.DebugHost.Tests
{
    public sealed class RemoteMiniToolSnapshotBridgeTests
    {
        private const string PackageRoot = "Packages/com.sas.dev-utilities/";

        [Test]
        public void CreationWizard_DiscoversSnapshotContractFromExistingPrefab()
        {
            var prefabRoot = new GameObject("GameInfoHostPrefab");
            try
            {
                prefabRoot.AddComponent<GameInfoComponent>();

                Type[] snapshotTypes = MiniToolSnapshotContractDiscovery.Find(prefabRoot);

                Assert.That(snapshotTypes, Is.EqualTo(new[] { typeof(GameInfoSnapshot) }));
                Assert.That(MiniToolSnapshotContractDiscovery.HasCompatibleSnapshot(typeof(SnapshotOnlyGameInfoProvider), snapshotTypes), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(prefabRoot);
            }
        }

        [Test]
        public void CreationWizard_GeneratesHostOnlySnapshotProvider()
        {
            string source = MiniToolProviderTemplateGenerator.CreateSnapshotProvider("GameInfoRemoteDataProvider", typeof(GameInfoSnapshot));

            StringAssert.Contains($"using {typeof(MiniToolDataProvider<>).Namespace};", source);
            StringAssert.Contains($"using {typeof(GameInfoSnapshot).Namespace};", source);
            StringAssert.Contains("MiniToolDataProvider<GameInfoSnapshot>", source);
            StringAssert.Contains("TryGetSnapshot", source);
            StringAssert.DoesNotContain("CaptureFields", source);
            StringAssert.DoesNotContain("IMiniToolFieldProvider", source);
            StringAssert.DoesNotContain("global::", source);
        }

        [Test]
        public void CreationWizard_GeneratesFieldProviderUsingSharedFactory()
        {
            string source = MiniToolProviderTemplateGenerator.CreateFieldProvider("NetworkFieldProvider");

            StringAssert.Contains($"using {typeof(MiniToolFieldDataProvider).Namespace};", source);
            StringAssert.Contains($"using {typeof(RemoteMiniToolField).Namespace};", source);
            StringAssert.Contains("NetworkFieldProvider : MiniToolFieldDataProvider", source);
            StringAssert.Contains("RemoteMiniToolField[] CaptureFields()", source);
            StringAssert.Contains("CreateField(\"status\", \"Status\", \"Running\")", source);
            StringAssert.DoesNotContain("new RemoteMiniToolField", source);
            StringAssert.DoesNotContain("global::", source);
        }

        [Test]
        public void ScaffoldGenerator_CreatesOneSharedCollectorPipeline()
        {
            var request = new MiniToolScaffoldRequest
            {
                ToolName = "Network Monitor",
                Namespace = "Project.DebugTools",
                OutputFolder = "Assets/DebugTools",
                CreateSubfolder = true,
                CreateCommand = true,
                UpdateInterval = 0.25f
            };
            MiniToolScaffoldState state = MiniToolScaffoldGenerator.CreateState(request);

            string localProvider = MiniToolScaffoldTemplateRenderer.Render(MiniToolScaffoldTemplate.SnapshotProvider, state);
            string remoteProvider = MiniToolScaffoldTemplateRenderer.Render(MiniToolScaffoldTemplate.DataProvider, state);
            string view = MiniToolScaffoldTemplateRenderer.Render(MiniToolScaffoldTemplate.View, state);

            Assert.That(state.ClassName, Is.EqualTo("NetworkMonitor"));
            Assert.That(state.TargetFolder, Is.EqualTo("Assets/DebugTools/NetworkMonitor"));
            StringAssert.Contains("NetworkMonitorCollector.Capture()", localProvider);
            StringAssert.Contains("NetworkMonitorCollector.Capture()", remoteProvider);
            StringAssert.Contains("IMiniToolSnapshotView<NetworkMonitorSnapshot>", view);
            StringAssert.DoesNotContain("{{", localProvider + remoteProvider + view);
        }

        [Test]
        public void ScaffoldTemplates_ResolveCurrentContractNamespaces()
        {
            var request = new MiniToolScaffoldRequest
            {
                ToolName = "Runtime State",
                Namespace = "Project.DebugTools",
                OutputFolder = "Assets"
            };
            MiniToolScaffoldState state = MiniToolScaffoldGenerator.CreateState(request);

            string snapshot = MiniToolScaffoldTemplateRenderer.Render(MiniToolScaffoldTemplate.Snapshot, state);
            string provider = MiniToolScaffoldTemplateRenderer.Render(MiniToolScaffoldTemplate.DataProvider, state);

            StringAssert.Contains($"using {typeof(IMiniToolSnapshot).Namespace};", snapshot);
            StringAssert.Contains($"using {typeof(MiniToolDataProvider<>).Namespace};", provider);
            StringAssert.DoesNotContain("{{CONTRACT_NAMESPACE}}", snapshot);
            StringAssert.DoesNotContain("{{REMOTE_PROVIDER_NAMESPACE}}", provider);
        }

        [TestCase("Assets/DebugTools", "Project.DebugTools", true)]
        [TestCase("Packages/DebugTools", "Project.DebugTools", false)]
        [TestCase("Assets/Editor/DebugTools", "Project.DebugTools", false)]
        [TestCase("Assets/DebugTools", "Invalid Namespace", false)]
        public void ScaffoldRequest_ValidatesRuntimeProjectLocationAndNamespace(string folder, string toolNamespace, bool expected)
        {
            var request = new MiniToolScaffoldRequest
            {
                ToolName = "Test Tool",
                Namespace = toolNamespace,
                OutputFolder = folder,
                UpdateInterval = 0.5f
            };

            Assert.That(request.TryValidate(out _), Is.EqualTo(expected));
        }

        [Test]
        public void MiniToolDataProvider_CreateFieldNormalizesOptionalText()
        {
            RemoteMiniToolField field = TestFieldFactoryProvider.BuildField("status", "Status", null, null);

            Assert.That(field.Name, Is.EqualTo("status"));
            Assert.That(field.DisplayName, Is.EqualTo("Status"));
            Assert.That(field.Value, Is.Empty);
            Assert.That(field.Unit, Is.Empty);
        }

        [Test]
        public void SnapshotContracts_DoNotRequireDuplicatedMiniToolIdentity()
        {
            Assert.That(ImplementsLegacyIdentity(typeof(IMiniToolSnapshotProvider<GameInfoSnapshot>)), Is.False);
            Assert.That(ImplementsLegacyIdentity(typeof(GameInfoSnapshotProvider)), Is.False);
            Assert.That(ImplementsLegacyIdentity(typeof(GameInfoComponent)), Is.False);
            Assert.That(ImplementsLegacyIdentity(typeof(RuntimeGameInfoMiniToolProvider)), Is.False);
        }

        [Test]
        public void TypedSnapshotOnlyProvider_DoesNotRequireNativeWorkspaceFields()
        {
            var definition = CreateDefinitionForProvider<SnapshotOnlyGameInfoProvider>();
            try
            {
                Assert.That(definition.TryValidate(out string error), Is.True, error);

                RemoteMiniToolDescriptor descriptor = definition.CreateDescriptor();
                Assert.That(descriptor.Capabilities, Is.EqualTo(RemoteMiniToolCapabilities.TypedDebugHostSnapshot));

                var registration = new MiniToolProviderRegistration(descriptor, new SnapshotOnlyGameInfoProvider());
                try
                {
                    RemoteMiniToolSample sample = registration.Capture(RemoteMiniToolDataChannels.TypedSnapshot | RemoteMiniToolDataChannels.NativeWorkspaceFields);
                    Assert.That(sample.SnapshotJson, Is.Not.Empty);
                    Assert.That(sample.Fields, Is.Empty);
                }
                finally
                {
                    registration.Dispose();
                }
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Registration_CapturesOnlyRequestedDataChannels()
        {
            var provider = new CountingDataChannelProvider();
            var registration = new MiniToolProviderRegistration(new RemoteMiniToolDescriptor
            {
                Id = "tests.data-channels"
            }, provider);

            try
            {
                RemoteMiniToolSample snapshotSample = registration.Capture(RemoteMiniToolDataChannels.TypedSnapshot);
                Assert.That(snapshotSample.SnapshotJson, Is.Not.Empty);
                Assert.That(snapshotSample.Fields, Is.Empty);
                Assert.That(provider.SnapshotCaptures, Is.EqualTo(1));
                Assert.That(provider.FieldCaptures, Is.Zero);

                RemoteMiniToolSample fieldSample = registration.Capture(RemoteMiniToolDataChannels.NativeWorkspaceFields);
                Assert.That(fieldSample.SnapshotJson, Is.Null.Or.Empty);
                Assert.That(fieldSample.Fields, Has.Length.EqualTo(1));
                Assert.That(provider.SnapshotCaptures, Is.EqualTo(1));
                Assert.That(provider.FieldCaptures, Is.EqualTo(1));
            }
            finally
            {
                registration.Dispose();
            }
        }

        [Test]
        public void StreamingProvider_AddsCapabilityWithoutChangingSnapshotProviders()
        {
            var streamingDefinition = CreateDefinitionForProvider<TestStreamingProvider>();
            var snapshotDefinition = CreateDefinitionForProvider<SnapshotOnlyGameInfoProvider>();
            try
            {
                Assert.That(streamingDefinition.CreateDescriptor().Capabilities, Is.EqualTo(RemoteMiniToolCapabilities.TypedDebugHostSnapshot | RemoteMiniToolCapabilities.EventStream));
                Assert.That(snapshotDefinition.CreateDescriptor().Capabilities, Is.EqualTo(RemoteMiniToolCapabilities.TypedDebugHostSnapshot));
            }
            finally
            {
                Object.DestroyImmediate(streamingDefinition);
                Object.DestroyImmediate(snapshotDefinition);
            }
        }

        [Test]
        public void StreamingProvider_CapturesAndAppliesTypedBatch()
        {
            var registration = new MiniToolProviderRegistration(new RemoteMiniToolDescriptor
            {
                Id = "tests.stream"
            }, new TestStreamingProvider());
            try
            {
                RemoteMiniToolStreamBatch batch = registration.CaptureStream();
                Assert.That(batch, Is.Not.Null);
                Assert.That(batch.Sequence, Is.EqualTo(1));
                Assert.That(batch.EventTypeName, Is.EqualTo(typeof(TestStreamEvent).AssemblyQualifiedName));

                var receiver = new TestStreamReceiver();
                var bridge = new RemoteMiniToolStreamView<TestStreamEvent>(receiver);
                Assert.That(bridge.TryApply(batch), Is.True);
                Assert.That(receiver.LastValue, Is.EqualTo(17));
                Assert.That(receiver.DroppedEventCount, Is.EqualTo(2));
            }
            finally
            {
                registration.Dispose();
            }
        }

        [Test]
        public void ActionProvider_AdvertisesAndExecutesRegisteredActions()
        {
            var provider = new TestActionProvider();
            var descriptor = new RemoteMiniToolDescriptor
            {
                Id = "tests.actions"
            };
            var registration = new MiniToolProviderRegistration(descriptor, provider);
            try
            {
                Assert.That(registration.SupportsActions, Is.True);
                Assert.That(descriptor.Capabilities & RemoteMiniToolCapabilities.Actions, Is.EqualTo(RemoteMiniToolCapabilities.Actions));
                Assert.That(descriptor.Actions, Has.Length.EqualTo(2));
                Assert.That(descriptor.Actions[0].Id, Is.EqualTo("pause"));
                Assert.That(registration.TryExecuteAction("pause", out string error), Is.True, error);
                Assert.That(provider.LastAction, Is.EqualTo("pause"));
                Assert.That(registration.TryExecuteAction("missing", out error), Is.False);
                Assert.That(error, Is.Not.Empty);
            }
            finally
            {
                registration.Dispose();
            }
        }

        [Test]
        public void FrameStepper_ExposesTwoNativeWorkspaceControls()
        {
            var provider = new RuntimeFrameStepperMiniToolProvider();
            RemoteMiniToolActionDescriptor[] actions = provider.GetActions();
            var visibleActions = new List<RemoteMiniToolActionDescriptor>();
            foreach (RemoteMiniToolActionDescriptor action in actions)
            {
                if (!action.HideInNativeWorkspace)
                    visibleActions.Add(action);
            }

            Assert.That(actions, Has.Length.EqualTo(2));
            Assert.That(visibleActions, Has.Count.EqualTo(2));
            Assert.That(visibleActions[0].Id, Is.EqualTo("toggle"));
            Assert.That(visibleActions[0].DisplayName, Is.EqualTo("Play / Pause"));
            Assert.That(visibleActions[1].Id, Is.EqualTo("step"));
            Assert.That(visibleActions[1].DisplayName, Is.EqualTo("Step"));
        }

        [Test]
        public void Definition_RejectsProviderWithNoPresentationData()
        {
            var definition = CreateDefinitionForProvider<NoOutputProvider>();
            try
            {
                Assert.That(definition.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("exposes no mini-tool data"));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void PresentationValidation_RejectsMismatchedTypedView()
        {
            var definition = CreateDefinitionForProvider<SnapshotOnlyGameInfoProvider>();
            var prefab = new GameObject("Mismatched Host");
            try
            {
                prefab.AddComponent<FPS>();

                Assert.That(MiniToolRegistrationValidator.TryValidatePrefab(definition, prefab, out string error), Is.False);
                Assert.That(error, Does.Contain($"IMiniToolSnapshotView<{typeof(GameInfoSnapshot).FullName}>"));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void PresentationValidation_RequiresRootPresentationComponent()
        {
            var definition = CreateDefinitionForProvider<SnapshotOnlyGameInfoProvider>();
            var prefab = new GameObject("Host Without Presentation");
            try
            {
                prefab.AddComponent<GameInfoComponent>();

                Assert.That(MiniToolRegistrationValidator.TryValidatePrefab(definition, prefab, out string error), Is.False);
                Assert.That(error, Does.Contain("must have DevUtilityPresentation on its root"));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void PresentationValidation_RequiresPrefabForSnapshotOnlyProvider()
        {
            var definition = CreateDefinitionForProvider<SnapshotOnlyGameInfoProvider>();
            try
            {
                var errors = new List<string>();
                var warnings = new List<string>();
                MiniToolRegistrationValidator.Validate(definition, "Assets/SnapshotOnly.asset", errors, warnings);

                Assert.That(string.Join("\n", errors), Does.Contain("no Debug Host prefab is assigned"));
                Assert.That(warnings, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void PresentationValidation_WarnsWhenTypedSnapshotUsesFieldFallback()
        {
            var definition = CreateDefinitionForProvider<RuntimeGameInfoMiniToolProvider>();
            try
            {
                var errors = new List<string>();
                var warnings = new List<string>();
                MiniToolRegistrationValidator.Validate(definition, "Assets/SnapshotAndFields.asset", errors, warnings);

                Assert.That(errors, Is.Empty);
                Assert.That(string.Join("\n", warnings), Does.Contain("generic Native Workspace fields"));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void GameInfoPrefab_SeparatesProviderViewAndController()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackageRoot + "Runtime/MiniTools/GameInfo/Assets/GameInfo.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<GameInfoSnapshotProvider>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<GameInfoComponent>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<GameInfoLocalController>(), Is.Not.Null);
            Assert.That(typeof(IMiniToolSnapshotProvider<GameInfoSnapshot>).IsAssignableFrom(typeof(GameInfoComponent)), Is.False);
            Assert.That(typeof(IMiniToolSnapshotView<GameInfoSnapshot>).IsAssignableFrom(typeof(GameInfoSnapshotProvider)), Is.False);
        }

        [Test]
        public void PerformancePrefab_SeparatesProvidersViewsAndControllers()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackageRoot + "Runtime/MiniTools/FPS/Assets/FPS.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<StatsSnapshotProvider>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<SAS.DevUtilities.Stats.Stats>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<StatsLocalController>(), Is.Not.Null);
            Assert.That(typeof(IMiniToolSnapshotProvider<StatsSnapshot>).IsAssignableFrom(typeof(SAS.DevUtilities.Stats.Stats)), Is.False);
            Assert.That(typeof(IMiniToolSnapshotView<StatsSnapshot>).IsAssignableFrom(typeof(StatsSnapshotProvider)), Is.False);

            Assert.That(prefab.GetComponent<FPSSnapshotProvider>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<FPS>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<FPSLocalController>(), Is.Not.Null);
            Assert.That(typeof(IMiniToolSnapshotProvider<FPSSnapshot>).IsAssignableFrom(typeof(FPS)), Is.False);
            Assert.That(typeof(IMiniToolSnapshotView<FPSSnapshot>).IsAssignableFrom(typeof(FPSSnapshotProvider)), Is.False);
        }

        [Test]
        public void PerformanceSelection_UsesStatsInEditor()
        {
            Assert.That(PerformanceOverlaySelection.UseDetailedStats, Is.True);
        }

        [Test]
        public void GameInfoRuntimeProvider_CapturesTypedViewSnapshot()
        {
            var registration = new MiniToolProviderRegistration(new RemoteMiniToolDescriptor
            {
                Id = "runtime.game-info",
                DisplayName = "Game Info"
            }, new RuntimeGameInfoMiniToolProvider());

            try
            {
                RemoteMiniToolSample sample = registration.Capture(RemoteMiniToolDataChannels.TypedSnapshot | RemoteMiniToolDataChannels.NativeWorkspaceFields);

                Assert.That(sample.ToolId, Is.EqualTo("runtime.game-info"));
                Assert.That(sample.SnapshotTypeName, Is.EqualTo(typeof(GameInfoSnapshot).AssemblyQualifiedName));
                Assert.That(sample.SnapshotJson, Is.Not.Empty);

                GameInfoSnapshot snapshot = JsonUtility.FromJson<GameInfoSnapshot>(sample.SnapshotJson);
                Assert.That(snapshot.GameVersion, Is.EqualTo(Application.version));
                Assert.That(snapshot.UnityVersion, Is.EqualTo(Application.unityVersion));
            }
            finally
            {
                registration.Dispose();
            }
        }

        [Test]
        public void GraphicsInfoRegistration_UsesTypedOriginalPrefabView()
        {
            MiniToolDefinition definition = AssetDatabase.LoadAssetAtPath<MiniToolDefinition>(PackageRoot + "RemoteDevUtilities/Features/MiniTools/Runtime/MiniTools/Definitions/" + "Graphics Info Mini Tool.asset");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackageRoot + "Runtime/MiniTools/GraphicsInfo/Assets/" + "GraphicsInfo.prefab");

            Assert.That(definition, Is.Not.Null);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<GraphicsInfo>(true), Is.AssignableTo<IMiniToolSnapshotView<GraphicsInfoSnapshot>>());
            Assert.That(prefab.GetComponent<GraphicsInfoSnapshotProvider>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<GraphicsInfoLocalController>(), Is.Not.Null);
            Assert.That(typeof(IMiniToolSnapshotProvider<GraphicsInfoSnapshot>).IsAssignableFrom(typeof(GraphicsInfo)), Is.False);
            Assert.That(typeof(IMiniToolSnapshotView<GraphicsInfoSnapshot>).IsAssignableFrom(typeof(GraphicsInfoSnapshotProvider)), Is.False);
            Assert.That(MiniToolRegistrationValidator.TryValidatePrefab(definition, prefab, out string error), Is.True, error);

            RemoteMiniToolCapabilities capabilities = definition.CreateDescriptor().Capabilities;
            Assert.That(capabilities.HasFlag(RemoteMiniToolCapabilities.TypedDebugHostSnapshot), Is.True);
            Assert.That(capabilities.HasFlag(RemoteMiniToolCapabilities.NativeWorkspaceFields), Is.True);
            Assert.That(definition.CommandRouting, Is.EqualTo(RemoteCommandRouting.ExecuteInBuildAndControlEditorTool));
        }

        [Test]
        public void GraphicsInfoRuntimeProvider_CapturesSnapshotAndOptionalFields()
        {
            var registration = new MiniToolProviderRegistration(new RemoteMiniToolDescriptor
            {
                Id = "runtime.graphics-info",
                DisplayName = "Graphics Info"
            }, new RuntimeGraphicsInfoMiniToolProvider());

            try
            {
                RemoteMiniToolSample sample = registration.Capture(RemoteMiniToolDataChannels.TypedSnapshot | RemoteMiniToolDataChannels.NativeWorkspaceFields);

                Assert.That(sample.SnapshotTypeName, Is.EqualTo(typeof(GraphicsInfoSnapshot).AssemblyQualifiedName));
                Assert.That(sample.SnapshotJson, Is.Not.Empty);
                Assert.That(sample.Fields, Is.Not.Empty);

                GraphicsInfoSnapshot snapshot = JsonUtility.FromJson<GraphicsInfoSnapshot>(sample.SnapshotJson);
                Assert.That(snapshot.GraphicsDeviceName, Is.EqualTo(SystemInfo.graphicsDeviceName));
                Assert.That(snapshot.Verbose, Is.False);
            }
            finally
            {
                registration.Dispose();
            }
        }

        [Test]
        public void GraphicsInfoHostBridge_AppliesSnapshotToOriginalPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackageRoot + "Runtime/MiniTools/GraphicsInfo/Assets/GraphicsInfo.prefab");
            Assert.That(prefab, Is.Not.Null);

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                IRemoteMiniToolSnapshotView[] views = RemoteMiniToolSnapshotViewFactory.Find(instance);
                var snapshot = new GraphicsInfoSnapshot
                {
                    GraphicsDeviceName = "Test GPU",
                    GraphicsMemorySizeMb = 8192,
                    GraphicsApi = "Direct3D12",
                    QualityName = "High",
                    VSyncCount = 1,
                    Shadows = "Enabled",
                    LodBias = 2f,
                    TargetFrameRate = 60,
                    HasRenderScale = true,
                    RenderScale = 1f
                };

                Assert.That(views, Has.Length.EqualTo(1));
                Assert.That(views[0].TryApply(CreateSample("runtime.graphics-info", snapshot)), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GraphicsInfoCommand_PublishesSnapshotUsedByRemoteProvider()
        {
            GraphicsInfoCommand commandAsset = AssetDatabase.LoadAssetAtPath<GraphicsInfoCommand>(PackageRoot + "Runtime/MiniTools/GraphicsInfo/Assets/GraphicsInfoCommand.asset");
            Assert.That(commandAsset, Is.Not.Null);

            GraphicsInfoCommand command = Object.Instantiate(commandAsset);
            GameObject instance = null;
            var registration = new MiniToolProviderRegistration(new RemoteMiniToolDescriptor
            {
                Id = "runtime.graphics-info"
            }, new RuntimeGraphicsInfoMiniToolProvider());
            try
            {
                Assert.That(command.Process(null, "GraphicsInfo", new[] { "On", "Extended" }), Is.True);
                FieldInfo instanceField = typeof(GraphicsInfoCommand).GetField("_graphics", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(instanceField, Is.Not.Null);
                instance = instanceField.GetValue(command) as GameObject;
                Assert.That(instance, Is.Not.Null);

                RemoteMiniToolSample sample = registration.Capture(RemoteMiniToolDataChannels.TypedSnapshot | RemoteMiniToolDataChannels.NativeWorkspaceFields);
                GraphicsInfoSnapshot snapshot = JsonUtility.FromJson<GraphicsInfoSnapshot>(sample.SnapshotJson);

                Assert.That(snapshot.Verbose, Is.True);
                Assert.That(command.Process(null, "GraphicsInfo", new[] { "Off" }), Is.True);
                Assert.That(GraphicsInfoSnapshotProvider.TryGetRequestedSnapshot(out _), Is.False);
            }
            finally
            {
                registration.Dispose();
                if (instance != null)
                    Object.DestroyImmediate(instance);
                Object.DestroyImmediate(command);
            }
        }

        [Test]
        public void AnimatorRegistration_UsesTypedOriginalPrefabView()
        {
            MiniToolDefinition definition = AssetDatabase.LoadAssetAtPath<MiniToolDefinition>(PackageRoot + "RemoteDevUtilities/Features/MiniTools/Runtime/MiniTools/Definitions/" + "Animator Mini Tool.asset");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackageRoot + "Runtime/MiniTools/Animator/Assets/" + "AnimatorStats.prefab");

            Assert.That(definition, Is.Not.Null);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<AnimatorStats>(), Is.AssignableTo<IMiniToolSnapshotView<AnimatorStatsSnapshot>>());
            Assert.That(prefab.GetComponent<AnimatorStatsSnapshotProvider>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<AnimatorStatsLocalController>(), Is.Not.Null);
            Assert.That(typeof(IMiniToolSnapshotProvider<AnimatorStatsSnapshot>).IsAssignableFrom(typeof(AnimatorStats)), Is.False);
            Assert.That(typeof(IMiniToolSnapshotView<AnimatorStatsSnapshot>).IsAssignableFrom(typeof(AnimatorStatsSnapshotProvider)), Is.False);
            Assert.That(MiniToolRegistrationValidator.TryValidatePrefab(definition, prefab, out string error), Is.True, error);

            RemoteMiniToolCapabilities capabilities = definition.CreateDescriptor().Capabilities;
            Assert.That(capabilities.HasFlag(RemoteMiniToolCapabilities.TypedDebugHostSnapshot), Is.True);
            Assert.That(capabilities.HasFlag(RemoteMiniToolCapabilities.NativeWorkspaceFields), Is.True);
            Assert.That(definition.CommandRouting, Is.EqualTo(RemoteCommandRouting.ControlEditorToolOnly));
        }

        [Test]
        public void AnimatorRuntimeProvider_CapturesSnapshotAndOptionalFields()
        {
            var registration = new MiniToolProviderRegistration(new RemoteMiniToolDescriptor
            {
                Id = "runtime.animators",
                DisplayName = "Animators"
            }, new RuntimeAnimatorMiniToolProvider());

            try
            {
                registration.Start();
                RemoteMiniToolSample sample = registration.Capture(RemoteMiniToolDataChannels.TypedSnapshot | RemoteMiniToolDataChannels.NativeWorkspaceFields);

                Assert.That(sample.SnapshotTypeName, Is.EqualTo(typeof(AnimatorStatsSnapshot).AssemblyQualifiedName));
                Assert.That(sample.SnapshotJson, Is.Not.Empty);
                Assert.That(sample.Fields, Is.Not.Empty);

                AnimatorStatsSnapshot snapshot = JsonUtility.FromJson<AnimatorStatsSnapshot>(sample.SnapshotJson);
                Assert.That(snapshot.Total, Is.GreaterThanOrEqualTo(0));
            }
            finally
            {
                registration.Stop();
                registration.Dispose();
            }
        }

        [Test]
        public void AnimatorHostBridge_AppliesSnapshotToOriginalPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackageRoot + "Runtime/MiniTools/Animator/Assets/AnimatorStats.prefab");
            Assert.That(prefab, Is.Not.Null);

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                IRemoteMiniToolSnapshotView[] views = RemoteMiniToolSnapshotViewFactory.Find(instance);
                var snapshot = new AnimatorStatsSnapshot
                {
                    ActiveAlways = 3,
                    ActiveCullUpdate = 4,
                    ActiveCullCompletely = 5,
                    DisabledAlways = 6,
                    DisabledCullUpdate = 7,
                    DisabledCullCompletely = 8,
                    HasCpuTiming = true,
                    CpuTimeMs = 1.25d
                };

                Assert.That(views, Has.Length.EqualTo(1));
                Assert.That(views[0].TryApply(CreateSample("runtime.animators", snapshot)), Is.True);
                Text display = instance.GetComponentInChildren<Text>(true);
                Assert.That(display, Is.Not.Null);
                Assert.That(display.text, Does.Contain("Always: 3"));
                Assert.That(display.text, Does.Contain("CullUpdate: 4"));
                Assert.That(display.text, Does.Contain("CPU:</color> 1.250 ms"));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ParticleRegistration_UsesTypedOriginalPrefabView()
        {
            MiniToolDefinition definition = AssetDatabase.LoadAssetAtPath<MiniToolDefinition>(PackageRoot + "RemoteDevUtilities/Features/MiniTools/Runtime/MiniTools/Definitions/" + "Particles Mini Tool.asset");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackageRoot + "Runtime/MiniTools/ParticleSystem/Assets/" + "ParticleStats.prefab");

            Assert.That(definition, Is.Not.Null);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<ParticleStats>(), Is.AssignableTo<IMiniToolSnapshotView<ParticleStatsSnapshot>>());
            Assert.That(prefab.GetComponent<ParticleStatsSnapshotProvider>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<ParticleStatsLocalController>(), Is.Not.Null);
            Assert.That(typeof(IMiniToolSnapshotProvider<ParticleStatsSnapshot>).IsAssignableFrom(typeof(ParticleStats)), Is.False);
            Assert.That(typeof(IMiniToolSnapshotView<ParticleStatsSnapshot>).IsAssignableFrom(typeof(ParticleStatsSnapshotProvider)), Is.False);
            Assert.That(MiniToolRegistrationValidator.TryValidatePrefab(definition, prefab, out string error), Is.True, error);

            RemoteMiniToolCapabilities capabilities = definition.CreateDescriptor().Capabilities;
            Assert.That(capabilities.HasFlag(RemoteMiniToolCapabilities.TypedDebugHostSnapshot), Is.True);
            Assert.That(capabilities.HasFlag(RemoteMiniToolCapabilities.NativeWorkspaceFields), Is.True);
            Assert.That(definition.CommandRouting, Is.EqualTo(RemoteCommandRouting.ControlEditorToolOnly));
        }

        [Test]
        public void ParticleRuntimeProvider_CapturesSnapshotAndOptionalFields()
        {
            var registration = new MiniToolProviderRegistration(new RemoteMiniToolDescriptor
            {
                Id = "runtime.particles",
                DisplayName = "Particles"
            }, new RuntimeParticleMiniToolProvider());

            try
            {
                registration.Start();
                RemoteMiniToolSample sample = registration.Capture(RemoteMiniToolDataChannels.TypedSnapshot | RemoteMiniToolDataChannels.NativeWorkspaceFields);

                Assert.That(sample.SnapshotTypeName, Is.EqualTo(typeof(ParticleStatsSnapshot).AssemblyQualifiedName));
                Assert.That(sample.SnapshotJson, Is.Not.Empty);
                Assert.That(sample.Fields, Is.Not.Empty);

                ParticleStatsSnapshot snapshot = JsonUtility.FromJson<ParticleStatsSnapshot>(sample.SnapshotJson);
                Assert.That(snapshot.TotalSystems, Is.GreaterThanOrEqualTo(0));
                Assert.That(snapshot.LiveParticles, Is.GreaterThanOrEqualTo(0));
            }
            finally
            {
                registration.Stop();
                registration.Dispose();
            }
        }

        [Test]
        public void ParticleHostBridge_AppliesSnapshotToOriginalPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackageRoot + "Runtime/MiniTools/ParticleSystem/Assets/ParticleStats.prefab");
            Assert.That(prefab, Is.Not.Null);

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                IRemoteMiniToolSnapshotView[] views = RemoteMiniToolSnapshotViewFactory.Find(instance);
                var snapshot = new ParticleStatsSnapshot
                {
                    TotalSystems = 10,
                    ActiveSystems = 7,
                    AliveSystems = 5,
                    DisabledSystems = 3,
                    LiveParticles = 250,
                    HasCpuTiming = true,
                    CpuTimeMs = 0.75d
                };

                Assert.That(views, Has.Length.EqualTo(1));
                Assert.That(views[0].TryApply(CreateSample("runtime.particles", snapshot)), Is.True);
                Text display = instance.GetComponentInChildren<Text>(true);
                Assert.That(display, Is.Not.Null);
                Assert.That(display.text, Does.Contain("Total: 10"));
                Assert.That(display.text, Does.Contain("Active: 7"));
                Assert.That(display.text, Does.Contain("Alive: 5"));
                Assert.That(display.text, Does.Contain("Live Particles:</color> 250"));
                Assert.That(display.text, Does.Contain("CPU:</color> 0.750 ms"));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void PerformanceRuntimeProvider_UsesStatsSnapshotInEditor()
        {
            SetPrivateStaticField(typeof(PerformanceSnapshotSource), "s_TotalElapsedSeconds", 1d);
            SetPrivateStaticField(typeof(PerformanceSnapshotSource), "s_TotalFrames", 60L);
            var provider = new RuntimePerformanceMiniToolProvider();
            var registration = new MiniToolProviderRegistration(new RemoteMiniToolDescriptor
            {
                Id = "runtime.performance",
                DisplayName = "Performance"
            }, provider);

            try
            {
                RemoteMiniToolSample sample = registration.Capture(RemoteMiniToolDataChannels.TypedSnapshot | RemoteMiniToolDataChannels.NativeWorkspaceFields);

                Assert.That(sample.SnapshotTypeName, Is.EqualTo(typeof(StatsSnapshot).AssemblyQualifiedName));
                Assert.That(sample.SnapshotJson, Is.Not.Empty);
            }
            finally
            {
                registration.Dispose();
                SetPrivateStaticField(typeof(PerformanceSnapshotSource), "s_TotalElapsedSeconds", 0d);
                SetPrivateStaticField(typeof(PerformanceSnapshotSource), "s_TotalFrames", 0L);
            }
        }

        [Test]
        public void GameInfoHostView_UsesOriginalMiniToolRenderer()
        {
            GameObject root = CreateTextTool(out Text text);
            try
            {
                var component = root.AddComponent<GameInfoComponent>();
                AssignObjectReference(component, "m_TextInfo", text);
                component.enabled = false;

                IRemoteMiniToolSnapshotView[] views = RemoteMiniToolSnapshotViewFactory.Find(root);
                var snapshot = new GameInfoSnapshot
                {
                    GameVersion = "2.5.0",
                    UnityVersion = "6000.3.12f1"
                };
                var sample = CreateSample("runtime.game-info", snapshot);

                Assert.That(views, Has.Length.EqualTo(1));
                Assert.That(views[0].TryApply(sample), Is.True);
                Assert.That(text.text, Is.EqualTo("Game Version: <color=cyan>2.5.0</color>\n" + "Unity Version: <color=cyan>6000.3.12f1</color>"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StatsHostView_UsesOriginalMiniToolRenderer()
        {
            GameObject root = CreateTextTool(out Text text);
            try
            {
                var component = root.AddComponent<SAS.DevUtilities.Stats.Stats>();
                AssignObjectReference(component, "m_Display", text);
                component.enabled = false;

                IRemoteMiniToolSnapshotView[] views = RemoteMiniToolSnapshotViewFactory.Find(root);
                var snapshot = new StatsSnapshot
                {
                    AverageFps = 60d,
                    AverageFrameTimeMs = 16.67d,
                    TargetFrameRate = 60,
                    VSyncCount = 1,
                    HasFrameTiming = false,
                    AllocatedMemoryBytes = 1073741824L,
                    ReservedMemoryBytes = 2147483648L,
                    UnusedReservedMemoryBytes = 536870912L
                };
                var sample = CreateSample("runtime.performance", snapshot);

                Assert.That(views, Has.Length.EqualTo(1));
                Assert.That(views[0].TryApply(sample), Is.True);
                Assert.That(text.text, Does.Contain("FPS: 60.0"));
                Assert.That(text.text, Does.Contain("Average Frame Time: 16.67 ms"));
                Assert.That(text.text, Does.Contain("Target FPS: 60"));
                Assert.That(text.text, Does.Contain("Detailed Frame Timing: unavailable"));
                Assert.That(text.text, Does.Contain("Allocated: 1.000 GiB"));
                Assert.That(text.text, Does.Contain("Reserved: 2.000 GiB"));
                Assert.That(text.text, Does.Contain("Unused: 0.500 GiB"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FpsHostView_UsesOriginalMiniToolRenderer()
        {
            GameObject root = CreateTextTool(out Text text);
            try
            {
                var component = root.AddComponent<FPS>();
                AssignObjectReference(component, "m_Display", text);

                IRemoteMiniToolSnapshotView[] views = RemoteMiniToolSnapshotViewFactory.Find(root);
                var snapshot = new FPSSnapshot
                {
                    AverageFps = 60d,
                    AverageFrameTimeMs = 16.67d,
                    TargetFrameRate = 60,
                    TargetFrameTimeMs = 16.67d,
                    IsFrameTimeOverBudget = false
                };
                var sample = CreateSample("performance.fps", snapshot);

                Assert.That(views, Has.Length.EqualTo(1));
                Assert.That(views[0].TryApply(sample), Is.True);
                Assert.That(text.text, Does.Contain("FPS: 60.0"));
                Assert.That(text.text, Does.Contain("Frame Time: 16.67 ms"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RemoteMiniToolSample CreateSample<TSnapshot>(string toolId, TSnapshot snapshot) where TSnapshot : IMiniToolSnapshot
        {
            return new RemoteMiniToolSample
            {
                ToolId = toolId,
                SnapshotTypeName = typeof(TSnapshot).AssemblyQualifiedName,
                SnapshotJson = JsonUtility.ToJson(snapshot)
            };
        }

        private static MiniToolDefinition CreateDefinitionForProvider<TProvider>() where TProvider : IMiniToolDataProvider
        {
            var definition = ScriptableObject.CreateInstance<MiniToolDefinition>();
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_toolId").stringValue = "tests.optional-fields";
            serialized.FindProperty("_displayName").stringValue = "Optional Fields";
            serialized.FindProperty("_providerTypeName").stringValue = $"{typeof(TProvider).FullName}, " + typeof(TProvider).Assembly.GetName().Name;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static GameObject CreateTextTool(out Text text)
        {
            var root = new GameObject("Mini Tool");
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(root.transform, false);
            text = textObject.GetComponent<Text>();
            return root;
        }

        private static void AssignObjectReference(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static void SetPrivateStaticField(Type targetType, string fieldName, object value)
        {
            FieldInfo field = targetType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(null, value);
        }

        private static bool ImplementsLegacyIdentity(Type type)
        {
            string legacyInterfaceName = typeof(IMiniToolSnapshot).Namespace + ".IMiniTool";
            foreach (Type implementedInterface in type.GetInterfaces())
            {
                if (implementedInterface.FullName == legacyInterfaceName)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class SnapshotOnlyGameInfoProvider : MiniToolDataProvider<GameInfoSnapshot>
        {
            public override bool TryGetSnapshot(out GameInfoSnapshot snapshot)
            {
                snapshot = new GameInfoSnapshot
                {
                    GameVersion = "test",
                    UnityVersion = "test"
                };
                return true;
            }
        }

        private sealed class NoOutputProvider : MiniToolDataProvider
        {
        }

        private sealed class CountingDataChannelProvider : MiniToolDataProvider<GameInfoSnapshot>, IMiniToolFieldProvider
        {
            public int SnapshotCaptures { get; private set; }
            public int FieldCaptures { get; private set; }

            public override bool TryGetSnapshot(out GameInfoSnapshot snapshot)
            {
                SnapshotCaptures++;
                snapshot = new GameInfoSnapshot
                {
                    GameVersion = "test",
                    UnityVersion = "test"
                };
                return true;
            }

            public RemoteMiniToolField[] CaptureFields()
            {
                FieldCaptures++;
                return new[]
                {
                    CreateField("status", "Status", "Running")
                };
            }
        }

        private sealed class TestFieldFactoryProvider : MiniToolFieldDataProvider
        {
            internal static RemoteMiniToolField BuildField(string name, string displayName, string value, string unit)
            {
                return CreateField(name, displayName, value, unit);
            }

            public override RemoteMiniToolField[] CaptureFields()
            {
                return Array.Empty<RemoteMiniToolField>();
            }
        }

        private sealed class TestActionProvider : MiniToolDataProvider<GameInfoSnapshot>
        {
            public string LastAction { get; private set; }

            public override RemoteMiniToolActionDescriptor[] GetActions()
            {
                return new[]
                {
                    new RemoteMiniToolActionDescriptor
                    {
                        Id = "pause",
                        DisplayName = "Pause"
                    },
                    new RemoteMiniToolActionDescriptor
                    {
                        Id = "step",
                        DisplayName = "Step"
                    },
                    new RemoteMiniToolActionDescriptor
                    {
                        Id = "PAUSE",
                        DisplayName = "Duplicate"
                    }
                };
            }

            public override bool TryExecuteAction(string actionId, out string error)
            {
                LastAction = actionId;
                error = string.Empty;
                return true;
            }

            public override bool TryGetSnapshot(out GameInfoSnapshot snapshot)
            {
                snapshot = default;
                return true;
            }
        }

        [Serializable]
        private struct TestStreamEvent : IMiniToolStreamEvent
        {
            public int Value;
        }

        private sealed class TestStreamingProvider : MiniToolStreamingDataProvider<GameInfoSnapshot, TestStreamEvent>
        {
            public override bool TryGetSnapshot(out GameInfoSnapshot snapshot)
            {
                snapshot = new GameInfoSnapshot
                {
                    GameVersion = "test",
                    UnityVersion = "test"
                };
                return true;
            }

            public override bool TryGetEvents(out TestStreamEvent[] events, out int droppedEventCount)
            {
                events = new[]
                {
                    new TestStreamEvent
                    {
                        Value = 17
                    }
                };
                droppedEventCount = 2;
                return true;
            }
        }

        private sealed class TestStreamReceiver : IMiniToolStreamView<TestStreamEvent>
        {
            public int LastValue { get; private set; }
            public int DroppedEventCount { get; private set; }

            public void ApplyEvents(TestStreamEvent[] events, int droppedEventCount)
            {
                LastValue = events != null && events.Length > 0 ? events[0].Value : 0;
                DroppedEventCount = droppedEventCount;
            }
        }
    }
}
