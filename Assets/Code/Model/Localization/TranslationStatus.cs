namespace Inno.VisualNovel.Model.Localization;

/// <summary>Distinguishes unfinished translations from text approved for runtime use.</summary>
public enum TranslationStatus
{
    /// <summary>Retains an editable draft while runtime uses the source text.</summary>
    Draft,
    /// <summary>Makes a translation available when its source revision matches.</summary>
    Translated,
    /// <summary>Records completed human review of the matching source revision.</summary>
    Reviewed
}
