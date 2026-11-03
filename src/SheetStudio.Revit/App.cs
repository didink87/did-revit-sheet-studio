using System.Reflection;
using Autodesk.Revit.UI;

namespace SheetStudio.Revit;

public sealed class App : IExternalApplication
{
    public const string TabName = "Sheet Studio";

    public Result OnStartup(UIControlledApplication application)
    {
        application.CreateRibbonTab(TabName);
        var panel = application.CreateRibbonPanel(TabName, "Lembar");
        var asm = Assembly.GetExecutingAssembly();
        panel.AddItem(new PushButtonData("SheetStudio", "Sheet\nStudio", asm.Location, typeof(ShowStudioCommand).FullName)
        {
            ToolTip = "Buka panel Sheet Studio: buat, urutkan, dan edit lembar",
        });
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
}
