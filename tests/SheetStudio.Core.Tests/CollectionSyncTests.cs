using System.Collections.ObjectModel;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Core.Tests;

public class CollectionSyncTests
{
    private sealed class Item(string n) { public string N { get; } = n; }

    [Fact]
    public void Apply_ReorderInsertRemove_MatchesDesiredKeepsInstances()
    {
        Item a = new("a"), b = new("b"), c = new("c"), d = new("d"), e = new("e");
        var target = new ObservableCollection<Item> { a, b, c, d };
        CollectionSync.Apply(target, [d, a, e, c]);
        Assert.Equal(new[] { d, a, e, c }, target.ToArray());
        Assert.Same(a, target[1]);
        Assert.Same(c, target[3]);
        Assert.Same(d, target[0]);
    }

    [Fact]
    public void Apply_Unchanged_RaisesNoEvents()
    {
        Item a = new("a"), b = new("b");
        var target = new ObservableCollection<Item> { a, b };
        var n = 0;
        target.CollectionChanged += (_, _) => n++;
        CollectionSync.Apply(target, [a, b]);
        Assert.Equal(0, n);
    }
}
