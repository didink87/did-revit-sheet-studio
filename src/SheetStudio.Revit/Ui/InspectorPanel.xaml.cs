using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Revit.Ui;

/// <summary>Edits the selected sheet(s). All edits are staged; the pane's TERAPKAN writes them.</summary>
public partial class InspectorPanel : UserControl
{
    public static readonly IValueConverter NotVis = new BoolToVisibility { Invert = true };
    public static readonly IValueConverter NullVis = new NullIsVisible();

    public InspectorPanel()
    {
        InitializeComponent();
    }

    private InspectorViewModel? Vm => DataContext as InspectorViewModel;

    private void OnTypeSlider(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Only user gestures change the type; binding refreshes also raise ValueChanged.
        if (Vm is null || !(TypeSlider.IsMouseCaptureWithin || TypeSlider.IsKeyboardFocusWithin)) return;
        Vm.SheetType = (int)Math.Round(e.NewValue);
    }

    private void OnTitleKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Vm?.ApplyTitle.Execute(null);
    }

    private void OnParamKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (sender as FrameworkElement)?.DataContext is ParamField f) Vm?.ApplyParam.Execute(f);
    }

    private void OnParamLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ParamField f && !(f.IsMixed && f.Value.Length == 0)) Vm?.ApplyParam.Execute(f);
    }

    private void OnDuplicateMenu(object sender, RoutedEventArgs e)
    {
        if (DuplicateButton.ContextMenu is { } menu)
        {
            menu.DataContext = DataContext;
            menu.PlacementTarget = DuplicateButton;
            menu.IsOpen = true;
        }
    }

    private sealed class NullIsVisible : IValueConverter
    {
        public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c) => value is null ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c) => Binding.DoNothing;
    }
}
