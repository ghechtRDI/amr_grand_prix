using AmrGrandPrix.API.Common;
using AmrGrandPrix.API.Models;
using FluentAssertions;

namespace AmrGrandPrix.API.Tests.Common;

public class AgeGradingTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(30)]
    [InlineData(44)]
    public void GradeTime_BelowMinimumAgeOrUnknown_ReturnsNull(int? age)
    {
        AgeGrading.GradeTime(TimeSpan.FromHours(1), age, Gender.Male).Should().BeNull();
    }

    [Fact]
    public void GradeTime_AtMinimumAge_AppliesFactor()
    {
        AgeGrading.GradeTime(TimeSpan.FromSeconds(1000), 45, Gender.Male)
            .Should().Be(TimeSpan.FromSeconds(930));
    }

    [Fact]
    public void GetFactor_InterpolatesBetweenAnchors()
    {
        AgeGrading.GetFactor(47, Gender.Male).Should().BeApproximately(0.916, 0.0001);
    }

    [Fact]
    public void GetFactor_DeclinesWithAge_AndClampsAtEnds()
    {
        foreach (var gender in new[] { Gender.Male, Gender.Female, Gender.Nonbinary })
        {
            var factors = Enumerable.Range(45, 60).Select(a => AgeGrading.GetFactor(a, gender)).ToList();
            factors.Should().BeInDescendingOrder();
            AgeGrading.GetFactor(20, gender).Should().Be(1.0);
            AgeGrading.GetFactor(110, gender).Should().Be(AgeGrading.GetFactor(95, gender));
        }
    }

    [Fact]
    public void GetFactor_Nonbinary_IsMidpointOfMaleAndFemale()
    {
        var expected = (AgeGrading.GetFactor(60, Gender.Male) + AgeGrading.GetFactor(60, Gender.Female)) / 2;
        AgeGrading.GetFactor(60, Gender.Nonbinary).Should().Be(expected);
    }
}
