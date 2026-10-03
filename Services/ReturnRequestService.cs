using System.Linq.Expressions;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Exceptions;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

// Customer returns (RMA) of delivered items:
//   Requested --approve--> Approved (return shipment created) --delivered--> Received
//             --reject---> Rejected
//             --cancel---> Cancelled            (approved: cancel the return shipment instead)
// Received / Cancelled after approval are derived from the return shipment's status, so
// the request and its shipment can't disagree. Each requested item gets a RETURN_REQUESTED
// event (system-managed, on the original shipment) with its reason.
public class ReturnRequestService : IReturnRequestService
{
    private const string ReturnRequestedEventCode = "RETURN_REQUESTED";
    private const string RmaAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly AppDbContext _context;
    private readonly ITrackingEventRecorder _recorder;
    private readonly IMasterDataCache _cache;
    private readonly ICurrentUser _currentUser;
    private readonly IShipmentService _shipments;
    // Returns:CustomerReturnWindowDays - days after delivery a return can be requested (0 = no limit).
    private readonly int _windowDays;

    public ReturnRequestService(
        AppDbContext context, ITrackingEventRecorder recorder, IMasterDataCache cache, ICurrentUser currentUser,
        IShipmentService shipments, IConfiguration configuration)
    {
        _context = context;
        _recorder = recorder;
        _cache = cache;
        _currentUser = currentUser;
        _shipments = shipments;
        _windowDays = configuration.GetValue("Returns:CustomerReturnWindowDays", 30);
    }

    private static readonly Expression<Func<ReturnRequest, ReturnRequestResponse>> Projection = r => new ReturnRequestResponse
    {
        Id = r.Id,
        RmaNumber = r.RmaNumber,
        Shipment = new ReferenceSummary(r.Shipment.Id, r.Shipment.TrackingNumber, r.Shipment.TrackingNumber),
        Status = r.Status == ReturnRequestStatus.Approved && r.ReturnShipment != null && r.ReturnShipment.Status == ShipmentStatus.Delivered
            ? ReturnRequestStatus.Received
            : r.Status == ReturnRequestStatus.Approved && r.ReturnShipment != null && r.ReturnShipment.Status == ShipmentStatus.Cancelled
                ? ReturnRequestStatus.Cancelled
                : r.Status,
        Note = r.Note,
        RequestedAt = r.RequestedAt,
        RequestedBy = r.RequestedBy,
        DecidedAt = r.DecidedAt,
        DecidedBy = r.DecidedBy,
        DecisionNote = r.DecisionNote,
        ReturnShipment = r.ReturnShipment == null
            ? null
            : new ReferenceSummary(r.ReturnShipment.Id, r.ReturnShipment.TrackingNumber, r.ReturnShipment.TrackingNumber),
        ReturnShipmentStatus = r.ReturnShipment == null ? null : r.ReturnShipment.Status,
        Items = r.Items
            .OrderBy(i => i.TrackedItem.TagCode)
            .Select(i => new ReturnRequestItemResponse
            {
                Item = new ReferenceSummary(i.TrackedItem.Id, i.TrackedItem.TagCode, i.TrackedItem.Name),
                Reason = new ReasonSummary(i.ReasonCode.Id, i.ReasonCode.Code, i.ReasonCode.NameTh, i.ReasonCode.NameEn),
                Note = i.Note
            })
            .ToList(),
        UpdatedAt = r.UpdatedAt
    };

