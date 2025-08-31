using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GSRP.Services
{
    public class FileApiKeyService : ApiKeyServiceBase
    {
        private readonly string _keyFilePath;

        private readonly IPathProvider _pathProvider;

        public FileApiKeyService(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
            _keyFilePath = Path.Combine(_pathProvider.GetAppDataPath(), ".steamapi");
        }

        public override string GetApiKey()
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_cachedKey))
                    return _cachedKey;

                try
                {
                    if (!File.Exists(_keyFilePath))
                        return string.Empty;

                    var encryptedData = File.ReadAllBytes(_keyFilePath);
                    var decryptedBytes = ProtectedData.Unprotect(
                        encryptedData,
                        _machineEntropy,
                        DataProtectionScope.CurrentUser
                    );

                    _cachedKey = Encoding.UTF8.GetString(decryptedBytes);

                    // Очищаем массив из памяти
                    Array.Clear(decryptedBytes, 0, decryptedBytes.Length);

                    return _cachedKey;
                }
                catch (Exception ex)
                {
                    // Логируем ошибку (можно добавить ILogger)
                    System.Diagnostics.Debug.WriteLine($"Error reading API key: {ex.Message}");

                    TryDeleteKeyFile();
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
                        _cachedKey = null;
                        TryDeleteKeyFile();
                        return;
                    }

                    var keyBytes = Encoding.UTF8.GetBytes(apiKey);
                    var encryptedBytes = ProtectedData.Protect(
                        keyBytes,
                        _machineEntropy,
                        DataProtectionScope.CurrentUser
                    );

                    // Атомарная запись через File.Replace
                    var tempFile = _keyFilePath + ".tmp";
                    var backupFile = _keyFilePath + ".bak";

                    File.WriteAllBytes(tempFile, encryptedBytes);

                    if (File.Exists(_keyFilePath))
                    {
                        File.Replace(tempFile, _keyFilePath, backupFile);
                        // Удаляем backup файл
                        if (File.Exists(backupFile))
                            File.Delete(backupFile);
                    }
                    else
                    {
                        File.Move(tempFile, _keyFilePath);
                    }

                    // Устанавливаем атрибут "скрытый" для файла
                    File.SetAttributes(_keyFilePath, FileAttributes.Hidden);

                    _cachedKey = apiKey;

                    // Очищаем массивы из памяти
                    Array.Clear(keyBytes, 0, keyBytes.Length);
                    Array.Clear(encryptedBytes, 0, encryptedBytes.Length);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving API key: {ex.Message}");
                    throw;
                }
            }
        }

        private void TryDeleteKeyFile()
        {
            try
            {
                if (File.Exists(_keyFilePath))
                    File.Delete(_keyFilePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting key file: {ex.Message}");
            }
        }
    }
}
