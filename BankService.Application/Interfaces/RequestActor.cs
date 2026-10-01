namespace BankService.Application.Interfaces;

/// <summary>
/// Describes the acting user's effective permissions for a service request,
/// derived from their role plus their relationship to the specific request.
/// </summary>
public enum RequestActor
{
    /// <summary>Not authenticated. Treated as a non-privileged actor.</summary>
    Anonymous = 0,

    /// <summary>Administrator or manager: full visibility and control over every request.</summary>
    Manager = 1,

    /// <summary>Support agent: may read requests they are assigned to and progress their status.</summary>
    SupportAgent = 2,

    /// <summary>Employee: may read and manage only their own requests.</summary>
    Requester = 3
}
