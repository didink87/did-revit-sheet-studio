using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using NUnit.Framework;
using SheetStudio.Core.Changes;
using SheetStudio.Core.Model;
using SheetStudio.Core.Numbering;
using SheetStudio.Core.Preferences;
using SheetStudio.Core.ViewModels;
using SheetStudio.Revit.Revit;
using static SheetStudio.Revit.Tests.Fixtures;

namespace SheetStudio.Revit.Tests;

[TestFixture]
public class ApplyTests
{
    private static ApplyPlan Plan(Document doc, IEnumerable<(ViewSheet Sheet, string Final)> renumber, IEnumerable<SheetChange>? edits = null,
        IEnumerable<ViewSheet>? deletes = null, ProjectState? state = null)
    {
        var snap = SnapshotReader.Read(doc, Prefs.Default, ProjectState.Empty);
        var versi = snap.Sheets.ToDictionary(s => s.UniqueId, s => s.Versi);
        var steps = renumber.Select((r, i) => new RenumberStep(r.Sheet.UniqueId, $"SSTMP-TEST00-{i + 1:000}", r.Final)).ToList();
        var e = (edits ?? []).ToList();
        var d = (deletes ?? []).Select(s => s.UniqueId).ToList();
        var ids = steps.Select(s => s.UniqueId).Concat(e.Select(x => x.UniqueId)).Concat(d).Distinct();
        return new ApplyPlan(snap.DocumentKey, steps, e, d, ids.ToDictionary(id => id, id => versi[id]), state ?? ProjectState.Empty);
    }

