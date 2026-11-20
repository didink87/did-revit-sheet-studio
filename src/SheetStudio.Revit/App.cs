using Autodesk.Revit.UI;
using SheetStudio.Core.Diagnostics;
using SheetStudio.Revit.Diagnostics;
using SheetStudio.Core.ViewModels;
using SheetStudio.Revit.Revit;
using SheetStudio.Revit.Ui;

namespace SheetStudio.Revit;

public sealed class App : IExternalApplication
{
    // Shared with Family Browser and Face View: one "DID" tab, one "DID" panel.
    public const string TabName = "DID";
    public const string PanelName = "DID";

    public static ExternalEventQueue? Queue { get; private set; }
    public static RevitStudioGateway? Gateway { get; private set; }
    public static StudioViewModel? ViewModel { get; private set; }
    private static ModelWatcher? _watcher;

    public Result OnStartup(UIControlledApplication application)
    {
        StudioLog.Sink = new FileLogSink(FileLogSink.DefaultDir).Write;
        using var _ = StudioLog.Time("startup");
        try { application.CreateRibbonTab(TabName); } catch (Autodesk.Revit.Exceptions.ArgumentException) { /* tab already created by another DID add-in */ }
        var panel = application.GetRibbonPanels(TabName).FirstOrDefault(p => p.Name == PanelName)
                    ?? application.CreateRibbonPanel(TabName, PanelName);
        var asm = typeof(App).Assembly;
        panel.AddItem(new PushButtonData("SheetStudio", "Sheet\nStudio", asm.Location, typeof(ShowStudioCommand).FullName)
        {
            LargeImage = LoadIcon(asm, "sheet32.png"),
            Image = LoadIcon(asm, "sheet16.png"),
            ToolTip = "Show/hide the Sheet Studio panel: create, reorder (layers), and edit sheets",
        });

        Queue = new ExternalEventQueue();
        var runner = new ActiveDocumentRunner(Queue);
        Gateway = new RevitStudioGateway(runner, uid => runner.RunUi(ui =>
        {
            if (ui.ActiveUIDocument?.Document.GetElement(uid) is Autodesk.Revit.DB.ViewSheet s) ui.ActiveUIDocument.RequestViewChange(s);
        }));
        ViewModel = new StudioViewModel(Gateway)
        {
            Titles = new Core.Naming.TitleLibrary(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SheetStudio", "judul-tersimpan.json")),
        };
        var pane = new StudioPane(ViewModel);
        application.RegisterDockablePane(PaneIds.Studio, "Sheet Studio", pane);

        _watcher = new ModelWatcher(application.ControlledApplication, 500, a => pane.Dispatcher.BeginInvoke(a));
        _watcher.Changed += Gateway.RaiseModelChanged;
        application.ViewActivated += (_, e) =>
        {
            if (e.PreviousActiveView?.Document?.Equals(e.Document) == true) return;
            pane.Dispatcher.BeginInvoke(async () =>
            {
                try { await ViewModel.ReloadAsync(ReloadReason.GantiDokumen); }
                catch (Exception ex) { ViewModel.ReportError(ex); }
            });
        };
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        _watcher?.Dispose();
        return Result.Succeeded;
    }

    private static System.Windows.Media.Imaging.BitmapImage LoadIcon(System.Reflection.Assembly asm, string name)
    {
        using var s = asm.GetManifestResourceStream($"SheetStudio.Revit.Resources.{name}")!;
        var img = new System.Windows.Media.Imaging.BitmapImage();
        img.BeginInit();
        img.StreamSource = s;
        img.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        img.EndInit();
        img.Freeze();
        return img;
    }
}
