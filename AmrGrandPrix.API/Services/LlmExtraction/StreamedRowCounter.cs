using System.Text;

namespace AmrGrandPrix.API.Services.LlmExtraction;

/// <summary>
/// Counts result rows in a tool-use JSON payload as it streams in, for the upload progress bar.
/// Every row object carries the required <c>"status"</c> key and section objects don't, so each
/// occurrence of that key is one row. A key inside a JSON string value would be escaped
/// (<c>\"status\"</c>) and so never matches.
/// </summary>
public sealed class StreamedRowCounter
{
    private const string Key = "\"status\"";
    private int _scanFrom;

    public int Count { get; private set; }

    /// <summary>Scan text appended to <paramref name="json"/> since the last call; true if the count grew.</summary>
    public bool Advance(StringBuilder json)
    {
        if (json.Length - _scanFrom < Key.Length)
            return false;

        var before = Count;
        var chunk = json.ToString(_scanFrom, json.Length - _scanFrom);
        var pos = 0;
        int idx;
        while ((idx = chunk.IndexOf(Key, pos, StringComparison.Ordinal)) >= 0)
        {
            Count++;
            pos = idx + Key.Length;
        }

        // Re-scan the tail next time in case the key is split across two deltas, but never
        // re-scan past the end of a match already counted.
        _scanFrom += Math.Max(pos, chunk.Length - (Key.Length - 1));
        return Count > before;
    }
}
