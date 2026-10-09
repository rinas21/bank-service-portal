namespace BankService.Application.Common;

public static class Pagination
{
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int page, int pageSize)
        => (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));
}
