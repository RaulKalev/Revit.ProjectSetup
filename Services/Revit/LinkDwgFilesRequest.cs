using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ProjectSetup.Services.Revit
{
    public class DwgLinkMapping
    {
        public string DwgPath { get; }
        public long   ViewId  { get; }
        public DwgLinkMapping(string dwgPath, long viewId) { DwgPath = dwgPath; ViewId = viewId; }
    }

    public class LinkDwgResult
    {
        public List<string>                      Linked  { get; } = new List<string>();
        public List<(string Name, string Error)> Failed  { get; } = new List<(string Name, string Error)>();
        public string                            ErrorMessage { get; set; }
    }

    /// <summary>
    /// Links each DWG file to its mapped floor plan view (current view only) and pins
    /// the resulting import instance.
    /// </summary>
    public class LinkDwgFilesRequest : IExternalEventRequest
    {
        private readonly List<DwgLinkMapping>    _mappings;
        private readonly Action<LinkDwgResult>   _onComplete;

        public LinkDwgFilesRequest(List<DwgLinkMapping> mappings, Action<LinkDwgResult> onComplete)
        {
            _mappings   = mappings;
            _onComplete = onComplete;
        }

        public void Execute(UIApplication app)
        {
            var result = new LinkDwgResult();
            try
            {
                var uidoc = app.ActiveUIDocument;
                var doc   = uidoc?.Document;
                if (doc == null)
                {
                    result.ErrorMessage = "Aktiivset dokumenti ei leitud.";
                    _onComplete?.Invoke(result);
                    return;
                }

                // Remember the original active view so we can restore it afterwards.
                var originalView = uidoc.ActiveView;
                var openedViewIds = new List<ElementId>();

                var options = new DWGImportOptions
                {
                    Placement         = ImportPlacement.Origin,
                    ColorMode         = ImportColorMode.Preserved,
                    Unit              = ImportUnit.Default,
                    OrientToView      = false,
                    VisibleLayersOnly = false,
                    ThisViewOnly      = true,
                };

                using var tg = new TransactionGroup(doc, "Link DWG Files");
                tg.Start();

                foreach (var mapping in _mappings)
                {
                    string displayName = Path.GetFileName(mapping.DwgPath);
                    try
                    {
                        var targetView = doc.GetElement(new ElementId(mapping.ViewId)) as View;
                        if (targetView == null)
                        {
                            result.Failed.Add((displayName, "Vaade ei leitud."));
                            continue;
                        }

                        // Switch the active view to the target floor plan before linking.
                        // Revit requires the target view to be active for "current view only"
                        // to take effect — passing the view parameter alone is not sufficient.
                        if (uidoc.ActiveView?.Id != targetView.Id)
                        {
                            openedViewIds.Add(targetView.Id);
                            uidoc.ActiveView = targetView;
                        }

                        using var tx = new Transaction(doc, $"Link DWG: {displayName}");
                        tx.Start();

                        doc.Link(mapping.DwgPath, options, targetView, out ElementId importedId);

                        if (importedId == ElementId.InvalidElementId)
                        {
                            tx.RollBack();
                            result.Failed.Add((displayName, "Link tagastas vigase elemendi."));
                            continue;
                        }

                        if (doc.GetElement(importedId) is ImportInstance inst && !inst.Pinned)
                            inst.Pinned = true;

                        tx.Commit();
                        result.Linked.Add(displayName);
                    }
                    catch (Exception ex)
                    {
                        result.Failed.Add((displayName, ex.Message));
                    }
                }

                tg.Assimilate();

                // Restore the original active view, then close any floor plan tabs opened during linking.
                try
                {
                    if (originalView != null) uidoc.ActiveView = originalView;
                    if (openedViewIds.Count > 0)
                    {
                        // CloseViewsAndMoveToView is available in Revit 2024+.
                        // We invoke via reflection so this compiles against older SDK stubs.
                        var closeMethod = typeof(UIDocument)
                            .GetMethods()
                            .FirstOrDefault(m => m.Name == "CloseViewsAndMoveToView");
                        closeMethod?.Invoke(uidoc, new object[] { openedViewIds, originalView });
                    }
                }
                catch { /* ignore if views are no longer valid */ }

                _onComplete?.Invoke(result);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Viga DWG linkimisel: {ex.Message}";
                _onComplete?.Invoke(result);
            }
        }
    }
}
