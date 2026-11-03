using Autodesk.Revit.UI;
using NUnit.Framework;
using SheetStudio.Revit.Ui;

namespace SheetStudio.Revit.Tests;

[TestFixture]
public class SmokeTests
{
    [Test]
    public void Smoke_PluginLoads_RibbonTab_And_PaneRegistered(UIControlledApplication app)
    {
        var result = new App().OnStartup(app);

        Assert.That(result, Is.EqualTo(Result.Succeeded));
        Assert.That(app.GetRibbonPanels(App.TabName).Select(p => p.Name), Does.Contain("Lembar"));
        Assert.That(App.ViewModel, Is.Not.Null);
        // Revit builds registered panes lazily, so prove registration by the duplicate-id rejection.
        Assert.That(() => app.RegisterDockablePane(PaneIds.Studio, "dup", new StudioPane(App.ViewModel!)),
            Throws.InstanceOf<Autodesk.Revit.Exceptions.ArgumentException>());
    }
}
