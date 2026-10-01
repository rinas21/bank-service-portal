using BankService.Application.Interfaces;
using BankService.Domain.Entities;
using BankService.Infrastructure.Data;
using BankService.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BankService.Tests.Unit;

public class RequestNumberServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly RequestNumberService _service;

    public RequestNumberServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _service = new RequestNumberService(_context);
    }

    private void Seed(params string[] requestNumbers)
    {
        var year = DateTime.UtcNow.Year;
        _context.ServiceRequests.AddRange(requestNumbers.Select((number, index) => new ServiceRequest
        {
            RequestNumber = number,
            Title = $"Request {index}",
            Description = "Description",
            Category = "General Inquiry",
            RequesterId = "user-1"
        }));
        _context.SaveChanges();
        _ = year;
    }

    [Fact]
    public async Task GenerateRequestNumberAsync_WithNoRequests_ReturnsFirstNumberOfYear()
    {
        var number = await _service.GenerateRequestNumberAsync();

        number.Should().Be($"SR-{DateTime.UtcNow.Year}-00001");
    }

    [Fact]
    public async Task GenerateRequestNumberAsync_ContinuesAfterHighestNumber()
    {
        Seed($"SR-{DateTime.UtcNow.Year}-00001", $"SR-{DateTime.UtcNow.Year}-00007", $"SR-{DateTime.UtcNow.Year}-00003");

        var number = await _service.GenerateRequestNumberAsync();

        number.Should().Be($"SR-{DateTime.UtcNow.Year}-00008");
    }

    [Fact]
    public async Task GenerateRequestNumberAsync_ComparesSequencesNumericallyNotAlphabetically()
    {
        // "0009" sorts after "00010" as a string, so a lexicographic max would hand out 00010 again.
        Seed($"SR-{DateTime.UtcNow.Year}-00009", $"SR-{DateTime.UtcNow.Year}-00010");

        var number = await _service.GenerateRequestNumberAsync();

        number.Should().Be($"SR-{DateTime.UtcNow.Year}-00011");
    }

    [Fact]
    public async Task GenerateRequestNumberAsync_IgnoresDisambiguatedNumbers()
    {
        // A suffixed number must never cause the counter to restart, which would
        // permanently reuse numbers that already exist.
        Seed(
            $"SR-{DateTime.UtcNow.Year}-00042",
            $"SR-{DateTime.UtcNow.Year}-00042-1a2b",
            $"SR-{DateTime.UtcNow.Year}-00042-3c4d");

        var number = await _service.GenerateRequestNumberAsync();

        number.Should().Be($"SR-{DateTime.UtcNow.Year}-00043");
    }

    [Fact]
    public async Task GenerateRequestNumberAsync_IgnoresPreviousYears()
    {
        Seed($"SR-{DateTime.UtcNow.Year - 1}-00999");

        var number = await _service.GenerateRequestNumberAsync();

        number.Should().Be($"SR-{DateTime.UtcNow.Year}-00001");
    }

    [Fact]
    public async Task GenerateRequestNumbersAsync_ReturnsDistinctSequentialNumbers()
    {
        Seed($"SR-{DateTime.UtcNow.Year}-00005");

        var numbers = await _service.GenerateRequestNumbersAsync(3);

        numbers.Should().Equal(
            $"SR-{DateTime.UtcNow.Year}-00006",
            $"SR-{DateTime.UtcNow.Year}-00007",
            $"SR-{DateTime.UtcNow.Year}-00008");
        numbers.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GenerateRequestNumbersAsync_WithZeroCount_ReturnsEmpty()
    {
        var numbers = await _service.GenerateRequestNumbersAsync(0);

        numbers.Should().BeEmpty();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
