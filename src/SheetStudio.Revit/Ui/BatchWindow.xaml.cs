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
            MessageBox.Show(this, "Batch masih berjalan. Tekan Hentikan dulu.", "Sheet Studio");
        };
        vm.WarningRequested += msg => MessageBox.Show(this, msg + Environment.NewLine + Environment.NewLine +
            "Atur kop per kertas di ⚙ Preferensi, atau ganti pilihan Kertas, lalu tekan JALANKAN lagi.",
            "Sheet Studio — Kop belum dipilih", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnLoadPreset(object sender, RoutedEventArgs e)
    {
        if (PresetBox.SelectedItem is string name) _vm.LoadPreset(name);
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "CSV (*.csv)|*.csv|Semua file|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        var errors = _vm.ImportCsv(File.ReadAllText(dlg.FileName));
        if (errors.Count > 0) MessageBox.Show(this, string.Join(Environment.NewLine, errors.Take(20)), "Sebagian baris CSV dilewati");
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "batch-lembar.csv" };
        if (dlg.ShowDialog(this) == true) File.WriteAllText(dlg.FileName, _vm.ExportCsv(), new System.Text.UTF8Encoding(true));
    }
}
