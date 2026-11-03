using System.Windows;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Revit.Ui;

public partial class PreferencesWindow : Window
{
    public PreferencesWindow(PreferencesViewModel vm)
    {
        InitializeComponent();
        Theme.Apply(this);
        DataContext = vm;
    }

    private void OnSaved(object sender, RoutedEventArgs e) => Close();
}
