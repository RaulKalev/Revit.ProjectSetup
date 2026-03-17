using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectSetup.Services.Revit
{
    public class CreateLevelsResult
    {
        public int         Renamed  { get; set; }
        public int         Created  { get; set; }
        public int         Deleted  { get; set; }
        public int         Skipped  { get; set; }
        public List<string> Messages { get; } = new List<string>();
        public string      ErrorMessage { get; set; }
    }

    /// <summary>
    /// Creates levels in the active document based on levels from the specified linked model.
    ///
    /// Strategy:
    ///   1. Sort IFC link levels by elevation ascending.
    ///   2. Sort existing active-doc levels by elevation ascending.
    ///   3. Pair them 1:1 by index.
    ///   4. Pass 1 (temp names): rename all existing levels to a temp GUID name to avoid
    ///      name-collision errors when the final names are applied.
    ///   5. Pass 2 (real names + elevations): rename + reposition the paired levels.
    ///   6. Create new Level elements for IFC levels beyond the existing count.
    ///   7. Delete unpaired leftover active-doc levels (skip + report if Revit refuses).
    /// </summary>
    public class CreateLevelsRequest : IExternalEventRequest
    {
        private readonly string _linkTitle;
        private readonly Action<CreateLevelsResult> _onComplete;

        public CreateLevelsRequest(string linkTitle, Action<CreateLevelsResult> onComplete)
        {
            _linkTitle  = linkTitle;
            _onComplete = onComplete;
        }

        public void Execute(UIApplication app)
        {
            var result = new CreateLevelsResult();
            try
            {
                var activeDoc = app.ActiveUIDocument?.Document;
                if (activeDoc == null)
                {
                    result.ErrorMessage = "No active document is open.";
                    _onComplete?.Invoke(result);
                    return;
                }

                // Find the link document
                Document linkDoc = null;
                foreach (RevitLinkInstance link in new FilteredElementCollector(activeDoc)
                    .OfClass(typeof(RevitLinkInstance)))
                {
                    var ld = link.GetLinkDocument();
                    if (ld != null && ld.Title == _linkTitle)
                    {
                        linkDoc = ld;
                        break;
                    }
                }
                if (linkDoc == null)
                {
                    result.ErrorMessage = $"Linked model '{_linkTitle}' is no longer loaded.";
                    _onComplete?.Invoke(result);
                    return;
                }

                var linkLevels = new FilteredElementCollector(linkDoc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                if (linkLevels.Count == 0)
                {
                    result.ErrorMessage = "The linked model contains no levels.";
                    _onComplete?.Invoke(result);
                    return;
                }

                var activeLevels = new FilteredElementCollector(activeDoc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                int pairCount  = Math.Min(linkLevels.Count, activeLevels.Count);
                var toDelete   = activeLevels.Skip(pairCount).ToList();

                using var t = new Transaction(activeDoc, "Create Levels from Link");
                t.Start();

                // Pass 1: rename all existing levels to unique temp names to free up all
                //         real names before we start assigning them.
                var tempNames = new Dictionary<ElementId, string>();
                foreach (var al in activeLevels)
                {
                    string tmp = $"__tmp_{Guid.NewGuid():N}";
                    al.Name   = tmp;
                    tempNames[al.Id] = tmp;
                }

                // Pass 2: rename + reposition paired levels
                for (int i = 0; i < pairCount; i++)
                {
                    var al = activeLevels[i];
                    var ll = linkLevels[i];
                    al.Elevation = ll.Elevation;
                    al.Name      = ll.Name;
                    result.Renamed++;
                }

                // Create new levels for IFC levels beyond the existing count
                for (int i = pairCount; i < linkLevels.Count; i++)
                {
                    var ll = linkLevels[i];
                    var newLevel = Level.Create(activeDoc, ll.Elevation);
                    newLevel.Name = ll.Name;
                    result.Created++;
                }

                t.Commit();

                // Delete leftover active levels in a separate transaction
                // (separate so a failed delete does not roll back the renames/creates)
                foreach (var al in toDelete)
                {
                    try
                    {
                        using var td = new Transaction(activeDoc, "Delete Extra Level");
                        td.Start();
                        activeDoc.Delete(al.Id);
                        td.Commit();
                        result.Deleted++;
                    }
                    catch (Exception ex)
                    {
                        result.Skipped++;
                        result.Messages.Add($"Could not delete '{al.Name}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }

            _onComplete?.Invoke(result);
        }
    }
}
