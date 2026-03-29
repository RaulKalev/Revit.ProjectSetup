using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectSetup.Services.Revit
{
    public class CreatePlanSetsResult
    {
        public int Created                            { get; set; }
        public int Skipped                            { get; set; }
        public List<string> Messages                  { get; } = new List<string>();
        public List<(string Name, string Error)> Failed { get; } = new List<(string Name, string Error)>();
        public string ErrorMessage                    { get; set; }
    }

    public class PlanSetCategory
    {
        public string Name             { get; }
        public string ViewTemplateName { get; }
        public PlanSetCategory(string name, string viewTemplateName)
        {
            Name             = name;
            ViewTemplateName = viewTemplateName;
        }
    }

    /// <summary>
    /// Duplicates all base floor plan views (Kaust 1 == "Baas plaanid") for each
    /// requested category, appending "- {category}" to the name and assigning the
    /// corresponding view template. Existing views are skipped.
    /// </summary>
    public class CreatePlanSetsRequest : IExternalEventRequest
    {
        private readonly List<PlanSetCategory>         _categories;
        private readonly Action<CreatePlanSetsResult>  _onComplete;

        public CreatePlanSetsRequest(List<PlanSetCategory> categories, Action<CreatePlanSetsResult> onComplete)
        {
            _categories = categories;
            _onComplete = onComplete;
        }

        public void Execute(UIApplication app)
        {
            var result = new CreatePlanSetsResult();
            try
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    result.ErrorMessage = "Aktiivset dokumenti ei leitud.";
                    _onComplete?.Invoke(result);
                    return;
                }

                // Collect base views (non-template floor plans with Kaust 1 == "Baas plaanid")
                var baseViews = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate)
                    .Where(v =>
                    {
                        var p = v.LookupParameter("Kaust 1");
                        return p != null && p.AsString() == "Baas plaanid";
                    })
                    .OrderBy(v => v.Name)
                    .ToList();

                if (baseViews.Count == 0)
                {
                    result.ErrorMessage = "Baas plaane (Kaust 1 = 'Baas plaanid') ei leitud. Loo esmalt baas vaated (Samm 4).";
                    _onComplete?.Invoke(result);
                    return;
                }

                // Collect all existing non-template view names for fast skip-check
                var existingNames = new HashSet<string>(
                    new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewPlan))
                        .Cast<ViewPlan>()
                        .Where(v => !v.IsTemplate)
                        .Select(v => v.Name),
                    StringComparer.OrdinalIgnoreCase);

                // Pre-collect view templates: name -> ElementId
                var templateMap = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => v.IsTemplate)
                    .ToDictionary(v => v.Name, v => v.Id, StringComparer.OrdinalIgnoreCase);

                // Pre-collect Revit link instances to hide in view templates
                var revitLinkIds = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .ToElementIds()
                    .ToList();
                var modifiedTemplates = new HashSet<ElementId>();

                using var tg = new TransactionGroup(doc, "Create Plan Sets");
                tg.Start();

                foreach (var category in _categories)
                {
                    templateMap.TryGetValue(category.ViewTemplateName, out ElementId templateId);

                    foreach (var baseView in baseViews)
                    {
                        string newName = $"{baseView.Name} - {category.Name}";
                        try
                        {
                            if (existingNames.Contains(newName))
                            {
                                result.Skipped++;
                                result.Messages.Add($"=  {newName}  (olemas)");
                                continue;
                            }

                            using var tx = new Transaction(doc, $"Create plan set view: {newName}");
                            tx.Start();

                            var newId   = baseView.Duplicate(ViewDuplicateOption.WithDetailing);
                            var newView = doc.GetElement(newId) as ViewPlan;
                            if (newView == null)
                            {
                                tx.RollBack();
                                result.Failed.Add((newName, "Duplicate ei tagastanud ViewPlan elementi."));
                                continue;
                            }

                            newView.Name = newName;

                            var kaust1 = newView.LookupParameter("Kaust 1");
                            if (kaust1 != null && !kaust1.IsReadOnly)
                                kaust1.Set(category.Name);

                            if (templateId != null && templateId != ElementId.InvalidElementId)
                            {
                                newView.ViewTemplateId = templateId;

                                // Modify the view template to hide all linked Revit models (once per template)
                                if (revitLinkIds.Count > 0 && !modifiedTemplates.Contains(templateId))
                                {
                                    var templateView = doc.GetElement(templateId) as View;
                                    if (templateView != null)
                                    {
                                        var toHide = revitLinkIds
                                            .Where(id => { var e = doc.GetElement(id); return e != null && !e.IsHidden(templateView); })
                                            .ToList();
                                        if (toHide.Count > 0)
                                            templateView.HideElements(toHide);
                                        modifiedTemplates.Add(templateId);
                                        result.Messages.Add($"   \u2193 Vaate mall '{category.ViewTemplateName}': lingitud mudelid peidetud");
                                    }
                                }
                            }
                            else
                                result.Messages.Add($"   ↳ Hoiatus: vaate malli '{category.ViewTemplateName}' ei leitud");

                            tx.Commit();

                            existingNames.Add(newName);
                            result.Created++;
                            result.Messages.Add($"+  {newName}");
                        }
                        catch (Exception ex)
                        {
                            result.Failed.Add((newName, ex.Message));
                        }
                    }
                }

                tg.Assimilate();
                _onComplete?.Invoke(result);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Viga plaanide loomisel: {ex.Message}";
                _onComplete?.Invoke(result);
            }
        }
    }
}
