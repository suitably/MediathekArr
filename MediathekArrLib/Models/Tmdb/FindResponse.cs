using System.Text.Json.Serialization;

namespace MediathekArr.Models.Tmdb;

public record FindResponse([property: JsonPropertyName("movie_results")] List<MovieResult> MovieResults);
