using System;
using Inno.VisualNovel.Model.Text;

namespace Inno.VisualNovel.Narrative.Reading;

/// <summary>Evaluates grapheme reveal and pauses from absolute logical time without frame accumulation drift.</summary>
public sealed class TextRevealSchedule
{
    private readonly double[] m_times;
    /// <summary>Builds a deterministic reveal schedule from normalized text and author pacing.</summary>
    /// <param name="document">The resolved immutable line.</param>
    /// <param name="charactersPerSecond">The base positive reveal rate.</param>
    /// <param name="punctuationPause">Optional pause after sentence punctuation.</param>
    public TextRevealSchedule(
        NovelTextDocument document,
        double charactersPerSecond,
        double punctuationPause = 0
    ) {
        if (!double.IsFinite(charactersPerSecond) || charactersPerSecond <= 0 || !double.IsFinite(punctuationPause) || punctuationPause is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(charactersPerSecond));
        m_times = new double[document.elements.Count]; double time = 0, speed = 1; int beat = 0;
        for (int index = 0; index < m_times.Length; index++)
        {
            int end = index + 1 == m_times.Length ? document.text.Length : document.elements[index + 1];
            while (beat < document.beats.Count && document.beats[beat].offset < end)
            { var current = document.beats[beat++]; time += current.pause; speed = current.speed; }
            time += 1 / (charactersPerSecond * speed); m_times[index] = time;
            string cluster = document.text[document.elements[index]..end];
            if (cluster is "." or "!" or "?" or "。" or "！" or "？" or "…") time += punctuationPause;
        }
        while (beat < document.beats.Count) time += document.beats[beat++].pause;
        duration = time;
    }
    /// <summary>Gets total reveal time including a trailing author pause.</summary>
    public double duration { get; }
    /// <summary>Returns the number of complete graphemes revealed at the supplied absolute time.</summary>
    /// <param name="seconds">Absolute nonnegative reveal time.</param>
    /// <returns>A value between zero and the total grapheme count.</returns>
    public double Evaluate(double seconds)
    {
        int low = 0, high = m_times.Length;
        while (low < high) { int middle = low + (high - low) / 2; if (m_times[middle] <= seconds) low = middle + 1; else high = middle; }
        return low;
    }
}
