using System;
using System.IO;

namespace GSRP.Services
{
    public class PathProvider : IPathProvider
    {
        public string GetAppDataPath()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GSRP");
            Directory.CreateDirectory(path);
            return path;
        }

        public string GetCachePath()
        {
            var path = Path.Combine(GetAppDataPath(), "cache");
            Directory.CreateDirectory(path);
            return path;
        }

        public string GetSettingsPath()
        {
            return Path.Combine(GetAppDataPath(), "settings.json");
        }
    }
}