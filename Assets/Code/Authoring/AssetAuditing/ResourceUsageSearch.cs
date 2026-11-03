using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Assets;
using Inno.VisualNovel.Authoring.Publishing;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Programs;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Model.Story.Nodes.Interface;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Authoring.AssetAuditing;

/// <summary>Locates saved instruction and UI references using native dependency ancestry instead of text matching resource names.</summary>
public sealed class ResourceUsageSearch : IDisposable
{
    private sealed record Chapter(Guid id, AssetPath path, StoryProgram program);
    private sealed record Page(Guid id, AssetPath path, ScreenAsset screen);
    private readonly IEnumerator<ResourceUsageLocation?> m_scan;
    private readonly List<ResourceUsageLocation> m_results = [];
    /// <summary>Freezes managed source declarations, using native parent edges to identify containing resource libraries.</summary>
    /// <param name="project">The selected game's saved roots.</param>
    /// <param name="closure">Its previously collected native dependency snapshot.</param>
    /// <param name="resourceId">The target native persistent identity.</param>
    public ResourceUsageSearch(
        NovelProjectAsset project,
        NovelPublishReport closure,
        Guid resourceId
    ) {
        if (resourceId == Guid.Empty) throw new ArgumentException("Usage lookup needs a native persistent resource identity.", nameof(resourceId));
        var byPath = closure.assets.ToDictionary(static item => item.path.ToString(), StringComparer.Ordinal);
        var byId = closure.assets.ToDictionary(static item => item.id);
        var containing = new HashSet<Guid> { resourceId }; var pending = new Queue<Guid>(); pending.Enqueue(resourceId);
        while (pending.TryDequeue(out var id))
            if (byId.TryGetValue(id, out var item))
                foreach (string parent in item.referrers)
                    if (byPath.TryGetValue(parent, out var owner) && containing.Add(owner.id)) pending.Enqueue(owner.id);
        var chapters = project.chapters.Select(static chapter => new Chapter(chapter.identity.persistentId, chapter.assetPath,
            StoryProgramCopy.Copy(chapter.program ?? throw new InvalidOperationException("Save and compile chapters before locating resource usage.")))).ToArray();
        var pages = project.screens.Concat(project.themes.SelectMany(static theme => theme.screens)).Where(static page => page.screen is not null)
            .Select(static page => page.screen!).Distinct().Select(static page => new Page(page.identity.persistentId, page.assetPath, UiDefinitionCopy.Copy(page))).ToArray();
        var stages = project.stages.ToDictionary(static asset => asset.resourceId, static asset => asset.identity.persistentId, StringComparer.Ordinal);
        var characters = project.characters.ToDictionary(static asset => asset.resourceId, static asset => asset.identity.persistentId, StringComparer.Ordinal);
        var audio = project.audio.ToDictionary(static asset => asset.resourceId, static asset => asset.identity.persistentId, StringComparer.Ordinal);
        var clips = project.performances.ToDictionary(static asset => asset.resourceId, static asset => asset.identity.persistentId, StringComparer.Ordinal);
        var effects = project.screenEffects.ToDictionary(static asset => asset.resourceId, static asset => asset.identity.persistentId, StringComparer.Ordinal);
        var transitions = project.transitions.ToDictionary(static asset => asset.resourceId, static asset => asset.identity.persistentId, StringComparer.Ordinal);
        var particles = project.particles.ToDictionary(static asset => asset.resourceId, static asset => asset.identity.persistentId, StringComparer.Ordinal);
        var themes = project.themes.ToDictionary(static asset => asset.resourceId, static asset => asset.identity.persistentId, StringComparer.Ordinal);
        var screens = project.screens.ToDictionary(static page => page.role, static page => page.screen?.identity.persistentId ?? Guid.Empty, StringComparer.Ordinal);
        m_scan = Scan().GetEnumerator();

        IEnumerable<ResourceUsageLocation?> Scan()
        {
            if (byId.TryGetValue(resourceId, out var target))
                foreach (string parent in target.referrers)
                    if (byPath.TryGetValue(parent, out var owner)) yield return new(owner.id, owner.path, "", "", "Direct native asset reference", false);
            foreach (var chapter in chapters)
                foreach (var instruction in chapter.program.instructions)
                {
                    var roots = new HashSet<Guid>();
                    if (instruction.stage is { } stage)
                    {
                        if (stage.operation == StageOperation.SetStage) Add(stages, stage.resourceId);
                        if (stage.operation == StageOperation.ShowCharacter) Add(characters, stage.resourceId);
                        if (stage.operation == StageOperation.StartParticles) Add(particles, stage.resourceId);
                    }
                    if (instruction.dialogue is { } line) { Add(characters, line.characterId); Add(audio, line.voiceCueId); }
                    if (instruction.audio is { } sound) Add(audio, sound.cueId);
                    if (instruction.performance is { } performance) Add(clips, performance.clipId);
                    if (instruction.screenEffect is { } effect) Add(effects, effect.resourceId);
                    if (instruction.transition is { } transition) { Add(transitions, transition.resourceId); Add(stages, transition.nextStage); }
                    if (instruction.system is { } system)
                    { if (system.operation == SystemOperation.OpenPage) Add(screens, system.argument); if (system.operation == SystemOperation.SetTheme) Add(themes, system.argument); }
                    bool match = roots.Overlaps(containing);
                    yield return match ? new(chapter.id, chapter.path, instruction.sourceNodeId.Length > 0 ? instruction.sourceNodeId : instruction.nodeId, "",
                        roots.Contains(resourceId) ? "Story instruction directly uses this resource" : "Story instruction uses a library containing this resource", !roots.Contains(resourceId)) : null;
                    void Add(
                        Dictionary<string, Guid> library,
                        string key
                    ) { if (library.TryGetValue(key, out var id)) roots.Add(id); }
                }
            foreach (var page in pages)
            {
                if (Matches(page.screen.font) || page.screen.textFonts.Any(binding => Matches(binding.font)) || page.screen.textIcons.Any(binding => Matches(binding.image)))
                    yield return new(page.id, page.path, "", page.screen.root.id, "Page font or inline image declaration", false);
                foreach (var row in ScreenEditService.Flatten(page.screen))
                    yield return Matches(row.element.image) || Matches(row.element.font) ? new(page.id, page.path, "", row.element.id, "Control image or font reference", false) : null;
            }
        }
        bool Matches(AssetObject? asset) => asset is not null && asset.identity.persistentId == resourceId;
    }
    /// <summary>Gets immutable collected declaration and instruction locations.</summary>
    public IReadOnlyList<ResourceUsageLocation> results => m_results.AsReadOnly();
    /// <summary>Gets the number of scanned candidate locations, including nonmatches.</summary>
    public int scanned { get; private set; }
    /// <summary>Gets whether all frozen candidates were visited.</summary>
    public bool completed { get; private set; }
    /// <summary>Gets whether the owner cancelled the lookup.</summary>
    public bool cancelled { get; private set; }
    /// <summary>Gets a source enumeration failure, empty when healthy.</summary>
    public string error { get; private set; } = "";
    /// <summary>Gets the transient enumeration cause without flattening its authoring format arguments.</summary>
    public Exception? failure { get; private set; }
    /// <summary>Advances a bounded number of candidate locations on the native asset owner thread.</summary>
    /// <param name="budget">One to 4096 candidates.</param>
    public void Step(int budget = 256)
    {
        if (budget is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(budget));
        try
        {
            for (int index = 0; index < budget && !completed && !cancelled; index++)
            {
                if (!m_scan.MoveNext()) { Finish(); break; }
                scanned++;
                if (m_scan.Current is { } match) m_results.Add(match);
                if (m_results.Count >= 100000) { error = "Usage results reached 100000 entries; refine the game content scope."; Finish(); }
            }
        }
        catch (Exception cause) when (cause is not OutOfMemoryException) { failure = cause; error = cause.Message; Finish(); }
    }
    /// <summary>Cancels enumeration while retaining already located results.</summary>
    public void Cancel() { if (completed || cancelled) return; cancelled = true; m_scan.Dispose(); }
    /// <inheritdoc />
    public void Dispose() => Cancel();
    private void Finish() { if (completed) return; completed = true; m_scan.Dispose(); }
}

/// <summary>Describes a native owner and stable authored location without storing a live selection object.</summary>
/// <param name="ownerId">The source asset's persistent identity.</param>
/// <param name="ownerPath">Its path at the start of the lookup.</param>
/// <param name="nodeId">The authored narrative node, empty for other source types.</param>
/// <param name="elementId">The authored UI element, empty outside UI sources.</param>
/// <param name="description">The exact declared reference relationship.</param>
/// <param name="throughLibrary">Whether the instruction uses a containing library instead of the target directly.</param>
public sealed record ResourceUsageLocation(Guid ownerId, AssetPath ownerPath, string nodeId, string elementId, string description, bool throughLibrary);
