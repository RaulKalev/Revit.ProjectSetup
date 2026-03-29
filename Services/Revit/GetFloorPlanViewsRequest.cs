using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ProjectSetup.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectSetup.Services.Revit
{
    /// <summary>
    /// Returns the list of non-template Floor Plan views in the active document,
    /// sorted by name, so the DWG mapping UI can populate its dropdowns.
    /// </summary>
    public class GetFloorPlanViewsRequest : IExternalEventRequest
    {
        private readonly Action<List<FloorPlanViewInfo>> _callback;

        public GetFloorPlanViewsRequest(Action<List<FloorPlanViewInfo>> callback)
            => _callback = callback;

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null) { _callback?.Invoke(new List<FloorPlanViewInfo>()); return; }

                var views = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate)
                    .OrderBy(v => v.Name)
                    .Select(v => new FloorPlanViewInfo(v.Name, v.Id.Value))
                    .ToList();

                _callback?.Invoke(views);
            }
            catch
            {
                _callback?.Invoke(new List<FloorPlanViewInfo>());
            }
        }
    }
}
