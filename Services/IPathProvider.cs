namespace GSRP.Services
{
    public interface IPathProvider
    {
        string GetAppDataPath();
        string GetCachePath();
        string GetSettingsPath();
    }
}
