using System;
using System.IO;
using Newtonsoft.Json;
using ProjectSetup.Models;
using ProjectSetup.Services.Core;

namespace ProjectSetup.Services
{
    public class SettingsService
    {
        private readonly string _settingsPath;
        private readonly ILogger _logger;

        public SettingsService(ILogger logger)
        {
            _logger = logger;
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RK Tools", "ProjectSetup");
            Directory.CreateDirectory(folder);
            _settingsPath = Path.Combine(folder, "settings.json");
        }

        public SettingsModel Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    string json = File.ReadAllText(_settingsPath);
                    return JsonConvert.DeserializeObject<SettingsModel>(json) ?? new SettingsModel();
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to load settings: {ex.Message}");
            }
            return new SettingsModel();
        }

        public void Save(SettingsModel settings)
        {
            try
            {
                File.WriteAllText(_settingsPath, JsonConvert.SerializeObject(settings, Formatting.Indented));
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to save settings: {ex.Message}");
            }
        }
    }
}