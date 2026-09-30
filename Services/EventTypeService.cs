using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public class EventTypeService
    : MasterDataService<EventType, EventTypeRequest, EventTypeResponse, MasterDataQuery>, IEventTypeService
{
    private readonly IMasterDataCache _cache;

    public EventTypeService(AppDbContext context, IMasterDataCache cache) : base(context)
    {
        _cache = cache;
    }

    protected override void OnChanged() => _cache.Invalidate();

    protected override DbSet<EventType> Set => Context.EventTypes;

    protected override string EntityName => "Event type";

    protected override Expression<Func<EventType, EventTypeResponse>> Projection => e => new EventTypeResponse
    {
        Id = e.Id,
        Code = e.Code,
        NameTh = e.NameTh,
        NameEn = e.NameEn,
        Description = e.Description,
        ResultingStatus = e.ResultingStatus,
        IsTerminal = e.IsTerminal,
        RequiresReason = e.RequiresReason,
        SortOrder = e.SortOrder,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    protected override Expression<Func<EventType, bool>> MatchesSearch(string pattern) =>
        e => EF.Functions.ILike(e.Code, pattern) ||
             EF.Functions.ILike(e.NameTh, pattern) ||
             EF.Functions.ILike(e.NameEn, pattern);

    // Lifecycle order (picked up -> ... -> delivered), not alphabetical.
    protected override IOrderedQueryable<EventType> ApplyOrder(IQueryable<EventType> items) =>
        items.OrderBy(e => e.SortOrder).ThenBy(e => e.Code);

    protected override void Apply(EventType entity, EventTypeRequest request)
    {
        entity.NameTh = request.NameTh.Trim();
        entity.NameEn = request.NameEn.Trim();
        entity.Description = QueryHelpers.NullIfBlank(request.Description);
        entity.ResultingStatus = request.ResultingStatus;
        entity.IsTerminal = request.IsTerminal;
        entity.RequiresReason = request.RequiresReason;
        entity.SortOrder = request.SortOrder;
    }
}
