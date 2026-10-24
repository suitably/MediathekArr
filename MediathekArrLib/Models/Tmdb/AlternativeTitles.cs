using System.Text.Json.Serialization;

namespace MediathekArr.Models.Tmdb;

public record AlternativeTitles(List<AlternativeTitle> Titles);

public record AlternativeTitle([property: JsonPropertyName("iso_3166_1")] string Country, string Title);
