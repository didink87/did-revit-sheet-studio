using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace SheetStudio.Revit;

[Transaction(TransactionMode.Manual)]
public sealed class ShowStudioCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        TaskDialog.Show("Sheet Studio", "Panel Sheet Studio belum tersedia.");
        return Result.Succeeded;
    }
}
