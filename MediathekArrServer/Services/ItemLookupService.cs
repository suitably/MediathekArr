using MediathekArr.Models.Movies;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace MediathekArr.Services;

public class ItemLookupService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IMemoryCache memoryCache, ILogger<ItemLookupService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
    private readonly string _apiBaseUrl = configuration["MEDIATHEKARR_API_BASE_URL"] ?? "https://mediathekarr.pcjones.de/api/v1";
    private readonly string _radarrMetadataBaseUrl = configuration["RADARR_METADATA_BASE_URL"] ?? "https://api.radarr.video/v1";
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly ILogger<ItemLookupService> _logger = logger;

    private static JsonSerializerOptions GetJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<Models.Tvdb.Data?> GetShowInfoByTvdbId(int? tvdbid)
    {
        if (tvdbid == null)
        {
            return null;
        }

        var cacheKey = $"TvdbInfo_{tvdbid}";
        if (_memoryCache.TryGetValue(cacheKey, out Models.Tvdb.Data? cachedTvdbInfo))
        {
            return cachedTvdbInfo;
        }

        var requestUrl = $"{_apiBaseUrl}/get_show.php?tvdbid={tvdbid}";

        var response = await _httpClient.GetAsync(requestUrl);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Error fetching data: {errorContent}");
        }

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var tvdbInfo = JsonSerializer.Deserialize<Models.Tvdb.InfoResponse>(jsonResponse, GetJsonSerializerOptions());

        if (tvdbInfo?.Status == "error")
        {
            _logger.LogError("Error fetching TVDB data: {Status}", tvdbInfo.Status);
            _memoryCache.Set(cacheKey, (Models.Tvdb.Data?)null, TimeSpan.FromHours(12));
            return null;
        }

        if (tvdbInfo == null || tvdbInfo.Status != "success" || tvdbInfo.Data == null)
        {
            throw new HttpRequestException($"Failed to fetch TVDB data for tvdbid {tvdbid}. Response: {jsonResponse}");
        }

        _memoryCache.Set(cacheKey, tvdbInfo.Data, TimeSpan.FromHours(12));

        return tvdbInfo.Data;
    }

    public async Task<MovieInfo?> GetMovieInfoByImdbId(string? imdbid)
    {
        if (string.IsNullOrWhiteSpace(imdbid))
        {
            return null;
        }

        var imdbId = imdbid.StartsWith("tt", StringComparison.OrdinalIgnoreCase) ? imdbid.ToLowerInvariant() : $"tt{imdbid}";
        var cacheKey = $"MovieInfo_{imdbId}";
        if (_memoryCache.TryGetValue(cacheKey, out MovieInfo? cachedMovieInfo))
        {
            return cachedMovieInfo;
        }

        var response = await _httpClient.GetAsync($"{_radarrMetadataBaseUrl}/movie/imdb/{imdbId}");
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error fetching movie data for {ImdbId}: {StatusCode}", imdbId, response.StatusCode);
            _memoryCache.Set(cacheKey, (MovieInfo?)null, TimeSpan.FromHours(1));
            return null;
        }

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var movie = JsonSerializer.Deserialize<List<RadarrMovie>>(jsonResponse, GetJsonSerializerOptions())?.FirstOrDefault();
        if (movie == null || string.IsNullOrEmpty(movie.Title))
        {
            _memoryCache.Set(cacheKey, (MovieInfo?)null, TimeSpan.FromHours(1));
            return null;
        }

        // German titles first, the Mediathek mostly uses them
        var searchTitles = new List<string?>();
        searchTitles.AddRange(movie.Translations?.Where(t => t.Language == "de-DE").Select(t => t.Title) ?? []);
        searchTitles.AddRange(movie.AlternativeTitles?.Where(t => t.Language == "de").Select(t => t.Title) ?? []);
        searchTitles.Add(movie.Title);
        searchTitles.Add(movie.OriginalTitle);

        var movieInfo = new MovieInfo(
            imdbId,
            movie.Title,
            movie.Year,
            movie.Runtime,
            [.. searchTitles.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)]);

        _memoryCache.Set(cacheKey, movieInfo, TimeSpan.FromHours(12));
        return movieInfo;
    }
}
