using System.Net.Http;

namespace GSRP.Services
{
    public interface IHttpClientService
    {
        HttpClient GetClient();
    }
}
