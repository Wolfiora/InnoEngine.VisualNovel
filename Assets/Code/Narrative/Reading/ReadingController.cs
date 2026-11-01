using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Programs;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Narrative.Reading;

internal sealed class ReadingController(
    IReadOnlyDictionary<string, StoryInstruction> instructions,
    StorySpeakerSource? speakers = null
)
{
    private DialogueParameter[] m_parameters = [];
    private ReadingPageController m_page = new();
    private TextRevealSchedule m_schedule = new(NovelTextDocument.Literal(string.Empty), 30);
    internal NovelTextDocument document { get; private set; } = NovelTextDocument.Literal(string.Empty);
    internal string speaker { get; private set; } = string.Empty;
    internal string locale { get; private set; } = string.Empty;
    internal int visibleCount { get; private set; }
    internal string visibleText => document.Prefix(visibleCount);
    internal double duration => m_schedule.duration;
    internal NovelTextDocument page => m_page.document;
    internal int visiblePageCount => Math.Max(0, page.elements.Count - document.elements.Count) + visibleCount;
    internal bool IsComplete(double elapsed) => visibleCount >= document.elements.Count && elapsed >= duration;
    internal void Evaluate(double elapsed) => visibleCount = (int)m_schedule.Evaluate(elapsed);
    internal void Complete() => visibleCount = document.elements.Count;
    internal void InheritPage(ReadingController source) => m_page.Restore(source.m_page.Capture());

    internal void Enter(
        StoryInstruction instruction,
        StoryTextSource? texts,
        Func<string, NovelValue> variables
    ) {
        DialogueNode definition = instruction.dialogue!;
        m_parameters = Names(definition).Order(StringComparer.Ordinal).Select(id => new DialogueParameter { id = id, value = variables(id).ToString() }).ToArray();
        speaker = Resolve(definition, m_parameters, texts, true);
        SetDocument(NovelTextMarkup.Parse(Resolve(definition, m_parameters, texts, false)), definition);
        if (definition.presentation != DialoguePresentation.Nvl || definition.clearPage) m_page.Clear();
        if (definition.presentation == DialoguePresentation.Nvl)
            m_page.Append(new() { nodeId = instruction.nodeId, markup = document.markup, speaker = speaker, parameters = m_parameters, locale = texts?.languageId ?? string.Empty });
        visibleCount = 0; locale = texts?.languageId ?? string.Empty;
    }

    internal double Reflow(
        StoryInstruction? active,
        StoryTextSource? texts,
        double elapsed
    ) {
        ReadingPageLine[] lines = m_page.Capture(); string language = texts?.languageId ?? string.Empty;
        if (lines.Any(line => line.locale != language))
        {
            foreach (var line in lines)
            {
                DialogueNode definition = instructions[line.nodeId].dialogue!;
                line.markup = Resolve(definition, line.parameters, texts, false); line.speaker = Resolve(definition, line.parameters, texts, true); line.locale = language;
            }
            m_page.Restore(lines);
        }
        if (active?.dialogue is not { } dialogue || locale == language) return elapsed;
        double fraction = duration == 0 ? 1 : Math.Clamp(elapsed / duration, 0, 1);
        speaker = Resolve(dialogue, m_parameters, texts, true);
        SetDocument(NovelTextMarkup.Parse(Resolve(dialogue, m_parameters, texts, false)), dialogue);
        locale = language; double time = fraction * duration; Evaluate(time); return time;
    }

    internal StoryExecutionState Capture(StoryExecutionState target)
    {
        target.dialogueText = document.text; target.dialogueMarkup = document.markup; target.revealProgress = visibleCount;
        target.dialogueSpeaker = speaker; target.textLocale = locale;
        target.dialogueParameters = m_parameters.Select(static item => new DialogueParameter { id = item.id, value = item.value }).ToArray();
        target.nvlLines = m_page.Capture(); return target;
    }

    internal void Restore(StoryExecutionState snapshot)
    {
        NovelTextDocument parsed = NovelTextMarkup.Parse(snapshot.dialogueMarkup);
        if (parsed.text != snapshot.dialogueText || snapshot.revealProgress > parsed.elements.Count || snapshot.revealProgress % 1 != 0)
            throw new InvalidOperationException("Snapshot rich text or reveal boundary does not match its plain text.");
        var candidatePage = new ReadingPageController(); candidatePage.Restore(snapshot.nvlLines);
        var instruction = instructions.GetValueOrDefault(snapshot.nodeId);
        if (snapshot.nvlLines.Any(line => !instructions.TryGetValue(line.nodeId, out var source) || source.dialogue?.presentation != DialoguePresentation.Nvl)
            || snapshot.status == StoryStatus.Dialogue && instruction!.dialogue!.presentation == DialoguePresentation.Nvl
                && (snapshot.nvlLines.Length == 0 || snapshot.nvlLines[^1].nodeId != snapshot.nodeId || snapshot.nvlLines[^1].markup != snapshot.dialogueMarkup))
            throw new InvalidOperationException("NVL page does not match its story instruction.");
        DialogueParameter[] parameters = ReadingPageController.Copy(new() { nodeId = snapshot.nodeId.Length == 0 ? "stopped" : snapshot.nodeId, parameters = snapshot.dialogueParameters }).parameters;
        if (snapshot.status == StoryStatus.Dialogue) ValidateParameters(instruction!.dialogue!, parameters);
        foreach (var line in snapshot.nvlLines) ValidateParameters(instructions[line.nodeId].dialogue!, line.parameters);
        SetDocument(parsed, instruction?.dialogue);
        if (snapshot.status == StoryStatus.Dialogue && m_schedule.Evaluate(snapshot.waitElapsed) != snapshot.revealProgress)
            throw new InvalidOperationException("Snapshot reveal progress does not match its logical reading clock.");
        m_parameters = parameters; m_page = candidatePage; speaker = snapshot.dialogueSpeaker; locale = snapshot.textLocale; visibleCount = (int)snapshot.revealProgress;
    }

    private void SetDocument(
        NovelTextDocument value,
        DialogueNode? definition
    ) { document = value; m_schedule = new(value, definition?.charactersPerSecond ?? 30, definition?.punctuationPause ?? 0); }

    private static HashSet<string> Names(DialogueNode definition) => NovelTextTemplate.Parse(definition.speaker).Concat(NovelTextTemplate.Parse(definition.text))
        .Where(static token => token.variable).Select(static token => token.value).ToHashSet(StringComparer.Ordinal);

    private static void ValidateParameters(
        DialogueNode definition,
        DialogueParameter[] parameters
    ) {
        if (!Names(definition).SetEquals(parameters.Select(static parameter => parameter.id))) throw new InvalidOperationException("Saved dialogue substitutions do not match the authored line.");
    }

    private string Resolve(
        DialogueNode definition,
        DialogueParameter[] parameters,
        StoryTextSource? texts,
        bool speaker
    ) {
        if (speaker && definition.characterId.Length > 0 && definition.speaker.Length == 0)
            return speakers?.ResolveName(definition.characterId, texts) ?? throw new InvalidOperationException("No character name source is configured: " + definition.characterId);
        var values = parameters.ToDictionary(static item => item.id, static item => item.value, StringComparer.Ordinal);
        string fallback = speaker ? definition.speaker : definition.text;
        string source = texts?.Resolve(speaker ? StoryTextKeys.Speaker(definition.textId) : StoryTextKeys.Dialogue(definition.textId), fallback) ?? fallback;
        var result = new StringBuilder();
        foreach (var token in NovelTextTemplate.Parse(source))
        {
            string value = token.variable ? values.TryGetValue(token.value, out string? literal) ? literal : throw new InvalidOperationException("Saved dialogue parameter is missing: " + token.value) : token.value;
            result.Append(token.variable && !speaker ? NovelTextMarkup.Escape(value) : value);
        }
        return result.ToString();
    }
}
