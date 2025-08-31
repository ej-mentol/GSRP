using System.Collections.Generic;

namespace GSRP.Services
{
    public interface IIconService
    {
        IReadOnlyList<string> AvailableIconNames { get; }
        string IconsDirectory { get; }
        void ScanForIcons();
        string? ResolveIconPath(string? name);
    }
}
