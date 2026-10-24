using System.Text.Json.Serialization;

namespace MediathekArr.Models.Tmdb;

public record MovieDetails(
    int Id,
    [property: JsonPropertyName("imdb_id")] string? ImdbId,
    string Title,
    [property: JsonPropertyName("original_title")] string OriginalTitle,
    [property: JsonPropertyName("release_date")] string? ReleaseDate,
    int? Runtime,
    [property: JsonPropertyName("alternative_titles")] AlternativeTitles? AlternativeTitles,
    Translations? Translations);
