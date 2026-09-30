using System.ComponentModel.DataAnnotations;
using MyFirstApi.Models;

namespace MyFirstApi.Dtos;

// multipart/form-data: the signature and photos are image files; the rest are form fields.
// Refused items are sent as refusedItems[0].tagCode, refusedItems[0].reasonCode, ...
public class ProofOfDeliveryRequest : IValidatableObject
{
    public const long MaxSignatureBytes = 1 * 1024 * 1024;
    public const long MaxPhotoBytes = 5 * 1024 * 1024;
    public const int MaxPhotos = 5;

    // PNG (e.g. from a signature pad canvas) or JPEG / WebP.
    [Required]
    public IFormFile? Signature { get; set; }

    public List<IFormFile>? Photos { get; set; }

    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string ReceiverName { get; set; } = string.Empty;

    [Required]
    public ReceiverRelation? ReceiverRelation { get; set; }

    // Defaults to now.
    public DateTimeOffset? SignedAt { get; set; }

    [Range(-90.0, 90.0)]
    public decimal? Latitude { get; set; }

    [Range(-180.0, 180.0)]
    public decimal? Longitude { get; set; }

    [Range(0.0, 100000.0)]
    public decimal? LocationAccuracyMeters { get; set; }

    // e.g. "Android 15 / Samsung A55 / ThingTag Driver 1.2".
    [StringLength(500)]
    public string? DeviceInfo { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }

    [MaxLength(500)]
    public List<RefusedItemRequest>? RefusedItems { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SignedAt.HasValue && SignedAt.Value > DateTimeOffset.UtcNow + TimeSpan.FromMinutes(10))
        {
            yield return new ValidationResult("SignedAt cannot be in the future.", [nameof(SignedAt)]);
        }
        if (Latitude.HasValue != Longitude.HasValue)
        {
            yield return new ValidationResult("Provide both Latitude and Longitude.", [nameof(Latitude), nameof(Longitude)]);
        }
    }
}

public class RefusedItemRequest
{
    [Required]
    [StringLength(64)]
    public string TagCode { get; set; } = string.Empty;

    // Reason for DELIVERY_FAILED, e.g. REFUSED.
    [Required]
    [StringLength(50)]
    public string ReasonCode { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Note { get; set; }
}

public class StoredFileResponse
{
    public Guid Id { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    // GET with the bearer token (an <img src> can't send it; fetch the blob instead).
    public string Url { get; set; } = string.Empty;

    public static StoredFileResponse From(StoredFile file) => new()
    {
        Id = file.Id,
        ContentType = file.ContentType,
        SizeBytes = file.SizeBytes,
        Sha256 = file.Sha256,
        Url = $"/api/v1/Files/{file.Id}"
    };
}

public class ProofOfDeliveryResponse
{
    public int Id { get; set; }
    public ReferenceSummary Shipment { get; set; } = null!;
    public string ReceiverName { get; set; } = string.Empty;
    public ReceiverRelation ReceiverRelation { get; set; }
    public DateTime SignedAt { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? LocationAccuracyMeters { get; set; }
    public string? DeliveredBy { get; set; }
    public string? DeviceInfo { get; set; }
    public string? Note { get; set; }
    public StoredFileResponse Signature { get; set; } = null!;
    public List<StoredFileResponse> Photos { get; set; } = new();
    public List<RefusedItem> RefusedItems { get; set; } = new();
    public int DeliveredItemCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public static ProofOfDeliveryResponse From(ProofOfDelivery pod, int deliveredItemCount) => new()
    {
        Id = pod.Id,
        Shipment = new ReferenceSummary(pod.Shipment.Id, pod.Shipment.TrackingNumber, pod.Shipment.TrackingNumber),
        ReceiverName = pod.ReceiverName,
        ReceiverRelation = pod.ReceiverRelation,
        SignedAt = pod.SignedAt,
        Latitude = pod.Latitude,
        Longitude = pod.Longitude,
        LocationAccuracyMeters = pod.LocationAccuracyMeters,
        DeliveredBy = pod.DeliveredBy,
        DeviceInfo = pod.DeviceInfo,
        Note = pod.Note,
        Signature = StoredFileResponse.From(pod.SignatureFile),
        Photos = pod.Photos.OrderBy(p => p.SortOrder).Select(p => StoredFileResponse.From(p.File)).ToList(),
        RefusedItems = pod.RefusedItems,
        DeliveredItemCount = deliveredItemCount,
        CreatedAt = pod.CreatedAt
    };
}
