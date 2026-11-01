using System;

namespace Inno.VisualNovel.Model.Story.Nodes.Interface;

/// <summary>Shares system action shape validation independently of a particular game resource library.</summary>
public static class SystemNodeRules
{
    /// <summary>Rejects unknown operations, invalid page roles and unintended action arguments.</summary>
    /// <param name="node">The detached action declaration.</param>
    public static void Validate(SystemNode node)
    {
        if (!Enum.IsDefined(node.operation) || node.argument.Length > 256
            || node.operation == SystemOperation.OpenPage && !IsPageRole(node.argument)
            || node.operation is SystemOperation.ReturnTitle or SystemOperation.AutoSave && node.argument.Length > 0
            || node.operation is SystemOperation.Checkpoint or SystemOperation.RollbackBarrier && string.IsNullOrWhiteSpace(node.argument))
            throw new InvalidOperationException("System action requires a known operation and a bounded explicit page or theme identity.");
    }
    /// <summary>Reports whether an applied action yields a stable continuation before its next instruction.</summary>
    /// <param name="operation">The concrete system action.</param>
    /// <returns>Whether the owner must resume the continuation on a later update.</returns>
    public static bool Suspends(SystemOperation operation) => operation is SystemOperation.OpenPage or SystemOperation.ReturnTitle or SystemOperation.Checkpoint or SystemOperation.AutoSave;
    /// <summary>Checks a stable role without interpreting it as a path or a CLR identifier.</summary>
    /// <param name="role">The authored page role.</param>
    /// <returns>Whether the role is nonempty, bounded and composed of ordinary identifier characters.</returns>
    public static bool IsPageRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role) || role.Length > 128) return false;
        foreach (char character in role) if (!char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-' or '_')) return false;
        return true;
    }
}
