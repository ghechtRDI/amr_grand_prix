using System.Text;
using ClosedXML.Excel;

namespace AmrGrandPrix.API.Services.LlmExtraction.TextExtractors;

public static class XlsxTextExtractor
{
    public static string Extract(Stream stream)
    {
        var sb = new StringBuilder();

        using var workbook = new XLWorkbook(stream);
        foreach (var worksheet in workbook.Worksheets)
        {
            sb.AppendLine($"--- Sheet: {worksheet.Name} ---");

            var usedRange = worksheet.RangeUsed();
            if (usedRange == null) continue;

            foreach (var row in usedRange.RowsUsed())
            {
                var cells = row.CellsUsed().Select(c => c.GetString().Trim());
                sb.AppendLine(string.Join("\t", cells));
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
