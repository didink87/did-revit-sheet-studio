namespace SheetStudio.Core.Create;

/// <summary>English display labels for the stored view-kind ids and plan statuses (ids/enum names are never shown raw).</summary>
public static class ViewKindLabels
{
    public static string Label(string? jenisView) => jenisView switch
    {
        "Denah" => "Plan",
        "Plafon" => "Ceiling plan",
        "Drafting" => "Drafting",
        "Legend" => "Legend",
        "TigaD" => "3D",
        "Tampak" => "Elevation",
        "Potongan" => "Section",
        "Schedule" => "Schedule",
        "Tanpa" => "None",
        null => "",
        _ => jenisView,
    };

    /// <summary>English label for a Revit <c>ViewType</c> name (as stored in PlacedView.Kind); unknown names pass through.</summary>
    public static string RevitTypeLabel(string? viewType) => viewType switch
    {
        "FloorPlan" => "Floor plan",
        "CeilingPlan" => "Ceiling plan",
        "Elevation" => "Elevation",
        "Section" => "Section",
        "Detail" => "Detail",
        "DraftingView" => "Drafting",
        "Legend" => "Legend",
        "ThreeD" => "3D",
        "AreaPlan" => "Area plan",
        "EngineeringPlan" => "Structural plan",
        "Schedule" => "Schedule",
        null => "",
        _ => viewType,
    };

    public static string Label(PlanStatus? status) => status switch
    {
        PlanStatus.Baru => "New",
        PlanStatus.SudahAda => "Exists",
        PlanStatus.Duplikat => "Duplicate",
        PlanStatus.Galat => "Error",
        _ => "",
    };
}
