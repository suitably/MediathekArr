namespace MediathekArr.Models.Movies;

public record MovieInfo(string ImdbId, string Title, int Year, int Runtime, List<string> SearchTitles);

// Response of the Radarr metadata API (api.radarr.video/v1/movie/imdb/{imdbId})
public record RadarrMovie(string ImdbId, string Title, string OriginalTitle, int Year, int Runtime, List<RadarrTitle>? AlternativeTitles, List<RadarrTitle>? Translations);

public record RadarrTitle(string Title, string? Language);
