using Autodesk.Revit.DB;
using SheetStudio.Core.Create;

namespace SheetStudio.Revit.Revit;

/// <summary>Finds the drawing area of a sheet and centres viewports in it (internal units, feet).</summary>
public static class ViewportPlacer
{
    private static double Mm(double v) => UnitUtils.ConvertToInternalUnits(v, UnitTypeId.Millimeters);

    /// <summary>Title block instance parameters AreaGambar_Kiri/Bawah/Lebar/Tinggi when present; else the title
    /// block extent minus a 10 mm margin; else an A3 landscape sheet at the origin.</summary>
    public static Rect DrawingArea(ViewSheet sheet)
    {
        var tb = TitleBlocks.On(sheet);
        if (tb is not null)
        {
            double? P(string n) => tb.LookupParameter(n) is { StorageType: StorageType.Double } p ? p.AsDouble() : null;
            if (P("AreaGambar_Kiri") is { } x && P("AreaGambar_Bawah") is { } y && P("AreaGambar_Lebar") is { } w && P("AreaGambar_Tinggi") is { } h && w > 0 && h > 0)
                return new Rect(x, y, w, h);
            var box = tb.get_BoundingBox(sheet);
            if (box is not null && box.Max.X - box.Min.X > Mm(50))
            {
                var m = Mm(10);
                return new Rect(box.Min.X + m, box.Min.Y + m, box.Max.X - box.Min.X - 2 * m, box.Max.Y - box.Min.Y - 2 * m);
            }
        }
        return new Rect(Mm(10), Mm(10), Mm(400), Mm(277));
    }

    public static void Place(Document doc, ViewSheet sheet, IReadOnlyList<View> views, out bool fitsAll)
    {
        fitsAll = true;
        if (views.Count == 0) return;
        var ports = views.Select(v => Viewport.Create(doc, sheet.Id, v.Id, XYZ.Zero)).ToList();
        doc.Regenerate();
        var sizes = ports.Select(p =>
        {
            var o = p.GetBoxOutline();
            return (o.MaximumPoint.X - o.MinimumPoint.X, o.MaximumPoint.Y - o.MinimumPoint.Y);
        }).ToList();
        var centres = LayoutGrid.Arrange(sizes, DrawingArea(sheet), Mm(10), out fitsAll);
        for (var i = 0; i < ports.Count; i++) ports[i].SetBoxCenter(new XYZ(centres[i].Cx, centres[i].Cy, 0));
    }
}
