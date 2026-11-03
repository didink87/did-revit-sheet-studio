using SheetStudio.Core.Model;

namespace SheetStudio.Core.Tests;

internal static class Fx
{
    public static SheetSnapshot S(string id, string number, string title = "JUDUL", string? kind = null, bool editable = true, string subjek = "") =>
        new(id, number, title, kind, subjek, true, editable, editable ? null : "budi", number + "|" + title,
            new Dictionary<string, string>(), "PUPR_KOP_A3");

    public static ProjectState State(Dictionary<string, IReadOnlyList<string>>? order = null, IEnumerable<string>? locked = null) =>
        new(order ?? new(), [], new HashSet<string>(locked ?? []));

    public static readonly IReadOnlySet<string> None = new HashSet<string>();
}
