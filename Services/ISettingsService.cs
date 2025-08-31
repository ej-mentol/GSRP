
using GSRP.Models;
using System;

namespace GSRP.Services
{
    public interface ISettingsService
    {
        AppSettings CurrentSettings { get; }
        event EventHandler<AppSettings>? SettingsChanged;
        void LoadSettings();
        void SaveSettings();
    }
}
