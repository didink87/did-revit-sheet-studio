using System.Windows;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Revit.Ui;

/// <summary>NamePicker in edit mode for one sheet; OK stages the result through InspectorViewModel.ApplyName.</summary>
public partial class EditNumberWindow : Window
{
    private readonly NamePickerViewModel _picker;
    private readonly InspectorViewModel _inspector;

    public EditNumberWindow(NamePickerViewModel picker, InspectorViewModel inspector)
    {
        InitializeComponent();
        Theme.Apply(this);
        _picker = picker;
        _inspector = inspector;
        DataContext = picker;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!_picker.CanConfirm) return;
        _inspector.ApplyName(_picker.Result);
        DialogResult = true;
    }
}
