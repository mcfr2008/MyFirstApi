using System.ComponentModel.DataAnnotations;

namespace MyFirstApi.Dtos;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

// Implemented by master-data responses so shared controllers can read the id.
public interface IMasterDataResponse
{
    int Id { get; }
}

// Keyset ("cursor") paging for large, append-heavy lists (tracking events):
// pass NextCursor back as ?cursor= to get the next page. Unlike page numbers it
// stays fast however deep you go, and new rows don't shift pages.
public class CursorPagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
    public string? NextCursor { get; set; }
    // Only filled when requested (includeTotalCount=true): counting millions of rows is slow.
    public int? TotalCount { get; set; }
}

// Compact reference to a master-data row, embedded in other responses.
public record ReferenceSummary(int Id, string Code, string Name);

public static class CodeRules
{
    public const string Pattern = @"^[A-Za-z0-9._-]{2,50}$";
    public const string ErrorMessage = "Code must be 2-50 characters: letters, digits, '.', '_' or '-'.";
}

public static class IsoRules
{
    public const string CountryPattern = "^[A-Za-z]{2}$";
    public const string CountryMessage = "Must be a 2-letter ISO 3166-1 country code.";
    public const string CurrencyPattern = "^[A-Za-z]{3}$";
    public const string CurrencyMessage = "Must be a 3-letter ISO 4217 currency code.";
}

// Compact event type reference embedded in event responses.
public record EventTypeSummary(int Id, string Code, string NameTh, string NameEn);

public record ReasonSummary(int Id, string Code, string NameTh, string NameEn);

public class PagedQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public abstract class MasterDataRequest
{
    // Stored upper-case; unique within its table.
    [Required]
    [RegularExpression(CodeRules.Pattern, ErrorMessage = CodeRules.ErrorMessage)]
    public string Code { get; set; } = string.Empty;
}

public class MasterDataQuery : PagedQuery
{
    // Matches code or name (case-insensitive, partial).
    public string? Search { get; set; }
    public bool IncludeInactive { get; set; }
}
