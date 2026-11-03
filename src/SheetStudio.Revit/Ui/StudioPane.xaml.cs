using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Autodesk.Revit.UI;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Revit.Ui;

public partial class StudioPane : UserControl, IDockablePaneProvider
{
    public static readonly IValueConverter StrategyLabel = new StrategyLabelConverter();

    public StudioPane(StudioViewModel vm)
    {
        InitializeComponent();
        Theme.Apply(this);
        DataContext = vm;
        ViewModel = vm;
        LayersView.DataContext = vm;
        vm.ConfirmApply = plan => Task.FromResult(new PreviewWindow(vm, plan, confirm: true) { Owner = Window.GetWindow(this) }.ShowDialog() == true);
        vm.CreateRequested += create => new CreateSheetWindow(create) { Owner = Window.GetWindow(this) }.ShowDialog();
        vm.BatchRequested += batch => new BatchWindow(batch) { Owner = Window.GetWindow(this) }.ShowDialog();
        InspectorHost.Content = new InspectorPanel { DataContext = vm.Inspector };
        vm.PreferencesRequested += prefs => new PreferencesWindow(prefs) { Owner = Window.GetWindow(this) }.ShowDialog();
        vm.Inspector.EditNumberRequested += picker => new EditNumberWindow(picker, vm.Inspector) { Owner = Window.GetWindow(this) }.ShowDialog();
        IsVisibleChanged += async (_, e) =>
        {
            var visible = e.NewValue is true;
            vm.IsPaneVisible = visible;
            if (visible) await vm.OnShownAsync();
        };
    }

    public StudioViewModel ViewModel { get; }

    public void SetupDockablePane(DockablePaneProviderData data)
    {
        data.FrameworkElement = this;
        data.InitialState = new DockablePaneState { DockPosition = DockPosition.Right, MinimumWidth = 560 };
        data.VisibleByDefault = false;
    }

    private void OnPreview(object sender, RoutedEventArgs e) =>
        new PreviewWindow(ViewModel, ViewModel.BuildPlan(), confirm: false) { Owner = Window.GetWindow(this) }.ShowDialog();

    private sealed class StrategyLabelConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value is true ? "Urut" : "Rentang";
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }
}
