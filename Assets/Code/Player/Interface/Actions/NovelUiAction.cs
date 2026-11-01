using System;
using Inno.VisualNovel.Model.Interface.Actions;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Player.Interface.Actions;

/// <summary>Implements a synchronous UI action; durable values belong in the supplied native session bindings, not private handler fields.</summary>
public abstract class NovelUiAction : IDisposable
{
    /// <summary>Gets the exact neutral contract exposed to authoring.</summary>
    public abstract UiActionDefinition definition { get; }
    /// <summary>Queries availability without performing writes, navigation or external effects.</summary>
    /// <param name="context">A read-only current game binding view.</param>
    /// <param name="argument">The validated typed argument.</param>
    /// <returns>Whether the current game state permits invocation.</returns>
    public virtual bool CanExecute(
        NovelUiActionContext context,
        NovelValue argument
    ) => true;
    /// <summary>Queries a selected visual state independently of keyboard focus.</summary>
    /// <param name="context">A read-only binding view.</param>
    /// <param name="argument">The validated argument.</param>
    /// <returns>Whether the action's choice is currently selected.</returns>
    public virtual bool IsSelected(
        NovelUiActionContext context,
        NovelValue argument
    ) => false;
    /// <summary>Performs one short synchronous action; asynchronous gameplay belongs in a resumable NovelCommand.</summary>
    /// <param name="context">The controlled current game bindings and page navigation.</param>
    /// <param name="argument">A detached typed argument.</param>
    public abstract void Execute(
        NovelUiActionContext context,
        NovelValue argument
    );
    /// <summary>Releases current-generation resources; persistent data must already belong to native session values.</summary>
    public virtual void Dispose() { }
}
