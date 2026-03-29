using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectSetup.Services.Revit
{
    public class CreateBaseViewsResult
    {
        public int Created                { get; set; }
        public int Renamed                { get; set; }
        public List<string> Messages     { get; } = new List<string>();
        public string ErrorMessage        { get; set; }
    }

    /// <summary>
    /// For each level in the active document (sorted bottom→top by elevation), creates a
    /// Floor Plan view named "{index:D2}_{levelName}" and sets the "Kaust 1" instance
    /// parameter to "Baas plaanid". If a floor plan already exists for that level it is
    /// renamed instead of duplicated.
    /// </summary>
    public class CreateBaseViewsRequest : IExternalEventRequest
    {
        private readonly Action<CreateBaseViewsResult> _onComplete;

        public CreateBaseViewsRequest(Action<CreateBaseViewsResult> onComplete)
            => _onComplete = onComplete;

        public void Execute(UIApplication app)
        {
            var result = new CreateBaseViewsResult();
            try
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    result.ErrorMessage = "Aktiivset dokumenti ei leitud.";
                    _onComplete?.Invoke(result);
                    return;
                }

                // Levels sorted by elevation ascending (lowest = index 0)
                var levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                if (levels.Count == 0)
                {
                    result.ErrorMessage = "Aktiivsest dokumendist ei leitud ühtegi tasandit.";
                    _onComplete?.Invoke(result);
                    return;
                }

                // Find the default Floor Plan ViewFamilyType
                var floorPlanType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.FloorPlan);

                if (floorPlanType == null)
                {
                    result.ErrorMessage = "Projektist ei leitud Floor Plan vaate perekonna tüüpi.";
                    _onComplete?.Invoke(result);
                    return;
                }

                // Collect all existing non-template floor plan views
                var existingFloorPlans = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate)
                    .ToList();

                using var tx = new Transaction(doc, "Create Base Views");
                tx.Start();

                for (int i = 0; i < levels.Count; i++)
                {
                    var level      = levels[i];
                    var targetName = $"{i:D2}_{level.Name}";

                    // Use the first existing floor plan for this level, if any
                    var existing = existingFloorPlans.FirstOrDefault(v => v.GenLevel?.Id == level.Id);

                    ViewPlan view;
                    if (existing != null)
                    {
                        view = existing;
                        var oldName = view.Name;
                        if (oldName != targetName)
                        {
                            view.Name = targetName;
                            result.Renamed++;
                            result.Messages.Add($"↺  {oldName}  →  {targetName}");
                        }
                        else
                        {
                            result.Messages.Add($"=  {targetName}  (muutmata)");
                        }
                    }
                    else
                    {
                        view = ViewPlan.Create(doc, floorPlanType.Id, level.Id);
                        view.Name = targetName;
                        result.Created++;
                        result.Messages.Add($"+  {targetName}");
                    }

                    // Set "Kaust 1" instance parameter
                    var kaust1 = view.LookupParameter("Kaust 1");
                    if (kaust1 != null && !kaust1.IsReadOnly)
                        kaust1.Set("Baas plaanid");
                    else
                        result.Messages.Add($"   ↳ Hoiatus: 'Kaust 1' ei leitud vaatel '{targetName}'");
                }

                tx.Commit();
                _onComplete?.Invoke(result);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Viga vaadete loomisel: {ex.Message}";
                _onComplete?.Invoke(result);
            }
        }
    }
}
