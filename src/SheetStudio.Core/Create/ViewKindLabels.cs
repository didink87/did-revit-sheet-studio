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

    public static string Label(PlanStatus? status) => status switch
    {
        PlanStatus.Baru => "New",
        PlanStatus.SudahAda => "Exists",
        PlanStatus.Duplikat => "Duplicate",
        PlanStatus.Galat => "Error",
        _ => "",
    };
}
