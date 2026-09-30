using Microsoft.EntityFrameworkCore;
using MyFirstApi.Exceptions;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

// Validates references from a request to master-data rows.
// An inactive row is rejected only when newly assigned (id != currentId), so
// records that already point at a since-deactivated row can still be edited.
// "field" is the request property name (e.g. "categoryId"), so the frontend can
// highlight the right input; "scope" tags errors from batch requests.
public static class ReferenceResolver
{
    public static async Task<T?> ResolveAsync<T>(
        IQueryable<T> source, int? id, int? currentId, string field, ErrorScope? scope = null)
        where T : class, IMasterData
    {
        if (id == null) return null;

        var entity = await source.FirstOrDefaultAsync(e => e.Id == id.Value);
        return Check(entity, id.Value, currentId, field, scope);
    }

    public static async Task<T> ResolveRequiredAsync<T>(
        IQueryable<T> source, int id, int? currentId, string field, ErrorScope? scope = null)
        where T : class, IMasterData
    {
        return (await ResolveAsync(source, id, currentId, field, scope))!;
    }

    public static T Check<T>(T? entity, int id, int? currentId, string field, ErrorScope? scope)
        where T : class, IMasterData
    {
        if (entity == null)
        {
            throw Errors.ReferenceNotFound(field, id).In(scope);
        }
        if (!entity.IsActive && id != currentId)
        {
            throw Errors.ReferenceInactive(field, entity.Code).In(scope);
        }
        return entity;
    }
}
