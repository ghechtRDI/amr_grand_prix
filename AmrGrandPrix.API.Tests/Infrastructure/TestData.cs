using AmrGrandPrix.API.Models;

namespace AmrGrandPrix.API.Tests.Infrastructure;

public static class TestData
{
    /// <summary>
    /// A new variant in a new series, for assigning to <see cref="Race.RaceVariant"/>; adding the
    /// race to the context adds the whole graph.
    /// </summary>
    public static RaceVariant Variant(string seriesName, string variantName = RaceVariant.StandardName, bool isGrandPrixByDefault = false)
    {
        var series = new RaceSeries { RaceSeriesId = Guid.NewGuid(), Name = seriesName };
        var variant = new RaceVariant
        {
            RaceVariantId = Guid.NewGuid(),
            RaceSeriesId = series.RaceSeriesId,
            RaceSeries = series,
            Name = variantName,
            IsGrandPrixByDefault = isGrandPrixByDefault
        };
        series.Variants.Add(variant);
        return variant;
    }

    /// <summary>Adds another variant to <paramref name="series"/>.</summary>
    public static RaceVariant AddVariant(RaceSeries series, string variantName, bool isGrandPrixByDefault = false)
    {
        var variant = new RaceVariant
        {
            RaceVariantId = Guid.NewGuid(),
            RaceSeriesId = series.RaceSeriesId,
            RaceSeries = series,
            Name = variantName,
            IsGrandPrixByDefault = isGrandPrixByDefault
        };
        series.Variants.Add(variant);
        return variant;
    }
}
