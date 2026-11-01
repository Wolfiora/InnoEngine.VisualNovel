using System;
using System.Collections.Generic;
using System.Linq;

namespace Inno.VisualNovel.Authoring.ScriptProjection;

/// <summary>Owns measured script-row geometry by source identity without reading or changing graph semantics.</summary>
public sealed class ScriptViewportLayout
{
    private string[] m_rows = [];
    private Dictionary<string, double> m_heights = new(StringComparer.Ordinal);
    private Dictionary<string, int> m_positions = new(StringComparer.Ordinal);
    private double[] m_prefix = [0];
    private bool m_dirty;
    private double m_total;
    /// <summary>Gets the current finite logical document height.</summary>
    public double height { get { Prepare(); return m_prefix[^1]; } }
    /// <summary>Gets the number of authored rows, independent of the visible interval.</summary>
    public int count => m_rows.Length;

    /// <summary>Updates row order while preserving measurements of retained stable source identities.</summary>
    /// <param name="rows">The finite projection, with each node appearing exactly once.</param>
    /// <param name="estimate">The initial positive height of rows not yet measured.</param>
    public void SetRows(
        IEnumerable<string> rows,
        double estimate
    ) {
        ValidHeight(estimate);
        string[] candidate = rows.ToArray();
        if (candidate.Length > 100000 || candidate.Any(string.IsNullOrWhiteSpace) || candidate.Distinct(StringComparer.Ordinal).Count() != candidate.Length)
            throw new ArgumentException("Script viewports require up to 100000 unique nonempty source identities.", nameof(rows));
        var heights = candidate.ToDictionary(static id => id, id => m_heights.GetValueOrDefault(id, estimate), StringComparer.Ordinal);
        double total = heights.Values.Sum(); ValidTotal(total);
        m_rows = candidate; m_heights = heights;
        m_positions = candidate.Select(static (id, index) => (id, index)).ToDictionary(static item => item.id, static item => item.index, StringComparer.Ordinal);
        m_total = total; m_dirty = true;
    }
    /// <summary>Invalidates width-dependent row measurements without changing source order.</summary>
    /// <param name="estimate">The positive estimate for the new width or presentation mode.</param>
    public void ResetMeasurements(double estimate)
    {
        ValidHeight(estimate);
        double total = m_rows.Length * estimate; ValidTotal(total);
        foreach (string id in m_rows) m_heights[id] = estimate;
        m_total = total; m_dirty = true;
    }
    /// <summary>Records actual rendered height for one retained source row.</summary>
    /// <param name="id">The original graph node identity.</param>
    /// <param name="height">Positive logical height including row spacing.</param>
    public void Measure(
        string id,
        double height
    ) {
        ValidHeight(height);
        if (!m_heights.TryGetValue(id, out double before)) throw new ArgumentException("The measured script row is missing: " + id, nameof(id));
        if (Math.Abs(height - before) < .01) return;
        double total = m_total - before + height; ValidTotal(total);
        m_heights[id] = height; m_total = total; m_dirty = true;
    }
    /// <summary>Finds the exact interval needed for viewport painting plus bounded overscan.</summary>
    /// <param name="top">The scroll offset relative to the first script row; negative offsets are allowed above it.</param>
    /// <param name="viewportHeight">The finite nonnegative visible extent.</param>
    /// <param name="overscan">Additional logical pixels prepared before and after the viewport.</param>
    /// <returns>Inclusive first index, exclusive end index, and unpainted leading/trailing geometry.</returns>
    public (int first, int end, double before, double after) Visible(
        double top,
        double viewportHeight,
        double overscan = 240
    ) {
        if (!double.IsFinite(top) || !double.IsFinite(viewportHeight) || viewportHeight < 0 || !double.IsFinite(overscan) || overscan < 0)
            throw new ArgumentOutOfRangeException(nameof(top), "Script viewport geometry must be finite and nonnegative except for its relative top.");
        Prepare();
        int first = After(Math.Max(0, top - overscan));
        int end = Math.Min(m_rows.Length, After(Math.Max(0, top + viewportHeight + overscan)) + 1);
        end = Math.Max(first, end);
        return (first, end, m_prefix[first], m_prefix[^1] - m_prefix[end]);
    }
    /// <summary>Finds a source row's logical top for shared selection and diagnostic navigation.</summary>
    /// <param name="id">The stable source identity.</param>
    /// <returns>The row's top, or null when the row is no longer present.</returns>
    public double? OffsetOf(string id) { Prepare(); return m_positions.TryGetValue(id, out int index) ? m_prefix[index] : null; }
    /// <summary>Gets the logical bottom of an exclusive painted interval after newly measured rows.</summary>
    /// <param name="end">An exclusive row index between zero and the row count.</param>
    /// <returns>The finite row boundary.</returns>
    public double EndOf(int end)
    {
        if (end < 0 || end > m_rows.Length) throw new ArgumentOutOfRangeException(nameof(end));
        Prepare(); return m_prefix[end];
    }
    private int After(double position)
    {
        int low = 0, high = m_rows.Length;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (m_prefix[middle + 1] <= position) low = middle + 1;
            else high = middle;
        }
        return low;
    }
    private void Prepare()
    {
        if (!m_dirty) return;
        var prefix = new double[m_rows.Length + 1];
        for (int index = 0; index < m_rows.Length; index++) prefix[index + 1] = prefix[index] + m_heights[m_rows[index]];
        ValidTotal(prefix[^1]);
        m_prefix = prefix; m_dirty = false;
    }
    private static void ValidHeight(double height)
    {
        if (!double.IsFinite(height) || height <= 0 || height > 1000000) throw new ArgumentOutOfRangeException(nameof(height));
    }
    private static void ValidTotal(double height)
    {
        if (height > 1e8) throw new InvalidOperationException("The script canvas exceeds its logical height budget; split very long text or chapters.");
    }
}
