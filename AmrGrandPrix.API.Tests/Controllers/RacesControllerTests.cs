using AmrGrandPrix.API.Controllers;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs;
using AmrGrandPrix.API.Services.GrandPrix;
using AmrGrandPrix.API.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AmrGrandPrix.API.Tests.Controllers;

public class RacesControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly RacesController _controller;

    public RacesControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
        var gp = new GrandPrixCalculationService(_context, Mock.Of<ILogger<GrandPrixCalculationService>>());
        _controller = new RacesController(_context, gp, Mock.Of<ILogger<RacesController>>());
    }

    public void Dispose() => _context.Database.EnsureDeleted();

    [Fact]
    public async Task CreateRace_DefaultsGrandPrixFlagFromVariant()
    {
        var fullMonty = TestData.Variant("Knoya", "Full Monty", isGrandPrixByDefault: true);
        var dome = TestData.AddVariant(fullMonty.RaceSeries, "Dome");
        _context.RaceVariants.AddRange(fullMonty, dome);
        await _context.SaveChangesAsync();

        var gpResult = await _controller.CreateRace(new CreateRaceRequest { RaceVariantId = fullMonty.RaceVariantId, Date = new DateOnly(2026, 5, 21) });
        var domeResult = await _controller.CreateRace(new CreateRaceRequest { RaceVariantId = dome.RaceVariantId, Date = new DateOnly(2026, 5, 21) });

        var gpRace = gpResult.Result.Should().BeOfType<CreatedAtActionResult>().Subject.Value.Should().BeOfType<RaceDto>().Subject;
        gpRace.IsGrandPrixRace.Should().BeTrue();
        gpRace.Name.Should().Be("Knoya");
        gpRace.CourseVariant.Should().Be("Full Monty");
        ((RaceDto)((CreatedAtActionResult)domeResult.Result!).Value!).IsGrandPrixRace.Should().BeFalse();
    }

    [Fact]
    public async Task CreateRace_SecondRaceForSameVariantAndYear_ReturnsBadRequest()
    {
        var variant = TestData.Variant("Crazy Lazy");
        _context.RaceVariants.Add(variant);
        await _context.SaveChangesAsync();
        await _controller.CreateRace(new CreateRaceRequest { RaceVariantId = variant.RaceVariantId, Date = new DateOnly(2026, 3, 14) });

        var result = await _controller.CreateRace(new CreateRaceRequest { RaceVariantId = variant.RaceVariantId, Date = new DateOnly(2026, 3, 21) });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateRace_SingleVariantSeries_HasNoCourseVariantLabel()
    {
        var variant = TestData.Variant("Crazy Lazy");
        _context.RaceVariants.Add(variant);
        await _context.SaveChangesAsync();

        var result = await _controller.CreateRace(new CreateRaceRequest { RaceVariantId = variant.RaceVariantId, Date = new DateOnly(2026, 3, 14) });

        ((RaceDto)((CreatedAtActionResult)result.Result!).Value!).CourseVariant.Should().BeNull();
    }
}
