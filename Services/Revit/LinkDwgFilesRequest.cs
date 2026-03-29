using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;

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
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    result.ErrorMessage = "Aktiivset dokumenti ei leitud.";
                    _onComplete?.Invoke(result);
                    return;
                }

                var options = new DWGImportOptions
                {
                    Placement         = ImportPlacement.Origin,
                    ColorMode         = ImportColorMode.Preserved,
                    Unit              = ImportUnit.Default,
                    OrientToView      = false,
                    VisibleLayersOnly = false,
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
