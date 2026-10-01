namespace BankService.Application.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsInRole(string role);
    RequestActor Actor { get; }

    /// <summary>
    /// The single role that determines data visibility. A user may hold several
    /// roles at once, and taking whichever came first in the token would let the
    /// claim order decide what they can see, so the most privileged role wins.
    /// </summary>
    string PrimaryRole { get; }
}
