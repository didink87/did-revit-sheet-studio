using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using NUnit.Framework;
using SheetStudio.Core.Preferences;
using SheetStudio.Core.ViewModels;
using SheetStudio.Revit.Revit;
using static SheetStudio.Revit.Tests.Fixtures;

namespace SheetStudio.Revit.Tests;

[TestFixture]
public class EndToEndTests
{
    /// <summary>Runs gateway work directly on a document (no ExternalEvent), so the real gateway code is exercised.</summary>
    private sealed class DirectRunner(Document doc) : IDocumentRunner
    {
        public Task<T> Run<T>(Func<Document, T> work) => Task.FromResult(work(doc));
    }

    private static void Levels(Document doc)
    {
        Tx(doc, () =>
        {
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
            levels[0].Name = "LANTAI 1";
            levels[0].Elevation = 0;
            levels[1].Name = "LANTAI 2";
            levels[1].Elevation = UnitUtils.ConvertToInternalUnits(4, UnitTypeId.Meters);
            foreach (var extra in levels.Skip(2)) doc.Delete(extra.Id);
            Level.Create(doc, UnitUtils.ConvertToInternalUnits(-3.5, UnitTypeId.Meters)).Name = "BASEMENT 1";
        });
    }

    /// <summary>The direct runner completes every task synchronously, so this never blocks.</summary>
    private static void Sync(Task t)
    {
        Assert.That(t.IsCompleted, Is.True, "Task harus selesai sinkron dengan DirectRunner");
        t.GetAwaiter().GetResult();
    }

    private static T Sync<T>(Task<T> t)
    {
        Sync((Task)t);
        return t.Result;
    }

    private static string Uid(StudioViewModel vm, string title) => vm.Sheets.Single(s => s.Title == title).UniqueId;
    private static LayerRowViewModel Row(StudioViewModel vm, string uid) => vm.Layers.Rows.Single(r => r.Key == uid);
    private static ViewSheet Sheet(Document doc, string uid) => (ViewSheet)doc.GetElement(uid);

    [Test]
    public void E2E_CreateReorderEditApply(Application app) // sync: Revit API must stay on the test (main) thread
    {
        var dir = NewTempDir();
        var path = Path.Combine(dir, "e2e.rvt");
        RevitStudioGateway.UserPrefsPath = Path.Combine(dir, "user-prefs.json");
        var doc = NewDoc(app);
        try
        {
            Levels(doc);
            LoadTitleBlock(app, doc, "A3");
            var vm = new StudioViewModel(new RevitStudioGateway(new DirectRunner(doc)));
            Sync(vm.LoadAsync());

            // 1. batch: one floor plan sheet per level, lowest first
            var batch = vm.NewBatch();
            batch.LoadPreset("DENAH SEMUA LANTAI");
            Sync(batch.RunAsync());
            Assert.That(batch.Report!.Rows.All(r => r.Ok), Is.True, string.Join("; ", batch.Report.Rows.Select(r => r.Pesan)));
            var b1 = Uid(vm, "DENAH BASEMENT 1");
            var l1 = Uid(vm, "DENAH LANTAI 1");
            var l2 = Uid(vm, "DENAH LANTAI 2");
            Assert.That((Sheet(doc, b1).SheetNumber, Sheet(doc, l1).SheetNumber, Sheet(doc, l2).SheetNumber), Is.EqualTo(("A-110", "A-111", "A-112")));
            Assert.That(Sheet(doc, l1).GetAllViewports(), Has.Count.EqualTo(1));

            // 2. drag LANTAI 2 to the top, lock LANTAI 1, apply
            vm.Layers.Click(l2);
            vm.Layers.Drop([l2], b1, DropPosition.Sebelum);
            Assert.That(Row(vm, l2).NewNumber, Is.EqualTo("A-110"));
            vm.Layers.ToggleLock(l1);
            Assert.That(Sync(vm.ApplyAsync()), Is.True, vm.Status);
            Assert.That((Sheet(doc, l2).SheetNumber, Sheet(doc, l1).SheetNumber, Sheet(doc, b1).SheetNumber), Is.EqualTo(("A-110", "A-111", "A-112")));

            // 3. inspector: prefix on the whole group, then panel undo
            vm.Layers.Click("A|1");
            vm.Inspector.Prefix = "RENCANA";
            vm.Inspector.ApplyPrefixSuffix.Execute(null);
            Assert.That(vm.PendingCount, Is.EqualTo(3)); // a lock freezes number and position, not the title
            vm.Undo.Execute(null);
            Assert.That(vm.PendingCount, Is.EqualTo(0));

            //    abbreviation warning → accept suggestion
            vm.Layers.RenameInline(b1, "Denah Lt. 2");
            Assert.That(Row(vm, b1).Warning, Is.True);
            vm.Layers.Click(b1);
            vm.Inspector.AcceptSuggestions.Execute(null);
            Assert.That(Row(vm, b1).Label, Is.EqualTo("DENAH LANTAI 2"));

            //    move one sheet to type 4 → large-scale number
            vm.Inspector.SheetType = 4;
            Assert.That(Row(vm, b1).NewNumber, Does.StartWith("A-4"));
            Assert.That(Sync(vm.ApplyAsync()), Is.True, vm.Status);
            Assert.That(Sheet(doc, b1).SheetNumber, Does.StartWith("A-4"));
            Assert.That(Sheet(doc, b1).Name, Is.EqualTo("DENAH LANTAI 2"));

            // project preferences
            Sync(vm.SavePrefsAsync(vm.Snapshot.Prefs with { Langkah = 5 }, userLevel: false));

            doc.SaveAs(path);
            doc.Close(false);

            // 4. reopen: order, lock and preferences survive
            doc = app.OpenDocumentFile(path);
            var vm2 = new StudioViewModel(new RevitStudioGateway(new DirectRunner(doc)));
            Sync(vm2.LoadAsync());
            Assert.That(vm2.Snapshot.State.Locked, Does.Contain(l1));
            Assert.That(vm2.Snapshot.State.OrderPerGroup["A|1"], Is.EqualTo(new[] { l2, l1 }));
            Assert.That(vm2.Snapshot.Prefs.Langkah, Is.EqualTo(5));
            Assert.That(Row(vm2, l1).Locked, Is.True);
        }
        finally
        {
            if (doc.IsValidObject) doc.Close(false);
            RevitStudioGateway.UserPrefsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SheetStudio", "preferensi.json");
        }
    }
}
