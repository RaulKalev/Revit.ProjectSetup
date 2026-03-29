namespace ProjectSetup.Models
{
    /// <summary>
    /// Represents a "reeper" generic model element found inside a linked Revit model.
    /// </summary>
    public class ReeperItemDto
    {
        /// <summary>Loaded-link document title (display name in the datagrid).</summary>
        public string LinkTitle  { get; set; }

        public string FamilyName { get; set; }
        public string TypeName   { get; set; }

        /// <summary>ElementId.IntegerValue of the FamilyInstance inside the link document.</summary>
        public long   ElementId  { get; set; }

        /// <summary>Value of the "X,Y,Z" parameter converted to millimetres (0 if not found).</summary>
        public double SizeMm     { get; set; }

        /// <summary>World X coordinate in Revit internal units (feet) after applying the link transform.</summary>
        public double LocationX  { get; set; }
        /// <summary>World Y coordinate.</summary>
        public double LocationY  { get; set; }
        /// <summary>World Z coordinate.</summary>
        public double LocationZ  { get; set; }

        public string DisplayName => $"{FamilyName} : {TypeName}";
        public string SizeDisplay => SizeMm > 0 ? $"{SizeMm:F0} mm" : "—";
    }
}
