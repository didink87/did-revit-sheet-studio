using System.ComponentModel;
using System.Windows;
using SheetStudio.Core.Create;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Revit.Ui;

public partial class CreateSheetWindow : Window
{
    public CreateSheetWindow(CreateSheetViewModel vm)
    {
        InitializeComponent();
        Theme.Apply(this);
        DataContext = vm;
        vm.Closed += () => { if (IsLoaded) Close(); };
        vm.WarningRequested += msg => MessageBox.Show(this, msg + Environment.NewLine + Environment.NewLine +
            "Choose a paper size (A3/A2/A1) and title block in the top-right column, then press CREATE SHEET again.",
            "Sheet Studio — Paper & title block not chosen", MessageBoxButton.OK, MessageBoxImage.Warning);
        vm.PropertyChanged += OnChanged;
        UpdatePanels(vm);
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CreateSheetViewModel.ViewMode) && sender is CreateSheetViewModel vm) UpdatePanels(vm);
    }

    private void UpdatePanels(CreateSheetViewModel vm)
    {
        ExistingPanel.Visibility = vm.ViewMode == ViewMode.PakaiYangAda ? Visibility.Visible : Visibility.Collapsed;
        NewPanel.Visibility = vm.ViewMode == ViewMode.BuatBaru ? Visibility.Visible : Visibility.Collapsed;
    }
}
