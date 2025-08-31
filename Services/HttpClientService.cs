using System;
using System.Net.Http;

namespace GSRP.Services
{
    public class HttpClientService : IHttpClientService, IDisposable
    {
        private readonly HttpClient _httpClient;

        public HttpClientService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public HttpClient GetClient() => _httpClient;

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}