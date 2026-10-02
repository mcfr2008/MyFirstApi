using Microsoft.EntityFrameworkCore;
using MyFirstApi.Dtos;
using MyFirstApi.Exceptions;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

// Return to sender (POST /Shipments/{id}/return-to-sender), all in one SaveChanges:
// 1. RETURNED (with a reason) is recorded on the original shipment for every item
//    that wasn't delivered: all of them on an InTransit shipment, the refused ones on
//    a Delivered one (proof of delivery with refused items).
// 2. An InTransit original is closed as ReturnedToSender; a Delivered one stays Delivered.
// 3. A Planned return shipment is created: receiver -> sender, from where the items
//    are now to the original origin, holding the returned items, linked by ReturnOfShipmentId.
// 4. With AutoRoute its legs are planned by the route planner. Planning runs before
//    anything is saved, so a missing route fails the whole request.
public partial class ShipmentService
{
    private const string ReturnedEventCode = "RETURNED";

    public async Task<ReturnToSenderResponse?> ReturnToSenderAsync(int id, ReturnToSenderRequest request)
    {
        var shipment = await ShipmentsWithDetails().FirstOrDefaultAsync(s => s.Id == id);
        if (shipment == null) return null;

        // Checked before the status so a returned shipment says where its return is.
        var existing = await _context.Shipments.AsNoTracking()
            .Where(s => s.ReturnOfShipmentId == id)
            .Select(s => s.TrackingNumber)
            .FirstOrDefaultAsync();
        if (existing != null)
        {
            throw Errors.ReturnAlreadyCreated(shipment.TrackingNumber, existing);
        }
        EnsureStatus(shipment, "returnToSender", ShipmentStatus.InTransit, ShipmentStatus.Delivered);

        var items = (await LoadShipmentItemsAsync(shipment))
            .Where(i => i.Status != ItemStatus.Delivered)
            .ToList();
        if (items.Count == 0)
        {
            throw Errors.NothingToReturn(shipment.TrackingNumber);
        }

        var start = await ReferenceResolver.ResolveRequiredAsync(_context.Locations.AsNoTracking(),
            request.LocationId ?? shipment.DestinationLocationId, shipment.DestinationLocationId, "locationId");
        var now = DateTime.UtcNow;
        var occurredAt = request.OccurredAt?.UtcDateTime ?? now;

        // Plan first: nothing is changed if there's no route back.
        List<ShipmentLegRequest> legs = [];
        RoutePlanResponse? plan = null;
        if (request.AutoRoute)
        {
            (legs, plan) = await PlanLegsAsync(start, shipment.OriginLocation, occurredAt, new RouteShipmentRequest
            {
                Objective = request.Objective,
                FirstMileMinutes = request.FirstMileMinutes,
                LastMileMinutes = request.LastMileMinutes
            });
        }

        await RecordForItemsAsync(shipment, items, ReturnedEventCode, start.Id, occurredAt, request.Note,
            legId: null, isSystemManaged: true, reasonCode: request.ReasonCode);

        if (shipment.Status == ShipmentStatus.InTransit)
        {
            shipment.Status = ShipmentStatus.ReturnedToSender;
        }
        shipment.UpdatedAt = now;

        var returnShipment = new Shipment
        {
            TrackingNumber = await GenerateTrackingNumberAsync(),
            Reference = shipment.Reference,
            ReturnOfShipmentId = shipment.Id,
            SenderPartyId = shipment.ReceiverPartyId,
            ReceiverPartyId = shipment.SenderPartyId,
            OriginLocationId = start.Id,
            DestinationLocationId = shipment.OriginLocationId,
            Status = ShipmentStatus.Planned,
            CustomsStatus = start.Country != shipment.OriginLocation.Country ? CustomsStatus.Pending : CustomsStatus.NotRequired,
            PlannedPickupAt = occurredAt,
            RequiresSignature = true,
            Notes = QueryHelpers.NullIfBlank(request.Note),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.Username
        };
        _context.Shipments.Add(returnShipment);
        // Added directly: the original is closing in this same save, so the
        // "item already in an open shipment" check (which reads the database) doesn't apply.
        foreach (var item in items)
        {
            _context.ShipmentItems.Add(new ShipmentItem { Shipment = returnShipment, TrackedItemId = item.Id, AddedAt = now });
        }
        await ApplyLegsAsync(returnShipment, legs, start.Id, shipment.OriginLocationId);

        await _context.SaveChangesAsync();

        return new ReturnToSenderResponse
        {
            Original = (await GetShipmentByIdAsync(id))!,
            ReturnShipment = (await GetShipmentByIdAsync(returnShipment.Id))!,
            ItemsReturned = items.Count,
            Plan = plan
        };
    }
}
