using SheetStudio.Core.Model;
using SheetStudio.Core.Profiles;

namespace SheetStudio.Core.Changes;

/// <summary>Carries staged edits onto a fresh snapshot after the model changed outside the panel.</summary>
public static class Rebase
{
    public static (ChangeSet Next, IReadOnlyList<string> Dropped) Onto(ChangeSet pending, IReadOnlyList<SheetSnapshot> fresh, NamingProfile p)
    {
        var alive = fresh.Select(s => s.UniqueId).ToHashSet();
        var dropped = pending.Changes.Keys.Union(pending.Touched).Union(pending.Relocked).Where(id => !alive.Contains(id)).Distinct().ToList();

        var changes = pending.Changes.Where(kv => alive.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
        var order = pending.State.OrderPerGroup.ToDictionary(
            kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value.Where(alive.Contains).ToList());

        var next = pending with
        {
            Changes = changes,
            Touched = pending.Touched.Where(alive.Contains).ToHashSet(),
            Relocked = pending.Relocked.Where(alive.Contains).ToHashSet(),
            State = pending.State with { OrderPerGroup = order, Locked = pending.State.Locked.Where(alive.Contains).ToHashSet() },
        };

        // New sheets go to the end of their group when that group has a saved order.
        foreach (var s in fresh)
        {
            var g = next.EffectiveGroup(s, p);
            if (order.TryGetValue(g, out var list) && !list.Contains(s.UniqueId))
                order[g] = list.Append(s.UniqueId).ToList();
        }
        return (next with { State = next.State with { OrderPerGroup = order } }, dropped);
    }
}
