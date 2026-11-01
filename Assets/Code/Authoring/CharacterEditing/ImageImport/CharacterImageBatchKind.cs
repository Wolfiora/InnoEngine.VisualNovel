namespace Inno.VisualNovel.Authoring.CharacterEditing.ImageImport;

/// <summary>Identifies the explicit filename destination used by a reviewed character image batch.</summary>
public enum CharacterImageBatchKind
{
    /// <summary>The filename does not identify a supported destination.</summary>
    Unmatched,
    /// <summary>The image replaces one pose's base part image.</summary>
    Pose,
    /// <summary>The image replaces one shared expression override.</summary>
    Expression
}
