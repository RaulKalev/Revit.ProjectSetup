using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;

namespace ProjectSetup.Services.Revit
{
    /// <summary>
    /// Returns the titles of all loaded RevitLinkInstance documents in the active document.
    /// </summary>
    public class GetLinkedModelsRequest : IExternalEventRequest
    {
        private readonly Action<List<string>> _onComplete;

        public GetLinkedModelsRequest(Action<List<string>> onComplete)
        {
            _onComplete = onComplete;
        }

        public void Execute(UIApplication app)
        {
            var result = new List<string>();
            try
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null) { _onComplete?.Invoke(result); return; }

                foreach (RevitLinkInstance link in new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance)))
                {
                    var linkDoc = link.GetLinkDocument();
                    if (linkDoc != null)
                        result.Add(linkDoc.Title);
                }
            }
            catch { /* return what we have */ }

            _onComplete?.Invoke(result);
        }
    }
}
