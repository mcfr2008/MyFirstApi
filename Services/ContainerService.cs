using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Exceptions;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

// Containers are reusable handling units (master data) plus load/unload/scan
// operations that record tracking events for the items inside.
public class ContainerService
    : MasterDataService<Container, ContainerRequest, ContainerResponse, ContainerQuery>, IContainerService
{
    private const string LoadedEventCode = "LOADED_INTO_CONTAINER";
    private const string UnloadedEventCode = "UNLOADED_FROM_CONTAINER";

    private readonly ITrackingEventRecorder _recorder;

    public ContainerService(AppDbContext context, ITrackingEventRecorder recorder) : base(context)
    {
        _recorder = recorder;
    }

    protected override DbSet<Container> Set => Context.Containers;

    protected override string EntityName => "Container";

    protected override Expression<Func<Container, ContainerResponse>> Projection => c => new ContainerResponse
    {
        Id = c.Id,
        Code = c.Code,
        Type = c.Type,
        SealNumber = c.SealNumber,
        ParentContainer = c.ParentContainer == null
            ? null
            : new ReferenceSummary(c.ParentContainer.Id, c.ParentContainer.Code, c.ParentContainer.Type.ToString()),
        CurrentLocation = c.CurrentLocation == null
            ? null
            : new ReferenceSummary(c.CurrentLocation.Id, c.CurrentLocation.Code, c.CurrentLocation.Name),
        MaxPayloadKg = c.MaxPayloadKg,
        ItemCount = Context.TrackedItems.Count(i => i.CurrentContainerId == c.Id && !i.IsArchived),
        ChildContainerCount = Context.Containers.Count(child => child.ParentContainerId == c.Id),
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };

    protected override Expression<Func<Container, bool>> MatchesSearch(string pattern) =>
        c => EF.Functions.ILike(c.Code, pattern) ||
             (c.SealNumber != null && EF.Functions.ILike(c.SealNumber, pattern));

    protected override IQueryable<Container> ApplyFilters(IQueryable<Container> items, ContainerQuery query)
    {
        if (query.Type.HasValue)
        {
            items = items.Where(c => c.Type == query.Type.Value);
        }
        if (query.ParentContainerId.HasValue)
        {
            items = items.Where(c => c.ParentContainerId == query.ParentContainerId);
        }
        if (query.CurrentLocationId.HasValue)
        {
            items = items.Where(c => c.CurrentLocationId == query.CurrentLocationId);
        }
        return items;
    }

    protected override async Task ValidateAsync(Container entity, ContainerRequest request)
    {
        var code = QueryHelpers.NormalizeCode(request.Code);
        if (Container.IsIsoContainer(request.Type!.Value) && !Iso6346.IsValid(code))
        {
            throw Errors.InvalidIso6346(code);
        }

        await ReferenceResolver.ResolveAsync(
            Context.Locations.AsNoTracking(), request.CurrentLocationId, entity.CurrentLocationId, "currentLocationId");

        var parent = await ReferenceResolver.ResolveAsync(
            Context.Containers.AsNoTracking(), request.ParentContainerId, entity.ParentContainerId, "parentContainerId");
        if (parent != null && entity.Id != 0)
        {
            await EnsureNoCycleAsync(entity.Id, parent.Id);
        }
    }

    protected override void Apply(Container entity, ContainerRequest request)
    {
        entity.Type = request.Type!.Value;
        entity.SealNumber = QueryHelpers.NullIfBlank(request.SealNumber)?.ToUpperInvariant();
        entity.ParentContainerId = request.ParentContainerId;
        entity.CurrentLocationId = request.CurrentLocationId;
        entity.MaxPayloadKg = request.MaxPayloadKg;
    }

    public async Task<ContainerContentsResponse?> GetContentsAsync(int id)
    {
        if (!await Set.AnyAsync(c => c.Id == id)) return null;

        var treeIds = await _recorder.GetContainerTreeIdsAsync(id);
        var containers = await Set.AsNoTracking()
            .Where(c => treeIds.Contains(c.Id))
            .Select(Projection)
            .ToDictionaryAsync(c => c.Id);
        var parentIds = await Set.AsNoTracking()
            .Where(c => treeIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.ParentContainerId);
        var items = await Context.TrackedItems.AsNoTracking()
            .Include(i => i.Category)
            .Include(i => i.CurrentLocation)
            .Include(i => i.OwnerParty)
            .Include(i => i.CurrentContainer)
            .Where(i => i.CurrentContainerId != null && treeIds.Contains(i.CurrentContainerId.Value) && !i.IsArchived)
            .OrderBy(i => i.TagCode)
            .ToListAsync();

        ContainerContentsResponse Build(int containerId)
        {
            var node = new ContainerContentsResponse
            {
                Container = containers[containerId],
                Items = items.Where(i => i.CurrentContainerId == containerId).Select(TrackedItemResponse.From).ToList(),
                ChildContainers = parentIds.Where(p => p.Value == containerId).Select(p => Build(p.Key)).ToList()
            };
            node.TotalItemCount = node.Items.Count + node.ChildContainers.Sum(c => c.TotalItemCount);
            return node;
        }

        return Build(id);
    }

    public async Task<EventsRecordedResponse?> LoadAsync(int id, ContainerLoadRequest request)
    {
        var container = await Set.FindAsync(id);
        if (container == null) return null;
        if (!container.IsActive)
        {
            throw Errors.ContainerInactive(container.Code);
        }

        foreach (var child in await FindChildContainersAsync(request.ChildContainerIds))
        {
            if (child.Id == id)
            {
                throw Errors.ContainerIntoItself();
            }
            await EnsureNoCycleAsync(child.Id, id);
            child.ParentContainerId = id;
            child.CurrentLocationId = container.CurrentLocationId;
            child.UpdatedAt = DateTime.UtcNow;
        }

        var recorded = 0;
        if (request.TrackedItemIds?.Count > 0 || request.TagCodes?.Count > 0)
        {
            var items = await _recorder.FindItemsAsync(request.TrackedItemIds, request.TagCodes);
            var alreadyElsewhere = items
                .Where(i => i.CurrentContainerId != null && i.CurrentContainerId != id)
                .Select(i => i.TagCode)
                .ToList();
            if (alreadyElsewhere.Count > 0)
            {
                throw Errors.ItemsInOtherContainer(alreadyElsewhere);
            }

            foreach (var item in items)
            {
                item.CurrentContainerId = id;
            }
            var eventType = await _recorder.GetEventTypeAsync(LoadedEventCode);
            await _recorder.AddEventsAsync(items, eventType,
                new EventContext(container.CurrentLocationId, DateTime.UtcNow, EventSource.Container,
                    ContainerId: id, IsSystemManaged: true));
            recorded = items.Count;
        }

        await Context.SaveChangesAsync();
        return new EventsRecordedResponse(LoadedEventCode, recorded);
    }

    public async Task<EventsRecordedResponse?> UnloadAsync(int id, ContainerUnloadRequest request)
    {
        var container = await Set.FindAsync(id);
        if (container == null) return null;

        var unloadAll = !(request.TrackedItemIds?.Count > 0 || request.TagCodes?.Count > 0 || request.ChildContainerIds?.Count > 0);

        List<TrackedItem> items;
        List<Container> children;
        if (unloadAll)
        {
            items = await Context.TrackedItems.Where(i => i.CurrentContainerId == id).ToListAsync();
            children = await Set.Where(c => c.ParentContainerId == id).ToListAsync();
        }
        else
        {
            items = request.TrackedItemIds?.Count > 0 || request.TagCodes?.Count > 0
                ? await _recorder.FindItemsAsync(request.TrackedItemIds, request.TagCodes)
                : [];
            children = await FindChildContainersAsync(request.ChildContainerIds);

            var notInside = items.Where(i => i.CurrentContainerId != id).Select(i => i.TagCode)
                .Concat(children.Where(c => c.ParentContainerId != id).Select(c => c.Code))
                .ToList();
            if (notInside.Count > 0)
            {
                throw Errors.NotInContainer(container.Code, notInside);
            }
        }

        if (items.Count == 0 && children.Count == 0)
        {
            throw Errors.ContainerEmpty(container.Code);
        }

        foreach (var item in items)
        {
            item.CurrentContainerId = null;
        }
        foreach (var child in children)
        {
            child.ParentContainerId = null;
            child.UpdatedAt = DateTime.UtcNow;
        }

        // Archived items are taken out too, but only active items get a history entry.
        var trackable = items.Where(i => !i.IsArchived).ToList();
        if (trackable.Count > 0)
        {
            var eventType = await _recorder.GetEventTypeAsync(UnloadedEventCode);
            await _recorder.AddEventsAsync(trackable, eventType,
                new EventContext(container.CurrentLocationId, DateTime.UtcNow, EventSource.Container, request.Note,
                    ContainerId: id, IsSystemManaged: true));
        }

        await Context.SaveChangesAsync();
        return new EventsRecordedResponse(UnloadedEventCode, trackable.Count);
    }

    public async Task<EventsRecordedResponse?> ScanAsync(int id, EventDetails request)
    {
        var container = await Set.FindAsync(id);
        if (container == null) return null;

        var items = await _recorder.FindItemsInContainersAsync([id]);
        if (items.Count == 0)
        {
            throw Errors.ContainerEmpty(container.Code);
        }

        var eventType = await _recorder.GetEventTypeAsync(request.EventTypeCode);
        var context = new EventContext(
            request.LocationId,
            request.OccurredAt?.UtcDateTime ?? DateTime.UtcNow,
            EventSource.Container,
            request.Note,
            request.Latitude,
            request.Longitude,
            ContainerId: id,
            ReasonCode: request.ReasonCode);
        var events = await _recorder.AddEventsAsync(items, eventType, context);

        // The container (and everything nested in it) moves with its contents.
        if (request.LocationId.HasValue)
        {
            var treeIds = await _recorder.GetContainerTreeIdsAsync(id);
            foreach (var nested in await Set.Where(c => treeIds.Contains(c.Id)).ToListAsync())
            {
                nested.CurrentLocationId = request.LocationId;
                nested.UpdatedAt = DateTime.UtcNow;
            }
        }

        await Context.SaveChangesAsync();
        return new EventsRecordedResponse(eventType.Code, items.Count, events.Count(e => e.OffRouteShipmentId != null));
    }

    private async Task<List<Container>> FindChildContainersAsync(List<int>? ids)
    {
        if (ids == null || ids.Count == 0) return [];

        var distinct = ids.Distinct().ToList();
        var children = await Set.Where(c => distinct.Contains(c.Id)).ToListAsync();
        var missing = distinct.Except(children.Select(c => c.Id)).ToList();
        if (missing.Count > 0)
        {
            throw Errors.ContainersNotFound(missing);
        }
        return children;
    }

    // Putting containerId inside newParentId must not make a loop
    // (i.e. newParentId must not already be nested inside containerId).
    private async Task EnsureNoCycleAsync(int containerId, int newParentId)
    {
        var descendants = await _recorder.GetContainerTreeIdsAsync(containerId);
        if (descendants.Contains(newParentId))
        {
            throw Errors.ContainerCycle();
        }
    }
}
