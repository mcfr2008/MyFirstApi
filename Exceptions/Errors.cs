using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace MyFirstApi.Exceptions;

// The error catalog. Every expected API error has a stable code here with
// English and Thai templates; GET /api/v1/ErrorCodes serves this list so the
// frontend can show messages in the user's language. Add new errors here.
public static class Errors
{
    private const int BadRequest = StatusCodes.Status400BadRequest;
    private const int Conflict = StatusCodes.Status409Conflict;

    // ---------------------------------------------------------------- general
    // (produced by the framework; listed so the frontend has translations)
    public static readonly ErrorDefinition ValidationFailed = new("VALIDATION_FAILED", BadRequest,
        "One or more fields are invalid (see errors).", "ข้อมูลบางช่องไม่ถูกต้อง (ดูรายละเอียดใน errors)");
    public static readonly ErrorDefinition Unauthorized = new("UNAUTHORIZED", StatusCodes.Status401Unauthorized,
        "Login required or the token has expired.", "กรุณาเข้าสู่ระบบ หรือ token หมดอายุแล้ว");
    public static readonly ErrorDefinition Forbidden = new("FORBIDDEN", StatusCodes.Status403Forbidden,
        "You don't have permission for this action.", "คุณไม่มีสิทธิ์ทำรายการนี้");
    public static readonly ErrorDefinition NotFound = new("NOT_FOUND", StatusCodes.Status404NotFound,
        "The requested record was not found.", "ไม่พบข้อมูลที่ต้องการ");
    public static readonly ErrorDefinition TooManyRequests = new("RATE_LIMITED", StatusCodes.Status429TooManyRequests,
        "Too many requests. Try again later (see Retry-After).", "ส่งคำขอบ่อยเกินไป กรุณาลองใหม่ภายหลัง (ดู Retry-After)");
    public static readonly ErrorDefinition InternalError = new("INTERNAL_ERROR", StatusCodes.Status500InternalServerError,
        "An unexpected error occurred.", "เกิดข้อผิดพลาดที่ไม่คาดคิด");

    public static readonly ErrorDefinition IdempotencyKeyInvalidDef = new("IDEMPOTENCY_KEY_INVALID", BadRequest,
        "Idempotency-Key must be 1-{maxLength} visible ASCII characters.", "Idempotency-Key ต้องเป็นอักขระ ASCII ที่มองเห็นได้ 1-{maxLength} ตัว");
    public static readonly ErrorDefinition IdempotencyKeyReusedDef = new("IDEMPOTENCY_KEY_REUSED", StatusCodes.Status422UnprocessableEntity,
        "Idempotency-Key {key} was already used for a different request. Use a new key for each new request.",
        "Idempotency-Key {key} ถูกใช้กับคำขออื่นไปแล้ว คำขอใหม่แต่ละครั้งต้องใช้ key ใหม่");

    public static readonly ErrorDefinition InvalidCursorDef = new("INVALID_CURSOR", BadRequest,
        "Invalid cursor. Use nextCursor from the previous page.", "cursor ไม่ถูกต้อง ให้ใช้ nextCursor จากหน้าก่อนหน้า");
    public static readonly ErrorDefinition ReferenceNotFoundDef = new("REFERENCE_NOT_FOUND", BadRequest,
        "{field} {id} does not exist.", "ไม่พบข้อมูลอ้างอิง {field} รหัส {id}");
    public static readonly ErrorDefinition ReferenceInactiveDef = new("REFERENCE_INACTIVE", BadRequest,
        "{field} {code} is inactive.", "ข้อมูลอ้างอิง {field} รหัส {code} ถูกปิดใช้งานแล้ว");
    public static readonly ErrorDefinition CodeAlreadyExistsDef = new("CODE_ALREADY_EXISTS", Conflict,
        "{entity} code already exists: {code}", "รหัส {code} มีอยู่แล้ว");
    public static readonly ErrorDefinition UnknownEventTypeCodesDef = new("UNKNOWN_EVENT_TYPE_CODES", BadRequest,
        "Unknown event type codes: {eventTypes}", "ไม่พบรหัสประเภทเหตุการณ์: {eventTypes}");

