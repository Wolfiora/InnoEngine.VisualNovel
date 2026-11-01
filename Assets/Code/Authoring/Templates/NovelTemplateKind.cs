namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Identifies complete editable project starting points offered by the creation wizard.</summary>
public enum NovelTemplateKind
{
    /// <summary>Creates only the game shell and one editable opening line.</summary>
    Blank,
    /// <summary>Creates a short character-led story with two routes and scenery changes.</summary>
    Branching,
    /// <summary>Creates linked lessons for logic, performance, reading layouts and collections.</summary>
    Showcase
}
