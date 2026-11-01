namespace Inno.VisualNovel.Model.Story.Nodes.Reading;

/// <summary>Declares the author-selected reading layout independently of any concrete game screen.</summary>
public enum DialoguePresentation
{
    /// <summary>Shows one line in the conventional lower dialogue box.</summary>
    Adv,
    /// <summary>Appends lines to an explicitly cleared full-page reading surface.</summary>
    Nvl,
    /// <summary>Shows a positioned speech panel.</summary>
    Bubble,
    /// <summary>Shows positioned text over the stage without a panel.</summary>
    Overlay,
    /// <summary>Shows a centered document panel suitable for letters and diaries.</summary>
    Letter
}
