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
        bool changed = fields.Select(id + "/operation", fields.Localize("游戏系统动作"), ref operation, new[]
        { ("OpenPage", fields.Localize("打开页面并暂停剧情")), ("SetTheme", fields.Localize("切换主题")), ("ReturnTitle", fields.Localize("返回标题并暂停剧情")), ("Checkpoint", fields.Localize("创建命名检查点")), ("AutoSave", fields.Localize("保存到自动槽")), ("RollbackBarrier", fields.Localize("设置回滚屏障")) });
        if (changed)
        { action.operation = Enum.Parse<SystemOperation>(operation); action.argument = action.operation switch
            { SystemOperation.OpenPage => "settings", SystemOperation.Checkpoint => "checkpoint", SystemOperation.RollbackBarrier => "不可逆剧情操作", _ => string.Empty }; }
        if (action.operation is SystemOperation.Checkpoint or SystemOperation.RollbackBarrier)
        { string argument = action.argument; changed |= fields.Text(id + "/boundary", action.operation == SystemOperation.Checkpoint ? fields.Localize("检查点名称") : fields.Localize("屏障原因"), ref argument); action.argument = argument; }
        if (action.operation == SystemOperation.OpenPage)
        {
            var game = NovelResourceSelection.Get<NovelProjectAsset>(interactions);
            var options = fields.BuiltIn(BuiltInUiChoices.Pages).Concat(game?.screens.Select(static binding => (binding.role, binding.role)) ?? []).DistinctBy(static item => item.Item1);
            string role = action.argument; changed |= fields.Select(id + "/page", fields.Localize("目标页面"), ref role, options);
            if (ImGui.CollapsingHeader(fields.Caption("自定义页面角色##" + id, fields.Localize("自定义页面角色##") + id))) changed |= fields.Text(id + "/custom-page", fields.Localize("已关联的自定义角色"), ref role);
            action.argument = role;
        }
        if (action.operation == SystemOperation.SetTheme)
        {
            string theme = action.argument; var game = NovelResourceSelection.Get<NovelProjectAsset>(interactions);
            if (game is not null) changed |= fields.Select(id + "/theme", fields.Localize("游戏主题"), ref theme, new[] { (string.Empty, fields.Localize("页面自身样式")) }.Concat(game.themes.Select(static item => (item.resourceId, item.title))));
            else changed |= fields.Text(id + "/theme-id", fields.Localize("主题资源 ID（空为页面样式）"), ref theme);
            if (NovelResourceSelection.Get<ThemeAsset>(interactions) is { } selected && ImGui.Button(fields.Label("使用所选主题"))) { fields.Finish(); theme = selected.resourceId; changed = true; }
            action.argument = theme;
        }
        if (changed) documents.Edit(documents.activeDocumentId, fields.Localize("编辑游戏系统动作"), draft => documents.edits.SetSettings(draft, node.id, action), fields.mergeKey);
        EditorUiSession.current.widgets.Hint(fields.Localize("页面、检查点和自动保存先提交系统动作，再停在下一条指令之前；恢复不会重放当前动作。显式自动保存需要原生持久化服务。主题随存档和回滚恢复，并行演出中禁止修改会话系统状态。"));
    }
}
