using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ProjectSetup.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectSetup.Services.Revit
{
    /// <summary>
    /// Reads all loadable (non-in-place) families from the specified source document.
    /// </summary>
    public class GetFamiliesRequest : IExternalEventRequest
    {
        private readonly string _sourceDocumentTitle;
        private readonly Action<List<FamilyItemDto>> _onComplete;

        public GetFamiliesRequest(string sourceDocumentTitle, Action<List<FamilyItemDto>> onComplete)
        {
            _sourceDocumentTitle = sourceDocumentTitle;
            _onComplete          = onComplete;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = FindDocument(app, _sourceDocumentTitle);
                if (doc == null)
                {
                    _onComplete?.Invoke(new List<FamilyItemDto>());
                    return;
                }

                var families = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .Where(f => f.FamilyCategory != null && !f.IsInPlace)
                    .OrderBy(f => f.FamilyCategory.Name)
                    .ThenBy(f => f.Name)
                    .Select(f => new FamilyItemDto
                    {
                        UniqueId  = f.UniqueId,
                        Name      = f.Name,
                        Category  = f.FamilyCategory.Name,
                        TypeCount = f.GetFamilySymbolIds().Count
                    })
                    .ToList();

                _onComplete?.Invoke(families);
            }
            catch (Exception ex)
            {
                _onComplete?.Invoke(new List<FamilyItemDto>
                {
                    new FamilyItemDto { Name = ex.Message, Category = "Error", TypeCount = 0, UniqueId = string.Empty }
                });
            }
        }

        internal static Document FindDocument(UIApplication app, string title)
        {
            foreach (Document d in app.Application.Documents)
                if (!d.IsFamilyDocument && d.Title == title)
                    return d;
            return null;
        }
    }
}
