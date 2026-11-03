using InnoEngine.Scene;
using InnoEditor.Inspection;
using InnoEditor.Interactions;
using Inno.VisualNovel.Integration.Components;

namespace Inno.VisualNovel.Authoring.SceneEditing;

/// <summary>Applies native performance samples as one reversible property operation on existing objects.</summary>
public sealed class NovelPerformanceEditService
{
    private readonly IEditorHistory m_history;
    private readonly IInspectionPropertyEditService m_properties;
    /// <summary>Creates an editor-generation property transaction owner.</summary>
    /// <param name="history">The host-owned history branch.</param>
    /// <param name="properties">The native scene property writer.</param>
    public NovelPerformanceEditService(
        IEditorHistory history,
        IInspectionPropertyEditService properties
    ) { m_history = history; m_properties = properties; }

    /// <summary>Applies an explicit time without starting continuous Edit-world playback.</summary>
    /// <param name="performance">The real component and its object bindings.</param>
    /// <param name="seconds">The requested logical clip position.</param>
    public void Seek(
        NovelPerformanceComponent performance,
        double seconds
    ) {
        using var transaction = m_history.BeginTransaction("Apply performance position");
        performance.Seek(seconds, Change);
        transaction.Commit();
    }
    private void Change(
        EngineObject owner,
        string property,
        System.Action edit
    ) => m_properties.ChangeProperty(owner, property, edit, "Apply performance position", null, null);
}
