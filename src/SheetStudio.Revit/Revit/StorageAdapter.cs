using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace SheetStudio.Revit.Revit;

/// <summary>Project-level Sheet Studio data (preferences JSON + panel order JSON) on Project Information.</summary>
public static class StorageAdapter
{
    private static readonly Guid SchemaGuid = new("5B8F2E61-4C7A-4D19-A3E2-9F0B6C1D7A48");
    private const string Preferensi = "Preferensi";
    private const string StatusUrutan = "StatusUrutan";

    private static Schema GetSchema()
    {
        var existing = Schema.Lookup(SchemaGuid);
        if (existing is not null) return existing;
        var b = new SchemaBuilder(SchemaGuid);
        b.SetSchemaName("SheetStudioData");
        b.SetReadAccessLevel(AccessLevel.Public);
        b.SetWriteAccessLevel(AccessLevel.Public);
        b.AddSimpleField(Preferensi, typeof(string));
        b.AddSimpleField(StatusUrutan, typeof(string));
        return b.Finish();
    }

    public static (string? Preferensi, string? StatusUrutan) Read(Document doc)
    {
        var schema = Schema.Lookup(SchemaGuid);
        if (schema is null) return (null, null);
        var entity = doc.ProjectInformation.GetEntity(schema);
        if (entity is null || !entity.IsValid()) return (null, null);
        return (Empty(entity.Get<string>(Preferensi)), Empty(entity.Get<string>(StatusUrutan)));
    }

    /// <summary>Must run inside an open transaction. A null argument keeps the stored value.</summary>
    public static void Write(Document doc, string? preferensi, string? statusUrutan)
    {
        var schema = GetSchema();
        var (oldPrefs, oldState) = Read(doc);
        var entity = new Entity(schema);
        entity.Set(Preferensi, preferensi ?? oldPrefs ?? "");
        entity.Set(StatusUrutan, statusUrutan ?? oldState ?? "");
        doc.ProjectInformation.SetEntity(entity);
    }

    private static string? Empty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}