    [Test]
    public void Apply_SwapNumbers_NoError(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var a = Sheet(doc, "A-110", "DENAH LANTAI 1");
            var b = Sheet(doc, "A-111", "DENAH LANTAI 2");
            var report = ApplyExecutor.Apply(doc, Plan(doc, [(a, "A-111"), (b, "A-110")]));
            Assert.That(report.Skipped, Is.Empty);
            Assert.That((a.SheetNumber, b.SheetNumber), Is.EqualTo(("A-111", "A-110")));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Apply_RenameAndParams(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var a = Sheet(doc, "A-110", "DENAH LT 1");
            var drawnBy = a.get_Parameter(BuiltInParameter.SHEET_DRAWN_BY).Definition.Name;
            var edit = new SheetChange(a.UniqueId, NewTitle: "DENAH LANTAI 1", InSheetList: false,
                NewParams: new Dictionary<string, string> { [drawnBy] = "BUDI" });
            ApplyExecutor.Apply(doc, Plan(doc, [], [edit]));
            Assert.That(a.Name, Is.EqualTo("DENAH LANTAI 1"));
            Assert.That(a.get_Parameter(BuiltInParameter.SHEET_DRAWN_BY).AsString(), Is.EqualTo("BUDI"));
            Assert.That(a.get_Parameter(BuiltInParameter.SHEET_SCHEDULED).AsInteger(), Is.EqualTo(0));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Apply_ChangeTitleBlock(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var a3 = LoadTitleBlock(app, doc, "A3");
            LoadTitleBlock(app, doc, "A1");
            var a = Sheet(doc, "A-110", "DENAH", a3);
            var report = ApplyExecutor.Apply(doc, Plan(doc, [], [new SheetChange(a.UniqueId, NewTitleBlockType: "PUPR_KOP_A1")]));
            Assert.That(report.Skipped, Is.Empty);
            Assert.That(TitleBlockFamilyOf(a), Is.EqualTo("PUPR_KOP_A1"));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Apply_DeleteSheet(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var a = Sheet(doc, "A-110", "DENAH");
            var id = a.Id;
            ApplyExecutor.Apply(doc, Plan(doc, [], deletes: [a]));
            Assert.That(doc.GetElement(id), Is.Null);
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Apply_SkipsSheetChangedSinceSnapshot(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var a = Sheet(doc, "A-110", "DENAH 1");
            var b = Sheet(doc, "A-111", "DENAH 2");
            var plan = Plan(doc, [(a, "A-120"), (b, "A-121")]);
            Tx(doc, () => a.SheetNumber = "A-150"); // someone else edits after the snapshot
            var report = ApplyExecutor.Apply(doc, plan);
            Assert.That(report.Skipped.Single().UniqueId, Is.EqualTo(a.UniqueId));
            Assert.That(report.Skipped.Single().Pesan, Does.Contain("berubah sejak dibaca"));
            Assert.That((a.SheetNumber, b.SheetNumber), Is.EqualTo(("A-150", "A-121")));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Apply_SkipsDeletedSheet(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var a = Sheet(doc, "A-110", "DENAH 1");
            var b = Sheet(doc, "A-111", "DENAH 2");
            var plan = Plan(doc, [(a, "A-120"), (b, "A-121")]);
            var uid = a.UniqueId;
            Tx(doc, () => doc.Delete(a.Id));
            var report = ApplyExecutor.Apply(doc, plan);
            Assert.That(report.Skipped.Single().UniqueId, Is.EqualTo(uid));
            Assert.That(b.SheetNumber, Is.EqualTo("A-121"));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Apply_CollisionWithUnplannedSheet_RollsBackAll(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var a = Sheet(doc, "A-110", "DENAH 1");
            var b = Sheet(doc, "A-111", "DENAH 2");
            Sheet(doc, "A-130", "LAIN"); // not in the plan, blocks A-130
            var report = ApplyExecutor.Apply(doc, Plan(doc, [(a, "A-130"), (b, "A-110")]));
            Assert.That(report.Applied, Is.EqualTo(0));
            Assert.That(report.Skipped.Select(s => s.Pesan), Has.Some.Contains("A-130"));
            Assert.That((a.SheetNumber, b.SheetNumber), Is.EqualTo(("A-110", "A-111")));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Apply_PersistsStateInStorage(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var a = Sheet(doc, "A-110", "DENAH 1");
            var state = new ProjectState(new Dictionary<string, IReadOnlyList<string>> { ["A|1"] = [a.UniqueId] }, [], new HashSet<string> { a.UniqueId });
            ApplyExecutor.Apply(doc, Plan(doc, [(a, "A-111")], state: state));
            var back = ProjectStateJson.Deserialize(StorageAdapter.Read(doc).StatusUrutan);
            Assert.That(back.OrderPerGroup["A|1"], Is.EqualTo(new[] { a.UniqueId }));
            Assert.That(back.Locked, Does.Contain(a.UniqueId));
        }
        finally { doc.Close(false); }
    }

    // ── final-review fixes ──────────────────────────────────────
    [Test]
    public void Apply_DeleteFirstOfThree_RenumbersOthers(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var a = Sheet(doc, "A-110", "DENAH 1");
            var b = Sheet(doc, "A-111", "DENAH 2");
            var c = Sheet(doc, "A-112", "DENAH 3");
            var bId = b.UniqueId;
            var report = ApplyExecutor.Apply(doc, Plan(doc, [(b, "A-110"), (c, "A-111")], deletes: [a]));
            Assert.That(report.Skipped, Is.Empty, string.Join("; ", report.Skipped.Select(x => x.Pesan)));
            Assert.That((((ViewSheet)doc.GetElement(bId)).SheetNumber, c.SheetNumber), Is.EqualTo(("A-110", "A-111")));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Apply_CommitRolledBack_LeavesNoTempNumbers(Application app)
    {
        var doc = NewDoc(app);
        var original = ApplyExecutor.CommitFn;
        try
        {
            var a = Sheet(doc, "A-110", "DENAH 1");
            var b = Sheet(doc, "A-111", "DENAH 2");
            var calls = 0;
            ApplyExecutor.CommitFn = t => ++calls == 2 ? Rollback(t) : t.Commit(); // 2nd transaction "fails at commit"
            var report = ApplyExecutor.Apply(doc, Plan(doc, [(a, "A-111"), (b, "A-110")]));
            Assert.That(report.Applied, Is.EqualTo(0));
            Assert.That(report.Skipped, Is.Not.Empty);
            Assert.That((a.SheetNumber, b.SheetNumber), Is.EqualTo(("A-110", "A-111")));
        }
        finally
        {
            ApplyExecutor.CommitFn = original;
            doc.Close(false);
        }

        static TransactionStatus Rollback(Transaction t) { t.RollBack(); return TransactionStatus.RolledBack; }
    }

    [Test]
    public void Apply_WrongDocument_SkipsAll_AndKeepsStoredState(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var a = Sheet(doc, "A-110", "DENAH 1");
            Tx(doc, () => StorageAdapter.Write(doc, null, "{\"keep\":1}"));
            var plan = Plan(doc, [(a, "A-111")]) with { DocumentKey = "dokumen-lain" };
            var report = ApplyExecutor.Apply(doc, plan);
            Assert.That(report.Applied, Is.EqualTo(0));
            Assert.That(report.Skipped.Single().Pesan, Does.Contain("Dokumen aktif berbeda"));
            Assert.That(a.SheetNumber, Is.EqualTo("A-110"));
            Assert.That(StorageAdapter.Read(doc).StatusUrutan, Is.EqualTo("{\"keep\":1}"));
        }
        finally { doc.Close(false); }
    }
}
