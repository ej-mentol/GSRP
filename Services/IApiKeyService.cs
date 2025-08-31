using System;

namespace GSRP.Services
{
    public interface IApiKeyService : IDisposable
    {
        string GetApiKey();
        void SetApiKey(string? apiKey);
        bool HasApiKey();
        void ClearApiKey();
    }
}
