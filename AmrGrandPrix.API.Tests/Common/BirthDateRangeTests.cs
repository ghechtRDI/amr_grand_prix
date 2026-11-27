using AmrGrandPrix.API.Common;
using AmrGrandPrix.API.Models;
using FluentAssertions;

namespace AmrGrandPrix.API.Tests.Common;

public class BirthDateRangeTests
{
    private static readonly DateOnly RaceDate = new(2026, 6, 15);

    private static BirthDateRange Range(string? earliest, string? latest) =>
        new(earliest is null ? null : DateOnly.Parse(earliest), latest is null ? null : DateOnly.Parse(latest));

    [Fact]
    public void FromAge_CoversEveryBirthDateGivingThatAgeOnTheDate()
    {
        var range = BirthDateRange.FromAge(40, RaceDate);

        range.Should().Be(Range("1985-06-16", "1986-06-15"));
        AgeCalculator.CalculateAge(range.Earliest!.Value, RaceDate).Should().Be(40);
        AgeCalculator.CalculateAge(range.Latest!.Value, RaceDate).Should().Be(40);
        AgeCalculator.CalculateAge(range.Earliest.Value.AddDays(-1), RaceDate).Should().Be(41);
        AgeCalculator.CalculateAge(range.Latest.Value.AddDays(1), RaceDate).Should().Be(39);
    }

    [Theory]
    [InlineData("40-49", "1976-06-16", "1986-06-15")]
    [InlineData("17 and Under", "2008-06-16", "2026-06-15")]
    [InlineData("90+", null, "1936-06-15")]   // oldest category is open-ended
    [InlineData("  30-39 ", "1986-06-16", "1996-06-15")]
    public void FromAgeCategory_UsesCategoryBounds(string category, string? earliest, string latest) =>
        BirthDateRange.FromAgeCategory(category, RaceDate).Should().Be(Range(earliest, latest));

    [Theory]
    [InlineData(null)]
    [InlineData("Masters")]
    public void FromAgeCategory_UnrecognizedIsUnknown(string? category) =>
        BirthDateRange.FromAgeCategory(category, RaceDate).IsUnknown.Should().BeTrue();

    [Fact]
    public void FromResult_PrefersExactAgeOverCategory() =>
        BirthDateRange.FromResult(40, "60-69", RaceDate).Should().Be(BirthDateRange.FromAge(40, RaceDate));

    [Fact]
    public void Intersect_NarrowsAndTreatsNullBoundsAsOpen()
    {
        Range("1985-01-01", "1986-12-31").Intersect(Range("1985-06-16", "1986-06-15"))
            .Should().Be(Range("1985-06-16", "1986-06-15"));
        Range(null, "1990-01-01").Intersect(Range("1980-01-01", null))
            .Should().Be(Range("1980-01-01", "1990-01-01"));
        Range("1990-01-01", "1990-12-31").Intersect(Range("1980-01-01", "1980-12-31"))
            .Should().BeNull();
    }

    [Fact]
    public void TwoAgesAtDifferentTimesOfYear_NarrowTheRange()
    {
        // 40 in June 2026 and 40 in December 2026: born mid-December 1985 .. mid-June 1986.
        var june = BirthDateRange.FromAge(40, new DateOnly(2026, 6, 15));
        var december = BirthDateRange.FromAge(40, new DateOnly(2026, 12, 15));

        var combined = BirthDateRange.Combine(june, december, out var conflict);

        conflict.Should().BeFalse();
        combined.Should().Be(Range("1985-12-16", "1986-06-15"));
    }

    [Fact]
    public void Combine_KeepsCurrentAndFlagsConflictWhenEvidenceContradicts()
    {
        var current = BirthDateRange.FromAge(40, RaceDate);

        var combined = BirthDateRange.Combine(current, BirthDateRange.FromAge(50, RaceDate), out var conflict);

        conflict.Should().BeTrue();
        combined.Should().Be(current);
    }

    [Fact]
    public void Combine_UnknownSideTakesTheOther()
    {
        var known = BirthDateRange.FromAge(40, RaceDate);

        BirthDateRange.Combine(BirthDateRange.Unknown, known, out _).Should().Be(known);
        BirthDateRange.Combine(known, BirthDateRange.Unknown, out _).Should().Be(known);
    }

    [Fact]
    public void Estimate_IsTheMiddleOfANarrowRangeOnly()
    {
        var oneAge = BirthDateRange.FromAge(40, RaceDate);
        oneAge.IsNarrow.Should().BeTrue();
        oneAge.EstimatedDateOfBirth.Should().Be(new DateOnly(1985, 12, 15));
        oneAge.EstimatedYear.Should().Be(1985);

        var category = BirthDateRange.FromAgeCategory("40-49", RaceDate);
        category.IsNarrow.Should().BeFalse();
        category.EstimatedYear.Should().BeNull();

        BirthDateRange.Unknown.EstimatedYear.Should().BeNull();
    }

    [Fact]
    public void Runner_EstimatedBirthYearIsComputedFromTheRange()
    {
        var runner = new Runner { EstimatedBirthRange = BirthDateRange.FromAge(40, RaceDate) };

        runner.EstimatedDobEarliest.Should().Be(new DateOnly(1985, 6, 16));
        runner.EstimatedDobLatest.Should().Be(new DateOnly(1986, 6, 15));
        runner.EstimatedBirthYear.Should().Be(1985);
        AgeCalculator.GetRunnerAge(runner, RaceDate).Should().Be(40);
        AgeCalculator.GetRunnerAgeRange(runner, new DateOnly(2027, 1, 1)).Should().Be((40, 41));
        AgeCalculator.IsAgeEstimated(runner).Should().BeTrue();
    }

    [Fact]
    public void Runner_CategoryOnlyRangeHasNoEstimatedAge()
    {
        var runner = new Runner { EstimatedBirthRange = BirthDateRange.FromAgeCategory("40-49", RaceDate) };

        runner.EstimatedBirthYear.Should().BeNull();
        AgeCalculator.GetRunnerAge(runner, RaceDate).Should().BeNull();
        AgeCalculator.GetRunnerAgeRange(runner, RaceDate).Should().Be((40, 49));
        AgeCalculator.IsAgeEstimated(runner).Should().BeFalse();
    }
}
