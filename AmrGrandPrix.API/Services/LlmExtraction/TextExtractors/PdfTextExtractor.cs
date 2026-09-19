using System.Text;
using UglyToad.PdfPig; // PdfPig NuGet package
using UglyToad.PdfPig.Content;

namespace AmrGrandPrix.API.Services.LlmExtraction.TextExtractors;

public static class PdfTextExtractor
{
    public static string Extract(Stream stream)
    {
        var sb = new StringBuilder();

        using var document = PdfDocument.Open(stream);
        foreach (var page in document.GetPages())
        {
            sb.AppendLine($"--- Page {page.Number} ---");
            // GetWords() preserves relative spatial ordering better than raw characters
            var words = page.GetWords();
            var lines = GroupIntoLines(words);
            foreach (var line in lines)
                sb.AppendLine(string.Join(" ", line.Select(w => w.Text)));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // Group words into approximate visual lines by Y-coordinate bucket.
    private static List<List<Word>> GroupIntoLines(IEnumerable<Word> words)
    {
        var groups = new SortedDictionary<int, List<Word>>();

        foreach (var word in words)
        {
            // Round Y to nearest 4 points to bucket lines robustly
            var bucket = (int)(Math.Round(word.BoundingBox.Bottom / 4.0) * 4);
            if (!groups.TryGetValue(bucket, out var list))
                groups[bucket] = list = new List<Word>();
            list.Add(word);
        }

        // PDF Y axis is bottom-up, so reverse for reading order
        return groups.Values
            .Reverse()
            .Select(g => g.OrderBy(w => w.BoundingBox.Left).ToList())
            .ToList();
    }
}
