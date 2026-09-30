using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Exceptions;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

// CRUD shared by every master-data table. A concrete service only describes
// its table: which DbSet, how to search/filter/sort it, how to copy a request
// onto the entity, and how to project the entity into a response.
public abstract class MasterDataService<TEntity, TRequest, TResponse, TQuery>
    : IMasterDataService<TRequest, TResponse, TQuery>
    where TEntity : class, IMasterData, new()
    where TRequest : MasterDataRequest
    where TResponse : IMasterDataResponse
    where TQuery : MasterDataQuery
{
    protected readonly AppDbContext Context;

    protected MasterDataService(AppDbContext context)
    {
        Context = context;
    }

    protected abstract DbSet<TEntity> Set { get; }

    // Used in error messages, e.g. "Location code already exists".
    protected abstract string EntityName { get; }

    protected abstract Expression<Func<TEntity, TResponse>> Projection { get; }

    // Search predicate for an ILIKE pattern (see QueryHelpers.ContainsPattern).
    protected abstract Expression<Func<TEntity, bool>> MatchesSearch(string pattern);

    protected abstract void Apply(TEntity entity, TRequest request);

    // Async checks (references, cross-field rules) run before Apply, so on update
    // the entity still holds its previous values for comparison.
    protected virtual Task ValidateAsync(TEntity entity, TRequest request) => Task.CompletedTask;

    // Runs after every successful create/update/activate/deactivate (e.g. to clear a cache).
    protected virtual void OnChanged()
    {
    }

    protected virtual IQueryable<TEntity> ApplyFilters(IQueryable<TEntity> items, TQuery query) => items;

    protected virtual IOrderedQueryable<TEntity> ApplyOrder(IQueryable<TEntity> items) => items.OrderBy(e => e.Code);

    public async Task<PagedResult<TResponse>> GetAllAsync(TQuery query)
    {
        var items = Set.AsNoTracking();

        if (!query.IncludeInactive)
        {
            items = items.Where(e => e.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            items = items.Where(MatchesSearch(QueryHelpers.ContainsPattern(query.Search)));
        }

        items = ApplyFilters(items, query);

        var totalCount = await items.CountAsync();
        var page = await ApplyOrder(items)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(Projection)
            .ToListAsync();

        return new PagedResult<TResponse>
        {
            Items = page,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public Task<TResponse?> GetByIdAsync(int id) =>
        Set.AsNoTracking().Where(e => e.Id == id).Select(Projection).FirstOrDefaultAsync();

    public Task<TResponse?> GetByCodeAsync(string code)
    {
        var normalized = QueryHelpers.NormalizeCode(code);
        return Set.AsNoTracking().Where(e => e.Code == normalized).Select(Projection).FirstOrDefaultAsync();
    }

    public async Task<TResponse> CreateAsync(TRequest request)
    {
        var code = QueryHelpers.NormalizeCode(request.Code);
        await EnsureCodeAvailableAsync(code);

        var now = DateTime.UtcNow;
        var entity = new TEntity();
        entity.Code = code;
        entity.IsActive = true;
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        await ValidateAsync(entity, request);
        Apply(entity, request);

        Set.Add(entity);
        await Context.SaveChangesOrConflictAsync(CodeExistsMessage(code));
        OnChanged();

        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<TResponse?> UpdateAsync(int id, TRequest request)
    {
        var entity = await Set.FindAsync(id);
        if (entity == null) return default;

        var code = QueryHelpers.NormalizeCode(request.Code);
        if (code != entity.Code)
        {
            await EnsureCodeAvailableAsync(code);
        }

        await ValidateAsync(entity, request);
        entity.Code = code;
        Apply(entity, request);
        entity.UpdatedAt = DateTime.UtcNow;

        await Context.SaveChangesOrConflictAsync(CodeExistsMessage(code));
        OnChanged();
        return await GetByIdAsync(id);
    }

    // Deactivated rows stay referenced by existing data but can't be newly assigned.
    public async Task<bool> SetActiveAsync(int id, bool isActive)
    {
        var entity = await Set.FindAsync(id);
        if (entity == null) return false;

        if (entity.IsActive != isActive)
        {
            entity.IsActive = isActive;
            entity.UpdatedAt = DateTime.UtcNow;
            await Context.SaveChangesAsync();
            OnChanged();
        }
        return true;
    }

    private async Task EnsureCodeAvailableAsync(string code)
    {
        if (await Set.AnyAsync(e => e.Code == code))
        {
            throw new ConflictException(CodeExistsMessage(code));
        }
    }

    private string CodeExistsMessage(string code) => $"{EntityName} code already exists: {code}";
}
