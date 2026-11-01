namespace Inno.VisualNovel.Model.Interface;

/// <summary>Aligns an element inside an automatic container's allocated slot; absolute panels continue to use anchors and pivots.</summary>
public enum UiAlignment
{
    /// <summary>Places the element at the leading edge after its margin.</summary>
    Start,
    /// <summary>Centers the element inside the available margin box.</summary>
    Center,
    /// <summary>Places the element at the trailing edge before its margin.</summary>
    End,
    /// <summary>Uses the available slot size subject to the element's minimum and maximum limits.</summary>
    Stretch
}
