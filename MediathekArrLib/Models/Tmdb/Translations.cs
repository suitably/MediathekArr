using System.Text.Json.Serialization;

namespace MediathekArr.Models.Tmdb;

public record Translations([property: JsonPropertyName("translations")] List<Translation> Items);

public record Translation([property: JsonPropertyName("iso_639_1")] string Language, [property: JsonPropertyName("iso_3166_1")] string Country, TranslationData? Data);

public record TranslationData(string? Title);
