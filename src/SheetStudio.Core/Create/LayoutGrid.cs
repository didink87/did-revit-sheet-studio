namespace SheetStudio.Core.Create;

/// <summary>Places viewports in rows (left→right, wrapping), the whole block centred in the drawing area. Y grows upward.</summary>
public static class LayoutGrid
{
    public static IReadOnlyList<(double Cx, double Cy)> Arrange(IReadOnlyList<(double W, double H)> sizes, Rect area, double gap, out bool fitsAll)
    {
        fitsAll = true;
        var rows = new List<List<int>>();
        var current = new List<int>();
        double width = 0;
        for (var i = 0; i < sizes.Count; i++)
        {
            var w = sizes[i].W;
            if (w > area.W) fitsAll = false;
            var needed = current.Count == 0 ? w : width + gap + w;
            if (current.Count > 0 && needed > area.W) { rows.Add(current); current = []; needed = w; }
            current.Add(i);
            width = needed;
        }
        if (current.Count > 0) rows.Add(current);

        var rowHeights = rows.Select(r => r.Max(i => sizes[i].H)).ToList();
        var totalH = rowHeights.Sum() + gap * Math.Max(0, rows.Count - 1);
        if (totalH > area.H) fitsAll = false;

        var result = new (double, double)[sizes.Count];
        var cx = area.X + area.W / 2;
        var top = area.Y + area.H / 2 + totalH / 2;
        for (var r = 0; r < rows.Count; r++)
        {
            var rowW = rows[r].Sum(i => sizes[i].W) + gap * (rows[r].Count - 1);
            var x = cx - rowW / 2;
            var cy = top - rowHeights[r] / 2;
            foreach (var i in rows[r])
            {
                result[i] = (x + sizes[i].W / 2, cy);
                x += sizes[i].W + gap;
            }
            top -= rowHeights[r] + gap;
        }
        return result;
    }
}
