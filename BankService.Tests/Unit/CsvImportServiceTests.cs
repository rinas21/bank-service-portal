using System.Text;
using BankService.Application.Interfaces;
using BankService.Domain.Entities;
using BankService.Domain.Enums;
using BankService.Infrastructure.Data;
using BankService.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BankService.Tests.Unit;

public class CsvImportServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly CsvImportService _service;
    private readonly Mock<IRequestNumberService> _requestNumberServiceMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;

    public CsvImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _requestNumberServiceMock = new Mock<IRequestNumberService>();
        _requestNumberServiceMock
            .Setup(x => x.GenerateRequestNumberAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("SR-2026-00001");

        _currentUserMock = new Mock<ICurrentUserService>();
        _currentUserMock.Setup(x => x.UserId).Returns("user-1");
        _currentUserMock.Setup(x => x.UserName).Returns("test@example.com");

        _service = new CsvImportService(
            _context,
            _requestNumberServiceMock.Object,
            _currentUserMock.Object,
            NullLogger<CsvImportService>.Instance);

        SeedData();
    }

    private void SeedData()
    {
        _context.Branches.Add(new Branch { Id = 1, Code = "HQ01", Name = "Headquarters", City = "New York" });
        _context.Users.Add(new ApplicationUser
        {
            Id = "user-1",
            UserName = "requester@test.com",
            Email = "requester@test.com",
            FirstName = "John",
            LastName = "Doe"
        });
        _context.ServiceRequests.Add(new ServiceRequest
        {
            Id = 1,
            RequestNumber = "SR-2026-00001",
            Title = "Existing",
            Description = "Existing request",
            Category = "General",
            RequesterId = "user-1",
            LegacyReference = "LEGACY-001"
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task ImportAsync_WithValidCsv_ImportsRecords()
    {
        var csv = "LegacyRef,Title,Description,Category,Priority,Status,BranchCode,RequesterEmail,DueDate\n" +
                  "LEGACY-002,New Request,Description,General,High,Open,HQ01,requester@test.com,2026-12-31\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await _service.ImportAsync(stream, "user-1");

        result.Imported.Should().Be(1);
        result.Duplicates.Should().Be(0);
        result.Rejected.Should().Be(0);
    }

    [Fact]
    public async Task ImportAsync_WithDuplicateLegacyRef_SkipsDuplicate()
    {
        var csv = "LegacyRef,Title,Description,Category,Priority,Status,BranchCode,RequesterEmail,DueDate\n" +
                  "LEGACY-001,Duplicate Request,Description,General,High,Open,HQ01,requester@test.com,2026-12-31\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await _service.ImportAsync(stream, "user-1");

        result.Imported.Should().Be(0);
        result.Duplicates.Should().Be(1);
        result.Rejected.Should().Be(0);
    }

    [Fact]
    public async Task ImportAsync_WithInvalidEmail_RejectsRecord()
    {
        var csv = "LegacyRef,Title,Description,Category,Priority,Status,BranchCode,RequesterEmail,DueDate\n" +
                  "LEGACY-003,New Request,Description,General,High,Open,HQ01,nonexistent@test.com,2026-12-31\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await _service.ImportAsync(stream, "user-1");

        result.Imported.Should().Be(0);
        result.Duplicates.Should().Be(0);
        result.Rejected.Should().Be(1);
    }

    [Fact]
    public async Task ImportAsync_WithInvalidPriority_RejectsRecord()
    {
        var csv = "LegacyRef,Title,Description,Category,Priority,Status,BranchCode,RequesterEmail,DueDate\n" +
                  "LEGACY-004,New Request,Description,General,InvalidPriority,Open,HQ01,requester@test.com,2026-12-31\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await _service.ImportAsync(stream, "user-1");

        result.Imported.Should().Be(0);
        result.Rejected.Should().Be(1);
    }

    [Fact]
    public async Task ImportAsync_WithInvalidBranchCode_RejectsRecord()
    {
        var csv = "LegacyRef,Title,Description,Category,Priority,Status,BranchCode,RequesterEmail,DueDate\n" +
                  "LEGACY-005,New Request,Description,General,High,Open,INVALID,requester@test.com,2026-12-31\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await _service.ImportAsync(stream, "user-1");

        result.Imported.Should().Be(0);
        result.Rejected.Should().Be(1);
    }

    [Fact]
    public async Task ImportAsync_WithMixedRecords_ReportsCorrectly()
    {
        var csv = "LegacyRef,Title,Description,Category,Priority,Status,BranchCode,RequesterEmail,DueDate\n" +
                  "LEGACY-001,Duplicate,Description,General,High,Open,HQ01,requester@test.com,2026-12-31\n" +
                  "LEGACY-006,Valid Request,Description,General,High,Open,HQ01,requester@test.com,2026-12-31\n" +
                  "LEGACY-007,Invalid Email,Description,General,High,Open,HQ01,bademail@test.com,2026-12-31\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await _service.ImportAsync(stream, "user-1");

        result.TotalRecords.Should().Be(3);
        result.Imported.Should().Be(1);
        result.Duplicates.Should().Be(1);
        result.Rejected.Should().Be(1);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
