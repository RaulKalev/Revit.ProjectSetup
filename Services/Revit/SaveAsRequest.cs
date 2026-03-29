using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace ProjectSetup.Services.Revit
{
    /// <summary>
    /// Saves the active document to a new path chosen by the user (Save As).
    /// Must be called with a pre-resolved file path (chosen on the UI thread via SaveFileDialog).
    /// </summary>
    public class SaveAsRequest : IExternalEventRequest
    {
        private readonly string _path;
        private readonly Action<string> _onComplete;

        public SaveAsRequest(string path, Action<string> onComplete)
        {
            _path = path;
            _onComplete = onComplete;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null) { _onComplete?.Invoke("No active document."); return; }

                var options = new SaveAsOptions
                {
                    OverwriteExistingFile = true,
                    MaximumBackups = 3
                };

                doc.SaveAs(_path, options);
                _onComplete?.Invoke($"Saved: {_path}");
            }
            catch (Exception ex)
            {
                _onComplete?.Invoke($"Save failed: {ex.Message}");
            }
        }
    }
}
