using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Player.Persistence;

/// <summary>Requests one automatic checkpoint per newly entered choice without writing again on load or rollback.</summary>
public sealed class AutomaticSaveController
{
    private readonly NovelSession m_session;
    private readonly SaveSlotController? m_slots;
    private readonly bool m_choices;
    private long m_revision = -1, m_interaction = -1;
    private long m_script = -1;
    /// <summary>Creates a view-owned policy over an optional host-provided native save service.</summary>
    /// <param name="session">The isolated game owner.</param>
    /// <param name="slots">The matching IO coordinator, absent when the host lacks persistence.</param>
    /// <param name="choices">Whether decision boundaries are enabled by the project.</param>
    public AutomaticSaveController(
        NovelSession session,
        SaveSlotController? slots,
        bool choices
    ) { m_session = session; m_slots = slots; m_choices = choices; }
    /// <summary>Observes the owner thread's latest interaction without running story or storage work on callbacks.</summary>
    public void Observe()
    {
        if (m_session.isReplay || m_session.story is not { } story) return;
        if (m_revision != m_session.stateRevision)
        {
            m_revision = m_session.stateRevision;
            m_interaction = m_session.lastReplacement == SessionReplacement.NewGame ? -1 : story.interactionSequence;
            m_script = m_session.lastReplacement == SessionReplacement.NewGame ? 0 : m_session.automaticSaveSequence;
        }
        if (m_script != m_session.automaticSaveSequence)
        {
            m_script = m_session.automaticSaveSequence;
            if (m_slots is null) story.Fail(m_session.automaticSaveSource + ": Explicit automatic save requires a native persistence service.");
            else if (!m_session.isAutomaticSaveBoundary) story.Fail(m_session.automaticSaveSource + ": Automatic-save boundary advanced before its persistence owner observed it.");
            else _ = m_slots.RequestAutomatic();
        }
        if (m_interaction == story.interactionSequence) return;
        m_interaction = story.interactionSequence;
        if (m_choices && m_session.interactiveStory?.status == StoryStatus.Choice) _ = m_slots?.RequestAutomatic();
    }
}
