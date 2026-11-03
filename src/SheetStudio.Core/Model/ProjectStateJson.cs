using System.Text.Json;

namespace SheetStudio.Core.Model;

/// <summary>Persists the panel order/locks in the project (Extensible Storage string).</summary>
public static class ProjectStateJson
{
    private sealed record Dto(Dictionary<string, List<string>>? OrderPerGroup, List<string>? DisciplineOrder, List<string>? Locked);

    public static string Serialize(ProjectState s) =>
        JsonSerializer.Serialize(new Dto(
            s.OrderPerGroup.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()),
            s.DisciplineOrder.ToList(),
            s.Locked.ToList()));

    /// <summary>Empty state for missing or corrupt data — a broken order must never block the panel.</summary>
    public static ProjectState Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return ProjectState.Empty;
        try
        {
            var d = JsonSerializer.Deserialize<Dto>(json);
            if (d is null) return ProjectState.Empty;
            return new ProjectState(
                (d.OrderPerGroup ?? []).ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value),
                d.DisciplineOrder ?? [],
                (d.Locked ?? []).ToHashSet());
        }
        catch (JsonException)
        {
            return ProjectState.Empty;
        }
    }
}
