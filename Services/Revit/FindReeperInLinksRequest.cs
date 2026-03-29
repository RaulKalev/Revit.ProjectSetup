using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ProjectSetup.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectSetup.Services.Revit
{
    public class FindReeperResult
    {
        public List<ReeperItemDto> Items        { get; } = new List<ReeperItemDto>();
        public string              ErrorMessage { get; set; }
    }

    /// <summary>
    /// Searches every loaded RevitLinkInstance for generic model elements
    /// (FamilyInstance or DirectShape) whose name or family name contains
    /// "reeper" (case-insensitive). Returns world position and the "X,Y,Z"
    /// length parameter value.
    /// </summary>
    public class FindReeperInLinksRequest : IExternalEventRequest
    {
        private readonly Action<FindReeperResult> _onComplete;

        public FindReeperInLinksRequest(Action<FindReeperResult> onComplete)
            => _onComplete = onComplete;

        public void Execute(UIApplication app)
        {
            var result = new FindReeperResult();
            try
            {
                var activeDoc = app.ActiveUIDocument?.Document;
                if (activeDoc == null)
                {
                    result.ErrorMessage = "Aktiivset dokumenti ei leitud.";
                    _onComplete?.Invoke(result);
                    return;
                }

                var links = new FilteredElementCollector(activeDoc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .ToList();

                foreach (var link in links)
                {
                    var linkDoc = link.GetLinkDocument();
                    if (linkDoc == null) continue;

                    var transform = link.GetTotalTransform();
                    var linkTitle = linkDoc.Title;

                    // Collect ALL generic model elements (FamilyInstance, DirectShape, etc.)
                    var elements = new FilteredElementCollector(linkDoc)
                        .OfCategory(BuiltInCategory.OST_GenericModel)
                        .WhereElementIsNotElementType()
                        .Cast<Element>()
                        .Where(e =>
                        {
                            // Check element name (covers DirectShape, etc.)
                            var eName = e.Name ?? string.Empty;
                            if (eName.IndexOf("reeper", StringComparison.OrdinalIgnoreCase) >= 0)
                                return true;
                            // Also check family name for FamilyInstance
                            if (e is FamilyInstance fi)
                            {
                                var famName = fi.Symbol?.Family?.Name ?? string.Empty;
                                return famName.IndexOf("reeper", StringComparison.OrdinalIgnoreCase) >= 0;
                            }
                            return false;
                        })
                        .ToList();

                    foreach (var elem in elements)
                    {
                        // Resolve display names
                        string familyName, typeName;
                        if (elem is FamilyInstance fi)
                        {
                            familyName = fi.Symbol?.Family?.Name ?? string.Empty;
                            typeName   = fi.Symbol?.Name          ?? string.Empty;
                        }
                        else
                        {
                            familyName = "Direct Shape";
                            typeName   = elem.Name ?? string.Empty;
                        }

                        // Get bounding box once — used for both size and location fallback
                        BoundingBoxXYZ elemBB = elem.get_BoundingBox(null);

                        // Read "X,Y,Z" parameter (internal units = feet → convert to mm)
                        double sizeMm = 0;
                        var xyzParam  = elem.LookupParameter("X,Y,Z");
                        if (xyzParam != null && xyzParam.HasValue &&
                            xyzParam.StorageType == StorageType.Double)
                        {
                            sizeMm = xyzParam.AsDouble() * 304.8;
                        }
                        else if (elemBB != null)
                        {
                            // Use the X dimension of the bounding box as the reeper edge length
                            // (reeper is a cube — X = Y = Z; X is most reliable for plan size)
                            var sz = elemBB.Max - elemBB.Min;
                            sizeMm = Math.Abs(sz.X) * 304.8;
                            if (sizeMm < 1.0)
                                sizeMm = Math.Max(Math.Abs(sz.Y), Math.Abs(sz.Z)) * 304.8;
                        }

                        // World location: prefer LocationPoint, fall back to bounding box MIN corner
                        // (xx_REEPER_v01 has its insertion point at the corner, not the center)
                        double wx = 0, wy = 0, wz = 0;
                        if (elem.Location is LocationPoint lp)
                        {
                            var worldPt = transform.OfPoint(lp.Point);
                            wx = worldPt.X;
                            wy = worldPt.Y;
                            wz = worldPt.Z;
                        }
                        else if (elemBB != null)
                        {
                            var worldPt = transform.OfPoint(elemBB.Min);
                            wx = worldPt.X;
                            wy = worldPt.Y;
                            wz = worldPt.Z;
                        }

                        result.Items.Add(new ReeperItemDto
                        {
                            LinkTitle  = linkTitle,
                            FamilyName = familyName,
                            TypeName   = typeName,
#if NET8_0_OR_GREATER
                            ElementId  = elem.Id.Value,
#else
                            ElementId  = (long)elem.Id.IntegerValue,
#endif
                            SizeMm     = sizeMm,
                            LocationX  = wx,
                            LocationY  = wy,
                            LocationZ  = wz
                        });
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
