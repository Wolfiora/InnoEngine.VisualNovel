using System;
using System.Linq;
using InnoEngine.Graphs;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story.Nodes.Interface;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class SystemNodeSettingsView(
    StoryInspectorFields fields,
    EditorInteractions interactions
)
{
    internal void Draw(
        StoryDocumentsModule documents,
        GraphNodeRecord node
    ) {
        SystemNode action = documents.codec.Read<SystemNode>(node); string id = node.id.value, operation = action.operation.ToString();
        bool changed = fields.Select(id + "/operation", "Game system action", ref operation, new[]
        { ("OpenPage", "Open screen and suspend story"), ("SetTheme", "Switch theme"), ("ReturnTitle", "Return to title and suspend story"), ("Checkpoint", "Create named checkpoint"), ("AutoSave", "Save to automatic slot"), ("RollbackBarrier", "Set rollback barrier") });
        if (changed)
        { action.operation = Enum.Parse<SystemOperation>(operation); action.argument = action.operation switch
            { SystemOperation.OpenPage => "settings", SystemOperation.Checkpoint => "checkpoint", SystemOperation.RollbackBarrier => "Irreversible story operation", _ => string.Empty }; }
        if (action.operation is SystemOperation.Checkpoint or SystemOperation.RollbackBarrier)
        { string argument = action.argument; changed |= fields.Text(id + "/boundary", action.operation == SystemOperation.Checkpoint ? "Checkpoint name" : "Barrier reason", ref argument); action.argument = argument; }
        if (action.operation == SystemOperation.OpenPage)
        {
            var game = NovelResourceSelection.Get<NovelProjectAsset>(interactions);
            var options = BuiltInUiChoices.Pages.Concat(game?.screens.Select(static binding => (binding.role, binding.role)) ?? []).DistinctBy(static item => item.Item1);
            string role = action.argument; changed |= fields.Select(id + "/page", "Target screen", ref role, options);
            if (ImGui.CollapsingHeader("Custom screen role##" + id)) changed |= fields.Text(id + "/custom-page", "Linked custom role", ref role);
            action.argument = role;
        }
        if (action.operation == SystemOperation.SetTheme)
        {
            string theme = action.argument; var game = NovelResourceSelection.Get<NovelProjectAsset>(interactions);
            if (game is not null) changed |= fields.Select(id + "/theme", "Game theme", ref theme, new[] { (string.Empty, "Screen's own styling") }.Concat(game.themes.Select(static item => (item.resourceId, item.title))));
            else changed |= fields.Text(id + "/theme-id", "Theme resource ID (empty for screen styling)", ref theme);
            if (NovelResourceSelection.Get<ThemeAsset>(interactions) is { } selected && ImGui.Button("Use selected theme")) { fields.Finish(); theme = selected.resourceId; changed = true; }
            action.argument = theme;
        }
        if (changed) documents.Edit(documents.activeDocumentId, "Edit game system action", draft => documents.edits.SetSettings(draft, node.id, action), fields.mergeKey);
        EditorUiSession.current.widgets.Hint("Screens, checkpoints and automatic saves commit their system action before stopping ahead of the next instruction. Restoration does not repeat the action. Explicit automatic saving requires native persistence. Themes restore with saves and rollback. Parallel presentation cannot modify session system state.");
    }
}
