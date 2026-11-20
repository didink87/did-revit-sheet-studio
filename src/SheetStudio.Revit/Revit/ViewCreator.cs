using Autodesk.Revit.DB;
using SheetStudio.Core.Create;
using SheetStudio.Core.Naming;

namespace SheetStudio.Revit.Revit;

/// <summary>Creates the view a new sheet asks for. Elevations/sections need a cut line the user draws, so they
/// start as drafting placeholders; legends cannot be created by the API, so the first legend is duplicated.</summary>
public static class ViewCreator
{
    public static View? Create(Document doc, ViewSpec spec, string title, out string? warning)
    {
        warning = null;
        View? view;
        switch (spec.JenisView)
        {
            case "Denah":
            case "Plafon":
                var level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                    .OrderBy(l => l.Elevation).FirstOrDefault(l => spec.LevelName is null || l.Name == spec.LevelName)
                    ?? throw new InvalidOperationException($"Level '{spec.LevelName}' not found");
                var family = spec.JenisView == "Denah" ? ViewFamily.FloorPlan : ViewFamily.CeilingPlan;
                view = ViewPlan.Create(doc, TypeOf(doc, family).Id, level.Id);
                break;
            case "Drafting":
                view = ViewDrafting.Create(doc, TypeOf(doc, ViewFamily.Drafting).Id);
                break;
            case "Tampak":
            case "Potongan":
                view = ViewDrafting.Create(doc, TypeOf(doc, ViewFamily.Drafting).Id);
                warning = $"{ViewKindLabels.Label(spec.JenisView)} was created as a temporary drafting view; draw the {ViewKindLabels.Label(spec.JenisView).ToLowerInvariant()} line on a plan, then replace the view";
                break;
            case "TigaD":
                view = View3D.CreateIsometric(doc, TypeOf(doc, ViewFamily.ThreeDimensional).Id);
                break;
            case "Legend":
                var legend = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                    .FirstOrDefault(v => v.ViewType == ViewType.Legend && !v.IsTemplate);
                if (legend is null) { warning = "There is no legend in the project; create one first so it can be duplicated"; return null; }
                view = (View)doc.GetElement(legend.Duplicate(ViewDuplicateOption.WithDetailing));
                break;
            default:
                return null;
        }

        if (spec.Skala > 0 && view.ViewType != ViewType.ThreeD) view.Scale = spec.Skala;
        if (spec.ViewTemplate is { } tplName)
        {
            var tpl = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().FirstOrDefault(v => v.IsTemplate && v.Name == tplName);
            if (tpl is null) warning = Join(warning, $"View template '{tplName}' not found");
            else view.ViewTemplateId = tpl.Id;
        }
        SetUniqueName(view, title);
        return view;
    }

    internal static void SetUniqueName(View view, string title)
    {
        var name = new string(title.Where(c => !TitleRules.Prohibited.Contains(c)).ToArray()).Trim();
        if (name.Length == 0) return;
        for (var i = 1; i < 100; i++)
        {
            try { view.Name = i == 1 ? name : $"{name} ({i})"; return; }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { }
        }
    }

    private static ViewFamilyType TypeOf(Document doc, ViewFamily family) =>
        new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault(t => t.ViewFamily == family)
        ?? throw new InvalidOperationException($"View type {family} does not exist in the project");

    private static string Join(string? a, string b) => a is null ? b : a + "; " + b;
}
