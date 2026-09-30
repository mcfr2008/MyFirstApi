using Microsoft.EntityFrameworkCore;
using MyFirstApi.Exceptions;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

// Validates references from a request to master-data rows.
// An inactive row is rejected only when newly assigned (id != currentId), so
// records that already point at a since-deactivated row can still be edited.
public static class ReferenceResolver
{
    public static async Task<T?> ResolveAsync<T>(
        IQueryable<T> source, int? id, int? currentId, string label, string errorPrefix = "")
        where T : class, IMasterData
    {
        if (id == null) return null;

        var entity = await source.FirstOrDefaultAsync(e => e.Id == id.Value);
        return Check(entity, id.Value, currentId, label, errorPrefix);
    }

    public static async Task<T> ResolveRequiredAsync<T>(
        IQueryable<T> source, int id, int? currentId, string label, string errorPrefix = "")
        where T : class, IMasterData
    {
        return (await ResolveAsync(source, id, currentId, label, errorPrefix))!;
    }

    public static T Check<T>(T? entity, int id, int? currentId, string label, string errorPrefix)
        where T : class, IMasterData
    {
        if (entity == null)
        {
            throw new BusinessRuleException($"{errorPrefix}{label} {id} does not exist.");
        }
        if (!entity.IsActive && id != currentId)
        {
            throw new BusinessRuleException($"{errorPrefix}{label} {entity.Code} is inactive.");
        }
        return entity;
    }
}
