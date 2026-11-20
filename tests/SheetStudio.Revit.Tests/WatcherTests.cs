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

    private static Autodesk.Revit.DB.ElementId PlaceViewport(Autodesk.Revit.DB.Document doc, Autodesk.Revit.DB.ViewSheet sheet)
    {
        Autodesk.Revit.DB.ElementId? vp = null;
        Tx(doc, () =>
        {
            var type = new Autodesk.Revit.DB.FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.ViewFamilyType))
                .Cast<Autodesk.Revit.DB.ViewFamilyType>().First(t => t.ViewFamily == Autodesk.Revit.DB.ViewFamily.Drafting);
            var v = Autodesk.Revit.DB.ViewDrafting.Create(doc, type.Id);
            doc.Create.NewDetailCurve(v, Autodesk.Revit.DB.Line.CreateBound(Autodesk.Revit.DB.XYZ.Zero, new Autodesk.Revit.DB.XYZ(5, 0, 0)));
            vp = Autodesk.Revit.DB.Viewport.Create(doc, sheet.Id, v.Id, Autodesk.Revit.DB.XYZ.Zero).Id;
        });
        return vp!;
    }

    [Test]
    public void Watcher_ViewportAdded_Raises(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var sheet = Sheet(doc, "A-110", "DENAH");
            using var watcher = new ModelWatcher(app, debounceMs: 0, dispatch: a => a());
            var raised = 0;
            watcher.Changed += () => raised++;

            PlaceViewport(doc, sheet);

            Assert.That(raised, Is.EqualTo(1));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Watcher_ViewportDeleted_Raises(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var sheet = Sheet(doc, "A-110", "DENAH");
            using var watcher = new ModelWatcher(app, debounceMs: 0, dispatch: a => a());
            var raised = 0;
            watcher.Changed += () => raised++;
            var vp = PlaceViewport(doc, sheet);
            raised = 0;

            Tx(doc, () => doc.Delete(vp));

            Assert.That(raised, Is.EqualTo(1));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Watcher_FirstEventIsViewportDelete_Raises(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var sheet = Sheet(doc, "A-110", "DENAH");
            var vp = PlaceViewport(doc, sheet);
            using var watcher = new ModelWatcher(app, debounceMs: 0, dispatch: a => a());
            var raised = 0;
            watcher.Changed += () => raised++;

            Tx(doc, () => doc.Delete(vp));

            Assert.That(raised, Is.EqualTo(1));
        }
        finally { doc.Close(false); }
    }
}
