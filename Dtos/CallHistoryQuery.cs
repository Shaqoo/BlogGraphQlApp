using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.DTOs
{
    public sealed record CallHistoryQuery(
        int Page = 1,
        int PageSize = 20,
        CallHistoryStatus? Status = null,
        CallType? CallType = null,
        DateTime? From = null,
        DateTime? To = null,
        string? Search = null);
}
