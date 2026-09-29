using AmrGrandPrix.API.Controllers;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Services.RaceStatistics;
using AmrGrandPrix.API.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AmrGrandPrix.API.Tests.Controllers;

public class RaceSeriesControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly RaceSeriesController _controller;

    public RaceSeriesControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
        _controller = new RaceSeriesController(
            _context, Mock.Of<IRaceStatisticsService>(), Mock.Of<ILogger<RaceSeriesController>>());
    }

    public void Dispose() => _context.Database.EnsureDeleted();

    private Race AddRace(RaceVariant variant, int year)
    {
        var race = new Race { RaceId = Guid.NewGuid(), RaceVariant = variant, Year = year, Date = new DateOnly(year, 5, 22) };
        _context.Races.Add(race);
        return race;
    }

    [Fact]
    public async Task Create_WithNoVariants_CreatesSingleStandardVariant()
    {
        var result = await _controller.Create(new CreateRaceSeriesRequest { Name = "Crazy Lazy", IsGrandPrix = true });

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var variant = await _context.RaceVariants.Include(v => v.RaceSeries).SingleAsync();
        variant.Name.Should().Be(RaceVariant.StandardName);
        variant.IsGrandPrixByDefault.Should().BeTrue();
        variant.RaceSeries.Name.Should().Be("Crazy Lazy");
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsBadRequest()
    {
        await _controller.Create(new CreateRaceSeriesRequest { Name = "Crazy Lazy" });

        var result = await _controller.Create(new CreateRaceSeriesRequest { Name = "Crazy Lazy" });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateVariant_NameMatchingExistingAlias_ReturnsBadRequest()
    {
        var dome = TestData.Variant("Knoya", "Dome");
        dome.Aliases = ["Original"];
        _context.RaceVariants.Add(dome);
        await _context.SaveChangesAsync();

        var result = await _controller.CreateVariant(dome.RaceSeriesId, new RaceVariantRequest { Name = "original" });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task MergeVariant_MovesRacesAndKeepsSourceNameAsAlias()
    {
        var happyTrails = TestData.Variant("Knoya", "Happy Trails");
        var happyTrail = TestData.AddVariant(happyTrails.RaceSeries, "Happy Trail");
        AddRace(happyTrails, 2026);
        var race2025 = AddRace(happyTrail, 2025);
        await _context.SaveChangesAsync();

        var result = await _controller.MergeVariant(happyTrail.RaceVariantId, happyTrails.RaceVariantId);

        result.Should().BeOfType<OkObjectResult>();
        (await _context.Races.FindAsync(race2025.RaceId))!.RaceVariantId.Should().Be(happyTrails.RaceVariantId);
        (await _context.RaceVariants.AnyAsync(v => v.RaceVariantId == happyTrail.RaceVariantId)).Should().BeFalse();
        happyTrails.Aliases.Should().Contain("Happy Trail");
    }

    [Fact]
    public async Task MergeVariant_BothHaveRaceInSameYear_ReturnsBadRequestAndChangesNothing()
    {
        var dome = TestData.Variant("Knoya", "Dome");
        var original = TestData.AddVariant(dome.RaceSeries, "Original");
        AddRace(dome, 2025);
        var originalRace = AddRace(original, 2025);
        await _context.SaveChangesAsync();

        var result = await _controller.MergeVariant(original.RaceVariantId, dome.RaceVariantId);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _context.Races.FindAsync(originalRace.RaceId))!.RaceVariantId.Should().Be(original.RaceVariantId);
    }

    [Fact]
    public async Task Merge_FoldsSameNamedVariantsAndMovesTheRest()
    {
        var target = TestData.Variant("Blueberry Rampage", "Full Mountain");
        var source = TestData.Variant("Blueberry Rampage - Junior", "Junior Blueberry Knoll");
        var sourceFullMountain = TestData.AddVariant(source.RaceSeries, "Full Mountain");
        AddRace(target, 2026);
        var juniorRace = AddRace(source, 2026);
        var oldFullMountainRace = AddRace(sourceFullMountain, 2025);
        await _context.SaveChangesAsync();

        var result = await _controller.Merge(target.RaceSeriesId,
            new MergeRaceSeriesRequest { SourceSeriesIds = [source.RaceSeriesId] });

        result.Should().BeOfType<OkObjectResult>();
        (await _context.RaceSeries.CountAsync()).Should().Be(1);
        var variants = await _context.RaceVariants.ToListAsync();
        variants.Select(v => v.Name).Should().BeEquivalentTo("Full Mountain", "Junior Blueberry Knoll");
        variants.Should().OnlyContain(v => v.RaceSeriesId == target.RaceSeriesId);
        (await _context.Races.FindAsync(oldFullMountainRace.RaceId))!.RaceVariantId.Should().Be(target.RaceVariantId);
        (await _context.Races.FindAsync(juniorRace.RaceId))!.RaceVariantId.Should().Be(source.RaceVariantId);
    }
}
