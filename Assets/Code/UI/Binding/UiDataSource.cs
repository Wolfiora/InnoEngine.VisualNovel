using Inno.VisualNovel.Model.Values;
using System.Collections.Generic;
using InnoEngine.Rendering;

namespace Inno.VisualNovel.UI.Binding;

/// <summary>Supplies typed values and receives validated visual control writes.</summary>
public abstract class UiDataSource
{
    /// <summary>Resolves a static or bound authored image through an optional host resource policy.</summary>
    /// <param name="image">The original borrowed resource.</param>
    /// <returns>The selected borrowed image; the default retains the original.</returns>
    public virtual TextureAsset? ResolveImage(TextureAsset? image) => image;
    /// <summary>Resolves immutable named fonts, inline images and optional fallback fonts for a page.</summary>
    /// <param name="resources">The authored page resource view.</param>
    /// <returns>The active resource view; the default preserves the source.</returns>
    public virtual Text.UiRichTextResources ResolveTextResources(Text.UiRichTextResources resources) => resources;
    /// <summary>Queries action availability without executing its behavior.</summary>
    /// <param name="action">The detached control invocation.</param>
    /// <returns>Whether the current data state permits activation.</returns>
    public virtual bool CanInvoke(Input.UiActionRequest action) => true;
    /// <summary>Queries selected presentation independently of keyboard focus or a held pointer.</summary>
    /// <param name="action">The detached control invocation.</param>
    /// <returns>Whether its choice is currently selected.</returns>
    public virtual bool IsActionSelected(Input.UiActionRequest action) => false;
    /// <summary>Supplies a full rich line and its reveal boundary independently of the ordinary visible string.</summary>
    /// <param name="path">The bound text field.</param>
    /// <param name="row">The optional repeated item.</param>
    /// <returns>A rich line, or null for ordinary literal data.</returns>
    public virtual Inno.VisualNovel.UI.Text.UiRichText? ReadRichText(
        string path,
        UiDataRow? row
    ) => null;
    /// <summary>Resolves generated raster imagery without adding it to the imported asset database.</summary>
    /// <param name="path">The authored image field.</param>
    /// <param name="row">The optional repeated item.</param>
    /// <returns>An immutable linear image, or null when absent.</returns>
    public virtual Imaging.UiBitmap? ReadBitmap(
        string path,
        UiDataRow? row
    ) => null;
    /// <summary>Resolves a declared image field independently of serializable narrative values.</summary>
    /// <param name="path">The authored image field.</param>
    /// <param name="row">The repeated item's neutral key, or null outside a repeated template.</param>
    /// <returns>A borrowed imported texture, or null when absent or locked.</returns>
    public virtual TextureAsset? ReadImage(
        string path,
        UiDataRow? row
    ) => null;
    /// <summary>Resolves literal UI text through a game-owned language view.</summary>
    /// <param name="id">The stable text identity, empty for an unlocalized literal.</param>
    /// <param name="source">The authored fallback.</param>
    /// <returns>The selected text; the default retains the source.</returns>
    public virtual string Localize(
        string id,
        string source
    ) => source;
    /// <summary>Constrains a bound text control using domain metadata without changing its authored upper bound.</summary>
    /// <param name="path">The bound string field.</param>
    /// <param name="authoredLimit">The control's authored grapheme limit.</param>
    /// <returns>The effective grapheme limit; the default preserves the authored constraint.</returns>
    public virtual int GetTextLimit(
        string path,
        int authoredLimit
    ) => authoredLimit;
    /// <summary>Reads a keyed collection for an authored repeated container.</summary>
    /// <param name="path">The collection identity.</param>
    /// <returns>Rows in display order with unique stable keys.</returns>
    public virtual IReadOnlyList<UiDataRow> ReadRows(string path) => [];
    /// <summary>Resolves a stable field path.</summary>
    /// <param name="path">The authored field identity.</param>
    /// <param name="value">Receives a detached value.</param>
    /// <returns>Whether the field exists.</returns>
    public abstract bool TryRead(string path, out NovelValue value);
    /// <summary>Reads a repeated item's detached scalar before falling back to session fields.</summary>
    /// <param name="path">The authored field identity.</param>
    /// <param name="row">The exact owner row, or null outside repetition.</param>
    /// <param name="value">Receives a detached value.</param>
    /// <returns>Whether the field exists.</returns>
    public virtual bool TryRead(
        string path,
        UiDataRow? row,
        out NovelValue value
    ) {
        if (path.StartsWith("item.", System.StringComparison.Ordinal) && row is not null && row.values.TryGetValue(path[5..], out var item))
        { value = item.Copy(); return true; }
        return TryRead(path, out value);
    }
    /// <summary>Attempts a typed control write.</summary>
    /// <param name="path">The bound field identity.</param>
    /// <param name="value">The proposed value.</param>
    /// <returns>Whether the source accepted the write.</returns>
    public abstract bool TryWrite(string path, NovelValue value);
    /// <summary>Attempts a write carrying the repeated owner's stable key; row data itself remains immutable.</summary>
    /// <param name="path">The authored field identity.</param>
    /// <param name="value">The proposed detached value.</param>
    /// <param name="row">The exact owner row, or null outside repetition.</param>
    /// <returns>Whether the source accepted the write; the default rejects item writes.</returns>
    public virtual bool TryWrite(
        string path,
        NovelValue value,
        UiDataRow? row
    ) => !path.StartsWith("item.", System.StringComparison.Ordinal) && TryWrite(path, value);
}

/// <summary>Describes one neutral keyed collection item.</summary>
/// <param name="key">Stable row identity.</param>
/// <param name="values">Typed fields addressed with the item. prefix.</param>
/// <param name="sourcePath">Optional owner collection path for routing custom source writes by stable row identity.</param>
public sealed record UiDataRow(string key, IReadOnlyDictionary<string, NovelValue> values, string sourcePath = "");
