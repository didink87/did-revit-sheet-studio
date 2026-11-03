using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SheetStudio.Core.Layers;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Revit.Ui;

/// <summary>
/// Photoshop-style layers list. Selection rules live in LayersViewModel; this class only translates mouse,
/// keyboard and drag gestures into its calls.
/// </summary>
public partial class LayersPanel : UserControl
{
    private const string DragFormat = "SheetStudio.Keys";
    private Point? _downAt;
    private LayerRowViewModel? _downRow;
    private bool _deferredClick;

    public static readonly IValueConverter EyeOpacity = new OpacityConverter();
    public static readonly IValueConverter LockOpacity = new OpacityConverter();

    public LayersPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private LayersViewModel? _subscribed;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribed is not null) _subscribed.ScrollIntoViewRequested -= OnScrollIntoView;
        _subscribed = Layers;
        if (_subscribed is not null) _subscribed.ScrollIntoViewRequested += OnScrollIntoView;
        BuildMoveTypeMenu();
    }

    private void OnScrollIntoView(string key) =>
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var row = Layers?.Rows.FirstOrDefault(r => r.Key == key);
            if (row is not null) LayerList.ScrollIntoView(row);
        }), System.Windows.Threading.DispatcherPriority.Loaded);

    private StudioViewModel? Vm => DataContext as StudioViewModel;
    private LayersViewModel? Layers => Vm?.Layers;

    // ── mouse selection ─────────────────────────────────────────
    private void OnRowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInside<ButtonBase>(e.OriginalSource) || IsInside<TextBox>(e.OriginalSource)) return;
        var row = RowAt(e.OriginalSource);
        if (row is null || Layers is null) return;
        LayerList.Focus();

        if (e.ClickCount == 2)
        {
            if (row.Kind == RowKind.Lembar && IsInside<TextBlock>(e.OriginalSource, "Label")) Layers.BeginRename.Execute(null);
            else if (row.Kind == RowKind.Lembar) _ = Layers.OpenAsync(row.Key);
            else Layers.ToggleCollapse(row.Key, all: false);
            e.Handled = true;
            return;
        }

        var mods = Keyboard.Modifiers;
        _downAt = e.GetPosition(LayerList);
        _downRow = row;
        _deferredClick = false;
        if (mods.HasFlag(ModifierKeys.Control) && mods.HasFlag(ModifierKeys.Shift)) Layers.CtrlShiftClick(row.Key);
        else if (mods.HasFlag(ModifierKeys.Control)) Layers.CtrlClick(row.Key);
        else if (mods.HasFlag(ModifierKeys.Shift)) Layers.ShiftClick(row.Key);
        else if (row.IsSelected && Layers.Selection.Selected.Count > 1) _deferredClick = true; // may start a multi-row drag
        else Layers.Click(row.Key);
        e.Handled = true;
    }

    private void OnRowMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_deferredClick && _downRow is not null) Layers?.Click(_downRow.Key);
        _deferredClick = false;
        _downAt = null;
    }

    private void OnRowRightDown(object sender, MouseButtonEventArgs e)
    {
        var row = RowAt(e.OriginalSource);
        if (row is not null && !row.IsSelected) Layers?.Click(row.Key);
    }

    private void OnArrowClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LayerRowViewModel row)
            Layers?.ToggleCollapse(row.Key, all: Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));
        e.Handled = true;
    }

    private void OnEyeClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LayerRowViewModel row) Layers?.ToggleEye(row.Key);
    }

    private void OnLockClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LayerRowViewModel row) Layers?.ToggleLock(row.Key);
    }

    // ── drag & drop ─────────────────────────────────────────────
    private void OnRowMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _downAt is null || _downRow is null || Layers is null) return;
        var pos = e.GetPosition(LayerList);
        if (Math.Abs(pos.X - _downAt.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _downAt.Value.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        _deferredClick = false;
        var keys = Layers.Selection.Selected.Contains(_downRow.Key) ? Layers.Selection.Selected.ToArray() : [_downRow.Key];
        _downAt = null;
        DragDrop.DoDragDrop(LayerList, new DataObject(DragFormat, keys), DragDropEffects.Move);
        Layers.ClearDropHint();
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        e.Handled = true;
        if (Layers is null || e.Data.GetData(DragFormat) is not string[] keys) return;
        AutoScroll(e.GetPosition(LayerList));
        var (row, fraction) = RowUnder(e);
        if (row is null) { Layers.ClearDropHint(); return; }
        var eval = Layers.ShowDropHint(keys, row.Key, fraction);
        e.Effects = eval.Allowed ? DragDropEffects.Move : DragDropEffects.None;
        if (Vm is not null) Vm.Status = eval.Allowed ? "" : eval.Reason is null ? "" : "⛔ " + eval.Reason;
    }

    private void OnDragLeave(object sender, DragEventArgs e) => Layers?.ClearDropHint();

    private void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (Layers is null || e.Data.GetData(DragFormat) is not string[] keys) return;
        var (row, fraction) = RowUnder(e);
        Layers.ClearDropHint();
        if (row is null) return;
        var eval = Layers.Evaluate(keys, row.Key, fraction);
        if (eval.Allowed) Layers.Drop(keys, row.Key, eval.Pos);
    }

    private void AutoScroll(Point p)
    {
        var sv = FindChild<ScrollViewer>(LayerList);
        if (sv is null) return;
        if (p.Y < 24) sv.ScrollToVerticalOffset(sv.VerticalOffset - 12);
        else if (p.Y > LayerList.ActualHeight - 24) sv.ScrollToVerticalOffset(sv.VerticalOffset + 12);
    }

    private (LayerRowViewModel? Row, double Fraction) RowUnder(DragEventArgs e)
    {
        var hit = LayerList.InputHitTest(e.GetPosition(LayerList)) as DependencyObject;
        if (hit is null || ItemsControl.ContainerFromElement(LayerList, hit) is not ListBoxItem item) return (null, 0);
        var y = e.GetPosition(item).Y;
        return (item.DataContext as LayerRowViewModel, item.ActualHeight <= 0 ? 0.5 : y / item.ActualHeight);
    }

    // ── inline rename ───────────────────────────────────────────
    private void OnEditorVisible(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.IsVisible)
            Dispatcher.BeginInvoke(() => { tb.Focus(); tb.SelectAll(); });
    }

    private void OnEditorKey(object sender, KeyEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not LayerRowViewModel row || Layers is null) return;
        if (e.Key == Key.Enter) { Layers.CommitRename(row); e.Handled = true; LayerList.Focus(); }
        else if (e.Key == Key.Escape) { Layers.CancelRename(row); e.Handled = true; LayerList.Focus(); }
    }

    private void OnEditorLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LayerRowViewModel row) Layers?.CommitRename(row);
    }

    // ── thumbnails ──────────────────────────────────────────────
    private System.Windows.Threading.DispatcherTimer? _thumbTimer;

    /// <summary>Debounced (250 ms): scrolling fast must not queue an export per row that flew past.</summary>
    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (Vm is null || !Vm.Snapshot.Prefs.Thumbnail) return;
        if (_thumbTimer is null)
        {
            _thumbTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _thumbTimer.Tick += (_, _) => { _thumbTimer.Stop(); RequestVisibleThumbnails(); };
        }
        _thumbTimer.Stop();
        _thumbTimer.Start();
    }

    /// <summary>Only realized containers inside the viewport; never loops all rows.</summary>
    private void RequestVisibleThumbnails()
    {
        if (Vm is null || Layers is null || !Vm.Snapshot.Prefs.Thumbnail) return;
        var sv = FindChild<ScrollViewer>(LayerList);
        var panel = FindChild<VirtualizingStackPanel>(LayerList);
        if (sv is null || panel is null) return;
        var viewport = new Rect(0, 0, sv.ViewportWidth > 0 ? sv.ViewportWidth : sv.ActualWidth, sv.ActualHeight);
        var visible = new List<string>();
        foreach (var child in panel.Children)
        {
            if (child is not ListBoxItem { IsVisible: true, DataContext: LayerRowViewModel { Kind: RowKind.Lembar, Thumbnail: null } row } item) continue;
            var bounds = item.TransformToAncestor(sv).TransformBounds(new Rect(0, 0, item.ActualWidth, item.ActualHeight));
            if (bounds.IntersectsWith(viewport)) visible.Add(row.Key);
        }
        if (visible.Count > 0) _ = Layers.LoadThumbnailsAsync(visible);
    }

    // ── context menu helpers ────────────────────────────────────
    private void BuildMoveTypeMenu()
    {
        MoveTypeMenu.Items.Clear();
        if (Vm is null) return;
        foreach (var t in Vm.Profile.Tipe)
        {
            var digit = t.Digit;
            MoveTypeMenu.Items.Add(new MenuItem
            {
                Header = $"{t.Digit}  {t.Keterangan.Split(':')[0].TrimEnd('.')}",
                Command = new RelayCommand(() => { if (Vm is not null) Vm.Inspector.SheetType = digit; }),
            });
        }
    }

    private void OnEditNumber(object sender, RoutedEventArgs e) => Vm?.Inspector.EditNumber.Execute(null);

    // ── helpers ─────────────────────────────────────────────────
    private LayerRowViewModel? RowAt(object source) =>
        source is DependencyObject d && ItemsControl.ContainerFromElement(LayerList, d) is ListBoxItem item ? item.DataContext as LayerRowViewModel : null;

    private static bool IsInside<T>(object source, string? name = null) where T : FrameworkElement
    {
        for (var d = source as DependencyObject; d is not null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is T fe && (name is null || fe.Name == name)) return true;
        return false;
    }

    private static T? FindChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            if (c is T t) return t;
            if (FindChild<T>(c) is { } found) return found;
        }
        return null;
    }

    private sealed class OpacityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value is true ? 1.0 : 0.25;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }
}
