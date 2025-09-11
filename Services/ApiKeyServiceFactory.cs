using System;
using System.IO;

namespace GSRP.Services
{
    public static class ApiKeyServiceFactory
    {
        public enum StorageType
        {
            File,
            Registry,
            Auto 
        }

        public static IApiKeyService Create(IPathProvider pathProvider, StorageType type = StorageType.Auto)
        {
            switch (type)
            {
                case StorageType.File:
                    return new FileApiKeyService(pathProvider);

                case StorageType.Registry:
                    return new RegistryApiKeyService();

                case StorageType.Auto:
                default:
                    return CreateBestOption(pathProvider);
            }
        }

        private static IApiKeyService CreateBestOption(IPathProvider pathProvider)
        {
            try
            {
                var appDataPath = pathProvider.GetAppDataPath();
                if (Directory.Exists(appDataPath) && HasWriteAccess(appDataPath))
                {
                    return new FileApiKeyService(pathProvider);
                }
            }
            catch
            {
                // 
            }

            return new RegistryApiKeyService();
        }

        private static bool HasWriteAccess(string path)
        {
            try
            {
                var testFile = Path.Combine(path, $"test_{Guid.NewGuid()}.tmp");
                File.WriteAllBytes(testFile, new byte[1]);
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}