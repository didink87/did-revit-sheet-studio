using Autodesk.Revit.DB;

namespace SheetStudio.Revit.Revit;

/// <summary>Creates the hidden-in-plain-sight sheet parameters SS_JenisGambar and SS_Subjek (text, Identity Data).</summary>
internal static class ProjectParams
{
    private static readonly (string Name, Guid Guid)[] Defs =
    [
        (SnapshotReader.KindParam, new Guid("0E8C6F2A-5B47-4D3E-9A1C-7F2B8D4E6A01")),
        (SnapshotReader.SubjectParam, new Guid("0E8C6F2A-5B47-4D3E-9A1C-7F2B8D4E6A02")),
    ];

    /// <summary>Must run inside an open transaction.</summary>
    public static void Ensure(Document doc)
    {
        var missing = Defs.Where(d => SharedParameterElement.Lookup(doc, d.Guid) is null).ToList();
        if (missing.Count == 0) return;

        var app = doc.Application;
        var original = app.SharedParametersFilename;
        var temp = Path.Combine(Path.GetTempPath(), $"SheetStudio_{Guid.NewGuid():N}.txt");
        File.WriteAllText(temp, "");
        try
        {
            app.SharedParametersFilename = temp;
            var group = app.OpenSharedParameterFile().Groups.Create("SheetStudio");
            var cats = app.Create.NewCategorySet();
            cats.Insert(Category.GetCategory(doc, BuiltInCategory.OST_Sheets));
            foreach (var (name, guid) in missing)
            {
                var def = group.Definitions.Create(new ExternalDefinitionCreationOptions(name, SpecTypeId.String.Text) { GUID = guid });
                doc.ParameterBindings.Insert(def, app.Create.NewInstanceBinding(cats), GroupTypeId.IdentityData);
            }
        }
        finally
        {
            try { app.SharedParametersFilename = original ?? ""; } catch (Autodesk.Revit.Exceptions.ApplicationException) { }
            File.Delete(temp);
        }
    }
}
