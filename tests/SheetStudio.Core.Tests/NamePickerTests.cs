using SheetStudio.Core.Changes;
using SheetStudio.Core.Model;
using SheetStudio.Core.Naming;
using SheetStudio.Core.Numbering;
using SheetStudio.Core.ViewModels;
using static SheetStudio.Core.Tests.Fx;

namespace SheetStudio.Core.Tests;

public class NamePickerTests
{
    private static readonly IReadOnlyList<SheetSnapshot> Sheets =
        [S("a", "A-110", "DENAH LANTAI 1", kind: "denah"), S("b", "A-111", "DENAH LANTAI 2", kind: "denah"), S("c", "A-101", "RENCANA MASSA BANGUNAN", kind: "blockplan")];

    private static NamePickerViewModel Vm(string? editing = null) =>
        new(Profiles.Pupr, Sheets, ChangeSet.Start(State()), NumberingOptions.Default, ["Lantai 1", "Lantai 2", "Lantai 3"], editing);

    [Fact]
    public void Default_A_Type1_AutoSeqFirstFree()
    {
        var vm = Vm();
        Assert.Equal(("A", 1, true), (vm.SelectedDiscipline, vm.SheetType, vm.AutoSeq));
        Assert.Equal("A-102", vm.NumberPreview); // whole type: 01 taken by block plan
        vm.SelectedKindId = "denah";
        Assert.Equal("A-112", vm.NumberPreview);
        Assert.Equal(12, vm.EffectiveSeq);
    }

    [Fact]
    public void SliderChange_UpdatesDescriptionAndKinds()
    {
        var vm = Vm();
        vm.SheetType = 3;
        Assert.Equal("Sections", vm.TypeName);
        Assert.StartsWith("Potongan", vm.TypeDescription);
        Assert.Contains(vm.KindOptions, k => k.Id == "potongan-bangunan");
        Assert.Null(vm.KindOptions[^1].Id); // "BEBAS" last
        Assert.Equal(10, vm.TypeTickLabels.Count);
    }

    [Fact]
    public void SelectKind_SetsDisciplineAndType()
    {
        var vm = Vm();
        vm.SelectedDiscipline = "S";
        vm.SelectedKindId = "detail-pintu-jendela";
        Assert.Equal(("A", 5), (vm.SelectedDiscipline, vm.SheetType));
        vm.SheetType = 2;
        Assert.Null(vm.SelectedKindId);
    }

    [Fact]
    public void ShowAll_Lists21()
    {
        var vm = Vm();
        Assert.True(vm.DisciplineOptions.Count < 21);
        Assert.Single(vm.DisciplineOptions, d => d.IsSelected && d.Code == "A");
        vm.ShowAllDisciplines = true;
        Assert.Equal(21, vm.DisciplineOptions.Count);
        Assert.Contains("Arsitektur", vm.DisciplineDescription);
    }

    [Fact]
    public void NextPrevDiscipline_Cycles()
    {
        var vm = Vm();
        var codes = vm.DisciplineOptions.Select(d => d.Code).ToList();
        vm.NextDiscipline.Execute(null);
        Assert.Equal(codes[(codes.IndexOf("A") + 1) % codes.Count], vm.SelectedDiscipline);
        vm.PrevDiscipline.Execute(null);
        Assert.Equal("A", vm.SelectedDiscipline);
    }

    [Fact]
    public void SecondLetter_EP_PreviewEP1xx()
    {
        var vm = Vm();
        vm.SelectedDiscipline = "E";
        Assert.Null(vm.SecondLetters[0].Huruf);
        vm.SelectedSecondLetter = 'P';
        Assert.Equal("EP101", vm.NumberPreview);
        vm.SelectedDiscipline = "A";
        Assert.Null(vm.SelectedSecondLetter);
    }

    [Fact]
    public void PickCell_TakenIgnored_FreeSets()
    {
        var vm = Vm();
        vm.SelectedKindId = "denah";
        Assert.Equal(CellState.Terpakai, vm.NumberMapCells[10 - 1]);
        vm.PickCell.Execute(10);
        Assert.True(vm.AutoSeq);
        vm.PickCell.Execute(20);
        Assert.Equal((false, 20, "A-120"), (vm.AutoSeq, vm.ManualSeq, vm.NumberPreview));
        Assert.Equal(CellState.Dipilih, vm.NumberMapCells[20 - 1]);
    }

