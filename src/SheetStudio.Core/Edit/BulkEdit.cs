using System.Text.RegularExpressions;
using SheetStudio.Core.Changes;
using SheetStudio.Core.Layers;
using SheetStudio.Core.Model;
using SheetStudio.Core.Naming;
using SheetStudio.Core.Profiles;

namespace SheetStudio.Core.Edit;

/// <summary>Multi-sheet edits from the Inspector. Titles work on the effective (already staged) title.</summary>
public static class BulkEdit
{
    public static string EffectiveTitle(ChangeSet cs, SheetSnapshot s) =>
        cs.Changes.TryGetValue(s.UniqueId, out var c) && c.NewTitle is not null ? c.NewTitle : s.Title;

    public static ChangeSet SetTitle(ChangeSet cs, IReadOnlyList<SheetSnapshot> sel, string title) =>
        MapTitles(cs, sel, _ => title);

    public static ChangeSet FindReplace(ChangeSet cs, IReadOnlyList<SheetSnapshot> sel, string find, string replace, bool wholeWord)
    {
        if (string.IsNullOrEmpty(find)) return cs;
        var pattern = wholeWord ? $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(find)}(?![\p{{L}}\p{{N}}])" : Regex.Escape(find);
        var rx = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return MapTitles(cs, sel, t => rx.Replace(t, (replace ?? "").ToUpperInvariant()));
    }

    public static ChangeSet PrefixSuffix(ChangeSet cs, IReadOnlyList<SheetSnapshot> sel, string prefix, string suffix) =>
        MapTitles(cs, sel, t => $"{prefix} {t} {suffix}");

    public static ChangeSet Uppercase(ChangeSet cs, IReadOnlyList<SheetSnapshot> sel) => MapTitles(cs, sel, t => t);

    public static ChangeSet ApplySuggestions(ChangeSet cs, IReadOnlyList<SheetSnapshot> sel, NamingProfile p) =>
        MapTitles(cs, sel, t => TitleRules.ApplySuggestions(t, p));

    public static ChangeSet SetParam(ChangeSet cs, IReadOnlyList<SheetSnapshot> sel, string name, string value) =>
        sel.Aggregate(cs, (acc, s) => acc.Upsert(new SheetChange(s.UniqueId, NewParams: new Dictionary<string, string> { [name] = value })).Next);

    public static ChangeSet SetTitleBlock(ChangeSet cs, IReadOnlyList<SheetSnapshot> sel, string type) =>
        sel.Aggregate(cs, (acc, s) => acc.Upsert(new SheetChange(s.UniqueId, NewTitleBlockType: type)).Next);

    /// <summary>Moves editable, unlocked sheets to another discipline and/or type (null = keep). Back to the
    /// original group clears the group change.</summary>
    public static ChangeSet SetDisciplineType(ChangeSet cs, IReadOnlyList<SheetSnapshot> sel, string? disc, int? type, NamingProfile p)
    {
        var changes = cs.Changes.ToDictionary(kv => kv.Key, kv => kv.Value);
        foreach (var s in sel)
        {
            if (!s.Editable || cs.State.Locked.Contains(s.UniqueId)) continue;
            var parts = cs.EffectiveGroup(s, p).Split('|');
            if (parts[0] == LayerListBuilder.UnknownDiscipline) continue;
            var target = LayerListBuilder.TypeKey(disc ?? parts[0], type ?? int.Parse(parts[1]));
            var original = LayerListBuilder.GroupOf(s.Number, p).Type;
            var c = changes.TryGetValue(s.UniqueId, out var e) ? e : new SheetChange(s.UniqueId);
            var t = target.Split('|');
            c = target == original ? c with { NewDiscipline = null, NewType = null } : c with { NewDiscipline = t[0], NewType = int.Parse(t[1]) };
            if (c.IsEmpty) changes.Remove(s.UniqueId); else changes[s.UniqueId] = c;
        }
        return cs with { Changes = changes };
    }

    private static ChangeSet MapTitles(ChangeSet cs, IReadOnlyList<SheetSnapshot> sel, Func<string, string> map)
    {
        foreach (var s in sel)
        {
            var current = EffectiveTitle(cs, s);
            var next = TitleRules.Normalize(map(current));
            if (next.Length == 0 || next == current) continue;
            cs = cs.Upsert(new SheetChange(s.UniqueId, NewTitle: next == s.Title ? null : next)).Next;
            if (next == s.Title && cs.Changes.TryGetValue(s.UniqueId, out var c)) // back to original title
            {
                var cleared = c with { NewTitle = null };
                var dict = cs.Changes.ToDictionary(kv => kv.Key, kv => kv.Value);
                if (cleared.IsEmpty) dict.Remove(s.UniqueId); else dict[s.UniqueId] = cleared;
                cs = cs with { Changes = dict };
            }
        }
        return cs;
    }
}
