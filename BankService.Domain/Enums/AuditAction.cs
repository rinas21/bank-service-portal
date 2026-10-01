namespace BankService.Domain.Enums;

public enum AuditAction
{
    UserCreated,
    UserUpdated,
    UserDeactivated,
    BranchCreated,
    BranchUpdated,
    RequestCreated,
    RequestUpdated,
    RequestAssigned,
    StatusChanged,
    CommentAdded,
    ApprovalRequested,
    ApprovalGranted,
    ApprovalRejected,
    CsvImport,
    Login,
    LoginFailed
}
