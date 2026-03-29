namespace ProjectSetup.Models
{
    public enum LevelAction
    {
        RenameExisting,
        CreateNew,
        DeleteExisting
    }

    public class LevelPreviewDto
    {
        public string      Name            { get; set; }
        public double      ElevationMm     { get; set; }
        public string      ElevationDisplay => $"{ElevationMm:F0} mm";
        public LevelAction Action           { get; set; }
        public string      ActionDisplay    => Action switch
        {
            LevelAction.RenameExisting => "Nimeta ümber",
            LevelAction.CreateNew      => "Loo uus",
            LevelAction.DeleteExisting => "Kustuta",
            _                          => ""
        };
    }
}