    [Fact]
    public void DuplicateNumber_StatusGalat_CannotConfirm()
    {
        var vm = Vm();
        vm.FreeTitle = "DENAH KHUSUS";
        vm.AutoSeq = false;
        vm.ManualSeq = 11;
        Assert.Equal(Severity.Galat, vm.Status);
        Assert.Contains("A-111", vm.StatusText);
        Assert.False(vm.CanConfirm);
    }

    [Fact]
    public void TitleWithAbbrev_Warns_AcceptSuggestions_Fixes()
    {
        var vm = Vm();
        vm.FreeTitle = "Denah Lt. 3";
        Assert.Equal(Severity.Peringatan, vm.Status);
        Assert.True(vm.CanConfirm);
        Assert.Equal("LANTAI", vm.Hints.Single().Suggestion);
        vm.AcceptSuggestions.Execute(null);
        Assert.Equal(("DENAH LANTAI 3", Severity.Ok), (vm.TitlePreview, vm.Status));
    }

    [Fact]
    public void KindTemplate_BuildsTitleFromSubject()
    {
        var vm = Vm();
        vm.SelectedKindId = "denah";
        vm.Subject = "Lantai 3";
        Assert.Equal("DENAH LANTAI 3", vm.TitlePreview);
        Assert.Contains("LANTAI 3", vm.SubjectOptions);
        Assert.Contains("TAMPAK DEPAN", vm.SubjectOptions);
        var r = vm.Result;
        Assert.Equal((new SheetId("A", 1, 12), "DENAH LANTAI 3", "denah", "LANTAI 3"), (r.Id, r.Title, r.KindId, r.Subjek));
    }

    [Fact]
    public void TypingTitle_ReleasesKindTemplate()
    {
        var vm = Vm();
        vm.SelectedKindId = "denah";
        vm.Subject = "Lantai 3";
        vm.FreeTitle = "DENAH KHUSUS";
        Assert.Null(vm.SelectedKindId);
        Assert.Equal("DENAH KHUSUS", vm.TitlePreview);
    }

    [Fact]
    public void Editing_TitleBoxShowsCurrentTitle()
    {
        var vm = Vm("a");
        Assert.Equal(("DENAH LANTAI 1", "denah"), (vm.FreeTitle, vm.SelectedKindId));
    }

    [Fact]
    public void EmptyTitle_CannotConfirm()
    {
        var vm = Vm();
        Assert.Equal(Severity.Galat, vm.Status);
        Assert.False(vm.CanConfirm);
    }

    [Fact]
    public void Editing_ExistingSheetNotCountedAsDuplicateOfItself()
    {
        var vm = Vm("a");
        Assert.Equal(("A", 1, "denah", "A-110"), (vm.SelectedDiscipline, vm.SheetType, vm.SelectedKindId, vm.NumberPreview));
        Assert.NotEqual(Severity.Galat, vm.Status);
    }

    [Fact]
    public void PropertyChanged_RaisedForPreview()
    {
        var vm = Vm();
        vm.SelectedKindId = "denah";
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        vm.Subject = "LANTAI 2";
        Assert.Contains(nameof(vm.TitlePreview), raised);
        Assert.Contains(nameof(vm.StatusText), raised);
    }

    [Fact]
    public void NumberCells_CarrySeqLabelAndState()
    {
        var vm = Vm();
        vm.SelectedKindId = "denah";
        var cells = vm.NumberCells;
        Assert.Equal(99, cells.Count);
        Assert.Equal((10, "10", CellState.Terpakai), (cells[9].Seq, cells[9].Label, cells[9].State));
        Assert.Equal(CellState.Dipilih, cells.Single(c => c.Seq == 12).State);
        vm.PickCell.Execute(cells[19].Seq);
        Assert.Equal(20, vm.ManualSeq);
    }

    [Fact]
    public void PickTypeLabel_SetsSheetType()
    {
        var vm = Vm();
        vm.PickType.Execute(5);
        Assert.Equal(5, vm.SheetType);
    }
}
