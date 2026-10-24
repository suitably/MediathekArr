namespace MediathekArr.Models.Tmdb;

/// <summary>
/// Movie data needed to find and name a movie from the Mediathek.
/// Title is the English title (what Radarr uses), SearchTitles contain the German titles first.
/// </summary>
public record MovieInfo(int TmdbId, string ImdbId, string Title, int Year, int Runtime, List<string> SearchTitles);
