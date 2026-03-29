using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectSetup.Services.Revit
{
    /// <summary>
    /// Reads the set of completed setup step numbers (1-10) from Extensible Storage
    /// on the active document's ProjectInformation element.
    /// </summary>
    public partial class ReadSetupProgressRequest : IExternalEventRequest
    {
        private readonly Action<HashSet<int>> _callback;

        public ReadSetupProgressRequest(Action<HashSet<int>> callback)
            => _callback = callback;

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null) { _callback?.Invoke(new HashSet<int>()); return; }

                var schema = GetOrCreateSchema();
                var entity = doc.ProjectInformation.GetEntity(schema);

                if (!entity.IsValid()) { _callback?.Invoke(new HashSet<int>()); return; }

                var raw = entity.Get<string>(ProgressFieldName);
                var completed = ParseSteps(raw);
                _callback?.Invoke(completed);
            }
            catch
            {
                _callback?.Invoke(new HashSet<int>());
            }
        }
    }

    /// <summary>
    /// Writes the set of completed setup step numbers to Extensible Storage inside a Transaction.
    /// </summary>
    public partial class WriteSetupProgressRequest : IExternalEventRequest
    {
        private readonly HashSet<int> _steps;
        private readonly Action<string> _onComplete;

        public WriteSetupProgressRequest(HashSet<int> steps, Action<string> onComplete)
        {
            _steps = new HashSet<int>(steps);
            _onComplete = onComplete;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null) { _onComplete?.Invoke("No active document."); return; }

                var schema = GetOrCreateSchema();

                using var tx = new Transaction(doc, "Save Setup Progress");
                tx.Start();

                var entity = new Entity(schema);
                entity.Set(ProgressFieldName, SerializeSteps(_steps));
                doc.ProjectInformation.SetEntity(entity);

                tx.Commit();
                _onComplete?.Invoke("Progress saved.");
            }
            catch (Exception ex)
            {
                _onComplete?.Invoke($"Error saving progress: {ex.Message}");
            }
        }
    }

    // ── Shared helpers (internal, accessed by both request classes) ──────────
    internal static class SetupProgressHelper
    {
        internal const string ProgressFieldName = "CompletedSteps";

        private static readonly Guid SchemaGuid = new Guid("4E2A8B3C-1F6D-4A7E-9C5B-8D0F2E3A4B5C");

        internal static Schema GetOrCreateSchema()
        {
            var existing = Schema.Lookup(SchemaGuid);
            if (existing != null) return existing;

            var builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName("EULE_ProjectSetupProgress");
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(ProgressFieldName, typeof(string));
            return builder.Finish();
        }

        internal static HashSet<int> ParseSteps(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new HashSet<int>();
            return new HashSet<int>(
                raw.Split(',')
                   .Select(s => int.TryParse(s.Trim(), out var n) ? n : 0)
                   .Where(n => n >= 1 && n <= 10));
        }

        internal static string SerializeSteps(HashSet<int> steps)
            => string.Join(",", steps.OrderBy(n => n));
    }

    // ── Pull helpers into the request classes via partial-class trick ────────
    public partial class ReadSetupProgressRequest
    {
        private static string ProgressFieldName => SetupProgressHelper.ProgressFieldName;
        private static Schema GetOrCreateSchema() => SetupProgressHelper.GetOrCreateSchema();
        private static HashSet<int> ParseSteps(string raw) => SetupProgressHelper.ParseSteps(raw);
    }

    public partial class WriteSetupProgressRequest
    {
        private static string ProgressFieldName => SetupProgressHelper.ProgressFieldName;
        private static Schema GetOrCreateSchema() => SetupProgressHelper.GetOrCreateSchema();
        private static string SerializeSteps(HashSet<int> steps) => SetupProgressHelper.SerializeSteps(steps);
    }
}
