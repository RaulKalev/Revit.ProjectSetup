namespace ProjectSetup.Models
{
    public class SettingsModel
    {
        public bool IsDarkMode { get; set; } = true;
        public double WindowLeft { get; set; } = double.NaN;
        public double WindowTop { get; set; } = double.NaN;
        public double WindowWidth { get; set; } = 780;
        public double WindowHeight { get; set; } = 620;
    }
}
