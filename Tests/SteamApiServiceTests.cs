using Xunit;
using GSRP.Services;
using GSRP.Models;
using Moq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Collections.Generic;

namespace GSRP.Tests
{
    public class SteamApiServiceTests
    {
        private readonly Mock<IHttpClientService> _mockHttpClientService;
        private readonly Mock<IApiKeyService> _mockApiKeyService;
        private readonly SteamApiService _service;

        public SteamApiServiceTests()
        {
            _mockHttpClientService = new Mock<IHttpClientService>();
            _mockApiKeyService = new Mock<IApiKeyService>();
            
            var handler = new Mock<HttpMessageHandler>();
            var client = new HttpClient(handler.Object);
            _mockHttpClientService.Setup(x => x.GetClient()).Returns(client);
            _mockApiKeyService.Setup(x => x.GetApiKey()).Returns("TEST_API_KEY");

            _service = new SteamApiService(_mockHttpClientService.Object, _mockApiKeyService.Object);
        }

        [Fact]
        public async Task EnrichPlayersAsync_NoApiKey_ReturnsError()
        {
            _mockApiKeyService.Setup(x => x.GetApiKey()).Returns(string.Empty);
            var result = await _service.EnrichPlayersAsync(new List<Player> { new Player() }, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Contains("API Key is not set", result.ErrorMessage);
        }
    }
}