    public async Task<PagedResult<ReturnRequestResponse>> GetAllAsync(ReturnRequestQuery query)
    {
        var requests = _context.ReturnRequests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            requests = requests.Where(r => EF.Functions.ILike(r.RmaNumber, QueryHelpers.ContainsPattern(query.Search)));
        }
        if (query.ShipmentId.HasValue)
        {
            requests = requests.Where(r => r.ShipmentId == query.ShipmentId || r.ReturnShipmentId == query.ShipmentId);
        }
        if (query.TrackedItemId.HasValue)
        {
            requests = requests.Where(r => r.Items.Any(i => i.TrackedItemId == query.TrackedItemId));
        }
        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            requests = requests.Where(HasEffectiveStatus(status));
        }

        var totalCount = await requests.CountAsync();
        var page = await requests
            .OrderByDescending(r => r.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(Projection)
            .ToListAsync();

        return new PagedResult<ReturnRequestResponse>
        {
            Items = page,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public Task<ReturnRequestResponse?> GetByIdAsync(int id) =>
        _context.ReturnRequests.AsNoTracking().Where(r => r.Id == id).Select(Projection).FirstOrDefaultAsync();

    public Task<ReturnRequestResponse?> GetByRmaNumberAsync(string rmaNumber)
    {
        var normalized = QueryHelpers.NormalizeCode(rmaNumber);
        return _context.ReturnRequests.AsNoTracking().Where(r => r.RmaNumber == normalized).Select(Projection).FirstOrDefaultAsync();
    }

    public async Task<ReturnRequestResponse> CreateAsync(CreateReturnRequestRequest request)
    {
        var shipment = await _context.Shipments.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ShipmentId)
            ?? throw Errors.ReferenceNotFound("shipmentId", request.ShipmentId!.Value);
        if (shipment.Status != ShipmentStatus.Delivered)
        {
            throw Errors.ShipmentStatusNotAllowed("requestReturn", shipment.Status, [ShipmentStatus.Delivered]);
        }
        var now = DateTime.UtcNow;
        if (_windowDays > 0 && shipment.DeliveredAt.HasValue && shipment.DeliveredAt.Value.AddDays(_windowDays) < now)
        {
            throw Errors.ReturnWindowExpired(shipment.TrackingNumber, shipment.DeliveredAt.Value, _windowDays);
        }

        var items = await _recorder.FindItemsAsync(
            request.Items.Where(i => i.TrackedItemId.HasValue).Select(i => i.TrackedItemId!.Value),
            request.Items.Where(i => i.TagCode != null).Select(i => i.TagCode!));
        var lines = request.Items
            .Select(input => (Input: input, Item: items.First(i =>
                input.TrackedItemId.HasValue ? i.Id == input.TrackedItemId : i.TagCode == QueryHelpers.NormalizeCode(input.TagCode!))))
            .ToList();

        var duplicated = lines.GroupBy(l => l.Item.Id).Where(g => g.Count() > 1).Select(g => g.First().Item.TagCode).ToList();
        if (duplicated.Count > 0) throw Errors.ReturnItemsDuplicated(duplicated);

        var itemIds = items.Select(i => i.Id).ToList();
        var inShipment = await _context.ShipmentItems
            .Where(si => si.ShipmentId == shipment.Id && itemIds.Contains(si.TrackedItemId))
            .Select(si => si.TrackedItemId)
            .ToListAsync();
        var notInShipment = items.Where(i => !inShipment.Contains(i.Id)).Select(i => i.TagCode).ToList();
        if (notInShipment.Count > 0) throw Errors.ReturnItemsNotInShipment(shipment.TrackingNumber, notInShipment);

        var notDelivered = items.Where(i => i.Status != ItemStatus.Delivered).Select(i => i.TagCode).ToList();
        if (notDelivered.Count > 0) throw Errors.ReturnItemsNotDelivered(notDelivered);

        await EnsureNoOpenReturnAsync(itemIds);

        // One RETURN_REQUESTED per reason group (the recorder validates each reason and note).
        var eventType = await _recorder.GetEventTypeAsync(ReturnRequestedEventCode);
        var returnRequest = new ReturnRequest
        {
            RmaNumber = await GenerateRmaNumberAsync(),
            ShipmentId = shipment.Id,
            Status = ReturnRequestStatus.Requested,
            Note = QueryHelpers.NullIfBlank(request.Note),
            RequestedAt = now,
            RequestedBy = _currentUser.Username,
            UpdatedAt = now
        };
        foreach (var group in lines.GroupBy(l => (Reason: QueryHelpers.NormalizeCode(l.Input.ReasonCode), Note: QueryHelpers.NullIfBlank(l.Input.Note))))
        {
            var note = group.Key.Note ?? returnRequest.Note;
            await _recorder.AddEventsAsync(group.Select(l => l.Item).ToList(), eventType, new EventContext(
                shipment.DestinationLocationId, now, EventSource.Shipment, note,
                ShipmentId: shipment.Id, ReasonCode: group.Key.Reason, IsSystemManaged: true));

            var reason = (await _cache.FindReasonCodeAsync(group.Key.Reason))!;
            returnRequest.Items.AddRange(group.Select(l => new ReturnRequestItem
            {
                TrackedItemId = l.Item.Id,
                ReasonCodeId = reason.Id,
                Note = QueryHelpers.NullIfBlank(l.Input.Note)
            }));
        }

        _context.ReturnRequests.Add(returnRequest);
        await _context.SaveChangesAsync();
        return (await GetByIdAsync(returnRequest.Id))!;
    }

    public async Task<ApproveReturnRequestResponse?> ApproveAsync(int id, ApproveReturnRequestRequest request)
    {
        var returnRequest = await _context.ReturnRequests
            .Include(r => r.Items).ThenInclude(i => i.TrackedItem)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (returnRequest == null) return null;
        EnsureStatus(returnRequest, "approve", ReturnRequestStatus.Requested);

        // The items may have joined another shipment since the request.
        var items = returnRequest.Items.Select(i => i.TrackedItem).ToList();
        var itemIds = items.Select(i => i.Id).ToList();
        var busy = await _context.ShipmentItems
            .Where(si => itemIds.Contains(si.TrackedItemId) &&
                         (si.Shipment.Status == ShipmentStatus.Planned || si.Shipment.Status == ShipmentStatus.InTransit))
            .Select(si => si.TrackedItem.TagCode)
            .ToListAsync();
        if (busy.Count > 0) throw Errors.ItemsInOpenShipment(busy);

        var route = request.AutoRoute
            ? new RouteShipmentRequest
            {
                Objective = request.Objective,
                FirstMileMinutes = request.FirstMileMinutes,
                LastMileMinutes = request.LastMileMinutes
            }
            : null;
        var (returnShipment, plan) = await _shipments.StageReturnShipmentAsync(
            returnRequest.ShipmentId, items, request.PickupLocationId,
            request.PickupAt?.UtcDateTime ?? DateTime.UtcNow, $"Customer return {returnRequest.RmaNumber}",
            route, linkAsReturnOf: false, routeIsOptional: false);

        var now = DateTime.UtcNow;
        returnRequest.Status = ReturnRequestStatus.Approved;
        returnRequest.DecidedAt = now;
        returnRequest.DecidedBy = _currentUser.Username;
        returnRequest.ReturnShipment = returnShipment;
        returnRequest.UpdatedAt = now;
        await _context.SaveChangesAsync();

        return new ApproveReturnRequestResponse
        {
            ReturnRequest = (await GetByIdAsync(id))!,
            ReturnShipment = (await _shipments.GetShipmentByIdAsync(returnShipment.Id))!,
            Plan = plan
        };
    }

    public Task<ReturnRequestResponse?> RejectAsync(int id, DecideReturnRequestRequest request) =>
        CloseAsync(id, request, ReturnRequestStatus.Rejected, "reject");

    // Only before approval; an approved request is cancelled by cancelling its return shipment.
    public Task<ReturnRequestResponse?> CancelAsync(int id, DecideReturnRequestRequest request) =>
        CloseAsync(id, request, ReturnRequestStatus.Cancelled, "cancel");

    private async Task<ReturnRequestResponse?> CloseAsync(
        int id, DecideReturnRequestRequest request, ReturnRequestStatus status, string action)
    {
        var returnRequest = await _context.ReturnRequests.FirstOrDefaultAsync(r => r.Id == id);
        if (returnRequest == null) return null;
        EnsureStatus(returnRequest, action, ReturnRequestStatus.Requested);

        var now = DateTime.UtcNow;
        returnRequest.Status = status;
        returnRequest.DecidedAt = now;
        returnRequest.DecidedBy = _currentUser.Username;
        returnRequest.DecisionNote = request.Reason.Trim();
        returnRequest.UpdatedAt = now;
        await _context.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    // Open = Requested, or Approved with a return shipment still on its way.
    private async Task EnsureNoOpenReturnAsync(List<int> itemIds)
    {
        var open = await _context.ReturnRequestItems
            .Where(i => itemIds.Contains(i.TrackedItemId) &&
                        (i.ReturnRequest.Status == ReturnRequestStatus.Requested ||
                         (i.ReturnRequest.Status == ReturnRequestStatus.Approved &&
                          i.ReturnRequest.ReturnShipment!.Status != ShipmentStatus.Delivered &&
                          i.ReturnRequest.ReturnShipment.Status != ShipmentStatus.Cancelled)))
            .Select(i => i.TrackedItem.TagCode + " (" + i.ReturnRequest.RmaNumber + ")")
            .ToListAsync();
        if (open.Count > 0) throw Errors.ItemsInOpenReturn(open);
    }

    private static void EnsureStatus(ReturnRequest returnRequest, string action, params ReturnRequestStatus[] allowed)
    {
        if (!allowed.Contains(returnRequest.Status))
        {
            throw Errors.ReturnRequestStatusNotAllowed(action, returnRequest.Status, allowed.Cast<object>());
        }
    }

    private static Expression<Func<ReturnRequest, bool>> HasEffectiveStatus(ReturnRequestStatus status) => status switch
    {
        ReturnRequestStatus.Received => r =>
            r.Status == ReturnRequestStatus.Approved && r.ReturnShipment!.Status == ShipmentStatus.Delivered,
        ReturnRequestStatus.Cancelled => r =>
            r.Status == ReturnRequestStatus.Cancelled ||
            (r.Status == ReturnRequestStatus.Approved && r.ReturnShipment!.Status == ShipmentStatus.Cancelled),
        ReturnRequestStatus.Approved => r =>
            r.Status == ReturnRequestStatus.Approved &&
            r.ReturnShipment!.Status != ShipmentStatus.Delivered && r.ReturnShipment.Status != ShipmentStatus.Cancelled,
        _ => r => r.Status == status
    };

    // RMA + yyMMdd + 6 random characters, e.g. RMA261003K4Q7XA.
    private async Task<string> GenerateRmaNumberAsync()
    {
        while (true)
        {
            var random = string.Concat(Enumerable.Range(0, 6)
                .Select(_ => RmaAlphabet[RandomNumberGenerator.GetInt32(RmaAlphabet.Length)]));
            var number = $"RMA{DateTime.UtcNow:yyMMdd}{random}";
            if (!await _context.ReturnRequests.AnyAsync(r => r.RmaNumber == number)) return number;
        }
    }
}
