using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ProjectSetup.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectSetup.Services.Revit
{
    /// <summary>
    /// Reads levels from the specified linked document and from the active document,
    /// then pairs them by elevation order to produce a preview list.
    /// </summary>
    public class GetLevelsFromLinkRequest : IExternalEventRequest
    {
        private readonly string _linkTitle;
        private readonly Action<List<LevelPreviewDto>> _onComplete;

        public GetLevelsFromLinkRequest(string linkTitle, Action<List<LevelPreviewDto>> onComplete)
        {
            _linkTitle  = linkTitle;
            _onComplete = onComplete;
        }

        public void Execute(UIApplication app)
        {
            var result = new List<LevelPreviewDto>();
            try
            {
                var activeDoc = app.ActiveUIDocument?.Document;
                if (activeDoc == null) { _onComplete?.Invoke(result); return; }

                // Find the link document by title
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
                if (linkDoc == null) { _onComplete?.Invoke(result); return; }

                // Levels from the linked document, sorted by elevation
                var linkLevels = new FilteredElementCollector(linkDoc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                // Existing levels in the active document, sorted by elevation
                var activeLevels = new FilteredElementCollector(activeDoc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                // Pair by index: existing levels get reused (renamed+repositioned), extras get deleted
                for (int i = 0; i < linkLevels.Count; i++)
                {
                    var ll = linkLevels[i];
                    double elevMm = ll.Elevation * 304.8;
                    if (i < activeLevels.Count)
                    {
                        result.Add(new LevelPreviewDto
                        {
                            Name            = ll.Name,
                            ElevationMm     = elevMm,
                            Action          = LevelAction.RenameExisting
                        });
                    }
                    else
                    {
                        result.Add(new LevelPreviewDto
                        {
                            Name            = ll.Name,
                            ElevationMm     = elevMm,
                            Action          = LevelAction.CreateNew
                        });
                    }
                }

                // Active levels beyond the IFC count will be deleted
                for (int i = linkLevels.Count; i < activeLevels.Count; i++)
                {
                    result.Add(new LevelPreviewDto
                    {
                        Name        = activeLevels[i].Name,
                        ElevationMm = activeLevels[i].Elevation * 304.8,
                        Action      = LevelAction.DeleteExisting
                    });
                }
            }
            catch (Exception ex)
            {
                result.Add(new LevelPreviewDto
                {
                    Name   = $"Error: {ex.Message}",
                    Action = LevelAction.CreateNew
                });
            }

            _onComplete?.Invoke(result);
        }
    }
}
