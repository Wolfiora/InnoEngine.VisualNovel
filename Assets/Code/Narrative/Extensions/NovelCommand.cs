using System;
using Inno.VisualNovel.Model.Extensions;

namespace Inno.VisualNovel.Narrative.Extensions;

/// <summary>Implements one generation-owned operation whose continuation is explicit neutral data.</summary>
public abstract class NovelCommand : IDisposable
{
    /// <summary>Gets the stable typed contract; construction and metadata access must be side-effect free.</summary>
    public abstract NovelCommandDefinition definition { get; }
    /// <summary>Executes one owner-thread step; returns true only after a successful complete result.</summary>
    /// <param name="context">A scoped context invalid outside this call.</param>
    /// <param name="state">The invocation's explicit continuation.</param>
    /// <param name="seconds">Elapsed logical time, zero on initial entry.</param>
    /// <returns>Whether the operation completed.</returns>
    public abstract bool Execute(
        NovelCommandContext context,
        NovelCommandState state,
        double seconds
    );
    /// <summary>Validates and privately prepares a restored continuation without changing VN or external state.</summary>
    /// <param name="state">A detached candidate payload.</param>
    public abstract void PrepareRestore(NovelCommandState state);
    /// <summary>Releases pending external work on cancellation or owner destruction without mutating VN state.</summary>
    /// <param name="state">The last explicit execution payload.</param>
    public abstract void Cancel(NovelCommandState state);
    /// <summary>Releases implementation-owned resources; successful completion also calls this method.</summary>
    public abstract void Dispose();
}
