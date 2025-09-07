using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using GSRP.Models;
using GSRP.Models.SteamApi;

namespace GSRP.Services
{
    public class SteamApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IApiKeyService _apiKeyService;

        public SteamApiService(IHttpClientService httpClientService, IApiKeyService apiKeyService)
        {
            _httpClient = httpClientService?.GetClient() ?? throw new ArgumentNullException(nameof(httpClientService));
            _apiKeyService = apiKeyService ?? throw new ArgumentNullException(nameof(apiKeyService));

            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public async Task<EnrichmentResult> EnrichPlayersAsync(List<Player> players, CancellationToken cancellationToken)
        {
            var apiKey = _apiKeyService.GetApiKey();

            if (string.IsNullOrEmpty(apiKey))
            {
                return new EnrichmentResult { Success = false, ErrorMessage = "Steam API Key is not set. Please configure it in the settings." };
            }

            if (players.Count == 0)
            {
                return new EnrichmentResult { Success = true, Data = new Dictionary<string, PlayerData>() };
            }

            try
            {
                var ids = string.Join(",", players.Select(p => p.SteamId64));
                var requestUrl = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={apiKey}&steamids={ids}";

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.GetAsync(requestUrl, cancellationToken);
                }
                catch (HttpRequestException ex)
                {
                    // Network-level error (DNS, connection refused, etc.)
                    return new EnrichmentResult { Success = false, ErrorMessage = $"Network error: {ex.Message}. Please check your internet connection." };
                }

                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage;
                    switch (response.StatusCode)
                    {
                        case System.Net.HttpStatusCode.Unauthorized:
                        case System.Net.HttpStatusCode.Forbidden:
                            errorMessage = "Invalid Steam API Key. Please check it in the settings.";
                            break;
                        case System.Net.HttpStatusCode.TooManyRequests:
                            errorMessage = "API request limit exceeded. Please wait a moment before refreshing.";
                            break;
                        default:
                            errorMessage = $"Steam API returned an error: {response.ReasonPhrase} (Code: {(int)response.StatusCode}).";
                            break;
                    }
                    return new EnrichmentResult { Success = false, ErrorMessage = errorMessage };
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var data = new Dictionary<string, PlayerData>();

                if (doc.RootElement.TryGetProperty("response", out var resp) &&
                    resp.TryGetProperty("players", out var playersArray))
                {
                    foreach (var p in playersArray.EnumerateArray())
                    {
                        var steamId = p.GetProperty("steamid").GetString() ?? "";
                        if (string.IsNullOrEmpty(steamId)) continue;

                        // A communityvisibilitystate of 3 means the profile is public.
                        bool isPrivate = true;
                        if (p.TryGetProperty("communityvisibilitystate", out var cvs) && cvs.TryGetInt32(out int visibilityState))
                        {
                            isPrivate = visibilityState != 3;
                        }

                        var personaName = p.TryGetProperty("personaname", out var pn) ? pn.GetString() ?? "" : "";
                        var timeCreated = p.TryGetProperty("timecreated", out var tc) ? tc.GetUInt32() : 0;
                        var avatarHash = p.TryGetProperty("avatarhash", out var ah) ? ah.GetString() ?? "" : "";

                        data[steamId] = new PlayerData
                        {
                            PersonaName = personaName,
                            TimeCreated = timeCreated,
                            AvatarHash = avatarHash,
                            IsPrivate = isPrivate
                        };
                    }
                }
                return new EnrichmentResult { Success = true, Data = data };
            }
            catch (OperationCanceledException)
            {
                // This is expected when an operation is cancelled. We don't need to show an error.
                return new EnrichmentResult { Success = false, ErrorMessage = "Operation cancelled." };
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Steam API JSON parsing error: {ex.Message}");
                return new EnrichmentResult { Success = false, ErrorMessage = "Failed to read response from Steam API." };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Steam API error: {ex.Message}");
                return new EnrichmentResult { Success = false, ErrorMessage = $"An unexpected error occurred: {ex.Message}" };
            }
        }

        public async Task<List<PlayerBanData>?> GetPlayerBansAsync(List<Player> players, CancellationToken cancellationToken)
        {
            var apiKey = _apiKeyService.GetApiKey();
            if (string.IsNullOrEmpty(apiKey) || !players.Any())
            {
                return null;
            }

            var allBans = new List<PlayerBanData>();
            const int batchSize = 100;

            for (int i = 0; i < players.Count; i += batchSize)
            {
                var batch = players.Skip(i).Take(batchSize).ToList();
                if (!batch.Any()) continue;

                try
                {
                    var ids = string.Join(",", batch.Select(p => p.SteamId64));
                    var requestUrl = $"https://api.steampowered.com/ISteamUser/GetPlayerBans/v1/?key={apiKey}&steamids={ids}";

                    var response = await _httpClient.GetAsync(requestUrl, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"Steam API (GetPlayerBans) returned an error: {response.ReasonPhrase}");
                        continue; // Continue to next batch
                    }

                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    var result = JsonSerializer.Deserialize<PlayerBansRoot>(json);
                    if (result?.Players != null)
                    {
                        allBans.AddRange(result.Players);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"Steam API (GetPlayerBans) batch error: {ex.Message}");
                    // Continue to next batch
                }
            }

            return allBans;
        }
    }
}