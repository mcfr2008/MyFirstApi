using Microsoft.EntityFrameworkCore;
using MyFirstApi.Dtos;
using MyFirstApi.Exceptions;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

// Proof of delivery: the receiver signs on the courier's device. One request
// stores the signature/photos, records who/where/when, marks the shipment
// delivered and records DELIVERED (or DELIVERY_FAILED for refused items) events.
public partial class ShipmentService
{
    private const string DeliveryFailedEventCode = "DELIVERY_FAILED";

    private static readonly FileSignatures.ImageType[] AllowedImageTypes =
        [FileSignatures.Png, FileSignatures.Jpeg, FileSignatures.Webp];

    public async Task<ProofOfDeliveryResponse?> CreateProofOfDeliveryAsync(int id, ProofOfDeliveryRequest request)
    {
        var shipment = await ShipmentsWithDetails().FirstOrDefaultAsync(s => s.Id == id);
        if (shipment == null) return null;

        if (await _context.ProofsOfDelivery.AnyAsync(p => p.ShipmentId == id))
        {
            throw Errors.ProofOfDeliveryExists(shipment.TrackingNumber);
        }
        EnsureDeliverable(shipment);

        // Validate everything before writing any file.
        var photos = request.Photos ?? [];
        if (photos.Count > ProofOfDeliveryRequest.MaxPhotos)
        {
            throw Errors.TooManyPhotos(ProofOfDeliveryRequest.MaxPhotos);
        }
        var signatureType = await ValidateImageAsync(request.Signature!, "signature", ProofOfDeliveryRequest.MaxSignatureBytes);
        var photoTypes = new List<FileSignatures.ImageType>();
        for (var i = 0; i < photos.Count; i++)
        {
            photoTypes.Add(await ValidateImageAsync(photos[i], $"photos[{i}]", ProofOfDeliveryRequest.MaxPhotoBytes));
        }

        var items = await LoadShipmentItemsAsync(shipment);
        var refused = MatchRefusedItems(items, request.RefusedItems ?? []);
        var delivered = items.Where(i => !refused.ContainsKey(i.Id)).ToList();
        if (items.Count == 0)
        {
            throw Errors.ShipmentHasNoItems(shipment.TrackingNumber);
        }
        if (delivered.Count == 0)
        {
            throw Errors.NoItemsDelivered();
        }

        var signedAt = request.SignedAt?.UtcDateTime ?? DateTime.UtcNow;
        var savedKeys = new List<string>();
        try
        {
            var signatureFile = await StoreAsync(request.Signature!, signatureType, FilePurpose.Signature, savedKeys);
            var proof = new ProofOfDelivery
            {
                Shipment = shipment,
                ReceiverName = request.ReceiverName.Trim(),
                ReceiverRelation = request.ReceiverRelation!.Value,
                SignatureFile = signatureFile,
                SignedAt = signedAt,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                LocationAccuracyMeters = request.LocationAccuracyMeters,
                DeliveredBy = _currentUser.Username,
                DeviceInfo = QueryHelpers.NullIfBlank(request.DeviceInfo),
                Note = QueryHelpers.NullIfBlank(request.Note),
                RefusedItems = refused.Values.ToList(),
                CreatedAt = DateTime.UtcNow
            };
            for (var i = 0; i < photos.Count; i++)
            {
                var photo = await StoreAsync(photos[i], photoTypes[i], FilePurpose.DeliveryPhoto, savedKeys);
                proof.Photos.Add(new ProofOfDeliveryPhoto { File = photo, SortOrder = i });
            }
            _context.ProofsOfDelivery.Add(proof);

            shipment.Status = ShipmentStatus.Delivered;
            shipment.DeliveredAt = signedAt;
            shipment.UpdatedAt = DateTime.UtcNow;

            var receivedNote = $"Received by {proof.ReceiverName} ({proof.ReceiverRelation}), signature on file";
            await RecordForItemsAsync(shipment, delivered, DeliveredEventCode, shipment.DestinationLocationId, signedAt,
                receivedNote, null, isSystemManaged: true, latitude: request.Latitude, longitude: request.Longitude);

            foreach (var group in refused.Values.GroupBy(r => (r.ReasonCode, r.Note)))
            {
                var refusedItems = items.Where(i => group.Any(r => r.TrackedItemId == i.Id)).ToList();
                await RecordForItemsAsync(shipment, refusedItems, DeliveryFailedEventCode, shipment.DestinationLocationId,
                    signedAt, group.Key.Note ?? "Refused at delivery", null, isSystemManaged: true,
                    reasonCode: group.Key.ReasonCode, latitude: request.Latitude, longitude: request.Longitude);
            }

            await _context.SaveChangesAsync();
        }
        catch
        {
            // Nothing was committed: remove the files written for this request.
            foreach (var key in savedKeys)
            {
                await _fileStorage.DeleteAsync(key);
            }
            throw;
        }

        return await GetProofOfDeliveryAsync(id);
    }

