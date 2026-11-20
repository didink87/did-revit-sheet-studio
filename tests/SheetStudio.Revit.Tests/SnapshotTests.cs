using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using NUnit.Framework;
using SheetStudio.Core.Model;
using SheetStudio.Core.Preferences;
using SheetStudio.Revit.Revit;
using static SheetStudio.Revit.Tests.Fixtures;

namespace SheetStudio.Revit.Tests;

[TestFixture]
public class SnapshotTests
{
    [Test]
    public void Snapshot_DocumentId_IsProjectInfoUniqueId(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var snap = SnapshotReader.Read(doc, Prefs.Default, ProjectState.Empty);

            Assert.That(snap.DocumentId, Is.Not.Empty);
            Assert.That(snap.DocumentId, Is.EqualTo(doc.ProjectInformation.UniqueId));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Snapshot_TitleBlockPerSheet_FromSingleCollector(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var a3 = LoadTitleBlock(app, doc, "A3");
            var a2 = LoadTitleBlock(app, doc, "A2");
            Sheet(doc, "A-110", "DENAH", a3);
            Sheet(doc, "A-111", "TAMPAK", a2);
            Sheet(doc, "G-001", "DAFTAR GAMBAR");

            var snap = SnapshotReader.Read(doc, Prefs.Default, ProjectState.Empty);

            Assert.That(snap.Sheets.Single(x => x.Number == "A-110").TitleBlockType, Is.EqualTo("PUPR_KOP_A3"));
            Assert.That(snap.Sheets.Single(x => x.Number == "A-111").TitleBlockType, Is.EqualTo("PUPR_KOP_A2"));
            Assert.That(snap.Sheets.Single(x => x.Number == "G-001").TitleBlockType, Is.Empty);
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Gateway_LoadPrefs_EqualsLoadPrefs(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            Assert.That(PreferencesStore.Serialize(RevitStudioGateway.LoadPrefs(doc)),
                Is.EqualTo(PreferencesStore.Serialize(RevitStudioGateway.Load(doc).Prefs)));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Snapshot_ReadsSheetsAndParams(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var tb = LoadTitleBlock(app, doc, "A3");
            var a = Sheet(doc, "A-110", "DENAH LANTAI 1", tb);
            Sheet(doc, "G-001", "DAFTAR GAMBAR");
            Tx(doc, () => a.get_Parameter(BuiltInParameter.SHEET_DRAWN_BY).Set("BUDI"));

            var snap = SnapshotReader.Read(doc, Prefs.Default, ProjectState.Empty);

            Assert.That(snap.Sheets.Select(s => s.Number), Is.EquivalentTo(new[] { "A-110", "G-001" }));
            var s = snap.Sheets.Single(x => x.Number == "A-110");
            Assert.That(s.Title, Is.EqualTo("DENAH LANTAI 1"));
            Assert.That(s.Versi, Is.EqualTo("A-110|DENAH LANTAI 1"));
            Assert.That(s.Editable, Is.True);
            Assert.That(s.InSheetList, Is.True);
            Assert.That(s.Params.Values, Does.Contain("BUDI"));
            Assert.That(s.TitleBlockType, Is.EqualTo("PUPR_KOP_A3"));
            Assert.That(snap.TitleBlockTypes, Does.Contain("PUPR_KOP_A3"));
            Assert.That(snap.Levels, Is.Not.Empty);
            Assert.That(snap.Views.Any(v => v.Jenis == "FloorPlan"), Is.True);
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Storage_RoundTrip(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            Assert.That(StorageAdapter.Read(doc), Is.EqualTo(((string?)null, (string?)null)));
            Tx(doc, () => StorageAdapter.Write(doc, "{\"Langkah\":5}", "{}"));
            Tx(doc, () => StorageAdapter.Write(doc, null, "{\"x\":1}")); // null keeps the other field
            Assert.That(StorageAdapter.Read(doc), Is.EqualTo(("{\"Langkah\":5}", "{\"x\":1}")));
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Preferences_SaveProjectWritesStorage(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            RevitStudioGateway.Save(doc, Prefs.Default with { Langkah = 10 }, null, userLevel: false);
            Assert.That(StorageAdapter.Read(doc).Preferensi, Does.Contain("\"Langkah\": 10"));
            Assert.That(RevitStudioGateway.Load(doc).Prefs.Langkah, Is.EqualTo(10));
        }
        finally { doc.Close(false); }
    }

    private static ViewDrafting Drafting(Document doc, string name)
    {
        ViewDrafting? v = null;
        Tx(doc, () =>
        {
            var type = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .First(t => t.ViewFamily == ViewFamily.Drafting);
            v = ViewDrafting.Create(doc, type.Id);
            v.Name = name;
            doc.Create.NewDetailCurve(v, Line.CreateBound(XYZ.Zero, new XYZ(5, 0, 0)));
        });
        return v!;
    }

    [Test]
    public void Snapshot_PlacedViews_ListsViewportViews(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var sheet = Sheet(doc, "A-110", "DENAH");
            Sheet(doc, "A-111", "KOSONG");
            var view = Drafting(doc, "Detail Uji");
            Tx(doc, () => Viewport.Create(doc, sheet.Id, view.Id, XYZ.Zero));

            var snap = SnapshotReader.Read(doc, Prefs.Default, ProjectState.Empty);

            var placed = snap.Sheets.Single(s => s.Number == "A-110").PlacedViews;
            Assert.That(placed, Has.Count.EqualTo(1));
            Assert.That(placed[0].Name, Is.EqualTo("Detail Uji"));
            Assert.That(placed[0].Kind, Is.EqualTo("DraftingView"));
            Assert.That(snap.Sheets.Single(s => s.Number == "A-111").PlacedViews, Is.Empty);
        }
        finally { doc.Close(false); }
    }

    [Test]
    public void Snapshot_PlacedViews_SchedulesCountRevisionDoesNot(Application app)
    {
        var doc = NewDoc(app);
        try
        {
            var tb = LoadTitleBlock(app, doc, "A3");
            var withSchedule = Sheet(doc, "A-110", "DENAH");
            Sheet(doc, "A-111", "HANYA KOP", tb);
            Tx(doc, () =>
            {
                var sch = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_Walls));
                ScheduleSheetInstance.Create(doc, withSchedule.Id, sch.Id, XYZ.Zero);
            });

            var snap = SnapshotReader.Read(doc, Prefs.Default, ProjectState.Empty);

            var placed = snap.Sheets.Single(s => s.Number == "A-110").PlacedViews;
            Assert.That(placed, Has.Count.EqualTo(1));
            Assert.That(placed[0].Kind, Is.EqualTo("Schedule"));
            Assert.That(snap.Sheets.Single(s => s.Number == "A-111").PlacedViews, Is.Empty);
        }
        finally { doc.Close(false); }
    }
}