    // ------------------------------------------------------- tracking events
    public static readonly ErrorDefinition EventTypeNotConfiguredDef = new("EVENT_TYPE_NOT_CONFIGURED", BadRequest,
        "Event type {eventType} is not configured.", "ยังไม่มีประเภทเหตุการณ์ {eventType} ในระบบ");
    public static readonly ErrorDefinition EventTypeInactiveDef = new("EVENT_TYPE_INACTIVE", BadRequest,
        "Event type {eventType} is inactive.", "ประเภทเหตุการณ์ {eventType} ถูกปิดใช้งานแล้ว");
    public static readonly ErrorDefinition ItemsNotFoundDef = new("ITEMS_NOT_FOUND", BadRequest,
        "Tracked items not found: {items}", "ไม่พบสิ่งของ: {items}");
    public static readonly ErrorDefinition NoItemsForEventDef = new("NO_ITEMS_FOR_EVENT", BadRequest,
        "There are no items to record the event for.", "ไม่มีสิ่งของให้บันทึกเหตุการณ์");
    public static readonly ErrorDefinition ItemsArchivedDef = new("ITEMS_ARCHIVED", BadRequest,
        "Archived items can't be tracked: {items}", "สิ่งของที่เก็บเข้าคลังแล้วบันทึกการติดตามไม่ได้: {items}");
    public static readonly ErrorDefinition ReasonRequiredDef = new("REASON_REQUIRED", BadRequest,
        "Event type {eventType} requires a reasonCode.", "เหตุการณ์ {eventType} ต้องระบุรหัสสาเหตุ");
    public static readonly ErrorDefinition ReasonNotFoundDef = new("REASON_NOT_FOUND", BadRequest,
        "Reason code {reason} does not exist.", "ไม่พบรหัสสาเหตุ {reason}");
    public static readonly ErrorDefinition ReasonInactiveDef = new("REASON_INACTIVE", BadRequest,
        "Reason code {reason} is inactive.", "รหัสสาเหตุ {reason} ถูกปิดใช้งานแล้ว");
    public static readonly ErrorDefinition ReasonNotAllowedDef = new("REASON_NOT_ALLOWED_FOR_EVENT", BadRequest,
        "Reason code {reason} can't be used with {eventType} (allowed: {allowedEventTypes}).",
        "รหัสสาเหตุ {reason} ใช้กับเหตุการณ์ {eventType} ไม่ได้ (ใช้ได้กับ: {allowedEventTypes})");
    public static readonly ErrorDefinition ReasonNoteRequiredDef = new("REASON_NOTE_REQUIRED", BadRequest,
        "Reason code {reason} requires a note explaining what happened.", "รหัสสาเหตุ {reason} ต้องเขียนหมายเหตุอธิบายเพิ่มเติม");
    public static readonly ErrorDefinition EventAlreadyVoidedDef = new("EVENT_ALREADY_VOIDED", Conflict,
        "Event is already voided.", "เหตุการณ์นี้ถูกยกเลิกไปแล้ว");
    public static readonly ErrorDefinition EventSystemManagedDef = new("EVENT_SYSTEM_MANAGED", Conflict,
        "This event was recorded by a shipment or container operation; undo it there (e.g. unload the container) instead of voiding it.",
        "เหตุการณ์นี้บันทึกโดยการทำงานของ Shipment หรือตู้ ต้องแก้ที่ต้นทาง (เช่น นำของออกจากตู้) แทนการยกเลิกโดยตรง");
    public static readonly ErrorDefinition EventItemArchivedDef = new("EVENT_ITEM_ARCHIVED", BadRequest,
        "Events of archived items can't be changed. Restore the item first.", "แก้ไขเหตุการณ์ของสิ่งของที่เก็บเข้าคลังไม่ได้ ต้องนำกลับมาใช้งานก่อน");

    // ---------------------------------------------------------- tracked items
    public static readonly ErrorDefinition TagCodesDuplicatedDef = new("TAG_CODES_DUPLICATED_IN_REQUEST", Conflict,
        "Tag codes appear more than once in the request: {tagCodes}", "รหัสแท็กซ้ำกันในคำขอเดียวกัน: {tagCodes}");
    public static readonly ErrorDefinition TagCodeExistsDef = new("TAG_CODE_EXISTS", Conflict,
        "Tag codes already exist: {tagCodes}", "รหัสแท็กมีอยู่แล้ว: {tagCodes}");
    public static readonly ErrorDefinition MissingRequiredAttributesDef = new("ITEM_MISSING_REQUIRED_ATTRIBUTES", BadRequest,
        "Category {category} requires attributes: {attributes}", "ประเภท {category} ต้องกรอกข้อมูลเพิ่มเติม: {attributes}");
    public static readonly ErrorDefinition ItemArchivedDef = new("ITEM_ARCHIVED", Conflict,
        "Item is archived. Restore it before making changes.", "สิ่งของถูกเก็บเข้าคลังแล้ว ต้องนำกลับมาใช้งานก่อนแก้ไข");

