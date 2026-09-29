using AmrGrandPrix.API.Services.LlmExtraction.Providers;
using FluentAssertions;

namespace AmrGrandPrix.API.Tests.Services.LlmExtraction.Providers;

public class KnownVariantsHintTests
{
    [Fact]
    public void BuildPrefix_NullList_ReturnsEmptyString()
    {
        KnownVariantsHint.BuildPrefix(null).Should().BeEmpty();
    }

    [Fact]
    public void BuildPrefix_EmptyList_ReturnsEmptyString()
    {
        KnownVariantsHint.BuildPrefix(new List<string>()).Should().BeEmpty();
    }

    [Fact]
    public void BuildPrefix_WithVariants_ListsEachOneAndInstructsReuse()
    {
        var prefix = KnownVariantsHint.BuildPrefix(new[] { "Junior - Blueberry Knoll", "Adult - Young at Heart Blueberry Knoll" });

        prefix.Should().Contain("- Junior - Blueberry Knoll");
        prefix.Should().Contain("- Adult - Young at Heart Blueberry Knoll");
        prefix.Should().Contain("reuse one of these exact names");
    }

    [Fact]
    public void BuildPrefix_EndsWithSeparatorBeforeFileContent()
    {
        var prefix = KnownVariantsHint.BuildPrefix(new[] { "Standard" });

        prefix.TrimEnd().Should().EndWith("---");
    }

    [Fact]
    public void BuildPrefix_OnlyTheseVariants_SaysListIsComplete()
    {
        var prefix = KnownVariantsHint.BuildPrefix(new[] { "Adult", "Junior" }, onlyTheseVariants: true);

        prefix.Should().Contain("- Adult");
        prefix.Should().Contain("- Junior");
        prefix.Should().Contain("ONLY the following course variants");
        prefix.Should().Contain("never repeat the same set of results");
        prefix.Should().NotContain("reuse one of these exact names");
        prefix.TrimEnd().Should().EndWith("---");
    }

    [Fact]
    public void BuildPrefix_OnlyTheseVariantsButNoVariants_ReturnsEmptyString()
    {
        KnownVariantsHint.BuildPrefix(new List<string>(), onlyTheseVariants: true).Should().BeEmpty();
    }
}
