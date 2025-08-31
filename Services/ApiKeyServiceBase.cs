using System;
using System.Security.Cryptography;
using System.Text;

namespace GSRP.Services
{
    public abstract class ApiKeyServiceBase : IApiKeyService
    {
        protected readonly byte[]? _machineEntropy;
        protected string? _cachedKey;
        protected readonly object _lock = new object();

        protected ApiKeyServiceBase()
        {
            _machineEntropy = GenerateMachineEntropy();
        }

        protected byte[] GenerateMachineEntropy()
        {
            var machineId = Environment.MachineName +
                           Environment.UserName +
                           Environment.ProcessorCount.ToString() +
                           Environment.OSVersion.Version.ToString();

            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(machineId));
            }
        }

        public abstract string GetApiKey();
        public abstract void SetApiKey(string? apiKey);

        public bool HasApiKey()
        {
            return !string.IsNullOrEmpty(GetApiKey());
        }

        public void ClearApiKey()
        {
            SetApiKey(null);
        }

        public virtual void Dispose()
        {
            lock (_lock)
            {
                _cachedKey = null;
            }
        }
    }
}
