using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectSetup.Services.Revit
{
    /// <summary>
    /// Checks which of the requested family names already exist in the active document.
    /// Must run on the Revit API thread via the external-event service.
    /// Returns a set of family names (not UniqueIds) that are already loaded.
    /// </summary>
    public class CheckFamiliesRequest : IExternalEventRequest
    {
        private readonly string              _sourceDocumentTitle;
        private readonly List<string>        _uniqueIds;
        private readonly Action<HashSet<string>> _onComplete;

        public CheckFamiliesRequest(
            string sourceDocumentTitle,
            List<string> uniqueIds,
            Action<HashSet<string>> onComplete)
        {
            _sourceDocumentTitle = sourceDocumentTitle;
            _uniqueIds           = uniqueIds;
            _onComplete          = onComplete;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var targetDoc = app.ActiveUIDocument?.Document;
                if (targetDoc == null) { _onComplete?.Invoke(new HashSet<string>()); return; }

                var sourceDoc = GetFamiliesRequest.FindDocument(app, _sourceDocumentTitle);
                if (sourceDoc == null) { _onComplete?.Invoke(new HashSet<string>()); return; }

                // Collect family names already in the target document
                var existingNames = new FilteredElementCollector(targetDoc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .Select(f => f.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Return only the names that clash
                var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var uid in _uniqueIds)
                {
                    if (sourceDoc.GetElement(uid) is Family f && existingNames.Contains(f.Name))
                        duplicates.Add(f.Name);
                }

                _onComplete?.Invoke(duplicates);
            }
            catch
            {
                _onComplete?.Invoke(new HashSet<string>());
            }
        }
    }
}
