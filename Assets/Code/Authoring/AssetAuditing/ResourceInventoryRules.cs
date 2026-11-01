using System;
using System.IO;
using System.Linq;

namespace Inno.VisualNovel.Authoring.AssetAuditing;

/// <summary>Defines the explicit VN-supported inventory scope without claiming to enumerate the engine's private importer registry.</summary>
public static class ResourceInventoryRules
{
    /// <summary>Describes evidence categories without exposing enum implementation names in creator workflows.</summary>
    /// <param name="use">The proven or unavailable closure category.</param>
    /// <returns>The current Simplified Chinese editor label.</returns>
    public static string DescribeUse(ResourceAuditUse use) => use switch
    { ResourceAuditUse.Referenced => "已引用", ResourceAuditUse.NotReferenced => "当前游戏未引用", ResourceAuditUse.AuthoringOnly => "创作数据", _ => "无法判断" };
    /// <summary>Checks validated image/font/WAV and VN native source extensions; other used formats still enter through the native closure.</summary>
    /// <param name="path">The relative source path.</param>
    /// <returns>Whether the VN tool understands this candidate source category.</returns>
    public static bool IsKnown(string path) => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".wav" or ".ttf" or ".otf"
        or ".ivnvariables" or ".ivnproject" or ".ivnstory" or ".ivncharacter" or ".ivnstage" or ".ivnclip" or ".ivnui" or ".ivntheme" or ".ivneffect" or ".ivnaudio" or ".ivnlocale" or ".ivncollection"
        or ".ivntest" or ".ivncommands" or ".ivnuiactions" or ".ivnuidata" or ".ivnlicenses" or ".ivnlabels";
    /// <summary>Identifies editor-owned source formats that are intentionally not Player content.</summary>
    /// <param name="path">The relative source path.</param>
    /// <returns>Whether ordinary game closure exclusion is expected.</returns>
    public static bool IsAuthoring(string path) => Path.GetExtension(path).ToLowerInvariant() is ".ivntest" or ".ivncommands" or ".ivnuiactions" or ".ivnuidata" or ".ivnlicenses" or ".ivnlabels";
    /// <summary>Excludes sample and generated hidden subtrees from ordinary production cleanup candidates.</summary>
    /// <param name="path">The relative source path with either platform separator.</param>
    /// <returns>Whether a tilde, hidden segment or parent traversal appears.</returns>
    public static bool IsExcluded(string path) => path.Replace('\\', '/').Split('/').Any(static part => part.StartsWith('~') || part.StartsWith('.') || part.Length == 0);
}