    // --------------------------------------------------------- master data
    public static readonly ErrorDefinition CarrierModeNotSupportedDef = new("CARRIER_MODE_NOT_SUPPORTED", BadRequest,
        "Carrier {carrier} does not operate {mode} transport.", "ผู้ให้บริการ {carrier} ไม่ได้ให้บริการขนส่งแบบ {mode}");
    public static readonly ErrorDefinition ImoOnlyForSeaDef = new("IMO_ONLY_FOR_SEA", BadRequest,
        "ImoNumber applies to Sea vehicles (vessels) only.", "หมายเลข IMO ใช้ได้กับเรือเท่านั้น");

    // ------------------------------------------------------------ containers
    public static readonly ErrorDefinition InvalidIso6346Def = new("INVALID_ISO6346_CONTAINER_NUMBER", BadRequest,
        "{code} is not a valid ISO 6346 container number (e.g. CSQU3054383).", "{code} ไม่ใช่หมายเลขตู้ตามมาตรฐาน ISO 6346 (ตัวอย่าง CSQU3054383)");
    public static readonly ErrorDefinition ContainerInactiveDef = new("CONTAINER_INACTIVE", BadRequest,
        "Container {container} is inactive.", "ตู้/พาเลท {container} ถูกปิดใช้งานแล้ว");
    public static readonly ErrorDefinition ContainerIntoItselfDef = new("CONTAINER_INTO_ITSELF", BadRequest,
        "A container can't be loaded into itself.", "ไม่สามารถบรรจุตู้/พาเลทเข้าไปในตัวเองได้");
    public static readonly ErrorDefinition ContainerCycleDef = new("CONTAINER_CYCLE", BadRequest,
        "A container can't be placed inside itself or one of its own nested containers.", "ไม่สามารถวางตู้/พาเลทไว้ในตัวเองหรือในหน่วยที่อยู่ข้างในตัวมันได้");
    public static readonly ErrorDefinition ItemsInOtherContainerDef = new("ITEMS_IN_OTHER_CONTAINER", Conflict,
        "Items are already in another container (unload them first): {items}", "สิ่งของอยู่ในตู้/พาเลทอื่นแล้ว (ต้องนำออกก่อน): {items}");
    public static readonly ErrorDefinition NotInContainerDef = new("NOT_IN_CONTAINER", BadRequest,
        "Not in container {container}: {entries}", "ไม่ได้อยู่ในตู้/พาเลท {container}: {entries}");
    public static readonly ErrorDefinition ContainerEmptyDef = new("CONTAINER_EMPTY", BadRequest,
        "Container {container} is empty.", "ตู้/พาเลท {container} ว่างอยู่");
    public static readonly ErrorDefinition ContainersNotFoundDef = new("CONTAINERS_NOT_FOUND", BadRequest,
        "Containers not found: {containers}", "ไม่พบตู้/พาเลท: {containers}");

