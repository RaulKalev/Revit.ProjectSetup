namespace ProjectSetup.Models
{
    public class FloorPlanViewInfo
    {
        public long   ElementId { get; }
        public string Name      { get; }

        public FloorPlanViewInfo(string name, long elementId)
        {
            Name      = name;
            ElementId = elementId;
        }

        public override string ToString() => Name;
    }
}
