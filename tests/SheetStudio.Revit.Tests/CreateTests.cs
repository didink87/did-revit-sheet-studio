using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using NUnit.Framework;
using SheetStudio.Core.Create;
using SheetStudio.Core.Naming;
using SheetStudio.Core.Numbering;
using SheetStudio.Core.Preferences;
using SheetStudio.Core.Profiles;
using SheetStudio.Core.ViewModels;
using SheetStudio.Revit.Revit;
using static SheetStudio.Revit.Tests.Fixtures;

namespace SheetStudio.Revit.Tests;

[TestFixture]
public class CreateTests
{
    private static readonly NamingProfile Pupr = ProfileLoader.LoadBuiltIn("pupr");

    private static CreateRequest Req(string number, string title, string? kind = null, string? tb = null, params ViewSpec[] views) =>
        new(new CreateName(SheetId.Parse(number, Pupr).Id, title, kind, ""), Kertas.A3, tb, views);

    private static int SheetCount(Document doc) => new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).GetElementCount();

    private static ViewSheet ByNumber(Document doc, string n) =>
        new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Single(s => s.SheetNumber == n);

    private static string LevelName(Document doc) =>
        new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).First().Name;

    [Test]
    public void Create_SingleWithNewFloorPlan_CenteredViewport(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            LoadTitleBlock(app, doc, "A3");
            var spec = new ViewSpec(ViewMode.BuatBaru, null, "Denah", LevelName(doc), 100, null);
            var report = CreateExecutor.Create(doc, [Req("A-110", "DENAH LANTAI 1", "denah", null, spec)], Prefs.Default, true, null, CancellationToken.None);

            Assert.That(report.Rows.Single().Ok, Is.True, report.Rows.Single().Pesan);
            var sheet = ByNumber(doc, "A-110");
            Assert.That(sheet.Name, Is.EqualTo("DENAH LANTAI 1"));
            Assert.That(sheet.LookupParameter(SnapshotReader.KindParam)?.AsString(), Is.EqualTo("denah"));
            Assert.That(TitleBlockFamilyOf(sheet), Is.EqualTo("PUPR_KOP_A3"));
            var vpId = sheet.GetAllViewports().Single();
            var vp = (Viewport)doc.GetElement(vpId);
            var area = ViewportPlacer.DrawingArea(sheet);
            var c = vp.GetBoxCenter();
            var mm = UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Millimeters);
            Assert.That(Math.Abs(c.X - (area.X + area.W / 2)), Is.LessThan(mm));
            Assert.That(Math.Abs(c.Y - (area.Y + area.H / 2)), Is.LessThan(mm));
            Assert.That(((View)doc.GetElement(vp.ViewId)).Scale, Is.EqualTo(100));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Create_Batch3_NumbersSequential(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            LoadTitleBlock(app, doc, "A3");
            var steps = new List<(int, int, string)>();
            var report = CreateExecutor.Create(doc, [Req("A-110", "DENAH 1"), Req("A-111", "DENAH 2"), Req("A-112", "DENAH 3")],
                Prefs.Default, true, new SyncProgress(steps), CancellationToken.None);
            Assert.That(report.Rows.All(r => r.Ok), Is.True);
            Assert.That(new[] { "A-110", "A-111", "A-112" }.Select(n => ByNumber(doc, n).Name), Is.EqualTo(new[] { "DENAH 1", "DENAH 2", "DENAH 3" }));
            Assert.That(steps.Last(), Is.EqualTo((3, 3, "DENAH 3")));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Create_MissingTitleBlock_SkipFailed_ContinuesOthers(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            LoadTitleBlock(app, doc, "A3");
            var report = CreateExecutor.Create(doc, [Req("A-110", "DENAH 1", tb: "TIDAK_ADA"), Req("A-111", "DENAH 2")],
                Prefs.Default, true, null, CancellationToken.None);
            Assert.That(report.Rows[0].Ok, Is.False);
            Assert.That(report.Rows[0].Pesan, Does.Contain("Title block 'TIDAK_ADA' tidak ditemukan"));
            Assert.That(report.Rows[1].Ok, Is.True);
            Assert.That(SheetCount(doc), Is.EqualTo(1));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Create_RollbackAllOnError(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            LoadTitleBlock(app, doc, "A3");
            var before = SheetCount(doc);
            var report = CreateExecutor.Create(doc, [Req("A-110", "DENAH 1"), Req("A-111", "DENAH 2", tb: "TIDAK_ADA")],
                Prefs.Default, false, null, CancellationToken.None);
            Assert.That(report.Rows.All(r => !r.Ok), Is.True);
            Assert.That(SheetCount(doc), Is.EqualTo(before));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Create_Cancel_RollsBack(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            LoadTitleBlock(app, doc, "A3");
            using var cts = new CancellationTokenSource();
            var progress = new CancelAfterFirst(cts);
            var report = CreateExecutor.Create(doc, [Req("A-110", "DENAH 1"), Req("A-111", "DENAH 2")], Prefs.Default, true, progress, cts.Token);
            Assert.That(report.Dibatalkan, Is.True);
            Assert.That(SheetCount(doc), Is.EqualTo(0));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Create_ViewAlreadyOnSheet_WarnsNoViewport(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            LoadTitleBlock(app, doc, "A3");
            var plan = new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>().First(v => !v.IsTemplate);
            CreateExecutor.Create(doc, [Req("A-110", "DENAH 1", null, null, new ViewSpec(ViewMode.PakaiYangAda, plan.UniqueId, null, null, 0, null))],
                Prefs.Default, true, null, CancellationToken.None);
            var report = CreateExecutor.Create(doc, [Req("A-111", "DENAH 2", null, null, new ViewSpec(ViewMode.PakaiYangAda, plan.UniqueId, null, null, 0, null))],
                Prefs.Default, true, null, CancellationToken.None);
            Assert.That(report.Rows.Single().Ok, Is.True);
            Assert.That(report.Rows.Single().Pesan, Does.Contain("sudah dipakai di lembar A-110"));
            Assert.That(ByNumber(doc, "A-111").GetAllViewports(), Is.Empty);
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Duplicate_WithViews_NewNumberInSameGroup(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            LoadTitleBlock(app, doc, "A3");
            var spec = new ViewSpec(ViewMode.BuatBaru, null, "Denah", LevelName(doc), 100, null);
            CreateExecutor.Create(doc, [Req("A-110", "DENAH LANTAI 1", "denah", null, spec)], Prefs.Default, true, null, CancellationToken.None);
            var report = CreateExecutor.Duplicate(doc, [ByNumber(doc, "A-110").UniqueId], DuplicateMode.DenganView, Pupr, NumberingOptions.Default);
            Assert.That(report.Rows.Single().Ok, Is.True, report.Rows.Single().Pesan);
            var copy = ByNumber(doc, "A-111");
            Assert.That(copy.Name, Is.EqualTo("DENAH LANTAI 1"));
            Assert.That(copy.GetAllViewports().Count, Is.EqualTo(1));
            Assert.That(copy.LookupParameter(SnapshotReader.KindParam)?.AsString(), Is.EqualTo("denah"));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void DeleteCreated_RemovesSheets(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            LoadTitleBlock(app, doc, "A3");
            var report = CreateExecutor.Create(doc, [Req("A-110", "DENAH 1")], Prefs.Default, true, null, CancellationToken.None);
            CreateExecutor.DeleteCreated(doc, report.Rows.Select(r => r.UniqueId!).ToList(), report.CreatedViews);
            Assert.That(SheetCount(doc), Is.EqualTo(0));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Thumbnail_ReturnsPng(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var tb = LoadTitleBlock(app, doc, "A3");
            var s = Sheet(doc, "A-110", "DENAH", tb);
            var bytes = ThumbnailService.Render(doc, s.UniqueId, NewTempDir());
            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes!.Take(4), Is.EqualTo(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }));
        }
        finally { doc.Close(false); }
    }

    private sealed class SyncProgress(List<(int, int, string)> steps) : IProgress<(int, int, string)>
    {
        public void Report((int, int, string) value) => steps.Add(value);
    }

    private sealed class CancelAfterFirst(CancellationTokenSource cts) : IProgress<(int, int, string)>
    {
        public void Report((int, int, string) value) => cts.Cancel();
    }

    [Test]
    public void DeleteCreated_AlsoRemovesCreatedViews(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            LoadTitleBlock(app, doc, "A3");
            int Plans() => new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>().Count(v => !v.IsTemplate);
            var before = Plans();
            var spec = new ViewSpec(ViewMode.BuatBaru, null, "Denah", LevelName(doc), 100, null);
            var report = CreateExecutor.Create(doc, [Req("A-110", "DENAH 1", "denah", null, spec)], Prefs.Default, true, null, CancellationToken.None);
            Assert.That(report.CreatedViews, Has.Count.EqualTo(1));
            Assert.That(Plans(), Is.EqualTo(before + 1));
            CreateExecutor.DeleteCreated(doc, report.Rows.Select(r => r.UniqueId!).ToList(), report.CreatedViews);
            Assert.That(Plans(), Is.EqualTo(before));
        }
        finally { doc.Close(false); }
    }
}