    // ------------------------------------------------------------- shipments
    public static readonly ErrorDefinition TrackingNumberExistsDef = new("TRACKING_NUMBER_EXISTS", Conflict,
        "Tracking number already exists: {trackingNumber}", "เลขติดตาม {trackingNumber} มีอยู่แล้ว");
    public static readonly ErrorDefinition ShipmentStatusNotAllowedDef = new("SHIPMENT_STATUS_NOT_ALLOWED", Conflict,
        "Action '{action}' isn't allowed while the shipment is {status} (allowed when: {allowedStatuses}).",
        "ทำรายการ '{action}' ไม่ได้ขณะที่ Shipment อยู่ในสถานะ {status} (ทำได้เมื่อ: {allowedStatuses})");
    public static readonly ErrorDefinition ShipmentCancelledDef = new("SHIPMENT_CANCELLED", Conflict,
        "Shipment is cancelled.", "Shipment ถูกยกเลิกแล้ว");
    public static readonly ErrorDefinition ShipmentOnCustomsHoldDef = new("SHIPMENT_ON_CUSTOMS_HOLD", Conflict,
        "Shipment is held by customs.", "Shipment ถูกกักที่ศุลกากร");
    public static readonly ErrorDefinition CustomsNotClearedDef = new("CUSTOMS_NOT_CLEARED", BadRequest,
        "Customs is not cleared (status: {customsStatus}).", "ยังไม่ผ่านพิธีการศุลกากร (สถานะ: {customsStatus})");
    public static readonly ErrorDefinition ShipmentHasNoItemsDef = new("SHIPMENT_HAS_NO_ITEMS", BadRequest,
        "Shipment {trackingNumber} has no items.", "Shipment {trackingNumber} ยังไม่มีสิ่งของ");
    public static readonly ErrorDefinition NoItemsToAddDef = new("NO_ITEMS_TO_ADD", BadRequest,
        "No items to add (the containers are empty).", "ไม่มีสิ่งของให้เพิ่ม (ตู้/พาเลทว่าง)");
    public static readonly ErrorDefinition ItemsInOpenShipmentDef = new("ITEMS_IN_OPEN_SHIPMENT", Conflict,
        "Items are already in another open shipment: {items}", "สิ่งของอยู่ใน Shipment อื่นที่ยังไม่ปิด: {items}");
    public static readonly ErrorDefinition ItemNotInShipmentDef = new("ITEM_NOT_IN_SHIPMENT", BadRequest,
        "Item {itemId} is not in shipment {trackingNumber}.", "สิ่งของรหัส {itemId} ไม่ได้อยู่ใน Shipment {trackingNumber}");
    public static readonly ErrorDefinition LegAlreadyDepartedDef = new("LEG_ALREADY_DEPARTED", Conflict,
        "Leg {leg} has already departed.", "ช่วงที่ {leg} ออกเดินทางไปแล้ว");
    public static readonly ErrorDefinition LegAlreadyArrivedDef = new("LEG_ALREADY_ARRIVED", Conflict,
        "Leg {leg} has already arrived.", "ช่วงที่ {leg} ถึงปลายทางแล้ว");
    public static readonly ErrorDefinition LegNotDepartedDef = new("LEG_NOT_DEPARTED", BadRequest,
        "Leg {leg} hasn't departed yet.", "ช่วงที่ {leg} ยังไม่ได้ออกเดินทาง");
    public static readonly ErrorDefinition LegNotArrivedDef = new("LEG_NOT_ARRIVED", BadRequest,
        "Leg {leg} hasn't arrived yet.", "ช่วงที่ {leg} ยังไม่ถึงปลายทาง");
    public static readonly ErrorDefinition PreviousLegNotArrivedDef = new("PREVIOUS_LEG_NOT_ARRIVED", BadRequest,
        "Leg {previousLeg} must arrive before leg {leg} departs.", "ช่วงที่ {previousLeg} ต้องถึงปลายทางก่อนช่วงที่ {leg} จะออกเดินทาง");
    public static readonly ErrorDefinition DepartureBeforePreviousArrivalDef = new("DEPARTURE_BEFORE_PREVIOUS_ARRIVAL", BadRequest,
        "Departure can't be earlier than the previous leg's arrival.", "เวลาออกเดินทางต้องไม่ก่อนเวลาถึงของช่วงก่อนหน้า");
    public static readonly ErrorDefinition ArrivalBeforeDepartureDef = new("ARRIVAL_BEFORE_DEPARTURE", BadRequest,
        "Arrival can't be earlier than departure.", "เวลาถึงต้องไม่ก่อนเวลาออกเดินทาง");
    public static readonly ErrorDefinition LegMustStartAtOriginDef = new("LEG_MUST_START_AT_ORIGIN", BadRequest,
        "The first leg must start at the shipment origin.", "ช่วงแรกต้องเริ่มที่ต้นทางของ Shipment");
    public static readonly ErrorDefinition LegNotContinuousDef = new("LEG_NOT_CONTINUOUS", BadRequest,
        "Must start where leg {previousLeg} ends.", "ต้องเริ่มที่จุดที่ช่วงที่ {previousLeg} สิ้นสุด");
    public static readonly ErrorDefinition LastLegMustEndAtDestinationDef = new("LAST_LEG_MUST_END_AT_DESTINATION", BadRequest,
        "The last leg must end at the shipment destination.", "ช่วงสุดท้ายต้องสิ้นสุดที่ปลายทางของ Shipment");
    public static readonly ErrorDefinition LegScheduleOverlapDef = new("LEG_SCHEDULE_OVERLAP", BadRequest,
        "PlannedDeparture is before leg {previousLeg}'s PlannedArrival.", "เวลาออกตามแผนอยู่ก่อนเวลาถึงตามแผนของช่วงที่ {previousLeg}");
    public static readonly ErrorDefinition VehicleModeMismatchDef = new("VEHICLE_MODE_MISMATCH", BadRequest,
        "Vehicle {vehicle} is a {vehicleMode} vehicle, not {mode}.", "ยานพาหนะ {vehicle} เป็นแบบ {vehicleMode} ไม่ใช่ {mode}");
    public static readonly ErrorDefinition VehicleCarrierMismatchDef = new("VEHICLE_CARRIER_MISMATCH", BadRequest,
        "Vehicle {vehicle} belongs to a different carrier.", "ยานพาหนะ {vehicle} เป็นของผู้ให้บริการรายอื่น");
    public static readonly ErrorDefinition DocumentTypeMismatchDef = new("DOCUMENT_TYPE_MISMATCH", BadRequest,
        "{documentType} is not a {mode} document (expected {expectedDocumentType}).", "{documentType} ไม่ใช่เอกสารของการขนส่งแบบ {mode} (ควรเป็น {expectedDocumentType})");

