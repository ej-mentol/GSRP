using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using GSRP.Models;

namespace GSRP.Services
{
    public class SettingsService : ISettingsService
    {

        private readonly string _settingsPath;
        private AppSettings _currentSettings = new AppSettings();

        private readonly IPathProvider _pathProvider;

        public SettingsService(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
            _settingsPath = _pathProvider.GetSettingsPath();
            LoadSettings();
        }

        public AppSettings CurrentSettings => _currentSettings;

        public event EventHandler<AppSettings>? SettingsChanged;

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _currentSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    _currentSettings = new AppSettings();
                    SaveSettings(); // Create default settings file
                }
            }
            catch (Exception)
            {
                // If loading fails, use defaults and try to save
                _currentSettings = new AppSettings();
                SaveSettings();
            }
        }

        public void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_settingsPath, json);
                SettingsChanged?.Invoke(this, _currentSettings);
            }
            catch (Exception)
            {
                // Log error in production
            }
        }

        public Task SaveSettingsAsync()
        {
            return Task.Run(() => SaveSettings());
        }
    }
}