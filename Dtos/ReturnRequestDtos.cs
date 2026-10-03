using System.ComponentModel.DataAnnotations;
using MyFirstApi.Models;

namespace MyFirstApi.Dtos;

public class ReturnRequestItemInput : IValidatableObject
{
    // Identify the item by id or by tag code (exactly one).
    public int? TrackedItemId { get; set; }

    [StringLength(64)]
    public string? TagCode { get; set; }

    // A reason allowed for RETURN_REQUESTED: DAMAGED_ON_ARRIVAL, WRONG_ITEM, DEFECTIVE,
    // NOT_AS_DESCRIBED, NO_LONGER_NEEDED, or OTHER (needs a note).
    [Required]
    [StringLength(50)]
    public string ReasonCode { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Note { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TrackedItemId.HasValue == !string.IsNullOrWhiteSpace(TagCode))
        {
            yield return new ValidationResult("Provide either trackedItemId or tagCode.", [nameof(TrackedItemId)]);
        }
    }
}

public class CreateReturnRequestRequest
{
    // The delivered shipment the items came in.
    [Required]
    public int? ShipmentId { get; set; }

    [Required]
    [MinLength(1)]
    [MaxLength(500)]
    public List<ReturnRequestItemInput> Items { get; set; } = new();

    [StringLength(2000)]
    public string? Note { get; set; }
}

public class ApproveReturnRequestRequest
{
    // Where the items are picked up. Defaults to the original shipment's destination.
    public int? PickupLocationId { get; set; }

    // Defaults to now.
    public DateTimeOffset? PickupAt { get; set; }

    // Plan the return shipment's legs with the route planner (needs lanes back to the sender).
    public bool AutoRoute { get; set; } = true;

    public RouteObjective Objective { get; set; } = RouteObjective.Fastest;

    [Range(0, 2880)]
    public int FirstMileMinutes { get; set; } = 120;

    [Range(0, 2880)]
    public int LastMileMinutes { get; set; } = 240;
}

public class DecideReturnRequestRequest
{
    // Why it is rejected / cancelled (shown to the customer service team).
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;
}

public class ReturnRequestQuery : PagedQuery
{
    // RMA number (partial).
    public string? Search { get; set; }
    // Effective status, including the derived Received.
    public ReturnRequestStatus? Status { get; set; }
    public int? ShipmentId { get; set; }
    public int? TrackedItemId { get; set; }
}

public class ReturnRequestResponse
{
    public int Id { get; set; }
    public string RmaNumber { get; set; } = string.Empty;
    // Original (delivered) shipment.
    public ReferenceSummary Shipment { get; set; } = null!;
    // Effective: Received when the return shipment was delivered, Cancelled when it was cancelled.
    public ReturnRequestStatus Status { get; set; }
    public string? Note { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? RequestedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecidedBy { get; set; }
    public string? DecisionNote { get; set; }
    public ReferenceSummary? ReturnShipment { get; set; }
    public ShipmentStatus? ReturnShipmentStatus { get; set; }
    public List<ReturnRequestItemResponse> Items { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
}

public class ReturnRequestItemResponse
{
    public ReferenceSummary Item { get; set; } = null!;
    public ReasonSummary Reason { get; set; } = null!;
    public string? Note { get; set; }
}

public class ApproveReturnRequestResponse
{
    public ReturnRequestResponse ReturnRequest { get; set; } = null!;
    public ShipmentResponse ReturnShipment { get; set; } = null!;
    public RoutePlanResponse? Plan { get; set; }
}