    // ---------------------------------------------------- proof of delivery
    public static readonly ErrorDefinition SignatureRequiredDef = new("SIGNATURE_REQUIRED", Conflict,
        "This shipment requires the receiver's signature. Use POST /api/v1/Shipments/{id}/proof-of-delivery.",
        "Shipment นี้ต้องมีลายเซ็นผู้รับ ให้ส่งมอบผ่านการบันทึกหลักฐานการส่งมอบ (proof-of-delivery)");
    public static readonly ErrorDefinition ProofOfDeliveryExistsDef = new("PROOF_OF_DELIVERY_EXISTS", Conflict,
        "Shipment {trackingNumber} already has a proof of delivery.", "Shipment {trackingNumber} มีหลักฐานการส่งมอบแล้ว");
    public static readonly ErrorDefinition InvalidFileTypeDef = new("INVALID_FILE_TYPE", BadRequest,
        "{field} must be an image of type: {allowedTypes}.", "{field} ต้องเป็นไฟล์รูปภาพประเภท: {allowedTypes}");
    public static readonly ErrorDefinition FileTooLargeDef = new("FILE_TOO_LARGE", BadRequest,
        "{field} is larger than {maxMegabytes} MB.", "{field} มีขนาดเกิน {maxMegabytes} MB");
    public static readonly ErrorDefinition TooManyPhotosDef = new("TOO_MANY_PHOTOS", BadRequest,
        "At most {max} photos are allowed.", "แนบรูปได้ไม่เกิน {max} รูป");
    public static readonly ErrorDefinition RefusedItemsNotInShipmentDef = new("REFUSED_ITEMS_NOT_IN_SHIPMENT", BadRequest,
        "Refused items are not in this shipment: {tagCodes}", "สิ่งของที่ปฏิเสธไม่ได้อยู่ใน Shipment นี้: {tagCodes}");
    public static readonly ErrorDefinition NoItemsDeliveredDef = new("NO_ITEMS_DELIVERED", BadRequest,
        "Every item was refused; record DELIVERY_FAILED instead of a proof of delivery.",
        "ผู้รับปฏิเสธสิ่งของทุกชิ้น ให้บันทึกเป็นนำส่งไม่สำเร็จแทนการบันทึกหลักฐานการส่งมอบ");

