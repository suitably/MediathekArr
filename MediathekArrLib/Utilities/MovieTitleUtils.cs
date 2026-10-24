using System.Text.RegularExpressions;

namespace MediathekArr.Utilities;
public static partial class MovieTitleUtils
{
    private static readonly string[] _movieTopicKeywords = ["film", "kino"];
    private static readonly string[] _titleSuffixSeparators = [" - ", " – ", " | "];

    /// <summary>
    /// Topics like "Spielfilm-Highlights", "Fernsehfilm", "FilmMittwoch im Ersten" or "Kino - Filme"
    /// </summary>
    public static bool IsMovieTopic(string? topic)
    {
        return !string.IsNullOrEmpty(topic) && _movieTopicKeywords.Any(keyword => topic.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// "Der Pate - Spielfilm, USA 1972" -> "Der Pate", "Michael Clayton (mit Untertitel)" -> "Michael Clayton".
    /// A " - " is only cut if a suffix like "Spielfilm" or a year follows, "Spiders - Ihr Biss ist der Tod" stays.
    /// </summary>
    public static string CutTitleSuffix(string title)
    {
        var cutIndices = new List<int> { title.IndexOf('(') };
        foreach (var separator in _titleSuffixSeparators)
        {
            var index = title.IndexOf(separator, StringComparison.Ordinal);
            if (index > 0 && TitleSuffixRegex().IsMatch(title[(index + separator.Length)..]))
            {
                cutIndices.Add(index);
            }
        }

        var cutIndex = cutIndices.Where(i => i > 0).DefaultIfEmpty(title.Length).Min();
        return title[..cutIndex].Trim();
    }

    /// <summary>
    /// Production year if the Mediathek title names one ("Spielfilm, USA 1972"),
    /// otherwise from a description starting like "Spielfilm Deutschland 2012"
    /// </summary>
    public static int? ExtractYear(string title, string? description = null)
    {
        var matches = YearRegex().Matches(title);
        if (matches.Count > 0)
        {
            return int.Parse(matches[^1].Value);
        }

        var descriptionMatch = string.IsNullOrEmpty(description) ? null : DescriptionYearRegex().Match(description);
        return descriptionMatch?.Success == true ? int.Parse(descriptionMatch.Groups[1].Value) : null;
    }

    public static bool MatchesYear(int? itemYear, int year)
    {
        return year <= 0 || itemYear == null || Math.Abs(itemYear.Value - year) <= 1;
    }

    /// <summary>
    /// TV versions are often a bit shorter (PAL speed-up, cut credits)
    /// </summary>
    public static bool MatchesRuntime(int durationSeconds, int runtimeMinutes)
    {
        if (runtimeMinutes <= 0)
        {
            return durationSeconds >= 60 * 60;
        }

        var runtimeSeconds = runtimeMinutes * 60;
        return durationSeconds >= runtimeSeconds * 0.8 && durationSeconds <= runtimeSeconds * 1.2;
    }

    public static string Normalize(string? title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return string.Empty;
        }

        return new string(title.ToLowerInvariant().Replace("&", "und").Where(char.IsLetterOrDigit).ToArray());
    }

    [GeneratedRegex(@"\b(19|20)\d{2}\b")]
    private static partial Regex YearRegex();
    [GeneratedRegex(@"film|\b(19|20)\d{2}\b", RegexOptions.IgnoreCase)]
    private static partial Regex TitleSuffixRegex();
    [GeneratedRegex(@"^\W*\w*film\b[^.\n]{0,40}?\b((?:19|20)\d{2})\b", RegexOptions.IgnoreCase)]
    private static partial Regex DescriptionYearRegex();
}
