using SheetStudio.Core.Profiles;

namespace SheetStudio.Core.Naming;

public static class SortKey
{
    /// <summary>"{discipline order:00}.{disc}.{type}.{position:0000}" — ordinal sort follows the set order.</summary>
    public static string For(SheetId id, NamingProfile p, int positionInGroup)
    {
        var order = p.FindDiscipline(id.Discipline)?.Order ?? 99;
        return $"{order:00}.{id.Discipline}.{id.Type}.{positionInGroup:0000}";
    }
}
