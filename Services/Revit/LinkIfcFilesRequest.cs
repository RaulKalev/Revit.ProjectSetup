using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ProjectSetup.Services.Revit
{
    /// <summary>Structured result returned to the UI after IFC linking completes.</summary>
    public class LinkIfcResult
    {
        public List<string> Linked { get; } = new List<string>();
        public List<(string Name, string Error)> Failed { get; } = new List<(string Name, string Error)>();
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Links the specified IFC files into the active Revit document, one at a time.
    /// Uses the Revit.IFC.Import.Importer class (via reflection) to perform the actual
    /// IFC-to-RVT conversion and linking — the same code path Revit uses internally.
    /// This bypasses RevitLinkType.CreateFromIFC which has unresolvable transaction conflicts.
    /// Pattern from github.com/Autodesk/revit-ifc#380 (simonhoeng + RichardWhitfield).
    /// </summary>
    public class LinkIfcFilesRequest : IExternalEventRequest
    {
        private readonly List<string> _paths;
        private readonly Action<LinkIfcResult> _onComplete;

        public LinkIfcFilesRequest(List<string> paths, Action<LinkIfcResult> onComplete)
        {
            _paths      = paths;
            _onComplete = onComplete;
        }

        public void Execute(UIApplication app)
        {
            var result = new LinkIfcResult();
            try
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    result.ErrorMessage = "No active document is open.";
                    _onComplete?.Invoke(result);
                    return;
                }

                // ── Locate Revit.IFC.Import assembly (loaded by Revit at startup) ────────
                Assembly ifcAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Revit.IFC.Import");

                if (ifcAssembly == null)
                {
                    // Fallback: try loading from Revit's application directory.
                    string revitDir = Path.GetDirectoryName(
                        typeof(Autodesk.Revit.ApplicationServices.Application).Assembly.Location);
                    string dllPath  = Path.Combine(revitDir, "Revit.IFC.Import.dll");
                    if (File.Exists(dllPath))
                        ifcAssembly = Assembly.LoadFrom(dllPath);
                }

                if (ifcAssembly == null)
                {
                    result.ErrorMessage = "Revit.IFC.Import assembly not found. Ensure 'IFC for Revit' add-in is installed.";
                    _onComplete?.Invoke(result);
                    return;
                }

                Type importerType = ifcAssembly.GetType("Revit.IFC.Import.Importer");
                if (importerType == null)
                {
                    result.ErrorMessage = "Importer class not found in Revit.IFC.Import assembly.";
                    _onComplete?.Invoke(result);
                    return;
                }

                // Importer.CreateImporter(Document, string, IDictionary<string, string>)
                MethodInfo createMethod = importerType.GetMethod("CreateImporter",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Document), typeof(string), typeof(IDictionary<string, string>) },
                    null);

                // Importer.ReferenceIFC(Document, string)
                MethodInfo referenceMethod = importerType.GetMethod("ReferenceIFC",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(Document), typeof(string) },
                    null);

                if (createMethod == null || referenceMethod == null)
                {
                    result.ErrorMessage = "Required Importer methods (CreateImporter / ReferenceIFC) not found.";
                    _onComplete?.Invoke(result);
                    return;
                }

                // ── Process each IFC file ────────────────────────────────────────────────
                using var tg = new TransactionGroup(doc, "Link IFC Files");
                tg.Start();

                foreach (string path in _paths)
                {
                    string displayName = Path.GetFileName(path);

                    // Remove any stale .ifc.rvt cache from a prior run so the importer
                    // does a full fresh conversion.
                    string rvtCachePath = path + ".rvt";
                    try { if (File.Exists(rvtCachePath)) File.Delete(rvtCachePath); } catch { }

                    try
                    {
                        // Snapshot existing instances so we can identify new ones later.
                        var existingIds = new HashSet<ElementId>(
                            new FilteredElementCollector(doc)
                                .OfClass(typeof(RevitLinkInstance))
                                .ToElementIds());

                        // Create the importer with Link + Reference options.
                        // These match the options Revit uses when you do Insert → Link IFC.
                        var options = new Dictionary<string, string>
                        {
                            { "Action", "Link" },
                            { "Intent", "Reference" }
                        };

                        object importer = createMethod.Invoke(null, new object[] { doc, path, options });
                        if (importer == null)
                        {
                            result.Failed.Add((displayName, "Importer.CreateImporter returned null."));
                            continue;
                        }

                        // ReferenceIFC converts the IFC, writes the .ifc.rvt cache, creates the
                        // RevitLinkType in the host document, and places a RevitLinkInstance.
                        // It manages its own internal transactions.
                        referenceMethod.Invoke(importer, new object[] { doc, path });

                        // Find any newly created instances and pin them.
                        var newInstances = new FilteredElementCollector(doc)
                            .OfClass(typeof(RevitLinkInstance))
                            .Where(e => !existingIds.Contains(e.Id))
                            .Cast<RevitLinkInstance>()
                            .ToList();

                        if (newInstances.Count > 0)
                        {
                            using var t = new Transaction(doc, $"Pin IFC Link: {displayName}");
                            t.Start();
                            foreach (var inst in newInstances)
                                inst.Pinned = true;
                            t.Commit();
                        }

                        result.Linked.Add(displayName);
                    }
                    catch (TargetInvocationException tex)
                    {
                        var inner = tex.InnerException ?? tex;
                        result.Failed.Add((displayName, $"[{inner.GetType().Name}] {inner.Message}"));
                    }
                    catch (Exception ex)
                    {
                        result.Failed.Add((displayName, $"[{ex.GetType().Name}] {ex.Message}"));
                    }
                }

                tg.Assimilate();
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"[{ex.GetType().Name}] {ex.Message}";
            }
            finally
            {
                _onComplete?.Invoke(result);
            }
        }
    }
}