    public async Task<ProofOfDeliveryResponse?> GetProofOfDeliveryAsync(int id)
    {
        var proof = await _context.ProofsOfDelivery.AsNoTracking()
            .Include(p => p.Shipment)
            .Include(p => p.SignatureFile)
            .Include(p => p.Photos).ThenInclude(p => p.File)
            .FirstOrDefaultAsync(p => p.ShipmentId == id);
        if (proof == null) return null;

        var itemCount = await _context.ShipmentItems.CountAsync(si => si.ShipmentId == id);
        return ProofOfDeliveryResponse.From(proof, itemCount - proof.RefusedItems.Count);
    }

    private static async Task<FileSignatures.ImageType> ValidateImageAsync(IFormFile file, string field, long maxBytes)
    {
        if (file.Length > maxBytes)
        {
            throw Errors.FileTooLarge(field, maxBytes);
        }

        await using var stream = file.OpenReadStream();
        var type = await FileSignatures.DetectImageAsync(stream);
        return type ?? throw Errors.InvalidFileType(field, AllowedImageTypes.Select(t => t.ContentType));
    }

    private async Task<StoredFile> StoreAsync(
        IFormFile upload, FileSignatures.ImageType type, FilePurpose purpose, List<string> savedKeys)
    {
        await using var stream = upload.OpenReadStream();
        var stored = await _fileStorage.SaveAsync(stream, type.Extension);
        savedKeys.Add(stored.StorageKey);

        var file = new StoredFile
        {
            Id = Guid.NewGuid(),
            StorageKey = stored.StorageKey,
            OriginalFileName = Path.GetFileName(upload.FileName),
            ContentType = type.ContentType,
            SizeBytes = stored.SizeBytes,
            Sha256 = stored.Sha256,
            Purpose = purpose,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = _currentUser.Username
        };
        _context.StoredFiles.Add(file);
        return file;
    }

    // Keyed by TrackedItemId; every refused tag must belong to this shipment.
    private static Dictionary<int, RefusedItem> MatchRefusedItems(List<TrackedItem> items, List<RefusedItemRequest> requests)
    {
        var result = new Dictionary<int, RefusedItem>();
        var unknown = new List<string>();
        foreach (var request in requests)
        {
            var tagCode = QueryHelpers.NormalizeCode(request.TagCode);
            var item = items.FirstOrDefault(i => i.TagCode == tagCode);
            if (item == null)
            {
                unknown.Add(tagCode);
                continue;
            }
            result[item.Id] = new RefusedItem
            {
                TrackedItemId = item.Id,
                TagCode = item.TagCode,
                ReasonCode = QueryHelpers.NormalizeCode(request.ReasonCode),
                Note = QueryHelpers.NullIfBlank(request.Note)
            };
        }
        if (unknown.Count > 0)
        {
            throw Errors.RefusedItemsNotInShipment(unknown);
        }
        return result;
    }
}
