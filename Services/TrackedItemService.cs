using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Exceptions;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public class TrackedItemService : ITrackedItemService
{
    // No 0/O or 1/I so generated codes are easy to read off a label.
    private const string TagCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const string TagCodePrefix = "TT-";

    // Recorded automatically when an item is created, if configured in EventTypes.
    private const string RegisteredEventCode = "REGISTERED";

    private readonly AppDbContext _context;
    private readonly ITrackingEventRecorder _recorder;
    private readonly ICurrentUser _currentUser;
    private readonly IMasterDataCache _cache;

    public TrackedItemService(
        AppDbContext context, ITrackingEventRecorder recorder, ICurrentUser currentUser, IMasterDataCache cache)
    {
        _context = context;
        _recorder = recorder;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<PagedResult<TrackedItemResponse>> GetItemsAsync(TrackedItemQuery query)
    {
        var items = ItemsWithReferences().AsNoTracking();

        if (!query.IncludeArchived)
        {
            items = items.Where(i => !i.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = QueryHelpers.ContainsPattern(query.Search);
            items = items.Where(i =>
                EF.Functions.ILike(i.TagCode, pattern) ||
                EF.Functions.ILike(i.Name, pattern) ||
                (i.Description != null && EF.Functions.ILike(i.Description, pattern)));
        }

        if (query.CategoryId.HasValue)
        {
            items = items.Where(i => i.CategoryId == query.CategoryId);
        }

        if (query.CurrentLocationId.HasValue)
        {
            items = items.Where(i => i.CurrentLocationId == query.CurrentLocationId);
        }

        if (query.OwnerPartyId.HasValue)
        {
            items = items.Where(i => i.OwnerPartyId == query.OwnerPartyId);
        }

        if (query.CurrentContainerId.HasValue)
        {
            items = items.Where(i => i.CurrentContainerId == query.CurrentContainerId);
        }

        if (query.Status.HasValue)
        {
            items = items.Where(i => i.Status == query.Status.Value);
        }

        var totalCount = await items.CountAsync();
        var page = await items
            .OrderByDescending(i => i.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<TrackedItemResponse>
        {
            Items = page.Select(TrackedItemResponse.From).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<TrackedItemResponse?> GetItemByIdAsync(int id)
    {
        var item = await ItemsWithReferences().AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        return item == null ? null : TrackedItemResponse.From(item);
    }

    public async Task<TrackedItemResponse?> GetItemByTagCodeAsync(string tagCode)
    {
        var normalized = QueryHelpers.NormalizeCode(tagCode);
        var item = await ItemsWithReferences().AsNoTracking().FirstOrDefaultAsync(i => i.TagCode == normalized);
        return item == null ? null : TrackedItemResponse.From(item);
    }

    public async Task<TrackedItemResponse> CreateItemAsync(CreateTrackedItemRequest request)
    {
        var created = await CreateItemsAsync([request]);
        return created[0];
    }

    // All-or-nothing: one SaveChanges call, so a single bad item rejects the whole batch.
    public async Task<IReadOnlyList<TrackedItemResponse>> CreateItemsAsync(IReadOnlyList<CreateTrackedItemRequest> requests)
    {
        var suppliedCodes = requests
            .Where(r => !string.IsNullOrWhiteSpace(r.TagCode))
            .Select(r => QueryHelpers.NormalizeCode(r.TagCode!))
            .ToList();

        var duplicatesInRequest = suppliedCodes
            .GroupBy(c => c)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicatesInRequest.Count > 0)
        {
            throw new ConflictException(
                $"Tag codes appear more than once in the request: {string.Join(", ", duplicatesInRequest)}");
        }

        var existing = await _context.TrackedItems
            .Where(i => suppliedCodes.Contains(i.TagCode))
            .Select(i => i.TagCode)
            .ToListAsync();
        if (existing.Count > 0)
        {
            throw new ConflictException($"Tag codes already exist: {string.Join(", ", existing)}");
        }

        var references = await LoadReferencesAsync(requests);
        var usedCodes = new HashSet<string>(suppliedCodes);
        var now = DateTime.UtcNow;
        var items = new List<TrackedItem>(requests.Count);

        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            var errorPrefix = requests.Count > 1 ? $"Item {index + 1}: " : string.Empty;

            var item = new TrackedItem
            {
                TagCode = string.IsNullOrWhiteSpace(request.TagCode)
                    ? await GenerateUniqueTagCodeAsync(usedCodes)
                    : QueryHelpers.NormalizeCode(request.TagCode),
                Status = ItemStatus.Registered,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = _currentUser.Username
            };
            ApplyFields(item, request);
            AssignReferences(item, request, request.CurrentLocationId, references, errorPrefix);
            items.Add(item);
        }

        _context.TrackedItems.AddRange(items);
        await RecordRegisteredEventsAsync(items, now);
        await _context.SaveChangesOrConflictAsync("Tag code already exists.");

        return items.Select(TrackedItemResponse.From).ToList();
    }

    public async Task<TrackedItemResponse?> UpdateItemAsync(int id, UpdateTrackedItemRequest request)
    {
        var item = await ItemsWithReferences().FirstOrDefaultAsync(i => i.Id == id);
        if (item == null) return null;
        EnsureNotArchived(item);

        var tagCode = QueryHelpers.NormalizeCode(request.TagCode);
        if (tagCode != item.TagCode &&
            await _context.TrackedItems.AnyAsync(i => i.TagCode == tagCode))
        {
            throw new ConflictException($"Tag code already exists: {tagCode}");
        }

        var references = await LoadReferencesAsync([request]);

        item.TagCode = tagCode;
        ApplyFields(item, request);
        AssignReferences(item, request, null, references, string.Empty);
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesOrConflictAsync($"Tag code already exists: {tagCode}");
        return TrackedItemResponse.From(item);
    }

    public Task<bool> ArchiveItemAsync(int id) => SetArchivedAsync(id, true);

    public Task<bool> RestoreItemAsync(int id) => SetArchivedAsync(id, false);

    private IQueryable<TrackedItem> ItemsWithReferences() =>
        _context.TrackedItems
            .Include(i => i.Category)
            .Include(i => i.CurrentLocation)
            .Include(i => i.OwnerParty)
            .Include(i => i.CurrentContainer);

    private async Task<bool> SetArchivedAsync(int id, bool archived)
    {
        var item = await _context.TrackedItems.FindAsync(id);
        if (item == null) return false;

        if (item.IsArchived != archived)
        {
            item.IsArchived = archived;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        return true;
    }

    private static void ApplyFields(TrackedItem item, TrackedItemFields fields)
    {
        item.Name = fields.Name.Trim();
        item.Description = QueryHelpers.NullIfBlank(fields.Description);
        item.WeightKg = fields.WeightKg;
        item.LengthCm = fields.LengthCm;
        item.WidthCm = fields.WidthCm;
        item.HeightCm = fields.HeightCm;
        item.DeclaredValue = fields.DeclaredValue;
        item.Currency = fields.DeclaredValue.HasValue
            ? QueryHelpers.NullIfBlank(fields.Currency)?.ToUpperInvariant() ?? "THB"
            : null;
        item.HsCode = QueryHelpers.NullIfBlank(fields.HsCode);
        item.OriginCountry = QueryHelpers.NullIfBlank(fields.OriginCountry)?.ToUpperInvariant();
        item.Attributes = fields.Attributes ?? new Dictionary<string, string>();
    }

    private record ReferenceLookup(
        Dictionary<int, ItemCategory> Categories,
        Dictionary<int, Location> Locations,
        Dictionary<int, Party> Parties);

    // One query per master table for the whole batch, instead of one per item.
    private async Task<ReferenceLookup> LoadReferencesAsync(IEnumerable<TrackedItemFields> requests)
    {
        var list = requests.ToList();
        var categoryIds = list.Where(r => r.CategoryId.HasValue).Select(r => r.CategoryId!.Value).Distinct().ToList();
        var locationIds = list.OfType<CreateTrackedItemRequest>()
            .Where(r => r.CurrentLocationId.HasValue).Select(r => r.CurrentLocationId!.Value).Distinct().ToList();
        var partyIds = list.Where(r => r.OwnerPartyId.HasValue).Select(r => r.OwnerPartyId!.Value).Distinct().ToList();

        return new ReferenceLookup(
            await _context.ItemCategories.Where(c => categoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id),
            await _context.Locations.Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id),
            await _context.Parties.Where(p => partyIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id));
    }

    private static void AssignReferences(
        TrackedItem item, TrackedItemFields fields, int? locationId, ReferenceLookup references, string errorPrefix)
    {
        item.Category = Resolve(references.Categories, fields.CategoryId, item.CategoryId, "Category", errorPrefix);
        item.CategoryId = item.Category?.Id;

        // The location is only set at creation; later it changes through tracking events.
        if (item.Id == 0)
        {
            item.CurrentLocation = Resolve(references.Locations, locationId, item.CurrentLocationId, "Location", errorPrefix);
            item.CurrentLocationId = item.CurrentLocation?.Id;
        }

        item.OwnerParty = Resolve(references.Parties, fields.OwnerPartyId, item.OwnerPartyId, "Party", errorPrefix);
        item.OwnerPartyId = item.OwnerParty?.Id;

        EnsureRequiredAttributes(item, errorPrefix);
    }

    private static T? Resolve<T>(
        Dictionary<int, T> lookup, int? id, int? currentId, string label, string errorPrefix)
        where T : class, IMasterData
    {
        if (id == null) return null;

        return ReferenceResolver.Check(lookup.GetValueOrDefault(id.Value), id.Value, currentId, label, errorPrefix);
    }

    private static void EnsureRequiredAttributes(TrackedItem item, string errorPrefix)
    {
        if (item.Category == null) return;

        var missing = item.Category.RequiredAttributes
            .Where(key => !item.Attributes.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            .ToList();
        if (missing.Count > 0)
        {
            throw new BusinessRuleException(
                $"{errorPrefix}Category {item.Category.Code} requires attributes: {string.Join(", ", missing)}");
        }
    }

    // Starts each item's timeline. Skipped if the REGISTERED event type was removed.
    private async Task RecordRegisteredEventsAsync(List<TrackedItem> items, DateTime occurredAt)
    {
        var registered = await _cache.FindEventTypeAsync(RegisteredEventCode);
        if (registered is not { IsActive: true }) return;

        foreach (var group in items.GroupBy(i => i.CurrentLocationId))
        {
            await _recorder.AddEventsAsync(
                group.ToList(), registered, new EventContext(group.Key, occurredAt, EventSource.Manual));
        }
    }

    private static void EnsureNotArchived(TrackedItem item)
    {
        if (item.IsArchived)
        {
            throw new ConflictException("Item is archived. Restore it before making changes.");
        }
    }

    private async Task<string> GenerateUniqueTagCodeAsync(HashSet<string> usedCodes)
    {
        while (true)
        {
            var code = TagCodePrefix + RandomNumberGenerator.GetString(TagCodeAlphabet, 10);
            if (usedCodes.Contains(code) || await _context.TrackedItems.AnyAsync(i => i.TagCode == code))
            {
                continue;
            }
            usedCodes.Add(code);
            return code;
        }
    }
}
