using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.UI.Binding;

namespace Inno.VisualNovel.Player.Binding;

internal sealed class GameShellDataSource(
    NovelSession session,
    SavePageDataSource saves,
    CollectionPageDataSource collections,
    HistoryPageDataSource history,
    Persistence.PlayerProfileController? profile = null
) : UiDataSource
{
    private readonly NovelSessionDataSource m_session = new(session);
    internal string confirmation { get; set; } = string.Empty;
    internal bool confirmProfileDefaults { get; set; }
    internal string? pageRole { get; set; }
    private string PageRole => pageRole ?? session.page;
    internal System.Func<UI.Input.UiActionRequest, bool>? canInvoke { get; set; }
    internal System.Func<UI.Input.UiActionRequest, bool>? selectedAction { get; set; }
    internal System.Func<string>? actionError { get; set; }
    internal Interface.Data.NovelUiDataSourceHost? customSources { get; set; }
    /// <inheritdoc />
    public override InnoEngine.Rendering.TextureAsset? ResolveImage(InnoEngine.Rendering.TextureAsset? image) => session.localization.resources.Image(image);
    /// <inheritdoc />
    public override Inno.VisualNovel.UI.Text.UiRichTextResources ResolveTextResources(Inno.VisualNovel.UI.Text.UiRichTextResources resources) => session.localization.resources.Text(resources);
    /// <inheritdoc />
    public override bool CanInvoke(UI.Input.UiActionRequest action) => canInvoke?.Invoke(action) ?? true;
    /// <inheritdoc />
    public override bool IsActionSelected(UI.Input.UiActionRequest action) => selectedAction?.Invoke(action) ?? false;
    /// <inheritdoc />
    public override Inno.VisualNovel.UI.Text.UiRichText? ReadRichText(
        string path,
        UiDataRow? row
    ) => m_session.ReadRichText(path, row);
    /// <inheritdoc />
    public override string Localize(
        string id,
        string source
    ) => id.Length == 0 ? source : session.localization.Resolve(id, source);
    /// <inheritdoc />
    public override int GetTextLimit(
        string path,
        int authoredLimit
    ) => m_session.GetTextLimit(path, authoredLimit);
    /// <inheritdoc />
    public override bool TryRead(string path, out NovelValue value)
    {
        if (path.StartsWith("custom.", System.StringComparison.Ordinal)) return customSources is not null ? customSources.TryRead(path, out value) : Missing(out value);
        if (path == "interface.dataError") { value = NovelValue.String(customSources?.error ?? ""); return true; }
        if (path == "interface.error" || path == "game.error" && actionError?.Invoke() is { Length: > 0 })
        { value = NovelValue.String(actionError?.Invoke() ?? ""); return true; }
        if (path.StartsWith("page.", System.StringComparison.Ordinal)) return session.pageState.TryRead(PageRole, path[5..], out value);
        bool failed = profile is { loading: false, error.Length: > 0 };
        value = path switch
        {
            "profile.ready" => NovelValue.Boolean(profile?.ready ?? true),
            "profile.loading" => NovelValue.Boolean(profile?.loading ?? false),
            "profile.failed" => NovelValue.Boolean(failed),
            "profile.error" => NovelValue.String(profile?.error ?? ""),
            "profile.saving" => NovelValue.Boolean(profile?.saving ?? false),
            "profile.canUseDefaults" => NovelValue.Boolean(failed && profile is { ready: false } && !confirmProfileDefaults),
            "profile.confirmDefaults" => NovelValue.Boolean(confirmProfileDefaults && profile is { ready: false, loading: false }),
            _ => NovelValue.Boolean(false)
        };
        if (path.StartsWith("profile.", System.StringComparison.Ordinal)) return path is "profile.ready" or "profile.loading" or "profile.failed" or "profile.error" or "profile.saving" or "profile.canUseDefaults" or "profile.confirmDefaults";
        if (path == "confirmation.message") { value = NovelValue.String(confirmation); return true; }
        return saves.TryRead(path, out value) || collections.TryRead(path, out value) || history.TryRead(path, out value) || m_session.TryRead(path, out value);
    }
    /// <inheritdoc />
    public override bool TryWrite(
        string path,
        NovelValue value
    ) => path.StartsWith("custom.", System.StringComparison.Ordinal) ? customSources?.TryWrite(path, value) ?? false
        : path.StartsWith("page.", System.StringComparison.Ordinal) ? session.pageState.TryWrite(PageRole, path[5..], value)
        : collections.TryWrite(path, value) || m_session.TryWrite(path, value);
    /// <inheritdoc />
    public override bool TryWrite(
        string path,
        NovelValue value,
        UiDataRow? row
    ) => row?.sourcePath.StartsWith("custom.", System.StringComparison.Ordinal) == true ? customSources?.TryWrite(path, value, row) ?? false
        : path.StartsWith("item.", System.StringComparison.Ordinal) ? m_session.TryWrite(path, value, row) : TryWrite(path, value);
    /// <inheritdoc />
    public override InnoEngine.Rendering.TextureAsset? ReadImage(
        string path,
        UiDataRow? row
    ) => path.StartsWith("custom.", System.StringComparison.Ordinal) ? customSources?.ReadImage(path, row) : collections.ReadImage(path, row) ?? m_session.ReadImage(path, row);
    /// <inheritdoc />
    public override Inno.VisualNovel.UI.Imaging.UiBitmap? ReadBitmap(
        string path,
        UiDataRow? row
    ) => saves.ReadBitmap(path, row);
    /// <inheritdoc />
    public override IReadOnlyList<UiDataRow> ReadRows(string path)
    {
        if (path.StartsWith("custom.", System.StringComparison.Ordinal)) return customSources?.ReadRows(path) ?? [];
        if (path.StartsWith("page.", System.StringComparison.Ordinal))
        {
            if (!TryRead(path, out var list) || list.kind != NovelValueKind.List) return [];
            return list.items.Select(static (value, index) => new UiDataRow(index.ToString(CultureInfo.InvariantCulture), new Dictionary<string, NovelValue>
            { ["value"] = value.Copy(), ["text"] = NovelValue.String(value.ToString()), ["index"] = NovelValue.Integer(index), ["enabled"] = NovelValue.Boolean(true) })).ToArray();
        }
        return path == "history" ? history.ReadRows(path) : path == "saves.slots" ? saves.ReadRows(path) : path == "collections.items" ? collections.ReadRows(path) : m_session.ReadRows(path);
    }
    private static bool Missing(out NovelValue value) { value = NovelValue.Boolean(false); return false; }
}
