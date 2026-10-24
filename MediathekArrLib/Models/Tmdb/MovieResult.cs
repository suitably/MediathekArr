using System.Text.Json.Serialization;

namespace MediathekArr.Models.Tmdb;

public record MovieResult(int Id, string Title, [property: JsonPropertyName("original_title")] string OriginalTitle, [property: JsonPropertyName("release_date")] string? ReleaseDate);
