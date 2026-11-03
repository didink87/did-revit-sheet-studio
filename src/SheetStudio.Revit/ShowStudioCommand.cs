using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SheetStudio.Revit.Ui;

namespace SheetStudio.Revit;

[Transaction(TransactionMode.Manual)]
public sealed class ShowStudioCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var pane = commandData.Application.GetDockablePane(PaneIds.Studio);
        if (pane.IsShown()) pane.Hide();
        else pane.Show();
        return Result.Succeeded;
    }
}
