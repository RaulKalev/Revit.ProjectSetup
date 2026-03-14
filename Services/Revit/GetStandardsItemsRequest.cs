using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ProjectSetup.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectSetup.Services.Revit
{
    public class GetStandardsItemsRequest : IExternalEventRequest
    {
        private readonly string _category;
        private readonly string _sourceDocumentTitle;
        private readonly Action<List<StandardsItemDto>> _onComplete;

        public GetStandardsItemsRequest(string category, string sourceDocumentTitle, Action<List<StandardsItemDto>> onComplete)
        {
            _category            = category;
            _sourceDocumentTitle = sourceDocumentTitle;
            _onComplete          = onComplete;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                // Find the requested source document; fall back to active document
                Document doc = null;
                if (!string.IsNullOrEmpty(_sourceDocumentTitle))
                {
                    foreach (Document d in app.Application.Documents)
                    {
                        if (!d.IsFamilyDocument && d.Title == _sourceDocumentTitle)
                        {
                            doc = d;
                            break;
                        }
                    }
                }
                doc ??= app.ActiveUIDocument?.Document;

                if (doc == null)
                {
                    _onComplete?.Invoke(new List<StandardsItemDto>());
                    return;
                }

                var items = _category switch
                {
                    "Line Styles"     => GetLineStyles(doc),
                    "Fill Patterns"   => GetFillPatterns(doc),
                    "Text Types"      => GetTextTypes(doc),
                    "Dimension Types" => GetDimensionTypes(doc),
                    "Materials"       => GetMaterials(doc),
                    "Object Styles"   => GetObjectStyles(doc),
                    "Filters"         => GetFilters(doc),
                    "View Templates"  => GetViewTemplates(doc),
                    _                 => new List<StandardsItemDto>()
                };

                _onComplete?.Invoke(items);
            }
            catch (Exception ex)
            {
                _onComplete?.Invoke(new List<StandardsItemDto>
                {
                    new StandardsItemDto { Name = "Error loading items", Description = ex.Message }
                });
            }
        }

        private static List<StandardsItemDto> GetLineStyles(Document doc)
        {
            var result = new List<StandardsItemDto>();
            try
            {
                var linesCategory = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
                if (linesCategory?.SubCategories == null) return result;

                foreach (Category sub in linesCategory.SubCategories)
                {
                    int? weight = sub.GetLineWeight(GraphicsStyleType.Projection);
                    result.Add(new StandardsItemDto
                    {
                        Name        = sub.Name,
                        Description = $"Weight: {(weight.HasValue ? weight.Value.ToString() : "—")}"
                    });
                }
            }
            catch { /* return what we have */ }

            return result.OrderBy(x => x.Name).ToList();
        }

        private static List<StandardsItemDto> GetFillPatterns(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .Where(fp => fp.GetFillPattern() != null)
                .OrderBy(fp => fp.Name)
                .Select(fp =>
                {
                    var pat = fp.GetFillPattern();
                    string detail = pat.IsSolidFill ? "Solid Fill"
                                  : pat.Target == FillPatternTarget.Model ? "Model Pattern"
                                  : "Drafting Pattern";
                    return new StandardsItemDto { Name = fp.Name, Description = detail };
                })
                .ToList();
        }

        private static List<StandardsItemDto> GetTextTypes(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .OrderBy(t => t.Name)
                .Select(t =>
                {
                    double sizeInternal = t.get_Parameter(BuiltInParameter.TEXT_SIZE)?.AsDouble() ?? 0;
                    double sizeMm = sizeInternal * 304.8; // feet → mm
                    return new StandardsItemDto
                    {
                        Name        = t.Name,
                        Description = $"Size: {sizeMm:F1} mm"
                    };
                })
                .ToList();
        }

        private static List<StandardsItemDto> GetDimensionTypes(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .OrderBy(d => d.Name)
                .Select(d => new StandardsItemDto
                {
                    Name        = d.Name,
                    Description = d.StyleType.ToString()
                })
                .ToList();
        }

        private static List<StandardsItemDto> GetMaterials(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .OrderBy(m => m.Name)
                .Select(m => new StandardsItemDto
                {
                    Name        = m.Name,
                    Description = m.MaterialCategory ?? string.Empty
                })
                .ToList();
        }

        private static List<StandardsItemDto> GetObjectStyles(Document doc)
        {
            var result = new List<StandardsItemDto>();
            try
            {
                foreach (Category cat in doc.Settings.Categories)
                {
                    if (cat.CategoryType == CategoryType.Internal) continue;
                    result.Add(new StandardsItemDto
                    {
                        Name        = cat.Name,
                        Description = cat.CategoryType.ToString()
                    });
                }
            }
            catch { /* return what we have */ }

            return result.OrderBy(x => x.Name).ToList();
        }

        private static List<StandardsItemDto> GetFilters(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FilterElement))
                .Cast<FilterElement>()
                .OrderBy(f => f.Name)
                .Select(f => new StandardsItemDto
                {
                    Name        = f.Name,
                    Description = f is ParameterFilterElement ? "Parameter Filter" : "Selection Filter"
                })
                .ToList();
        }

        private static List<StandardsItemDto> GetViewTemplates(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.IsTemplate)
                .OrderBy(v => v.Name)
                .Select(v => new StandardsItemDto
                {
                    Name        = v.Name,
                    Description = v.ViewType.ToString()
                })
                .ToList();
        }
    }
}
