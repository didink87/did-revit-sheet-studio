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
        SizeChanged += (_, e) => { if (e.WidthChanged) ApplyLayout(e.NewSize.Width); };
        vm.IsPaneVisible = IsVisible; // the pane starts hidden; IsVisibleChanged does not fire for that initial state
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
        data.InitialState = new DockablePaneState { DockPosition = DockPosition.Right, MinimumWidth = 240 };
        data.VisibleByDefault = false;
    }

    /// <summary>Di bawah lebar ini Inspektur pindah ke bawah daftar lapisan, agar pane tetap muat di samping Project Browser.</summary>
    internal const double NarrowWidth = 560;
    private bool? _narrow;

    private void ApplyLayout(double width)
    {
        var narrow = width < NarrowWidth;
        if (_narrow == narrow) return;
        _narrow = narrow;
        MainGrid.ColumnDefinitions.Clear();
        MainGrid.RowDefinitions.Clear();
        if (narrow)
        {
            MainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star), MinHeight = 120 });
            MainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
            MainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star), MinHeight = 80 });
            Place(LayersArea, 0, 0); Place(Splitter, 1, 0); Place(InspectorArea, 2, 0);
            Splitter.ResizeDirection = GridResizeDirection.Rows;
        }
        else
        {
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 220 });
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300), MinWidth = 200 });
            Place(LayersArea, 0, 0); Place(Splitter, 0, 1); Place(InspectorArea, 0, 2);
            Splitter.ResizeDirection = GridResizeDirection.Columns;
        }
    }

    private static void Place(UIElement e, int row, int col)
    {
        Grid.SetRow(e, row);
        Grid.SetColumn(e, col);
    }

    private void OnPreview(object sender, RoutedEventArgs e) =>
        new PreviewWindow(ViewModel, ViewModel.BuildPlan(), confirm: false) { Owner = Window.GetWindow(this) }.ShowDialog();

    private sealed class StrategyLabelConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value is true ? "Urut" : "Rentang";
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }
}
