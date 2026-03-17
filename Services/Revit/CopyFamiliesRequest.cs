using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;

namespace ProjectSetup.Services.Revit
{
    /// <summary>Structured result returned to the UI after LoadFamily completes.</summary>
    public class ImportFamiliesResult
    {
        public List<string> Imported { get; } = new List<string>();
        public List<string> Skipped  { get; } = new List<string>();
        public List<string> Failed   { get; } = new List<string>();
        public string ErrorMessage   { get; set; }
    }

    /// <summary>
    /// Loads the specified family definitions (by UniqueId) from the source document
    /// into the active Revit document, reporting per-family results back to the caller.
    /// </summary>
    public class CopyFamiliesRequest : IExternalEventRequest
    {
        private readonly string                       _sourceDocumentTitle;
        private readonly List<string>                 _uniqueIds;
        private readonly Action<ImportFamiliesResult> _onComplete;

        public CopyFamiliesRequest(
            string sourceDocumentTitle,
            List<string> uniqueIds,
            Action<ImportFamiliesResult> onComplete)
        {
            _sourceDocumentTitle = sourceDocumentTitle;
            _uniqueIds           = uniqueIds;
            _onComplete          = onComplete;
        }

        public void Execute(UIApplication app)
        {
            var tempDirs = new List<string>();
            var result   = new ImportFamiliesResult();
            try
            {
                var targetDoc = app.ActiveUIDocument?.Document;
                if (targetDoc == null)
                {
                    result.ErrorMessage = "No active document open.";
                    _onComplete?.Invoke(result);
                    return;
                }

                var sourceDoc = GetFamiliesRequest.FindDocument(app, _sourceDocumentTitle);
                if (sourceDoc == null)
                {
                    result.ErrorMessage = $"Source document '{_sourceDocumentTitle}' is no longer open.";
                    _onComplete?.Invoke(result);
                    return;
                }

                if (sourceDoc.Equals(targetDoc))
                {
                    result.ErrorMessage = "Source and active document are the same.";
                    _onComplete?.Invoke(result);
                    return;
                }

                // Pass 1: export each family to a unique temp .rfa (filename == family name)
                var pendingLoads = new List<(string tempPath, string name)>();

                foreach (var uid in _uniqueIds)
                {
                    var family = sourceDoc.GetElement(uid) as Family;
                    if (family == null) { result.Failed.Add(uid); continue; }

                    Document familyDoc = null;
                    try
                    {
                        familyDoc = sourceDoc.EditFamily(family);
                        if (familyDoc == null) { result.Failed.Add(family.Name); continue; }

                        string safeName = string.Concat(family.Name.Split(Path.GetInvalidFileNameChars()));
                        string tempDir  = Path.Combine(Path.GetTempPath(), $"_ps_{Guid.NewGuid():N}");
                        Directory.CreateDirectory(tempDir);
                        string tempPath = Path.Combine(tempDir, $"{safeName}.rfa");

                        familyDoc.SaveAs(tempPath, new SaveAsOptions { OverwriteExistingFile = true });
                        familyDoc.Close(false);
                        familyDoc = null;

                        tempDirs.Add(tempDir);
                        pendingLoads.Add((tempPath, family.Name));
                    }
                    catch (Exception)
                    {
                        familyDoc?.Close(false);
                        result.Failed.Add(family?.Name ?? uid);
                    }
                }

                if (pendingLoads.Count == 0)
                {
                    result.ErrorMessage = "No families could be prepared for import.";
                    _onComplete?.Invoke(result);
                    return;
                }

                // Pass 2: load each temp .rfa into the target document
                var loadOptions = new OverwriteLoadOptions();

                using var t = new Transaction(targetDoc, "Load Families");
                t.Start();
                try
                {
                    foreach (var (tempPath, name) in pendingLoads)
                    {
                        try
                        {
                            bool loaded = targetDoc.LoadFamily(tempPath, loadOptions, out _);
                            if (loaded) result.Imported.Add(name);
                            else        result.Skipped.Add(name);
                        }
                        catch
                        {
                            result.Failed.Add(name);
                        }
                    }
                    t.Commit();
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    result.ErrorMessage = $"Transaction error: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Unexpected error: {ex.Message}";
            }
            finally
            {
                foreach (var d in tempDirs)
                    try { Directory.Delete(d, true); } catch { }

                _onComplete?.Invoke(result);
            }
        }

        private sealed class OverwriteLoadOptions : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = false;
                return true;
            }

            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse,
                out FamilySource source, out bool overwriteParameterValues)
            {
                source = FamilySource.Family;
                overwriteParameterValues = false;
                return true;
            }
        }
    }
}
