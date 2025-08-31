using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace GSRP.Services
{
    public sealed class IconService : IIconService
    {
        

        private readonly string[] extensions = new[] { ".png" }; // .webp requires a decoder
        private readonly object gate = new();
        private Dictionary<string, string> nameToPath = new(StringComparer.OrdinalIgnoreCase);

        public string IconsDirectory { get; }
        public IReadOnlyList<string> AvailableIconNames
        {
            get
            {
                lock (gate) 
                {
                    // Return names only, with a placeholder for "None"
                    return nameToPath.Keys.Prepend("None").ToList();
                }
            }
        }

        private readonly IPathProvider _pathProvider;

        public IconService(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
            IconsDirectory = Path.Combine(_pathProvider.GetAppDataPath(), "icons");
            Directory.CreateDirectory(IconsDirectory);
        }

        public void ScanForIcons()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            const int maxSize = 64; // Max width/height for icons

            try
            {
                foreach (var file in Directory.EnumerateFiles(IconsDirectory, "*", SearchOption.TopDirectoryOnly)
                                            .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant())))
                {
                    try
                    {
                        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                            var frame = decoder.Frames[0];
                            if (frame.PixelWidth <= maxSize && frame.PixelHeight <= maxSize)
                            {
                                var name = Path.GetFileNameWithoutExtension(file);
                                if (!string.IsNullOrEmpty(name))
                                {
                                    map[name] = file;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error for specific file and continue
                        System.Diagnostics.Debug.WriteLine($"[IconService] Failed to process icon file '{file}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IconService] Failed to scan icons directory: {ex.Message}");
                map.Clear();
            }

            lock (gate) nameToPath = map;
        }

        public string? ResolveIconPath(string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            lock (gate)
            {
                if (nameToPath.TryGetValue(name, out var path) && File.Exists(path))
                    return path;

                // Fallback for cases where the name in DB might have an extension
                // or the cache (nameToPath) is stale.
                var directPath = Path.Combine(IconsDirectory, name);
                if (File.Exists(directPath))
                {
                    return directPath;
                }

                // Last chance: try adding extensions manually.
                foreach (var ext in extensions)
                {
                    var candidate = Path.Combine(IconsDirectory, name + ext);
                    if (File.Exists(candidate)) return candidate;
                }
                return null;
            }
        }
    }
}