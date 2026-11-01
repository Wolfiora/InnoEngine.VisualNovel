using System;
using System.IO;
using System.Text;
using Inno.VisualNovel.Model.Localization;

namespace Inno.VisualNovel.Authoring.LocalizationEditing;

/// <summary>Reads external translation exchanges and writes exports within the active project's artifacts directory.</summary>
public static class LocalizationExchangeFiles
{
    /// <summary>Reads a bounded UTF-8 exchange without modifying the source file.</summary>
    /// <param name="path">The explicitly selected CSV/TSV path.</param>
    /// <returns>The complete exchange text.</returns>
    public static string Read(string path)
    {
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidOperationException("Translation file exceeds the 32 MiB input budget.");
        return File.ReadAllText(path, new UTF8Encoding(false, true));
    }
    /// <summary>Exports a native table to an explicit project-owned artifact location.</summary>
    /// <param name="projectDirectory">The current editor project.</param>
    /// <param name="table">The detached native declaration.</param>
    /// <param name="documentId">The shared document identity used for the export filename.</param>
    /// <param name="separator">Comma or tab.</param>
    /// <returns>The absolute exported path.</returns>
    public static string Export(
        string projectDirectory,
        LocalizationTableAsset table,
        Guid documentId,
        char separator
    ) {
        string root = Path.GetFullPath(projectDirectory);
        string directory = Path.GetFullPath(Path.Combine(root, "artifacts", "Exports"));
        if (!directory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Translation export must remain inside the current project.");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "Localization-" + documentId.ToString("N") + (separator == '\t' ? ".tsv" : ".csv"));
        File.WriteAllText(path, LocalizationDelimitedText.Export(table, separator), new UTF8Encoding(false)); return path;
    }
}
