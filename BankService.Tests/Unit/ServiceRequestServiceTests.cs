using BankService.Application.DTOs;
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

public class ServiceRequestServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ServiceRequestService _service;
    private readonly Mock<IRequestNumberService> _requestNumberServiceMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;

    public ServiceRequestServiceTests()
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

        _service = new ServiceRequestService(
            _context,
            _requestNumberServiceMock.Object,
            _currentUserMock.Object,
            NullLogger<ServiceRequestService>.Instance);

        SeedData();
    }

    private void SeedData()
    {
        var branch = new Branch { Id = 1, Code = "HQ01", Name = "Headquarters", City = "New York" };
        _context.Branches.Add(branch);

        var requester = new ApplicationUser
        {
            Id = "user-1",
            UserName = "requester@test.com",
            Email = "requester@test.com",
            FirstName = "John",
            LastName = "Doe",
            BranchId = 1
        };
        var assignee = new ApplicationUser
        {
            Id = "user-2",
            UserName = "assignee@test.com",
            Email = "assignee@test.com",
            FirstName = "Jane",
            LastName = "Smith"
        };
        _context.Users.AddRange(requester, assignee);

        _context.ServiceRequests.AddRange(
            new ServiceRequest
            {
                Id = 1,
                RequestNumber = "SR-2026-00001",
                Title = "Test Request 1",
                Description = "Description 1",
                Category = "Technical Support",
                Status = RequestStatus.Open,
                Priority = RequestPriority.High,
                RequesterId = "user-1",
                BranchId = 1
            },
            new ServiceRequest
            {
                Id = 2,
                RequestNumber = "SR-2026-00002",
                Title = "Test Request 2",
                Description = "Description 2",
                Category = "Account Services",
                Status = RequestStatus.InProgress,
                Priority = RequestPriority.Critical,
                RequesterId = "user-1",
                AssignedToId = "user-2",
                BranchId = 1
            },
            new ServiceRequest
            {
                Id = 3,
                RequestNumber = "SR-2026-00003",
                Title = "Test Request 3",
                Description = "Description 3",
                Category = "Card Services",
                Status = RequestStatus.Resolved,
                Priority = RequestPriority.Low,
                RequesterId = "user-2",
                BranchId = 1
            }
        );
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetListAsync_WithNoFilters_ReturnsAllRequests()
    {
        var query = new ServiceRequestListQuery(null, null, null, null, null, null, null, null, null, false, 1, 10);
        var result = await _service.GetListAsync(query, "user-1", true);

        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetListAsync_WithStatusFilter_ReturnsFilteredRequests()
    {
        var query = new ServiceRequestListQuery(null, RequestStatus.Open, null, null, null, null, null, null, null, false, 1, 10);
        var result = await _service.GetListAsync(query, "user-1", true);

        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be(RequestStatus.Open);
    }

    [Fact]
    public async Task GetListAsync_WithSearchFilter_ReturnsMatchingRequests()
    {
        var query = new ServiceRequestListQuery("Test Request 1", null, null, null, null, null, null, null, null, false, 1, 10);
        var result = await _service.GetListAsync(query, "user-1", true);

        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("Test Request 1");
    }

    [Fact]
    public async Task GetListAsync_ForNonAdminUser_ReturnsOnlyOwnRequests()
    {
        var query = new ServiceRequestListQuery(null, null, null, null, null, null, null, null, null, false, 1, 10);
        var result = await _service.GetListAsync(query, "user-1", false);

        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsRequest()
    {
        var result = await _service.GetByIdAsync(1, "user-1", true);

        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.RequestNumber.Should().Be("SR-2026-00001");
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(999, "user-1", true);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesRequest()
    {
        var request = new CreateServiceRequestRequest(
            "New Request", "New Description", "General Inquiry", RequestPriority.Medium, 1, null);

        var result = await _service.CreateAsync(request, "user-1");

        result.Should().NotBeNull();
        result.Title.Should().Be("New Request");
        result.Status.Should().Be(RequestStatus.Open);
        result.RequestNumber.Should().Be("SR-2026-00001");
    }

    [Fact]
    public async Task UpdateStatusAsync_WithValidStatus_UpdatesStatus()
    {
        var request = new UpdateStatusRequest(RequestStatus.InProgress, "Starting work");

        var result = await _service.UpdateStatusAsync(1, request, "user-1", true);

        result.Should().NotBeNull();
        result!.Status.Should().Be(RequestStatus.InProgress);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithSameStatus_ThrowsException()
    {
        var request = new UpdateStatusRequest(RequestStatus.Open, "No change");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateStatusAsync(1, request, "user-1", true));
    }

    [Fact]
    public async Task AssignAsync_WithValidAssignee_AssignsRequest()
    {
        var request = new AssignRequest("user-2", "Please handle this");

        var result = await _service.AssignAsync(1, request, "user-1");

        result.Should().NotBeNull();
        result!.AssignedToId.Should().Be("user-2");
        result.Status.Should().Be(RequestStatus.InProgress);
    }

    [Fact]
    public async Task AddCommentAsync_WithValidComment_AddsComment()
    {
        var request = new AddCommentRequest("This is a comment", false);

        var result = await _service.AddCommentAsync(1, request, "user-1");

        result.Should().NotBeNull();
        result!.Comments.Should().Contain(c => c.Body == "This is a comment");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
