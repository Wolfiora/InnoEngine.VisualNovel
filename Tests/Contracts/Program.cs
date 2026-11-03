using Inno.Core.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Inno.Adapter.Modules.DotNet;
using Inno.Adapter.Serialization.DotNet;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Diagnostics;
using Inno.Core.Identity;
using Inno.Core.Graphs;
using Inno.Core.Logging;
using Inno.Core.Serialization;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.ScriptProjection;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Narrative.Values;
using Inno.VisualNovel.Stage.Timeline;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("INNO_VN_REBUILD_CONSUMER") is { Length: > 0 } consumer)
        { ConsumerSceneFixture.Rebuild(Environment.CurrentDirectory, consumer); return 0; }
        if (Environment.GetEnvironmentVariable("INNO_VN_VERIFY_CONSUMER") is { Length: > 0 } installed)
        { ConsumerSceneFixture.Rebuild(Environment.CurrentDirectory, installed, true); return 0; }
        var identities = new IdentityAllocator();
        using IDisposable identityScope = identities.EnterScope();
        var sceneAssets = Environment.GetEnvironmentVariable("INNO_VN_PREPARE_GAME_BUILD") == "1"
            ? System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "Inno.Scene.Assets.dll")) : typeof(Program).Assembly;
        using var modules = new ModuleHost(new ModuleHostOptions { catalogSource = new DotNetAssemblyCatalogSource(typeof(Program).Assembly,
            typeof(Inno.Build.Toolchains.Bgfx.Tools.BgfxShaderSourceFrontend).Assembly,
            typeof(Inno.Rendering.Assets.Authoring.ShaderGraphSourceStore).Assembly, typeof(Inno.Audio.Assets.AudioClipImporter).Assembly,
            typeof(Inno.Text.Assets.FontImporter).Assembly, typeof(Inno.Adapter.UI.RmlUi.Authoring.RmlUiDocumentFrontend).Assembly, sceneAssets) });
        _ = modules.Register("visualnovel.runtime-contracts", [typeof(StoryAsset).Assembly, typeof(StoryMachine).Assembly, typeof(PerformancePlayer).Assembly,
            typeof(Inno.VisualNovel.UI.Documents.UiDocumentInstance).Assembly, typeof(Inno.VisualNovel.Player.Sessions.NovelSession).Assembly,
            typeof(Inno.VisualNovel.Integration.Hosting.NovelSceneSystem).Assembly]);
        _ = modules.Register("visualnovel.editor-contracts", [typeof(StoryCompiler).Assembly, typeof(FirstStoryTemplate).Assembly]);
        _ = modules.Register("visualnovel.font-import-contracts", [typeof(Inno.Text.Assets.FontImporter).Assembly]);
        using var types = new TypeCatalog(modules, new ReflectionTypeCatalogSource());
        using var serialization = new SerializationRegistry(types, new ReflectionSerializationMetadataSource());
        var native = new EditorStorySerialization(serialization);
        var codec = new StoryNodeDataCodec(native);
        var compiler = new StoryCompiler(native);
        var edits = new StoryEditService(codec);
        int passed = 0;
        var failures = new List<string>();
        Run("engine UI actual native backend produces offscreen geometry and scene button actions", () => EngineUiSceneContracts.NativeBackendProducesGeometryWithoutDesktop(identities, types, modules, serialization));
        Run("engine UI service owns interaction while native scene objects own document content", () => EngineUiSceneContracts.EngineOwnsInteractionAndNativeObjectsOwnContent(identities, types));
        Run("engine UI unsupported capabilities and failed candidates preserve the valid document", () => EngineUiSceneContracts.CandidateFailureDoesNotReplaceTheDocument(identities, types));
        Run("engine UI rotated clipping preserves area UVs and detached resource generations", EngineUiGeometryContracts.RotatedClipsRetainTextureCoordinatesAndDetachedFrames);
        Run("native scene presentation follows actual objects without starting a story", () => NativeScenePresentationContracts.ObjectsAreTheOnlyPresentationAuthority(identities, types, serialization));
        Run("native scene defaults display without a game or Play and retain build dependencies", () => NativeSceneMaterialContracts.BareSceneUsesNativePackagedDefaults(Environment.CurrentDirectory, modules, types, serialization, identities));
        Run("native scene authoring uses real History Prefab saving and scene reopening", () => NativeSceneHistoryContracts.CreateUndoPrefabAndReopen(args.Length == 2 ? Path.GetFullPath(args[1]) : Environment.CurrentDirectory));
        Run("native scene performances write actual targets with atomic conflict validation", () => NativePerformanceContracts.ExplicitWritersAndAtomicTargets(identities, types));
        Run("native scene audio reuses cue policy and owns restoration fades volume tracks and parent lifetime", () => NativeAudioContracts.NativeLifetimeAndSharedPolicy(identities, types));
        Run("native scene effects obey component clocks transforms and inactive parents", () => NativeSceneEffectsContracts.ComponentClocksAndParentOwnership(identities, types));
        Run("native scene characters use actual parts and reject incomplete appearances atomically", () => NativeSceneEffectsContracts.RealCharacterPartsAndAtomicAppearance(identities, types));
        Run("native scene output and camera use explicit ownership", () => NativeScenePresentationContracts.OutputAndCameraOwnership(identities, types));
        Run("native scene camera parallax preserves transforms picking particles and editor navigation", () => NativeParallaxContracts.CameraTranslationPreservesNativeOwnership(identities, types));
        Run("native scene target bindings retain explicit identities types inactive objects and missing references", () => NativeTargetBindingContracts.ExplicitReferencesDoNotRepairByName(identities, types));
        Run("native scene Prefab instances preserve ownership through native serialization", () => NativeScenePresentationContracts.PrefabInstancesAndNativeRoundTrip(identities, types, serialization));
        Run("frame-owned native storage preserves chained IO across retired scopes and cancels queued consumers", FrameStorageContracts.NativeIoAcrossRetiredFrames);
        Run("persistence IO completes without capturing an unpumped host synchronization context", () => FrameStorageContracts.PersistenceWithoutCallerPump(serialization));
        Run("exact-build code fingerprints freeze concrete commands UI registrations host contributions and atomic native restoration", () => CodeFingerprintContracts.FrozenRegistrationsAndNativeRestoration(codec, compiler, serialization));
        Run("exact-build code fingerprints reject factory implementation substitution before story execution", () => CodeFingerprintContracts.ChangedFactoryFailsBeforeExecution(codec, compiler));
        Run("single-line input preserves grapheme caret ranges bounded replacement composition rejected writes and native capture", () => TextInputContracts.EditingGraphemesLimitsAndNativeCapture(serialization));
        Run("single-line input preserves pointer selection full-text viewports word navigation alignment and production paint", TextInputContracts.PointerScrollingWordSelectionAndRendering);
        Run("stage layer batches preserve native geometry mirror signs identities optional overrides and atomic rejection", () => StageBatchContracts.RelativeChangesAndAtomicNativeState(serialization));
        Run("keyboard chords preserve exact modifier conflicts native profiles atomic selector values and hold lifecycle", () => KeyboardChordContracts.ExactModifiersPersistenceAndHoldOwnership(serialization));
        Run("keyboard chords and configurable hold selectors preserve actual modal focus and single reading ownership", () => KeyboardChordContracts.ActualHoldSelectorAndModalReading(codec, compiler));
        Run("UI margins and flow distribution preserve content measurement fill allocation and actual pointer ownership", UiLayoutContracts.MarginsFlowAndHitTesting);
        Run("UI grid alignment and scroll margins preserve native declarations frozen runtime ownership and bounds validation", () => UiLayoutContracts.NativeGridScrollAndOwnership(serialization));
        Run("custom node compilers preserve native source identity production waits restoration and Missing recovery", () => NodeCompilerContracts.NativeLoweringRestoreAndMissing(native, serialization));
        Run("custom node compilers reject port identity recursion factory and validation failures atomically", () => NodeCompilerContracts.InvalidExtensionsRemainAtomic(native));
        Run("shared variable libraries compile one authority and preserve immutable runtime defaults native lifetimes and atomic conflicts", () => VariableLibraryContracts.CompilationAndOwnership(codec, compiler, serialization));
        Run("reading keyboard settings preserve native preferences stable selector rows conflicts resets and current profile ownership", () => KeyboardSettingsContracts.NativeSettingsAndActualSelectors(codec, compiler, serialization));
        Run("reading keyboard shortcuts preserve formal viewport scope text composition modal routing and single input consumption", () => KeyboardSettingsContracts.FormalMappingAndReadingOwnership(codec, compiler));
        Run("held skipping preserves unread decisions script barriers save restoration and independent reading modes", () => HeldSkipContracts.ReadingEdgesAndBarriers(codec, compiler, serialization));
        Run("held skipping respects actual UI composition text focus popup menus and formal viewport modifiers", () => HeldSkipContracts.ActualUiAndViewportOwnership(codec, compiler));
        Run("tooltips preserve pointer dwell keyboard accessibility disabled rows modal ownership and capture isolation", UiTooltipContracts.InputLayoutAndIsolation);
        Run("UI animations preserve native frames stable text layout pointer geometry visibility reversal and reduced motion", () => UiAnimationContracts.VisibilityInputAndRendering(serialization));
        Run("UI animation previews preserve interaction clocks nested clipping frozen exits and native component state", () => UiAnimationContracts.PurePreviewStatesAndFrozenExit(serialization));
        Run("UI page animations preserve real game navigation input isolation save capture and session replacement", () => UiAnimationContracts.ActualGameNavigation(codec, compiler));
        Run("UI performance tracks share production stage clocks native snapshots rollback capture and writer cancellation", () => UiPerformanceContracts.SharedSessionClockAndRestoration(codec, compiler, serialization));
        Run("UI performance bindings preserve theme contracts repeated template input and detached channel ownership", UiPerformanceContracts.ContractsThemesAndRepeatedTemplates);
        Run("screen effect performance channels preserve all mechanisms native state accessibility and atomic authoring bounds", () => EffectTimelineContracts.MechanismsNativeStateAndAtomicBounds(serialization));
        Run("screen effect tracks share production clocks snapshots rollback and source qualified instance contracts", () => EffectTimelineContracts.ProductionSessionAndSourceContracts(codec, compiler, serialization));
        Run("script virtualization preserves measured stable rows reordering bounds and empty documents", ScriptViewportContracts.MeasuredRowsAndStableIdentity);
        Run("a 10000 node chapter preserves real graph projection compilation execution and bounded visible rows", () => ScriptViewportContracts.LargeGraphProjectionAndCompilation(native, codec, compiler));
        Run("parallel variable scopes preserve shared game local global state native restore rollback and candidate isolation", () => ParallelVariableContracts.SharedScopesAndCandidateRestore(codec, compiler, serialization));
        Run("parallel variable writes preserve explicit order source conflicts runtime checks and wait any cancellation", () => ParallelVariableContracts.OrderConflictsAndWaitAny(codec, compiler, native));
        Run("interactive parallel tasks preserve real input ownership native restore shared clocks and rollback", () => InteractiveParallelContracts.InputNativeRestoreAndRollback(codec, compiler, serialization));
        Run("interactive parallel routes retain global visits and NVL page ownership across joins", () => InteractiveParallelContracts.NvlAndRecordedRoutes(codec, compiler, serialization));
        Run("interactive parallel focus conflicts fail and cancelled task voices release after native restore", () => InteractiveParallelContracts.FocusConflictsAndCancelledVoice(codec, compiler, serialization));
        Run("interactive parallel saves and native rollback retain actual presentation language and localized voices", () => InteractiveParallelContracts.SnapshotLanguageAndRestoredVoice(codec, compiler, serialization));
        Run("script card reordering preserves native graph identities layout projection and actual execution", () => StoryReorderContracts.LinearIdentityAndExecution(native, codec, compiler));
        Run("script card moves review branch changes and atomically reject stale entries and invalid scopes", () => StoryReorderContracts.ReviewedBoundariesAndAtomicRejection(native, codec, compiler));
        Run("script card drops resolve exact graph edges and reject ambiguous structural destinations", () => StoryReorderContracts.CardDropDestinations(native, codec, compiler));
        Run("native compiler diagnostics retain node edge identity English messages and authored values", () => CompilerDiagnosticContracts.NativeIdentityAndOwnerFormatting(native, codec, compiler));
        Run("extension performance channels retain production time native saves rollback cancellation and replay isolation", () => ExtensionPerformanceContracts.SharedClockNativeRestoreRollbackAndReplay(codec, compiler, serialization));
        Run("extension performance contracts freeze capabilities validate bindings and reject incompatible saves atomically", () => ExtensionPerformanceContracts.FrozenCapabilitiesAndExactSaveIdentity(codec, compiler, serialization));
        Run("UI canvas edge handles preserve measured anchors pivots snapping native layouts and gesture origins", () => UiCanvasContracts.MeasuredTransformsAndNativeLayout(serialization));
        Run("UI canvas transforms preserve automatic ownership and reject stale invalid layouts atomically", () => UiCanvasContracts.AutomaticOwnershipAndAtomicRejection(serialization));
        Run("native rollback buffers obey memory budgets eviction disposal and discarded future semantics", () => RollbackBudgetContracts.NativeRetentionAndDiscardedFutures(codec, compiler, serialization));
        Run("rollback budgets distinguish oversized native packets codec failures and unavailable host accounting", () => RollbackBudgetContracts.OversizeCodecFailuresAndUnknownHost(codec, compiler, serialization));
        Run("native graph group selection shares script ranges primary reconciliation and isolated ownership", () => GroupSelectionContracts.NativeMembershipRangesAndReconciliation(native, codec));
        Run("reviewed group paste preserves actual execution fresh identities native buffers and explicit Missing targets", () => GroupSelectionContracts.ReviewedGroupPasteAndMissingTargets(native, codec, compiler));
        Run("copied variable maps preserve typed references escaped text target scopes defaults and real commands", () => ClipboardVariableContracts.TypedRemappingAndActualCommandExecution(native, codec, compiler));
        Run("copied variable maps reject incompatible merges enums Missing safely and support simultaneous swaps", () => ClipboardVariableContracts.SimultaneousMappingsAndAtomicConflicts(native, codec, compiler));
        Run("group deletion preserves reviewed linear continuations native identities entry protection and stale rejection", () => GroupDeletionContracts.LinearReviewNativeIdentityAndStaleness(native, codec, compiler));
        Run("group deletion requires explicit junction loop decisions and complete parallel boundaries", () => GroupDeletionContracts.ExplicitJunctionsCyclesAndParallelScopes(native, codec, compiler));
        Run("stage canvas picking and transforms preserve real cropped mirrored rotated geometry and native restore", () => StageCanvasContracts.ProductionGeometryAndNativeTransforms(serialization));
        Run("stage canvas gestures retain frozen constraints and reject invalid mutations atomically", StageCanvasContracts.FrozenConstraintsAndAtomicRejection);
        Run("camera follow preserves pure timeline anchors native restore atomic validation and frozen framing", () => CameraFollowContracts.PureTimelineAnchorsAndNativeFreeze(serialization));
        Run("camera follow preserves actual narrative pivots session rollback and cancelled parallel task ownership", () => CameraFollowContracts.ActualSessionPivotsRestoreAndTaskCancellation(codec, compiler, serialization));
        Run("character placement aligns actual cropped mirrored poses using safe areas and nine-point relative anchors", CharacterPlacementContracts.ActualPoseBoundsAndRelativeAnchors);
        Run("character placement rejects invalid and invisible candidates without modifying authored declarations", CharacterPlacementContracts.InvalidCandidatesPreserveDeclarations);
        Run("quick instruction insertion creates real editable choice fragments and complete parallel continuations", () => QuickInsertContracts.CompleteChoiceAndParallelContinuations(native, codec, compiler));
        Run("quick instruction insertion preserves writing identities and never guesses terminal branch or stale connections", () => QuickInsertContracts.WritingDetachedNodesAndRejectedEdges(native, codec, compiler));
        Run("native tooltips preserve localization glyph contexts typed bindings and independent component ownership", () => UiTooltipContracts.NativeAuthoringAndLocalization(serialization));
        Run("title continuation restores the newest compatible packet and excludes corrupt incompatible empty slots", () => PersistenceContracts.ContinueFromTitle(codec, compiler, serialization));
        Run("project save capacity preserves page bounds independent categories and native service limits", () => PersistenceContracts.ProjectCapacity(codec, compiler, serialization));
        Run("native resource audit preserves provenance duplicates partial closure changes and cancellation ownership", () => ResourceAuditContracts.NativeInventoryProvenanceAndFailureEvidence(
            args.Length == 2 ? Path.GetFullPath(args[1]) : Environment.CurrentDirectory, modules, types, serialization, identities));
        Run("glyph coverage keeps bounded theme source contexts cancellation and unavailable hosts distinct", () => GlyphCoverageContracts.BudgetsContextsAndUnavailableHost(codec, compiler));
        Run("font fallback selection preserves grapheme ownership ligatures ruby and full text diagnostics", LocalizedResourcesContracts.FallbackGraphemesAndRuby);
        Run("localized image and font policies resolve real UI stable stage native paint and input retirement", () => LocalizedResourcesContracts.ActualLanguageResources(codec, compiler, serialization));
        Run("custom UI sources preserve native bindings stable writable row ownership and disposal", () => UiDataSourceContracts.ActualBindingsAndRows(codec, compiler, serialization));
        Run("custom UI sources reject preview side effects query writes and mismatched registration", () => UiDataSourceContracts.PoliciesAndMismatch(codec, compiler));
        Run("custom UI source typed authoring schemas and native metadata remain atomic", () => UiDataSourceContracts.AuthoringAndNeutralSchemas(codec, compiler, serialization));
        Run("custom UI actions preserve typed input native state selected styling and handler ownership", () => UiActionContracts.ActualControlsAndLifetime(codec, compiler, serialization));
        Run("custom UI actions enforce preview query purity rollback and source addressed failures", () => UiActionContracts.PoliciesAndDiagnostics(codec, compiler));
        Run("custom UI action native schemas and authoring reject incompatible arguments atomically", () => UiActionContracts.SchemasAndAuthoring(codec, compiler, serialization));
        Run("whole-game search preserves bounded source snapshots opaque notes cancellation and chapter-qualified matches", () => GameSearchContracts.BoundedSnapshotsAndCancellation(native, codec));
        Run("image viewports preserve pointer keyboard clip capture and resource ownership", () => GalleryViewportContracts.ImageInputAndNeutralPaint(serialization));
        Run("CG variants keep stable unlocked browsing and retire pointer input before narrative exposure", () => GalleryViewportContracts.ActualVariantsAndInputOwnership(codec, compiler));
        Run("native CG variant sources retain image dependencies and canonical reference ownership", () => GalleryViewportContracts.NativeReferences(
            args.Length == 2 ? Path.GetFullPath(args[1]) : Environment.CurrentDirectory, modules, types, serialization, identities));
        Run("page state uses actual controls native restoration exact rollback and independent narrative scopes", () => PageStateContracts.ActualControlsAndPersistence(codec, compiler, serialization));
        Run("page state themes field edits and component identities preserve atomic native contracts", () => PageStateContracts.SchemasEditingAndComponents(serialization));
        Run("native scene object component activation replacement and destruction own independent VN sessions", () =>
            SceneOwnershipContracts.NativeActivationAndDestruction(identities, types, codec, compiler));
        Run("template compiles and preserves merge", () =>
        {
            StoryDraftState draft = FirstStoryTemplate.Create(codec);
            Check(Compile(draft).instructions.Length == 6, "Expected six nodes.");
            Check(draft.graph.document.edges.Count(edge => edge.input.nodeId.value == "end") == 2, "Merge paths were lost.");
        });
        Run("native graph roundtrip preserves opaque Missing data", () =>
        {
            StoryDraftState draft = FirstStoryTemplate.Create(codec);
            draft.graph.document.AddNode(new(new("missing"), "unknown.extension"));
            draft.graph.document.FindNode(new("missing"))!.SetValue("opaque", new(serialization.Serialize(new WaitNode { seconds = 42 })));
            StoryDraftState restored = native.Deserialize<StoryDraftState>(native.Serialize(draft));
            Check(restored.graph.document.FindNode(new("missing"))!.values["opaque"].data.Span.SequenceEqual(draft.graph.document.FindNode(new("missing"))!.values["opaque"].data.Span), "Missing payload was changed.");
            Check(!compiler.Compile(restored.ToAsset()).success, "Missing nodes must block execution.");
        });
        Run("dialogue action consumption and choice isolation", () =>
        {
            var machine = new StoryMachine(Compile(FirstStoryTemplate.Create(codec)));
            machine.Start();
            Check(machine.status == StoryStatus.Dialogue && machine.visibleText == "", "Dialogue should initially reveal no text.");
            machine.Advance();
            Check(machine.status == StoryStatus.Dialogue && machine.isTextComplete, "First action must only reveal text.");
            machine.Advance();
            Check(machine.status == StoryStatus.Choice, "Second action must open choices.");
            Check(!machine.Advance() && !machine.Choose("missing"), "Invalid input was accepted.");
            Check(machine.Choose("sea") && machine.status == StoryStatus.Dialogue && !machine.isTextComplete, "Choice input leaked into following dialogue.");
            machine.Advance(); machine.Advance();
            Check(machine.status == StoryStatus.Completed, machine.error);
        });
        Run("grapheme reveal keeps emoji and combining marks intact", () =>
        {
            StoryDraftState draft = FirstStoryTemplate.Create(codec);
            DialogueNode line = codec.Read<DialogueNode>(draft.graph.document.FindNode(new("hello"))!);
            line.text = "A\u0301👨‍👩‍👧‍👦中";
            line.charactersPerSecond = 1;
            codec.Write(draft.graph.document.FindNode(new("hello"))!, line);
            var machine = new StoryMachine(Compile(draft));
            machine.Start(); machine.Tick(1);
            Check(machine.visibleText == "A\u0301", "Combining cluster was split.");
            machine.Tick(1);
            Check(machine.visibleText == "A\u0301👨‍👩‍👧‍👦", "Emoji cluster was split.");
        });
        Run("snapshot restores partial text and isolates writes", () =>
        {
            var machine = new StoryMachine(Compile(FirstStoryTemplate.Create(codec)));
            machine.Start(); machine.Tick(0.1);
            StoryExecutionState snapshot = machine.Capture();
            string text = machine.visibleText;
            machine.SetVariable("player", NovelValue.String("Changed"));
            machine.Advance(); machine.Advance();
            machine.Restore(snapshot);
            Check(machine.visibleText == text && machine.GetVariable("player").text == "旅人", "Checkpoint failed to restore exact state.");
            snapshot.gameVariables[0].value = NovelValue.String("Aliased");
            Check(machine.GetVariable("player").text == "旅人", "Snapshot retained mutable aliases.");
        });
        Run("invalid snapshot is atomic", () =>
        {
            var machine = new StoryMachine(Compile(FirstStoryTemplate.Create(codec)));
            machine.Start(); machine.Tick(0.1);
            StoryExecutionState bad = machine.Capture();
            string before = machine.visibleText;
            bad.gameVariables[0].value = NovelValue.Integer(9);
            Throws<InvalidOperationException>(() => machine.Restore(bad));
            Check(machine.visibleText == before && machine.GetVariable("player").text == "旅人", "Failed restoration changed active state.");
            bad = machine.Capture(); bad.fingerprint = "wrong";
            Throws<InvalidOperationException>(() => machine.Restore(bad));
        });
        Run("typed writes and immutable list values", () =>
        {
            var store = new VariableStore([new() { id = "x", initialValue = NovelValue.Integer(3) }]);
            Throws<InvalidOperationException>(() => store.Set("x", NovelValue.String("bad")));
            Check(store.Get("x").integer == 3, "Failed write changed variable.");
            NovelValue value = NovelValue.List(NovelValueKind.Integer, [NovelValue.Integer(2)]);
            NovelValue[] items = value.items; items[0] = NovelValue.Integer(9);
            Check(value.items[0].integer == 2, "List exposed shared storage.");
            Throws<ArgumentException>(() => NovelValue.List(NovelValueKind.Integer, [NovelValue.String("bad")]));
        });
        Run("expression short circuit and checked arithmetic", () =>
        {
            var evaluator = new ExpressionEvaluator(_ => throw new InvalidOperationException("must not evaluate"));
            var expression = new ExpressionDocument { operation = ExpressionOperation.And, operands = [new() { literal = NovelValue.Boolean(false) }, new() { operation = ExpressionOperation.Variable, variableId = "missing" }] };
            Check(!evaluator.Evaluate(expression).boolean, "Short circuit failed.");
            expression = new() { operation = ExpressionOperation.Add, operands = [new() { literal = NovelValue.Integer(long.MaxValue) }, new() { literal = NovelValue.Integer(1) }] };
            Throws<OverflowException>(() => evaluator.Evaluate(expression));
            expression = new() { operation = ExpressionOperation.Divide, operands = [new() { literal = NovelValue.Integer(1) }, new() { literal = NovelValue.Integer(0) }] };
            Throws<DivideByZeroException>(() => evaluator.Evaluate(expression));
        });
        Run("reconnection retains other incoming merge edges", () =>
        {
            StoryDraftState draft = FirstStoryTemplate.Create(codec);
            edits.Connect(draft, new(new("sea"), new("next")), new("end"));
            Check(draft.graph.document.edges.Count(edge => edge.input.nodeId.value == "end") == 2, "Reconnecting removed a different incoming path.");
        });
        Run("linear delete bridges and complex delete is explicit", () =>
        {
            StoryDraftState draft = FirstStoryTemplate.Create(codec);
            edits.Remove(draft, new("hello"));
            Check(draft.graph.document.edges.Any(edge => edge.output.nodeId.value == "entry" && edge.input.nodeId.value == "choice"), "Linear deletion failed to bridge.");
            Throws<InvalidOperationException>(() => edits.Remove(draft, new("choice")));
            Check(draft.graph.document.FindNode(new("choice")) is not null, "Rejected deletion mutated graph.");
        });
        Run("free loop projects finitely and layout is not execution", () =>
        {
            StoryDraftState draft = FirstStoryTemplate.Create(codec);
            edits.Connect(draft, new(new("sea"), new("next")), new("hello"));
            Check(ScriptProjectionBuilder.Build(draft).Count == 6, "Cycle projection duplicated nodes.");
            string fingerprint = Compile(draft).fingerprint;
            edits.Move(draft, new Dictionary<Inno.Core.Graphs.GraphNodeId, Inno.Core.Graphs.GraphPosition> { [new("hello")] = new(999, 999) });
            Check(Compile(draft).fingerprint == fingerprint, "Graph layout changed executable fingerprint.");
        });
        Run("loop budget prevents host hang", () =>
        {
            StoryDraftState draft = FirstStoryTemplate.Create(codec);
            draft.graph.document.RemoveNode(new("hello"));
            foreach (StoryFragment fragment in draft.fragments) fragment.nodes = fragment.nodes.Where(id => id != "hello").ToArray();
            var wait = new Inno.Core.Graphs.GraphNodeRecord(new("spin"), StoryNodeIds.wait);
            codec.Write(wait, new WaitNode { seconds = 0 });
            draft.graph.document.AddNode(wait);
            draft.fragments[0].nodes = [.. draft.fragments[0].nodes, "spin"];
            edits.Connect(draft, new(new("entry"), new("next")), new("spin"));
            edits.Connect(draft, new(new("spin"), new("next")), new("spin"));
            var machine = new StoryMachine(Compile(draft)); machine.Start();
            Check(machine.status == StoryStatus.Faulted && machine.error.Contains("budget", StringComparison.Ordinal), "Unbounded loop was not isolated.");
        });
        Run("random state replays exactly", () =>
        {
            var random = new StoryRandomState(47);
            random.Next(); ulong snapshot = random.state;
            double first = random.Next(); random.Restore(snapshot);
            Check(first == random.Next(), "Random restoration diverged.");
        });
        Run("same authored content compiles deterministically", () =>
        {
            StoryDraftState draft = FirstStoryTemplate.Create(codec);
            Check(Compile(draft).fingerprint == Compile(native.Deserialize<StoryDraftState>(native.Serialize(draft))).fingerprint, "Native roundtrip changed executable content.");
        });
        Run("compiled program cannot be mutated by its caller", () =>
        {
            var program = Compile(FirstStoryTemplate.Create(codec));
            var machine = new StoryMachine(program);
            program.instructions.Single(item => item.nodeId == "hello").dialogue!.text = "Corrupted";
            program.entries[0].nodeId = "end";
            machine.Start();
            Check(machine.dialogueText.Contains("旅人", StringComparison.Ordinal), "Session retained mutable program input.");
        });
        Run("invalid expression types fail at compile time", () =>
        {
            StoryDraftState draft = FirstStoryTemplate.Create(codec);
            ChoiceNode choice = codec.Read<ChoiceNode>(draft.graph.document.FindNode(new("choice"))!);
            choice.options[0].visible.literal = NovelValue.String("not Boolean");
            codec.Write(draft.graph.document.FindNode(new("choice"))!, choice);
            StoryCompilationResult result = compiler.Compile(draft.ToAsset());
            Check(!result.success && result.diagnostics.Any(item => item.code == "VN_EXPRESSION_TYPE"), "Invalid condition reached runtime.");
        });
        Run("native interpreter checkpoint roundtrip", () =>
        {
            var machine = new StoryMachine(Compile(FirstStoryTemplate.Create(codec)));
            machine.Start(); machine.Tick(0.25);
            StoryExecutionState state = native.Deserialize<StoryExecutionState>(native.Serialize(machine.Capture()));
            string before = machine.visibleText;
            machine.Advance(); machine.Advance(); machine.Restore(state);
            Check(machine.visibleText == before && machine.status == StoryStatus.Dialogue, "Native checkpoint roundtrip changed reading state.");
        });
        Run("invocation locals and return stack restore independently", () =>
        {
            StoryDraftState draft = FirstStoryTemplate.Create(codec);
            draft.variables = [.. draft.variables, new() { id = "scratch", scope = VariableScope.Local, initialValue = NovelValue.Integer(5) }];
            GraphNodeId call = edits.Add(draft, "opening", StoryNodeIds.call, new CallNode { entryId = "visit" }, new(0, 400));
            draft.graph.document.AddNode(new(new("return"), StoryNodeIds.returnFromCall));
            draft.fragments[0].nodes = [.. draft.fragments[0].nodes, "return"];
            draft.entries = [.. draft.entries, new() { id = "visit", nodeId = "sea" }];
            edits.Connect(draft, new(new("hello"), new("next")), call);
            edits.Connect(draft, new(call, new("next")), new("end"));
            edits.Connect(draft, new(new("sea"), new("next")), new("return"));
            var machine = new StoryMachine(Compile(draft)); machine.Start();
            machine.SetVariable("scratch", NovelValue.Integer(9));
            machine.Advance(); machine.Advance();
            Check(machine.nodeId == "sea" && machine.GetVariable("scratch").integer == 5, "Callee reused caller locals.");
            machine.SetVariable("scratch", NovelValue.Integer(2));
            StoryExecutionState state = machine.Capture();
            machine.Advance(); machine.Advance();
            Check(machine.status == StoryStatus.Completed && machine.GetVariable("scratch").integer == 9, "Return did not restore caller locals.");
            machine.Restore(state); machine.Advance(); machine.Advance();
            Check(machine.status == StoryStatus.Completed && machine.GetVariable("scratch").integer == 9, "Restored return stack diverged.");
        });
        Run("timer checkpoint preserves remaining logical duration", () =>
        {
            StoryDraftState draft = FirstStoryTemplate.Create(codec);
            GraphNodeId timer = edits.Add(draft, "opening", StoryNodeIds.wait, new WaitNode { seconds = 1 }, new(0, 400));
            edits.Connect(draft, new(new("entry"), new("next")), timer);
            edits.Connect(draft, new(timer, new("next")), new("hello"));
            var machine = new StoryMachine(Compile(draft)); machine.Start(); machine.Tick(0.4);
            StoryExecutionState snapshot = machine.Capture();
            machine.Tick(0.7); Check(machine.status == StoryStatus.Dialogue, "Timer did not complete.");
            machine.Restore(snapshot); machine.Tick(0.5); Check(machine.status == StoryStatus.Timer, "Restored timer completed too early.");
            machine.Tick(0.1); Check(machine.status == StoryStatus.Dialogue, "Restored timer did not complete.");
        });
        Run("rollback leaves global progress intact", () =>
        {
            StoryDraftState draft = FirstStoryTemplate.Create(codec);
            draft.variables = [.. draft.variables, new() { id = "unlocked", scope = VariableScope.Global, initialValue = NovelValue.Boolean(false) }];
            var machine = new StoryMachine(Compile(draft)); machine.Start();
            StoryExecutionState snapshot = machine.Capture();
            machine.SetVariable("unlocked", NovelValue.Boolean(true)); machine.Restore(snapshot);
            Check(machine.GetVariable("unlocked").boolean, "Rollback reversed global progress.");
        });
        Run("timeline sampling is seekable and independent of frame subdivision", () =>
        {
            var clip = new PerformanceClipAsset { duration = 2, tracks = [new() { target = "actor", property = PerformanceProperty.X, keys = [new() { time = 0, value = -10 }, new() { time = 2, value = 10 }] }] };
            var a = new PerformancePlayer(clip);
            var b = new PerformancePlayer(clip);
            a.Tick(1); b.Tick(0.25); b.Tick(0.75);
            Check(a.Evaluate(a.elapsed)[0].value == b.Evaluate(b.elapsed)[0].value && a.Evaluate(1)[0].value == 0, "Timeline depends on frame subdivision.");
            PerformanceState state = a.Capture(); a.Tick(1); a.Restore(state);
            Check(a.elapsed == 1 && !a.completed, "Playback snapshot did not restore.");
            clip.tracks[0].keys[1].value = 999;
            Check(a.Evaluate(1)[0].value == 0, "Playback retained mutable source keyframes.");
        });
        Run("timeline rejects competing property writers", () =>
        {
            var track = new PerformanceTrack { target = "actor", property = PerformanceProperty.Opacity, keys = [new() { time = 0, value = 1 }] };
            Throws<ArgumentException>(() => new PerformancePlayer(new PerformanceClipAsset { tracks = [track, track] }));
        });
        Run("real editor documents share native edits Undo Redo and save", () =>
            HistoryContracts.Run(args.Length == 2 ? Path.GetFullPath(args[1]) : Environment.CurrentDirectory, modules, types, serialization, identities));
        Run("all built-in instructions support defaults and explicit required content bindings", () => AuthoringContracts.Factory(codec, compiler, edits));
        Run("dynamic output removal is explicit atomic and merge preserving", () => AuthoringContracts.DynamicPorts(native, codec, edits));
        Run("chapter grouping entries and variable references retain meaning", () => AuthoringContracts.StructureAndVariables(codec, compiler));
        Run("UI layout fill and source ownership are isolated", UiContracts.LayoutAndOwnership);
        Run("UI repeated rows preserve keyed actions and consume input", UiContracts.RepeatedRowsAndConsumption);
        Run("UI focus IME text and modal input are deterministic", UiContracts.FocusTextAndModal);
        Run("game session reading rollback and system pages preserve state", () => SessionContracts.ReadingAndRollback(codec, compiler));
        Run("stage affine geometry and rejected restoration are atomic", StageContracts.GeometryAndAtomicRestore);
        Run("stage crop source and pixel pivots preserve native snapshots and atomic candidates", () => StageSurfaceContracts.CropPivotAndRestore(serialization));
        Run("stage masks and blend plans preserve backdrop alpha dependencies and packed UVs", StageSurfaceContracts.ImagePlanAndVertices);
        Run("native scene engine UI image effects output transitions and delayed captures retain mesh ownership", NativeUiImagePlanContracts.EffectsTransitionsAndCaptureOwnership);
        Run("character variant edits preserve explicit defaults independent copies and source addressed missing references", () => CharacterVariantContracts.DefaultsAndReferences(codec, compiler, edits));
        Run("narrative stage requests and rollback share one session", () => StageContracts.NarrativeCoordination(codec, compiler, edits));
        Run("native session candidates reject damage without replacing active state", () => PersistenceContracts.SessionCandidates(codec, compiler, serialization));
        Run("atomic native save slots survive write failure and reconstruct metadata", () => PersistenceContracts.AtomicSlots(codec, compiler, serialization, args.Length == 2 ? Path.GetFullPath(args[1]) : Environment.CurrentDirectory));
        Run("audio crossfade ducking pause and native snapshots retain policy", () => AudioContracts.CrossfadeAndRestore(serialization));
        Run("audio narrative waits and rejected candidates preserve active voices", () => AudioContracts.NarrativeWait(codec, compiler, edits));
        Run("linked chapter calls jumps and saves retain source identity", () => ChapterContracts.LinkCallAndJump(codec, compiler, edits));
        Run("visual list operations and random ranges retain type and determinism boundaries", () => ExpressionContracts.TypedListsAndRanges(codec, compiler));
        Run("native PNG character and WAV cue import preserve resource identity", () => NativeResourceContracts.Run(args.Length == 2 ? Path.GetFullPath(args[1]) : Environment.CurrentDirectory, modules, types, serialization, identities));
        Run("native artifacts retain exact dependency roots across cold cache reload", () => NativeArtifactContracts.RootsAndColdReload(args.Length == 2 ? Path.GetFullPath(args[1]) : Environment.CurrentDirectory, modules, types, serialization, identities, codec));
        Run("native package references retain source-local ownership and stable dependency identity", () => NativeArtifactContracts.SourceMountOwnership(
            args.Length == 2 ? Path.GetFullPath(args[1]) : Environment.CurrentDirectory, modules, types, serialization, identities));
        Run("UI subtree duplication and reparenting preserve stable ownership", ScreenEditingContracts.TreeOperations);
        Run("bound performances retain property ownership pure seeking and native stage state", () => PerformanceContracts.StageOwnership(serialization));
        Run("performance waits and content-frozen session restoration share the runtime", () => PerformanceContracts.SessionWait(codec, compiler, edits));
        Run("performance markers restore narrative waits and respect cancellation and motion policy", () => PerformanceMarkerContracts.WaitRestoreAndPolicy(codec, compiler, serialization));
        Run("performance markers and range copies retain native identities and explicit collision policy", () => PerformanceMarkerContracts.MarkerEditsAndRangeCopies(serialization));
        Run("timeline key groups preserve frozen relative timing native curves and production evaluation", () => TimelineSelectionContracts.FrozenGroupAndNativeEvaluation(serialization));
        Run("timeline key groups reject collisions atomically and require explicit replacement and empty track removal", TimelineSelectionContracts.CollisionsDeletionAndAtomicFailure);
        Run("story logs use the native engine router with source identities frozen programs preview policy and restore boundaries", () => StoryLogContracts.EngineRoutingRestoreAndPreviewPolicy(codec, compiler, serialization));
        Run("story logs share authoring search variable usage reviewed clipboard remapping and compiler diagnostics", () => StoryLogContracts.AuthoringReferencesAndDiagnostics(native, codec, compiler));
        Run("high contrast preserves authored geometry rich text pacing artwork and real input routing", HighContrastContracts.PaintGeometryRichTextAndImages);
        Run("high contrast works through every theme keyboard settings and native player preferences independently of story restore", () => HighContrastContracts.ActualSettingsThemesAndNativePreferences(codec, compiler, serialization));
        Run("UI tree drops preserve native placements authored identities coordinates and actual paint order", UiHierarchyDropContracts.PlacementAndActualPaintOrder);
        Run("UI tree drops expose row context changes and reject roots cycles missing and ambiguous positions atomically", UiHierarchyDropContracts.DataContextsAndAtomicRejection);
        Run("variable metadata preserves native declarations compilation ownership actual execution and reviewed clipboard identities", () => VariableMetadataContracts.NativeDeclarationsCompilationAndClipboard(codec, compiler, native, serialization));
        Run("variable metadata survives reusable UI fields and rejects unbounded annotations atomically", VariableMetadataContracts.ReusableUiFieldsAndAtomicBounds);
        Run("read identities preserve stable authored lines across substitutions skip decisions and native restoration", () => ReadIdentityContracts.ParametersSkipAndNativeRestore(codec, compiler, serialization));
        Run("read identities distinguish changed authored content translations and newly assigned dialogue identities", () => ReadIdentityContracts.AuthoredChangesAndLanguageContent(codec, compiler));
        Run("nested canvas rendering and inverse input agree at fractional sizes", CanvasContracts.NestedInputAndComposition);
        Run("independent native progress and preferences preserve packets and candidate ownership", () => PersistenceContracts.SeparateProgressAndPreferences(codec, compiler, serialization));
        Run("reduced motion preserves essential performances and removes decorative motion", PerformanceContracts.ReducedMotion);
        Run("native graph clipboard remaps identities and retains unresolved external links", () => FlowEditingContracts.Clipboard(native, codec, compiler));
        Run("visual counted-loop template stays ordinary editable graph control flow", () => FlowEditingContracts.CountedLoop(codec, compiler, edits));
        Run("modal player input Unicode drafts cancellation and dynamic speakers roundtrip", () => PlayerInputContracts.NativeDraftAndCancellation(codec, compiler, serialization));
        Run("actual shell consumes player input submission before the following dialogue", () => PlayerInputContracts.ActualShellConsumption(codec, compiler));
        Run("keyboard focus reveals scrolling controls and space remains editable text", UiContracts.ScrollFocusAndSpace);
        Run("presentation validation locates missing required story input bindings", () => PlayerInputContracts.RequiredScreenBindings(codec, compiler));
        Run("dialogue voices history replay auto waiting and restore share ownership", () => AudioContracts.DialogueVoice(codec, compiler));
        Run("timed self-loop choices retain distinct rollback boundaries", () => AudioContracts.RepeatedBoundary(codec, compiler, edits));
        Run("independent shake offsets coexist with bounded ordinary motion writers", PerformanceContracts.IndependentShake);
        Run("slot pages serialize IO and commit candidates only on the owner thread", () => PersistenceContracts.OwnerThreadSlots(codec, compiler, serialization));
        Run("editable save and load pages consume confirmation and restore game state", () => PersistenceContracts.EditableSaveShell(codec, compiler, serialization));
        Run("enum list declarations reject unknown members and freeze caller-owned metadata", () => ExpressionContracts.EnumListDeclarations(codec, compiler));
        Run("title ambience starts once and is released when a story starts", () => AudioContracts.TitleAmbience(codec, compiler));
        Run("cancelled late storage reads cannot call a retired native codec", StorageCancellationContracts.RetiredCodec);
        Run("particle seeds and pure logical time retain bounded deterministic geometry", ParticleContracts.PureSeedAndTime);
        Run("particle lifetime sizes gradients and eased keys preserve native restore and independent ownership", () => ParticleCurveContracts.NativeLifetimeAndOwnership(serialization));
        Run("PNG thumbnails validate chunks and normalize pitched premultiplied readback", CaptureContracts.ImageFormats);
        Run("screenshot queues retain exact frames and retire transferred requests", CaptureContracts.QueueOwnership);
        Run("capture graphs separate attachments from readback transfer resources", CaptureContracts.TransferGraph);
        Run("nine-slice images preserve corners UVs native state and complete image opacity", () => NineSliceContracts.GeometryAndNative(serialization));
        Run("timing curves invert horizontal progress and retain native edited handles", () => CurveContracts.TimingAndEdits(serialization));
        Run("long backlogs page original identities and restore exact retained checkpoints", () => BacklogContracts.PagedBacklogAndExactRollback(codec, compiler, edits, serialization));
        Run("authored page focus falls back safely and rejects dynamic decorative targets", ScreenFocusContracts.AuthoredFocus);
        Run("native theme styles inherit tokens local ownership and actual control states", () => ThemeContracts.InheritanceAndInputStates(serialization));
        Run("theme style cycles missing tokens and invalid field ownership fail before rendering", ThemeContracts.RejectInvalidGraphs);
        Run("live theme actions preserve modal flow and restore exact save and rollback boundaries", () => ThemeSessionContracts.SwitchingSaveRollback(codec, compiler, serialization));
        Run("every theme variant validates narrative bindings and typed selector choices", () => ThemeSessionContracts.ValidateEveryVariant(codec, compiler));
        Run("visual theme token edits preserve references and explicit typed field ownership", ThemeEditingContracts.TokensAndOwnership);
        Run("three complete editable theme families retain large text and disabled choice explanations", () => ThemeEditingContracts.CompleteFamilies(codec, compiler));
        Run("all theme families reflow nested settings and reveal expanded pause controls at double text size", ThemeReflowContracts.NestedSettingsAndScrollablePause);
        Run("packaged preset metadata and scrubbing use native resources and pure initial states", () => BuiltInResourceContracts.CatalogAndPreview(args.Length == 2 ? Path.GetFullPath(args[1]) : Environment.CurrentDirectory));
        Run("beginner visual story shows actors and changes reversible route scenery", () => BuiltInResourceContracts.StarterStory(codec, compiler));
        Run("no-code page and theme actions suspend before effects and restore exact native continuations", () => SystemActionContracts.ModalAndNativeResume(codec, compiler, serialization));
        Run("system resources diagnose source errors and cannot compete for parallel modal ownership", () => SystemActionContracts.ValidationAndTitle(codec, compiler));
        Run("named checkpoint journals avoid rollback traps and preserve explicit barrier semantics", () => CheckpointContracts.NamedBoundariesAndBarriers(codec, compiler, serialization));
        Run("explicit automatic saves freeze continuations and native preview packets stay isolated", () => CheckpointContracts.ExplicitSavesAndPreviewIsolation(codec, compiler, serialization));
        Run("linked speakers retain frozen localized names portraits and native restore", () => CharacterPresentationContracts.NamesPortraitsAndRestore(codec, compiler, serialization));
        Run("recording reviews and metadata validation share stable character identities", () => CharacterPresentationContracts.RecordingAndValidation(codec, compiler));
        Run("character voice policies persist independently and survive narrative audio restoration", () => AudioContracts.CharacterPolicies(serialization));
        Run("repeated sliders toggles inputs and selectors write exact stable owner rows", RowBindingContracts.InteractiveOwners);
        Run("reusable UI components retain independent controls resources and actual editable actions", UiWidgetContracts.IndependentInstances);
        Run("five tutorial graphs execute bounded branching and the actual distributed typed C# command", () => TutorialTemplateContracts.GraphsAndExtension(codec, compiler, serialization));
        Run("author notes tags bookmarks and search preserve native graph identity and execution", () => StoryNotesContracts.NativeCopySearchAndSemantics(native, codec, compiler));
        Run("script authoring continues real dialogue edges and displays linear execution segments", () => ScriptAuthoringContracts.ContinueAlongActualEdges(native, codec, compiler));
        Run("script authoring keeps cycles multiple entries Missing and cross-fragment targets finite", () => ScriptAuthoringContracts.FiniteFreeGraphs(native));
        Run("reviewed dialogue tables preserve native graph flow matching and proofreading export", () => StoryImportContracts.ReviewedNativeFlow(
            args.Length == 2 ? Path.GetFullPath(args[1]) : Environment.CurrentDirectory, native, codec, compiler));
        Run("dialogue imports reject stale ambiguous and forged reviews without partial graph edits", () => StoryImportContracts.RejectedCandidates(native, codec));
        Run("native font references retain imported identity and source ownership through page decoding", () => TutorialTemplateContracts.NativeFontReference(
            args.Length == 2 ? Path.GetFullPath(args[1]) : Environment.CurrentDirectory, modules, types, serialization, identities));
        Run("native project templates instantiate independent publishable assets and actual complete lesson routes", () =>
        {
            string created = TutorialTemplateContracts.NativeInstantiation(args.Length == 2 ? Path.GetFullPath(args[1]) : Environment.CurrentDirectory, modules, types, serialization, identities, codec);
            ImportContextContracts.WithoutAmbientAssets(created, modules, types, serialization);
        });
        Run("character frames breathing and speech retain pure native time and isolated ownership", () => CharacterAnimationContracts.PureFramesAndBreathing(serialization));
        Run("character animation edits and rejected candidates preserve active composition", CharacterAnimationContracts.AtomicValidationAndEditing);
        Run("narrative speech markers restore roll back and cancel with their owning parallel tasks", () => CharacterAnimationContracts.NarrativeMarkersAndCancellation(codec, compiler, serialization));
        Run("offline audio waveforms preserve sample formats and lossless crop intervals", WaveformContracts.SampleFormatsAndLosslessCrop);
        Run("offline audio analysis rejects invalid frames formats and crop boundaries", WaveformContracts.InvalidBoundaries);
        Run("voice batches review stable identities and language bindings without ambiguous overwrite", () => VoiceBatchContracts.NativeReviewAndApply(
            args.Length == 2 ? Path.GetFullPath(args[1]) : Environment.CurrentDirectory, modules, types, serialization, identities, codec));
        Run("source debugging freezes pure conditions and steps without leaking into native saves", () => DebuggerContracts.BreakpointAndStep(codec, compiler, serialization));
        Run("debug condition failures preserve story ownership and callee return inspection", () => DebuggerContracts.ConditionFailureAndCalls(codec, compiler));
        Run("parallel debug stops suspend all task and session clocks deterministically", () => DebuggerContracts.ParallelAndSessionClocks(codec, compiler));
        Run("publishing audits native dynamic closures and identifies excluded sample references", () => PublishingContracts.NativeClosure(
            args.Length == 2 ? Path.GetFullPath(args[1]) : Environment.CurrentDirectory, modules, types, serialization, identities));
        Run("save screenshots freeze narrative boundaries and retain valid packets after failure", () => PersistenceContracts.ExactThumbnails(codec, compiler, serialization));
        Run("native profiles restore autosave and retain valid packets after failure", () => ProfileContracts.RestoreAutosaveAndFailure(codec, compiler, serialization));
        Run("profile startup gates input rejects partial loads and retires late reads", () => ProfileContracts.StartupGateAndLateRead(codec, compiler, serialization));
        Run("screen and transition clocks restore independently and honor motion policies", () => ImageEffectContracts.ClocksAndRestore(serialization));
        Run("output transitions restore exact UI boundaries and explicit completion waits", () => ImageEffectContracts.SessionSnapshots(codec, compiler, edits, serialization));
        Run("image plans preserve UI bands two-axis blur and previous resource closure", ImageEffectContracts.ImagePlans);
        Run("particle native restoration removal and reduced motion retain ownership", () => ParticleContracts.NativeRestoreAndMotion(serialization));
        Run("particle narrative lifecycle and frozen session saves share production presentation", () => ParticleContracts.SessionFlow(codec, compiler, edits, serialization));
        Run("particle completion waits respect finite lifetime and reduced motion", () => ParticleContracts.CompletionWait(codec, compiler, edits));
        Run("game localization refreshes runtime UI and exact snapshots without replaying lines", () => LocalizationContracts.RuntimeAndRestore(codec, compiler, serialization));
        Run("translation CSV TSV preview preserves Unicode quotes and review conflicts", LocalizationContracts.Exchange);
        Run("translation exchange maps arbitrary headers and retains every review record", TranslationExchangeContracts.MappingsAndCompleteReview);
        Run("translation exchange revalidates atomic application and cross-language source identity", TranslationExchangeContracts.AtomicApplicationAndCrossLanguageConflicts);
        Run("collection unlocks survive rollback and native progress while replay stays isolated", () => CollectionContracts.ProgressAndReplay(codec, compiler, serialization));
        Run("editable collections hide locked content and music restores caller audio", () => CollectionContracts.GalleryAndMusic(codec, compiler));
        Run("typed selectors preserve modal ownership cancellation and dynamic identities", () => UiContracts.SelectorModalOwnership(serialization));
        Run("searchable selectors preserve transient Unicode filters native captures rejection and stable modal identities", () => SelectorSearchContracts.FilteringOwnershipAndPersistence(serialization));
        Run("searchable selectors preserve page navigation and filtered pointer coordinates", SelectorSearchContracts.PageNavigationAndPointerRows);
        Run("scrollbars preserve proportional dragging track paging capture cancellation overlay order and release ownership", ScrollBarContracts.DragTrackAndOcclusion);
        Run("timed choice pages observe one native clock through modal pause saving default selection and rollback", () => ChoiceCountdownContracts.ActualPagesNativeClockAndRollback(codec, compiler, serialization));
        Run("navigation keyboard mappings preserve native profiles cross-table conflicts and physical text keys", () => NavigationKeyboardContracts.NativeMappingsAndPhysicalText(serialization));
        Run("navigation settings selectors preserve modal ownership and printable text cancellation", () => NavigationKeyboardContracts.ActualSettingsAndTextCancellation(codec, compiler));
        Run("explicit advance waits preserve editable game pages native saving rollback and one input consumption", () => AdvanceWaitContracts.ActualPagesSavingAndSingleConsumption(codec, compiler, serialization));
        Run("explicit advance waits retain one parallel input owner and independent native sibling clocks", () => AdvanceWaitContracts.ParallelScopeAndNativeTasks(codec, compiler, serialization));
        Run("explicit advance waits support local preview recorded routes and exact interactive targets", () => AdvanceWaitContracts.LocalAndRecordedRouteTargets(codec, compiler));
        Run("preview native text bridges preserve full Unicode clearing focus release and recorded input submission", () => PreviewTextBridgeContracts.FullUnicodeValueClearingAndRecordedSubmission(codec, compiler));
        Run("English authoring diagnostics preserve literal user values and exception causes", () => AuthoringDiagnosticContracts.NativeResourcesActualErrorsAndLiteralValues(serialization));
        Run("typed C# commands restore neutral continuations results and cancellation", () => CommandContracts.SaveRestoreAndCancellation(codec, compiler, serialization));
        Run("external command policies enforce preview save and rollback boundaries", () => CommandContracts.SideEffectPolicies(codec, compiler));
        Run("parallel scopes restore child waits and preserve copied identities", () => ParallelContracts.TimingRestoreAndScope(codec, compiler, serialization));
        Run("wait-any cancels task-owned audio and rejects competing writes", () => ParallelContracts.AnyCancelsOwnedAudio(codec, compiler));
        Run("rich dialogue restores paced graphemes and treats player values as literal text", () => RichTextContracts.ReadingAndRestoration(codec, compiler, serialization));
        Run("visual text formatting preserves pacing and full layout stays stable during reveal", RichTextContracts.FormattingAndStableLayout);
        Run("NVL pages preserve per-line values across language native restore and rollback", () => ReadingPresentationContracts.NvlSaveLanguageAndRollback(codec, compiler, serialization));
        Run("editable reading layouts and repeated geometry use the actual runtime bindings", () => ReadingPresentationContracts.LayoutsAndGeometry(codec, compiler, serialization));
        Run("automatic choice saves freeze boundaries rotate slots and ignore load restoration", () => PersistenceContracts.AutomaticBoundaries(codec, compiler, serialization));
        Run("scroll following tracks revealed text and respects manual review", ReadingPresentationContracts.RevealTailFollowing);
        Run("large-font long dialogue stays readable and keyboard page review consumes input", () => ReadingPresentationContracts.LongReadingAndKeyboardPages(codec, compiler));
        Run("production preview compiles unsaved drafts with isolated explicit preconditions", () => PreviewContracts.DraftAndPreconditions(codec, compiler, serialization));
        Run("recorded route preview stops at targets and retains actionable failures", () => PreviewContracts.RecordedRoutes(codec, compiler));
        Run("selected choice marks restore with saves and discard abandoned rollback futures", () => InteractionContracts.SelectedMarks(codec, compiler, serialization));
        Run("actual route recording preserves loop visits and consumes mixed input once", () => InteractionContracts.RecordedLoopAndInputOwnership(codec, compiler));
        Run("paragraph edits preserve source text and artwork sizing preserves aspect ratio", () =>
        {
            string text = "甲\r\n👩🏽‍💻\n";
            string inserted = ParagraphText.InsertAfter(text, 1);
            Check(inserted == "甲\n👩🏽‍💻\n\n" && ParagraphText.RemoveEmpty(inserted, 2) == "甲\n👩🏽‍💻\n", "Paragraph editing lost text or trailing empty lines.");
            bool rejected = false; try { ParagraphText.RemoveEmpty(text, 0); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Removing an empty paragraph deleted nonempty authored text.");
            var contain = Inno.VisualNovel.UI.Layout.UiImageSizing.Fit(new(0, 0, 200, 200), 400, 200, Inno.VisualNovel.Model.Interface.UiImageFit.Contain);
            var cover = Inno.VisualNovel.UI.Layout.UiImageSizing.Fit(new(0, 0, 200, 200), 400, 200, Inno.VisualNovel.Model.Interface.UiImageFit.Cover);
            Check(contain == new Inno.Core.Mathematics.Rect(0, 50, 200, 100) && cover == new Inno.Core.Mathematics.Rect(-100, 0, 400, 200), "Artwork fit stretched the source aspect ratio.");
        });

        if (args.Length == 2 && args[0] == "--write-sample" && failures.Count == 0)
        {
            string root = Path.GetFullPath(args[1]);
            string directory = Path.Combine(root, "Assets", "~Samples", "FirstStory");
            Directory.CreateDirectory(directory);
            using var logs = new LogRouter();
            using var assets = new AssetPipeline(modules, types, serialization, identities, new DiagnosticHub(), logs,
                AssetPipelineOptions.Create(Path.Combine(root, "Assets"), Path.Combine(root, "Library", "ContentWriter")) with
                { enableFileSystemWatcher = false, deferUnavailableExtensions = true });
            string destination = Path.Combine(directory, "FirstStory.ivnstory");
            if (!File.Exists(destination))
                File.WriteAllBytes(destination, assets.CreateSourceStore().Encode(FirstStoryTemplate.Create(codec).ToAsset()));
            Console.WriteLine("Native sample written: " + destination);
        }
        Console.WriteLine($"Contracts: {passed} passed, {failures.Count} failed.");
        return failures.Count == 0 ? 0 : 1;

        Inno.VisualNovel.Model.Programs.StoryProgram Compile(StoryDraftState draft)
        {
            StoryCompilationResult result = compiler.Compile(draft.ToAsset());
            Check(result.success, string.Join("; ", result.diagnostics.Select(static item => item.code + ": " + item.message)));
            return result.program!;
        }
        void Run(
            string name,
            Action test
        ) {
            string filter = Environment.GetEnvironmentVariable("INNO_VN_CONTRACT_FILTER") ?? string.Empty;
            if (filter.Length > 0 && !filter.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase))) return;
            try { test(); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception failure) { failures.Add(name); Console.WriteLine("FAIL " + name + Environment.NewLine + failure); }
        }
    }

    private static void Check(
        bool condition,
        string message
    ) {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Throws<T>(Action operation) where T : Exception
    {
        try { operation(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
