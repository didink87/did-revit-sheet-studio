using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace SheetStudio.Revit.Revit;

/// <summary>Where gateway work runs. Production: the active document through the ExternalEvent queue.</summary>
public interface IDocumentRunner
{
    Task<T> Run<T>(Func<Document, T> work);
}

public sealed class ActiveDocumentRunner(ExternalEventQueue queue) : IDocumentRunner
{
    public Task<T> Run<T>(Func<Document, T> work) => queue.Run(ui => work(ActiveDoc(ui)));

    public Task RunUi(Action<UIApplication> work) => queue.Run(work);

    internal static Document ActiveDoc(UIApplication ui) =>
        ui.ActiveUIDocument?.Document ?? throw new InvalidOperationException("No active Revit document");
}
