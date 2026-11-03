using System.Collections.ObjectModel;

namespace SheetStudio.Core.ViewModels;

/// <summary>Brings an ObservableCollection to a desired sequence with the fewest change events,
/// keeping existing instances (reference equality) so the UI does not recreate their containers.</summary>
public static class CollectionSync
{
    public static void Apply<T>(ObservableCollection<T> target, IReadOnlyList<T> desired) where T : class
    {
        var wanted = new HashSet<T>(desired, ReferenceEqualityComparer.Instance as IEqualityComparer<T>);
        for (var i = target.Count - 1; i >= 0; i--)
            if (!wanted.Contains(target[i])) target.RemoveAt(i);

        for (var i = 0; i < desired.Count; i++)
        {
            if (i < target.Count && ReferenceEquals(target[i], desired[i])) continue;
            var at = -1;
            for (var j = i + 1; j < target.Count; j++)
                if (ReferenceEquals(target[j], desired[i])) { at = j; break; }
            if (at >= 0) target.Move(at, i);
            else target.Insert(i, desired[i]);
        }
    }
}
