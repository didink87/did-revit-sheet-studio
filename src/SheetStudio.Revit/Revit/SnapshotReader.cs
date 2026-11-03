using Autodesk.Revit.DB;
using SheetStudio.Core.Model;
using SheetStudio.Core.Preferences;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Revit.Revit;

/// <summary>Reads the model into Core's read-only snapshot.</summary>
public static class SnapshotReader
{
    public const string KindParam = "SS_JenisGambar";
    public const string SubjectParam = "SS_Subjek";

    private static readonly HashSet<BuiltInParameter> Hidden =
        [BuiltInParameter.SHEET_NUMBER, BuiltInParameter.SHEET_NAME, BuiltInParameter.VIEW_NAME];

    private static readonly HashSet<ViewType> Placeable =
    [
        ViewType.FloorPlan, ViewType.CeilingPlan, ViewType.Elevation, ViewType.Section, ViewType.Detail,
        ViewType.DraftingView, ViewType.Legend, ViewType.ThreeD, ViewType.AreaPlan, ViewType.EngineeringPlan,
    ];

    public static StudioSnapshot Read(Document doc, Prefs prefs, ProjectState state)
    {
        var titleBlocks = TitleBlocks.ByOwner(doc);
        var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
            .Where(s => !s.IsPlaceholder)
            .Select(s => Sheet(doc, s, titleBlocks))
            .ToList();

        var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .Select(l => (l.Name, UnitUtils.ConvertFromInternalUnits(l.Elevation, UnitTypeId.Meters)))
            .ToList();

        var onSheet = new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>().Select(v => v.ViewId).ToHashSet();
        var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
            .Where(v => !v.IsTemplate && Placeable.Contains(v.ViewType))
            .Select(v => new ViewInfo(v.UniqueId, v.Name, v.ViewType.ToString(), v.ViewType != ViewType.Legend && onSheet.Contains(v.Id)))
            .OrderBy(v => v.Name)
            .ToList();

        return new StudioSnapshot(DocumentKey(doc), sheets, state, prefs, levels, TitleBlocks.All(doc).Select(TitleBlocks.Display).ToList(), views)
        {
            DocumentId = doc.ProjectInformation?.UniqueId ?? "",
            OpenDocumentKeys = doc.Application.Documents.Cast<Document>().Where(d => !d.IsLinked).Select(DocumentKey).ToHashSet(),
        };
    }

    private static SheetSnapshot Sheet(Document doc, ViewSheet s, Dictionary<ElementId, FamilyInstance> titleBlocks)
    {
        var (editable, owner) = Ownership(doc, s.Id);
        titleBlocks.TryGetValue(s.Id, out var tb);
        return new SheetSnapshot(
            s.UniqueId,
            s.SheetNumber,
            s.Name,
            NullIfEmpty(s.LookupParameter(KindParam)?.AsString()),
            s.LookupParameter(SubjectParam)?.AsString() ?? "",
            s.get_Parameter(BuiltInParameter.SHEET_SCHEDULED)?.AsInteger() != 0,
            editable,
            owner,
            SheetSnapshot.MakeVersi(s.SheetNumber, s.Name),
            Params(s),
            tb is null ? "" : TitleBlocks.Display(tb.Symbol));
    }

    private static Dictionary<string, string> Params(ViewSheet s)
    {
        var result = new Dictionary<string, string>();
        foreach (Parameter p in s.Parameters)
        {
            if (p.StorageType != StorageType.String || p.IsReadOnly) continue;
            if (p.Definition is InternalDefinition d && Hidden.Contains(d.BuiltInParameter)) continue;
            var name = p.Definition.Name;
            if (name.StartsWith("SS_", StringComparison.Ordinal)) continue;
            result.TryAdd(name, p.AsString() ?? "");
        }
        return result;
    }

    /// <summary>Identity of a document for "is this still the one the panel read?": path, or title when unsaved.</summary>
    public static string DocumentKey(Document doc) => doc.PathName is { Length: > 0 } p ? p : "tanpa-file:" + doc.Title;

    internal static (bool Editable, string? Owner) Ownership(Document doc, ElementId id)
    {
        if (!doc.IsWorkshared) return (true, null);
        var status = WorksharingUtils.GetCheckoutStatus(doc, id);
        if (status != CheckoutStatus.OwnedByOtherUser) return (true, null);
        return (false, WorksharingUtils.GetWorksharingTooltipInfo(doc, id).Owner);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
