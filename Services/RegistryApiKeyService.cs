using Microsoft.Win32;
using System;
using System.Security.Cryptography;
using System.Text;

namespace GSRP.Services
{
    public class RegistryApiKeyService : ApiKeyServiceBase
    {
        private const string RegistryPath = @"HKEY_CURRENT_USER\SOFTWARE\GSRP";
        private const string? ValueName = "ApiKey";

        public override string GetApiKey()
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_cachedKey))
                    return _cachedKey;

                try
                {
                    var encryptedBase64 = Registry.GetValue(RegistryPath, ValueName, null) as string;
                    if (string.IsNullOrEmpty(encryptedBase64))
                        return string.Empty;

                    var encryptedBytes = Convert.FromBase64String(encryptedBase64);
                    var decryptedBytes = ProtectedData.Unprotect(
                        encryptedBytes,
                        _machineEntropy,
                        DataProtectionScope.CurrentUser
                    );

                    _cachedKey = Encoding.UTF8.GetString(decryptedBytes);
                    Array.Clear(decryptedBytes, 0, decryptedBytes.Length);

                    return _cachedKey;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading API key from registry: {ex.Message}");

                    try
                    {
                        Registry.SetValue(RegistryPath, ValueName, "");
                    }
                    catch { }

                    return string.Empty;
                }
            }
        }

        public override void SetApiKey(string? apiKey)
        {
            lock (_lock)
            {
                try
                {
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        Registry.SetValue(RegistryPath, ValueName, "");
                        _cachedKey = null;
                        return;
                    }

                    var keyBytes = Encoding.UTF8.GetBytes(apiKey);
                    var encryptedBytes = ProtectedData.Protect(
                        keyBytes,
                        _machineEntropy,
                        DataProtectionScope.CurrentUser
                    );

                    var base64 = Convert.ToBase64String(encryptedBytes);
                    Registry.SetValue(RegistryPath, ValueName, base64);

                    _cachedKey = apiKey;

                    Array.Clear(keyBytes, 0, keyBytes.Length);
                    Array.Clear(encryptedBytes, 0, encryptedBytes.Length);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving API key to registry: {ex.Message}");
                    throw;
                }
            }
        }
    }
}
