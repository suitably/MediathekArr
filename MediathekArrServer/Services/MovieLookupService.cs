using MediathekArr.Models.Tmdb;
using MediathekArr.Utilities;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MediathekArr.Services;

/// <summary>
/// Looks up movies on TMDB. Requires an own TMDB API Read Access Token (TMDB_API_TOKEN),
/// without it all movie features are disabled.
/// </summary>
public class MovieLookupService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IMemoryCache memoryCache, ILogger<MovieLookupService> logger)
{
    private const string TmdbApiBaseUrl = "https://api.themoviedb.org/3";
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
    private readonly string? _tmdbApiToken = configuration["TMDB_API_TOKEN"];
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly ILogger<MovieLookupService> _logger = logger;
    private readonly TimeSpan _cacheTimeSpan = TimeSpan.FromDays(3);
    private readonly TimeSpan _notFoundCacheTimeSpan = TimeSpan.FromHours(12);

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_tmdbApiToken);

    private static JsonSerializerOptions GetJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<MovieInfo?> GetMovieInfoByImdbId(string? imdbid)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(imdbid))
        {
            return null;
        }

        var imdbId = imdbid.StartsWith("tt", StringComparison.OrdinalIgnoreCase) ? imdbid.ToLowerInvariant() : $"tt{imdbid}";
        var cacheKey = $"TmdbImdb_{imdbId}";
        if (_memoryCache.TryGetValue(cacheKey, out MovieInfo? cachedMovieInfo))
        {
            return cachedMovieInfo;
        }

        var findResponse = await GetFromTmdb<FindResponse>($"/find/{imdbId}?external_source=imdb_id");
        if (findResponse == null)
        {
            // TMDB error, do not cache
            return null;
        }

        var tmdbId = findResponse.MovieResults.FirstOrDefault()?.Id;
        var movieInfo = tmdbId == null ? null : await GetMovieInfoByTmdbId(tmdbId.Value);

        _memoryCache.Set(cacheKey, movieInfo, movieInfo == null ? _notFoundCacheTimeSpan : _cacheTimeSpan);
        return movieInfo;
    }

    /// <summary>
    /// Finds the movie for a Mediathek title. Only returns a movie if the title matches exactly
    /// and the runtime fits, so a wrong movie is very unlikely.
    /// </summary>
    public async Task<MovieInfo?> FindMovieByTitle(string title, int? year, int durationSeconds)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var cacheKey = $"TmdbTitle_{title}_{year}";
        if (_memoryCache.TryGetValue(cacheKey, out MovieInfo? cachedMovieInfo))
        {
            return cachedMovieInfo;
        }

        var yearQuery = year != null ? $"&year={year}" : "";
        var searchResponse = await GetFromTmdb<SearchResponse>($"/search/movie?query={Uri.EscapeDataString(title)}&language=de-DE{yearQuery}");
        if (searchResponse == null)
        {
            // TMDB error, do not cache
            return null;
        }

        MovieInfo? movieInfo = null;
        var normalizedTitle = MovieTitleUtils.Normalize(title);
        var candidates = searchResponse.Results
            .Where(result => MovieTitleUtils.Normalize(result.Title) == normalizedTitle || MovieTitleUtils.Normalize(result.OriginalTitle) == normalizedTitle)
            .Take(3);

        foreach (var candidate in candidates)
        {
            var candidateInfo = await GetMovieInfoByTmdbId(candidate.Id);
            if (candidateInfo != null && MovieTitleUtils.MatchesRuntime(durationSeconds, candidateInfo.Runtime))
            {
                movieInfo = candidateInfo;
                break;
            }
        }

        _memoryCache.Set(cacheKey, movieInfo, movieInfo == null ? _notFoundCacheTimeSpan : _cacheTimeSpan);
        return movieInfo;
    }

    private async Task<MovieInfo?> GetMovieInfoByTmdbId(int tmdbId)
    {
        var cacheKey = $"TmdbMovie_{tmdbId}";
        if (_memoryCache.TryGetValue(cacheKey, out MovieInfo? cachedMovieInfo))
        {
            return cachedMovieInfo;
        }

        var details = await GetFromTmdb<MovieDetails>($"/movie/{tmdbId}?append_to_response=alternative_titles,translations");
        if (details == null)
        {
            // TMDB error, do not cache
            return null;
        }

        if (string.IsNullOrEmpty(details.ImdbId))
        {
            _memoryCache.Set(cacheKey, (MovieInfo?)null, _notFoundCacheTimeSpan);
            return null;
        }

        var translations = details.Translations?.Items ?? [];
        var englishTitle = translations.FirstOrDefault(t => t.Language == "en" && t.Country == "US")?.Data?.Title;

        // German titles first, the Mediathek mostly uses them
        var searchTitles = new List<string?>();
        searchTitles.AddRange(translations.Where(t => t.Language == "de").Select(t => t.Data?.Title));
        searchTitles.AddRange(details.AlternativeTitles?.Titles.Where(t => t.Country is "DE" or "AT" or "CH").Select(t => t.Title) ?? []);
        searchTitles.Add(details.Title);
        searchTitles.Add(details.OriginalTitle);

        var movieInfo = new MovieInfo(
            details.Id,
            details.ImdbId,
            string.IsNullOrWhiteSpace(englishTitle) ? details.Title : englishTitle,
            DateTime.TryParse(details.ReleaseDate, out var releaseDate) ? releaseDate.Year : 0,
            details.Runtime ?? 0,
            [.. searchTitles.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)]);

        _memoryCache.Set(cacheKey, movieInfo, _cacheTimeSpan);
        return movieInfo;
    }

    private async Task<T?> GetFromTmdb<T>(string path) where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{TmdbApiBaseUrl}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tmdbApiToken);

        try
        {
            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error fetching TMDB data for {Path}: {StatusCode}", path, response.StatusCode);
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(jsonResponse, GetJsonSerializerOptions());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "Error fetching TMDB data for {Path}", path);
            return null;
        }
    }
}
