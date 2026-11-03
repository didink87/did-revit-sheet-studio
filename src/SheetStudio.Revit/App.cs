using Autodesk.Revit.UI;
using SheetStudio.Core.ViewModels;
using SheetStudio.Revit.Revit;
using SheetStudio.Revit.Ui;

namespace SheetStudio.Revit;

public sealed class App : IExternalApplication
{
    public const string TabName = "Sheet Studio";

    public static ExternalEventQueue? Queue { get; private set; }
    public static RevitStudioGateway? Gateway { get; private set; }
    public static StudioViewModel? ViewModel { get; private set; }
    private static ModelWatcher? _watcher;

    public Result OnStartup(UIControlledApplication application)
    {
        application.CreateRibbonTab(TabName);
        var panel = application.CreateRibbonPanel(TabName, "Lembar");
        var asm = typeof(App).Assembly;
        panel.AddItem(new PushButtonData("SheetStudio", "Sheet\nStudio", asm.Location, typeof(ShowStudioCommand).FullName)
        {
            ToolTip = "Buka/tutup panel Sheet Studio: buat, urutkan (lapisan), dan edit lembar",
        });

        Queue = new ExternalEventQueue();
        Gateway = new RevitStudioGateway(Queue);
        ViewModel = new StudioViewModel(Gateway);
        var pane = new StudioPane(ViewModel);
        application.RegisterDockablePane(PaneIds.Studio, "Sheet Studio", pane);

        _watcher = new ModelWatcher(application.ControlledApplication, 500, a => pane.Dispatcher.BeginInvoke(a));
        _watcher.Changed += Gateway.RaiseModelChanged;
        application.ViewActivated += (_, e) =>
        {
            if (e.PreviousActiveView?.Document?.Equals(e.Document) == true || !pane.IsVisible) return;
            pane.Dispatcher.BeginInvoke(async () => await ViewModel.SafeLoadAsync());
        };
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        _watcher?.Dispose();
        return Result.Succeeded;
    }
}
