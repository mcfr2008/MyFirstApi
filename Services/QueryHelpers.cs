using Microsoft.EntityFrameworkCore;
using MyFirstApi.Exceptions;
using Npgsql;

namespace MyFirstApi.Services;

public static class QueryHelpers
{
    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    public static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Wraps a search term for ILIKE, escaping its own wildcard characters.
    public static string ContainsPattern(string search) =>
        $"%{search.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";

    // Unique indexes are the real guard against duplicate codes; this turns a
    // violation that slipped past a service's pre-check (concurrent insert) into 409.
    public static async Task SaveChangesOrConflictAsync(this DbContext context, string conflictMessage)
    {
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException(conflictMessage);
        }
    }
}
