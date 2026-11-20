using System.Windows;
using Microsoft.Win32;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Revit.Ui;

public partial class BatchWindow : Window
{
    private readonly BatchViewModel _vm;

    public BatchWindow(BatchViewModel vm)
    {
        InitializeComponent();
        Theme.Apply(this);
        _vm = vm;
        DataContext = vm;
        Closing += (_, e) =>
        {
            if (!vm.IsRunning) return;
            e.Cancel = true;
            MessageBox.Show(this, "The batch is still running. Press Stop first.", "Sheet Studio");
        };
        vm.WarningRequested += msg => MessageBox.Show(this, msg + Environment.NewLine + Environment.NewLine +
            "Set the title block per paper size in ⚙ Preferences, or change the Paper choice, then press RUN again.",
            "Sheet Studio — Title block not chosen", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnLoadPreset(object sender, RoutedEventArgs e)
    {
        if (PresetBox.SelectedItem is string name) _vm.LoadPreset(name);
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "CSV (*.csv)|*.csv|All files|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        var errors = _vm.ImportCsv(File.ReadAllText(dlg.FileName));
        if (errors.Count > 0) MessageBox.Show(this, string.Join(Environment.NewLine, errors.Take(20)), "Some CSV rows were skipped");
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "batch-sheets.csv" };
        if (dlg.ShowDialog(this) == true) File.WriteAllText(dlg.FileName, _vm.ExportCsv(), new System.Text.UTF8Encoding(true));
    }
}