    // All catalog entries, for GET /api/v1/ErrorCodes.
    public static IReadOnlyList<ErrorDefinition> All { get; } = typeof(Errors)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(ErrorDefinition))
        .Select(f => (ErrorDefinition)f.GetValue(null)!)
        .OrderBy(d => d.Code)
        .ToList();

    // ------------------------------------------------------------ factories
    private static ApiException Create(ErrorDefinition definition, params (string Name, object? Value)[] args) =>
        new(definition, args.ToDictionary(a => a.Name, a => a.Value));

    public static ApiException IdempotencyKeyInvalid(int maxLength) => Create(IdempotencyKeyInvalidDef, ("maxLength", maxLength));
    public static ApiException IdempotencyKeyReused(string key) => Create(IdempotencyKeyReusedDef, ("key", key));
    public static ApiException InvalidCursor() => Create(InvalidCursorDef);
    public static ApiException ReferenceNotFound(string field, int id) => Create(ReferenceNotFoundDef, ("field", field), ("id", id));
    public static ApiException ReferenceInactive(string field, string code) => Create(ReferenceInactiveDef, ("field", field), ("code", code));
    public static ApiException CodeAlreadyExists(string entity, string code) => Create(CodeAlreadyExistsDef, ("entity", entity), ("code", code));
    public static ApiException UnknownEventTypeCodes(IEnumerable<string> codes) => Create(UnknownEventTypeCodesDef, ("eventTypes", codes.ToList()));

    public static ApiException EventTypeNotConfigured(string eventType) => Create(EventTypeNotConfiguredDef, ("eventType", eventType));
    public static ApiException EventTypeInactive(string eventType) => Create(EventTypeInactiveDef, ("eventType", eventType));
    public static ApiException ItemsNotFound(IEnumerable<string> items) => Create(ItemsNotFoundDef, ("items", items.ToList()));
    public static ApiException NoItemsForEvent() => Create(NoItemsForEventDef);
    public static ApiException ItemsArchived(IEnumerable<string> items) => Create(ItemsArchivedDef, ("items", items.ToList()));
    public static ApiException ReasonRequired(string eventType) => Create(ReasonRequiredDef, ("eventType", eventType));
    public static ApiException ReasonNotFound(string reason) => Create(ReasonNotFoundDef, ("reason", reason));
    public static ApiException ReasonInactive(string reason) => Create(ReasonInactiveDef, ("reason", reason));
    public static ApiException ReasonNotAllowed(string reason, string eventType, IEnumerable<string> allowed) =>
        Create(ReasonNotAllowedDef, ("reason", reason), ("eventType", eventType), ("allowedEventTypes", allowed.ToList()));
    public static ApiException ReasonNoteRequired(string reason) => Create(ReasonNoteRequiredDef, ("reason", reason));
    public static ApiException EventAlreadyVoided() => Create(EventAlreadyVoidedDef);
    public static ApiException EventSystemManaged() => Create(EventSystemManagedDef);
    public static ApiException EventItemArchived() => Create(EventItemArchivedDef);

    public static ApiException TagCodesDuplicated(IEnumerable<string> tagCodes) => Create(TagCodesDuplicatedDef, ("tagCodes", tagCodes.ToList()));
    public static ApiException TagCodeExists(IEnumerable<string> tagCodes) => Create(TagCodeExistsDef, ("tagCodes", tagCodes.ToList()));
    public static ApiException MissingRequiredAttributes(string category, IEnumerable<string> attributes) =>
        Create(MissingRequiredAttributesDef, ("category", category), ("attributes", attributes.ToList()));
    public static ApiException ItemArchived() => Create(ItemArchivedDef);

    public static ApiException CarrierModeNotSupported(string carrier, object mode) =>
        Create(CarrierModeNotSupportedDef, ("carrier", carrier), ("mode", mode.ToString()));
    public static ApiException ImoOnlyForSea() => Create(ImoOnlyForSeaDef);

    public static ApiException InvalidIso6346(string code) => Create(InvalidIso6346Def, ("code", code));
    public static ApiException ContainerInactive(string container) => Create(ContainerInactiveDef, ("container", container));
    public static ApiException ContainerIntoItself() => Create(ContainerIntoItselfDef);
    public static ApiException ContainerCycle() => Create(ContainerCycleDef);
    public static ApiException ItemsInOtherContainer(IEnumerable<string> items) => Create(ItemsInOtherContainerDef, ("items", items.ToList()));
    public static ApiException NotInContainer(string container, IEnumerable<string> entries) =>
        Create(NotInContainerDef, ("container", container), ("entries", entries.ToList()));
    public static ApiException ContainerEmpty(string container) => Create(ContainerEmptyDef, ("container", container));
    public static ApiException ContainersNotFound(IEnumerable<int> ids) =>
        Create(ContainersNotFoundDef, ("containers", ids.Select(i => i.ToString()).ToList()));

    public static ApiException SignatureRequired() => Create(SignatureRequiredDef);
    public static ApiException ProofOfDeliveryExists(string trackingNumber) => Create(ProofOfDeliveryExistsDef, ("trackingNumber", trackingNumber));
    public static ApiException InvalidFileType(string field, IEnumerable<string> allowedTypes) =>
        Create(InvalidFileTypeDef, ("field", field), ("allowedTypes", allowedTypes.ToList()));
    public static ApiException FileTooLarge(string field, long maxBytes) =>
        Create(FileTooLargeDef, ("field", field), ("maxMegabytes", maxBytes / (1024 * 1024)));
    public static ApiException TooManyPhotos(int max) => Create(TooManyPhotosDef, ("max", max));
    public static ApiException RefusedItemsNotInShipment(IEnumerable<string> tagCodes) =>
        Create(RefusedItemsNotInShipmentDef, ("tagCodes", tagCodes.ToList()));
    public static ApiException NoItemsDelivered() => Create(NoItemsDeliveredDef);

    public static ApiException TrackingNumberExists(string trackingNumber) => Create(TrackingNumberExistsDef, ("trackingNumber", trackingNumber));
    public static ApiException ShipmentStatusNotAllowed(string action, object status, IEnumerable<object> allowed) =>
        Create(ShipmentStatusNotAllowedDef, ("action", action), ("status", status.ToString()),
            ("allowedStatuses", allowed.Select(a => a.ToString()!).ToList()));
    public static ApiException ShipmentCancelled() => Create(ShipmentCancelledDef);
    public static ApiException ShipmentOnCustomsHold() => Create(ShipmentOnCustomsHoldDef);
    public static ApiException CustomsNotCleared(object customsStatus) => Create(CustomsNotClearedDef, ("customsStatus", customsStatus.ToString()));
    public static ApiException ShipmentHasNoItems(string trackingNumber) => Create(ShipmentHasNoItemsDef, ("trackingNumber", trackingNumber));
    public static ApiException NoItemsToAdd() => Create(NoItemsToAddDef);
    public static ApiException ItemsInOpenShipment(IEnumerable<string> items) => Create(ItemsInOpenShipmentDef, ("items", items.ToList()));
    public static ApiException ItemNotInShipment(int itemId, string trackingNumber) =>
        Create(ItemNotInShipmentDef, ("itemId", itemId), ("trackingNumber", trackingNumber));
    public static ApiException LegAlreadyDeparted(int leg) => Create(LegAlreadyDepartedDef, ("leg", leg));
    public static ApiException LegAlreadyArrived(int leg) => Create(LegAlreadyArrivedDef, ("leg", leg));
    public static ApiException LegNotDeparted(int leg) => Create(LegNotDepartedDef, ("leg", leg));
    public static ApiException LegNotArrived(int leg) => Create(LegNotArrivedDef, ("leg", leg));
    public static ApiException PreviousLegNotArrived(int previousLeg, int leg) =>
        Create(PreviousLegNotArrivedDef, ("previousLeg", previousLeg), ("leg", leg));
    public static ApiException DepartureBeforePreviousArrival() => Create(DepartureBeforePreviousArrivalDef);
    public static ApiException ArrivalBeforeDeparture() => Create(ArrivalBeforeDepartureDef);
    public static ApiException LegMustStartAtOrigin() => Create(LegMustStartAtOriginDef);
    public static ApiException LegNotContinuous(int previousLeg) => Create(LegNotContinuousDef, ("previousLeg", previousLeg));
    public static ApiException LastLegMustEndAtDestination() => Create(LastLegMustEndAtDestinationDef);
    public static ApiException LegScheduleOverlap(int previousLeg) => Create(LegScheduleOverlapDef, ("previousLeg", previousLeg));
    public static ApiException VehicleModeMismatch(string vehicle, object vehicleMode, object mode) =>
        Create(VehicleModeMismatchDef, ("vehicle", vehicle), ("vehicleMode", vehicleMode.ToString()), ("mode", mode.ToString()));
    public static ApiException VehicleCarrierMismatch(string vehicle) => Create(VehicleCarrierMismatchDef, ("vehicle", vehicle));
    public static ApiException DocumentTypeMismatch(object documentType, object mode, object expected) =>
        Create(DocumentTypeMismatchDef, ("documentType", documentType.ToString()), ("mode", mode.ToString()),
            ("expectedDocumentType", expected.ToString()));
}
