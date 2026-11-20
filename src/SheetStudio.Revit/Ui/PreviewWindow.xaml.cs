using System.Windows;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Revit.Ui;

public sealed record PreviewRow(string Aksi, string Lama, string Baru, string Judul);

/// <summary>Table of what Apply will do: renumbers (old → new), edits and deletions.</summary>
public partial class PreviewWindow : Window
{
    public PreviewWindow(StudioViewModel vm, ApplyPlan plan, bool confirm)
    {
        InitializeComponent();
        var byId = vm.Sheets.ToDictionary(s => s.UniqueId);
        var rows = new List<PreviewRow>();
        foreach (var r in plan.Renumber)
        {
            var s = byId[r.UniqueId];
            rows.Add(new PreviewRow("Number", s.Number, r.Final, plan.Edits.FirstOrDefault(e => e.UniqueId == r.UniqueId)?.NewTitle ?? s.Title));
        }
        foreach (var e in plan.Edits.Where(e => plan.Renumber.All(r => r.UniqueId != e.UniqueId)))
        {
            var s = byId[e.UniqueId];
            rows.Add(new PreviewRow("Edit", s.Number, s.Number, e.NewTitle is null ? s.Title + " (parameters/display)" : $"{s.Title} → {e.NewTitle}"));
        }
        foreach (var id in plan.Deletes) rows.Add(new PreviewRow("Delete", byId[id].Number, "—", byId[id].Title));
        Rows = rows;
        Grid.ItemsSource = rows;
        Header.Text = $"{plan.Renumber.Count} renumbered · {plan.Edits.Count} edited · {plan.Deletes.Count} deleted." +
                      (confirm ? " Everything goes in as a single Undo step in Revit." : "");
        OkButton.Visibility = confirm ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.Content = confirm ? "Cancel" : "Close";
    }

    public IReadOnlyList<PreviewRow> Rows { get; }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
