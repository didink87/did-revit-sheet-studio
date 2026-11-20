using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using SheetStudio.Core.Create;

namespace SheetStudio.Revit.Ui;

public sealed class BoolToVisibility : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type t, object p, CultureInfo c) =>
        (value is true) ^ Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type t, object p, CultureInfo c) => (value is Visibility.Visible) ^ Invert;
}

public sealed class NullToCollapsed : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is null || value is string { Length: 0 } ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Row indent: 16 px per level.</summary>
public sealed class IndentToMargin : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => new Thickness((value is int i ? i : 0) * 16, 0, 0, 0);
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

public sealed class BytesToImage : IValueConverter
{
    public object? Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is not byte[] { Length: > 0 } bytes) return null;
        var img = new BitmapImage();
        using var ms = new MemoryStream(bytes);
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = ms;
        img.EndInit();
        img.Freeze();
        return img;
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Equality with the converter parameter (enum name) → Visible.</summary>
public sealed class EqualsToVisibility : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        string.Equals(value?.ToString(), p?.ToString(), StringComparison.Ordinal) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Two-way radio binding for enums: IsChecked = (value == parameter).</summary>
public sealed class EnumEquals : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => string.Equals(value?.ToString(), p?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
        value is true && p is string s ? Enum.Parse(Nullable.GetUnderlyingType(t) ?? t, s) : Binding.DoNothing;
}

/// <summary>Plan status enum → English label (Batch grid).</summary>
public sealed class PlanStatusLabel : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => ViewKindLabels.Label(value as PlanStatus?);
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Stored view-kind id (Denah, Tampak…) → English label.</summary>
public sealed class ViewKindLabel : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => ViewKindLabels.Label(value as string);
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

internal static class Theme
{
    /// <summary>Merges the light or dark palette following Revit's UI theme.</summary>
    public static void Apply(FrameworkElement root)
    {
        var dark = false;
        try { dark = UIThemeManager.CurrentTheme == UITheme.Dark; }
        catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException or Autodesk.Revit.Exceptions.ApplicationException) { }
        var uri = new Uri($"pack://application:,,,/SheetStudio.Revit;component/Ui/Theme/{(dark ? "Dark" : "Light")}.xaml");
        var dicts = root.Resources.MergedDictionaries;
        var old = dicts.FirstOrDefault(d => d.Source?.OriginalString.Contains("/Ui/Theme/") == true);
        if (old is not null) dicts.Remove(old);
        dicts.Add(new ResourceDictionary { Source = uri });
    }
}
