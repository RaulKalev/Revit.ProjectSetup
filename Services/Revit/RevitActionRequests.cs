using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;

namespace ProjectSetup.Services.Revit
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Project Information
    // ─────────────────────────────────────────────────────────────────────────
    public class ProjectInformationRequest : IExternalEventRequest
    {
        private readonly Action<string> _onComplete;
        public ProjectInformationRequest(Action<string> onComplete) => _onComplete = onComplete;

        public void Execute(UIApplication app)
        {
            try
            {
                var cmdId = RevitCommandId.LookupCommandId("ID_SETTINGS_PROJECT_INFORMATION");
                if (cmdId != null && app.CanPostCommand(cmdId))
                {
                    app.PostCommand(cmdId);
                    _onComplete?.Invoke("Project Information dialog opened.");
                }
                else
                {
                    _onComplete?.Invoke("Navigate to Manage → Project Information in Revit.");
                }
            }
            catch (Exception ex)
            {
                _onComplete?.Invoke($"Error: {ex.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Purge Unused
    // ─────────────────────────────────────────────────────────────────────────
    public class PurgeUnusedRequest : IExternalEventRequest
    {
        private readonly Action<string> _onComplete;
        public PurgeUnusedRequest(Action<string> onComplete) => _onComplete = onComplete;

        public void Execute(UIApplication app)
        {
            try
            {
                var cmdId = RevitCommandId.LookupCommandId("ID_PURGE_UNUSED");
                if (cmdId != null && app.CanPostCommand(cmdId))
                {
                    app.PostCommand(cmdId);
                    _onComplete?.Invoke("Purge Unused dialog opened.");
                }
                else
                {
                    _onComplete?.Invoke("Navigate to Manage → Purge Unused in Revit.");
                }
            }
            catch (Exception ex)
            {
                _onComplete?.Invoke($"Error: {ex.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Model Warnings — reads warning count from active document
    // ─────────────────────────────────────────────────────────────────────────
    public class ModelWarningsRequest : IExternalEventRequest
    {
        private readonly Action<string> _onComplete;
        public ModelWarningsRequest(Action<string> onComplete) => _onComplete = onComplete;

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    _onComplete?.Invoke("No active document open.");
                    return;
                }

                var warnings = doc.GetWarnings();
                int count = warnings.Count;
                _onComplete?.Invoke(count == 0
                    ? $"{doc.Title}: no warnings — model is clean."
                    : $"{doc.Title}: {count} warning{(count == 1 ? "" : "s")} found.");
            }
            catch (Exception ex)
            {
                _onComplete?.Invoke($"Error reading warnings: {ex.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Transfer Project Standards
    // ─────────────────────────────────────────────────────────────────────────
    // ─────────────────────────────────────────────────────────────────────────
    //  Get Open Documents — returns titles of all non-family open documents
    // ─────────────────────────────────────────────────────────────────────────
    public class GetOpenDocumentsRequest : IExternalEventRequest
    {
        private readonly Action<List<string>> _onComplete;
        public GetOpenDocumentsRequest(Action<List<string>> onComplete) => _onComplete = onComplete;

        public void Execute(UIApplication app)
        {
            var docs = new List<string>();
            try
            {
                foreach (Document doc in app.Application.Documents)
                {
                    if (!doc.IsFamilyDocument)
                        docs.Add(doc.Title);
                }
            }
            catch { /* return what we have */ }
            _onComplete?.Invoke(docs);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Transfer Project Standards
    // ─────────────────────────────────────────────────────────────────────────
    public class TransferStandardsRequest : IExternalEventRequest
    {
        private readonly Action<string> _onComplete;
        public TransferStandardsRequest(Action<string> onComplete) => _onComplete = onComplete;

        public void Execute(UIApplication app)
        {
            try
            {
                var cmdId = RevitCommandId.LookupCommandId("ID_PROJECTSTANDARDS_TRANSFER");
                if (cmdId != null && app.CanPostCommand(cmdId))
                {
                    app.PostCommand(cmdId);
                    _onComplete?.Invoke("Transfer Project Standards dialog opened.");
                }
                else
                {
                    _onComplete?.Invoke("Navigate to Manage → Transfer Project Standards in Revit.");
                }
            }
            catch (Exception ex)
            {
                _onComplete?.Invoke($"Error: {ex.Message}");
            }
        }
    }
}