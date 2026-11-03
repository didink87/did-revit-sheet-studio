using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SheetStudio.Core.Naming;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Revit.Ui;

/// <summary>Interactive number + title picker (discipline chips, type slider, number map, title builder).</summary>
public partial class NamePicker : UserControl
{
    public static readonly IValueConverter CountToVis = new CountVisibility();

    public NamePicker()
    {
        InitializeComponent();
        Theme.Apply(this);
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is INotifyPropertyChanged o) o.PropertyChanged -= OnVmChanged;
            if (e.NewValue is INotifyPropertyChanged n) n.PropertyChanged += OnVmChanged;
            UpdateStatusColour();
        };
    }

    private NamePickerViewModel? Vm => DataContext as NamePickerViewModel;

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NamePickerViewModel.Status)) UpdateStatusColour();
        if (e.PropertyName == nameof(NamePickerViewModel.AutoSeq) && Vm is not null) ManualRadio.IsChecked = !Vm.AutoSeq;
    }

    private void UpdateStatusColour()
    {
        if (Vm is null) return;
        var key = Vm.Status switch { Severity.Ok => "SS.Ok", Severity.Peringatan => "SS.Warn", _ => "SS.Danger" };
        StatusLine.SetResourceReference(TextBlock.ForegroundProperty, key);
        PreviewCard.SetResourceReference(Border.BorderBrushProperty, key);
    }

    private void OnDisciplineChecked(object sender, RoutedEventArgs e)
    {
        if (Vm is not null && (sender as FrameworkElement)?.DataContext is DisciplineOption d) Vm.SelectedDiscipline = d.Code;
    }

    private void OnDisciplineKey(object sender, KeyEventArgs e)
    {
        if (Vm is null) return;
        if (e.Key == Key.Right) { Vm.NextDiscipline.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Left) { Vm.PrevDiscipline.Execute(null); e.Handled = true; }
    }

    private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Vm is not null) Vm.SheetType = (int)Math.Round(e.NewValue);
    }

    private void OnSliderWheel(object sender, MouseWheelEventArgs e)
    {
        if (Vm is null) return;
        Vm.SheetType += e.Delta > 0 ? 1 : -1;
        e.Handled = true;
    }

    private void OnTickClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is string label) Vm?.PickType.Execute(label.Split(' ')[0]);
    }

    private void OnManualFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (Vm is not null) Vm.AutoSeq = false;
    }

    private sealed class CountVisibility : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value is int n && n > 0 ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }
}
