using Autodesk.Revit.ApplicationServices;
using NUnit.Framework;
using SheetStudio.Revit.Revit;
using static SheetStudio.Revit.Tests.Fixtures;

namespace SheetStudio.Revit.Tests;

[TestFixture]
public class WatcherTests
{
    [Test]
    public void Watcher_RaisesOnSheetAdd(Application app)
    {
        var doc = NewDoc(app);
        using var watcher = new ModelWatcher(app, debounceMs: 0, dispatch: a => a());
        var raised = 0;
        var modifiableInHandler = false;
        watcher.Changed += () => { raised++; modifiableInHandler |= doc.IsModifiable; };
        try
        {
            Sheet(doc, "A-110", "DENAH");
            Assert.That(raised, Is.EqualTo(1));
            Assert.That(modifiableInHandler, Is.False);
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Watcher_IgnoresOwnTransactions_AndUnrelatedChanges(Application app)
    {
        var doc = NewDoc(app);
        using var watcher = new ModelWatcher(app, debounceMs: 0, dispatch: a => a());
        var raised = 0;
        watcher.Changed += () => raised++;
        try
        {
            Tx(doc, () => Autodesk.Revit.DB.ViewSheet.Create(doc, Autodesk.Revit.DB.ElementId.InvalidElementId), "Sheet Studio: Terapkan");
            Tx(doc, () => Autodesk.Revit.DB.Level.Create(doc, 10));
            Assert.That(raised, Is.EqualTo(0));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Watcher_UndoOfOwnTransaction_IsNotIgnored()
    {
        Assert.That(ModelWatcher.ShouldIgnore(["Sheet Studio: Terapkan"], Autodesk.Revit.DB.Events.UndoOperation.TransactionCommitted), Is.True);
        Assert.That(ModelWatcher.ShouldIgnore(["Sheet Studio: Terapkan"], Autodesk.Revit.DB.Events.UndoOperation.TransactionUndone), Is.False);
        Assert.That(ModelWatcher.ShouldIgnore(["Sheet Studio: Terapkan"], Autodesk.Revit.DB.Events.UndoOperation.TransactionRedone), Is.False);
        Assert.That(ModelWatcher.ShouldIgnore(["Edit"], Autodesk.Revit.DB.Events.UndoOperation.TransactionCommitted), Is.False);
    }
}
