using AmrGrandPrix.API.Tests.Infrastructure;
using FluentAssertions;

namespace AmrGrandPrix.API.Tests.Docs;

/// <summary>
/// Keeps <c>DOCS/ERD.md</c> in step with the EF Core model. After a model change, regenerate with
/// <c>UPDATE_ERD=1 dotnet test --filter "FullyQualifiedName~ErdTests"</c> and commit the result.
/// </summary>
public class ErdTests
{
    [Fact]
    public void ErdMarkdown_MatchesModel()
    {
        var path = Path.Combine(FindRepoRoot(), "DOCS", "ERD.md");
        var expected = ErdGenerator.GenerateMarkdown().ReplaceLineEndings("\n");

        if (Environment.GetEnvironmentVariable("UPDATE_ERD") == "1")
        {
            File.WriteAllText(path, expected);
            return;
        }

        File.Exists(path).Should().BeTrue(
            "DOCS/ERD.md should exist; create it with UPDATE_ERD=1 dotnet test --filter \"FullyQualifiedName~ErdTests\"");
        var actual = File.ReadAllText(path).ReplaceLineEndings("\n");
        // Compared as a bool: FluentAssertions formats string diffs as a format string, and the
        // Mermaid braces in the ERD make it throw a FormatException instead of this message.
        (actual == expected).Should().BeTrue(
            "DOCS/ERD.md is out of date with the EF Core model; regenerate it with UPDATE_ERD=1 dotnet test --filter \"FullyQualifiedName~ErdTests\"");
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "AmrGrandPrix.API")) &&
                Directory.Exists(Path.Combine(dir.FullName, "DOCS")))
                return dir.FullName;
        }
        throw new InvalidOperationException("Could not find the repository root from " + AppContext.BaseDirectory);
    }
}
