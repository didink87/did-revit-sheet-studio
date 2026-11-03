using Autodesk.Revit.DB;

namespace SheetStudio.Revit.Revit;

/// <summary>Title block types are named "Family" when the type carries the family name, else "Family : Type".</summary>
internal static class TitleBlocks
{
    public static string Display(FamilySymbol s) => s.Name == s.FamilyName ? s.FamilyName : $"{s.FamilyName} : {s.Name}";

    public static IReadOnlyList<FamilySymbol> All(Document doc) =>
        new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks).OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>().OrderBy(Display).ToList();

    /// <summary>Exact display name, then family name (first type), then type name.</summary>
    public static FamilySymbol? Find(Document doc, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var all = All(doc);
        return all.FirstOrDefault(s => Display(s) == name)
               ?? all.FirstOrDefault(s => s.FamilyName == name)
               ?? all.FirstOrDefault(s => s.Name == name);
    }

    public static FamilyInstance? On(ViewSheet sheet) =>
        new FilteredElementCollector(sheet.Document, sheet.Id).OfCategory(BuiltInCategory.OST_TitleBlocks)
            .OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().FirstOrDefault();
}
