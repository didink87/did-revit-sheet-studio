using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace SheetStudio.Revit.Tests;

internal static class Fixtures
{
    private const string Template = @"C:\ProgramData\Autodesk\RVT 2026\Templates\English\DefaultMetric.rte";
    private const string TitleBlockTemplates = @"C:\ProgramData\Autodesk\RVT 2026\Family Templates\English\Titleblocks";

    public static string NewTempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "SheetStudioTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    public static Document NewDoc(Application app) =>
        File.Exists(Template) ? app.NewProjectDocument(Template) : app.NewProjectDocument(UnitSystem.Metric);

    public static void Tx(Document doc, Action action, string name = "test")
    {
        using var t = new Transaction(doc, name);
        t.Start();
        action();
        t.Commit();
    }

    /// <summary>Builds a title block family "PUPR_KOP_{size}" from the stock template and loads it.</summary>
    public static FamilySymbol LoadTitleBlock(Application app, Document doc, string size)
    {
        var name = $"PUPR_KOP_{size}";
        var existing = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks).OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>().FirstOrDefault(s => s.FamilyName == name);
        if (existing is not null) return existing;

        var path = Path.Combine(NewTempDir(), name + ".rfa");
        var fam = app.NewFamilyDocument(Path.Combine(TitleBlockTemplates, $"{size} metric.rft"));
        fam.SaveAs(path);
        fam.Close(false);
        FamilySymbol? symbol = null;
        Tx(doc, () =>
        {
            doc.LoadFamily(path, out var family);
            symbol = (FamilySymbol)doc.GetElement(family.GetFamilySymbolIds().First());
            if (!symbol.IsActive) symbol.Activate();
        });
        return symbol!;
    }

    public static ViewSheet Sheet(Document doc, string number, string name, FamilySymbol? tb = null)
    {
        ViewSheet? s = null;
        Tx(doc, () =>
        {
            s = ViewSheet.Create(doc, tb?.Id ?? ElementId.InvalidElementId);
            s.SheetNumber = number;
            s.Name = name;
        });
        return s!;
    }

    public static string TitleBlockFamilyOf(ViewSheet sheet) =>
        new FilteredElementCollector(sheet.Document, sheet.Id).OfCategory(BuiltInCategory.OST_TitleBlocks)
            .Cast<FamilyInstance>().Single().Symbol.FamilyName;
}
