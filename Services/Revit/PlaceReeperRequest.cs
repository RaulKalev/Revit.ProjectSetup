using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using ProjectSetup.Models;
using System;
using System.Linq;

namespace ProjectSetup.Services.Revit
{
    public class PlaceReeperResult
    {
        public bool   Success      { get; set; }
        public string ErrorMessage { get; set; }
        public string Message      { get; set; }
    }

    /// <summary>
    /// Places a family instance of "xx_REEPER_v01" using the type
    /// "{discipline}_REEPER_v01" (EL or EN) at the world location
    /// of the source reeper element, and sets the "X,Y,Z" parameter.
    /// </summary>
    public class PlaceReeperRequest : IExternalEventRequest
    {
        private readonly ReeperItemDto             _source;
        private readonly string                    _discipline;
        private readonly Action<PlaceReeperResult> _onComplete;

        public PlaceReeperRequest(
            ReeperItemDto             source,
            string                    discipline,
            Action<PlaceReeperResult> onComplete)
        {
            _source     = source;
            _discipline = discipline;
            _onComplete = onComplete;
        }

        public void Execute(UIApplication app)
        {
            var result = new PlaceReeperResult();
            try
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    result.ErrorMessage = "Aktiivset dokumenti ei leitud.";
                    _onComplete?.Invoke(result);
                    return;
                }

                // Locate the family in the active document
                var family = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .FirstOrDefault(f => f.Name == "xx_REEPER_v01");

                if (family == null)
                {
                    result.ErrorMessage =
                        "Perekond 'xx_REEPER_v01' ei leitud aktiivses dokumendis. " +
                        "Laadi perekond esmalt.";
                    _onComplete?.Invoke(result);
                    return;
                }

                // Locate the requested type
                string       typeName = $"{_discipline}_REEPER_v01";
                FamilySymbol symbol   = null;
                foreach (var symId in family.GetFamilySymbolIds())
                {
                    if (doc.GetElement(symId) is FamilySymbol s && s.Name == typeName)
                    {
                        symbol = s;
                        break;
                    }
                }

                if (symbol == null)
                {
                    result.ErrorMessage =
                        $"Tüüp '{typeName}' ei leitud perekonnas 'xx_REEPER_v01'.";
                    _onComplete?.Invoke(result);
                    return;
                }

                // Activate symbol if required
                if (!symbol.IsActive)
                {
                    using var actTx = new Transaction(doc, "Activate Symbol");
                    actTx.Start();
                    symbol.Activate();
                    actTx.Commit();
                    doc.Regenerate();
                }

                var location = new XYZ(_source.LocationX, _source.LocationY, _source.LocationZ);

                using var tx = new Transaction(doc, "Paigalda Reeper");
                tx.Start();

                var instance = doc.Create.NewFamilyInstance(
                    location, symbol, StructuralType.NonStructural);

                // Set X,Y,Z size — it is a type parameter so it lives on the FamilySymbol
                if (_source.SizeMm > 0)
                {
                    var xyzParam = symbol.LookupParameter("X,Y,Z");
                    if (xyzParam != null && !xyzParam.IsReadOnly)
                        xyzParam.Set(_source.SizeMm / 304.8);
                }

                tx.Commit();

                result.Success = true;
                result.Message = $"{typeName} paigaldatud ({_source.SizeMm:F0} mm).";
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }

            _onComplete?.Invoke(result);
        }
    }
}
