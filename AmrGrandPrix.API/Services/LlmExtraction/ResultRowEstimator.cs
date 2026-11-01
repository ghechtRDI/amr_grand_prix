using System.Text.RegularExpressions;

namespace AmrGrandPrix.API.Services.LlmExtraction;

/// <summary>
/// Rough count of result rows in a results file's text, used only to scale the upload progress
/// bar while the LLM extracts. A line with a finish time (or a DNF/DNS/DQ marker) is one row.
/// Over-counts files that print each result twice (gender + age-group views), so treat it as "~".
/// </summary>
public static partial class ResultRowEstimator
{
    [GeneratedRegex(@"(?<![\d:])\d{1,2}:\d{2}(?::\d{2})?(?:\.\d+)?(?![\d:])|\b(?:DNF|DNS|DQ|DSQ)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RowMarker();

    /// <returns>The estimated row count, or null when nothing looked like a result row.</returns>
    public static int? Estimate(string text)
    {
        var rows = text.Split('\n').Count(line => RowMarker().IsMatch(line));
        return rows > 0 ? rows : null;
    }
}
