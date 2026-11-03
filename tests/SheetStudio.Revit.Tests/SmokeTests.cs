using Autodesk.Revit.UI;
using NUnit.Framework;

namespace SheetStudio.Revit.Tests;

[TestFixture]
public class SmokeTests
{
    [Test]
    public void Smoke_PluginLoadsAndRibbonTabExists(UIControlledApplication app)
    {
        var result = new App().OnStartup(app);

        Assert.That(result, Is.EqualTo(Result.Succeeded));
        Assert.That(app.GetRibbonPanels(App.TabName).Select(p => p.Name), Does.Contain("Lembar"));
    }
}
