-- Feature: Table and column comments (English | ภาษาไทย)
--
-- Documents every table and column in the database itself, so tools such as
-- DBeaver / pgAdmin show the meaning next to the schema. Format: "English | ไทย".
-- COMMENT ON simply overwrites, so this script is safe to re-run.
-- When adding a table or column, add its comment here (or in the feature's script);
-- Scripts/maintenance/health_check.sql section 7 lists anything left undocumented.

-- ---------------------------------------------------------------------------
-- Columns shared by many tables (specific comments below override these)
-- ---------------------------------------------------------------------------
DO $$
DECLARE
    t TEXT;
BEGIN
    FOR t IN
        SELECT c.relname FROM pg_class c
        WHERE c.relnamespace = 'public'::regnamespace AND c.relkind IN ('r', 'p') AND NOT c.relispartition
    LOOP
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = t AND column_name = 'Id') THEN
            EXECUTE format('COMMENT ON COLUMN %I."Id" IS %L', t, 'Primary key (auto-generated) | รหัสอ้างอิงหลัก (ระบบสร้างให้)');
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = t AND column_name = 'Code') THEN
            EXECUTE format('COMMENT ON COLUMN %I."Code" IS %L', t, 'Unique business code, stored upper-case | รหัสอ้างอิงทางธุรกิจ ไม่ซ้ำ เก็บเป็นตัวพิมพ์ใหญ่');
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = t AND column_name = 'IsActive') THEN
            EXECUTE format('COMMENT ON COLUMN %I."IsActive" IS %L', t, 'Active flag; inactive rows are kept for existing references but cannot be newly assigned | สถานะเปิดใช้งาน หากปิดจะยังคงอยู่สำหรับข้อมูลเดิมที่อ้างอิง แต่เลือกใช้ใหม่ไม่ได้');
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = t AND column_name = 'CreatedAt') THEN
            EXECUTE format('COMMENT ON COLUMN %I."CreatedAt" IS %L', t, 'Created at (UTC) | วันเวลาที่สร้าง (UTC)');
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = t AND column_name = 'UpdatedAt') THEN
            EXECUTE format('COMMENT ON COLUMN %I."UpdatedAt" IS %L', t, 'Last updated at (UTC) | วันเวลาที่แก้ไขล่าสุด (UTC)');
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = t AND column_name = 'CreatedBy') THEN
            EXECUTE format('COMMENT ON COLUMN %I."CreatedBy" IS %L', t, 'Username of the creator (from JWT) | ชื่อผู้ใช้ที่สร้างรายการ (จาก JWT)');
        END IF;
    END LOOP;
END $$;

-- ---------------------------------------------------------------------------
-- Authentication / authorization
-- ---------------------------------------------------------------------------
COMMENT ON TABLE "Users" IS 'User accounts that can log in to the API | บัญชีผู้ใช้ที่เข้าสู่ระบบ API ได้';
COMMENT ON COLUMN "Users"."Username" IS 'Login name, unique | ชื่อสำหรับเข้าสู่ระบบ ไม่ซ้ำ';
COMMENT ON COLUMN "Users"."PasswordHash" IS 'BCrypt hash of the password (never the plain password) | รหัสผ่านที่เข้ารหัสด้วย BCrypt (ไม่เก็บรหัสผ่านจริง)';
COMMENT ON COLUMN "Users"."Role" IS 'Role name (free text, e.g. Admin, User); permissions come from RolePermissions | ชื่อบทบาท (ข้อความอิสระ เช่น Admin, User) สิทธิ์กำหนดใน RolePermissions';

COMMENT ON TABLE "Permissions" IS 'Catalog of permission codes used by [Authorize(Policy)] | รายการรหัสสิทธิ์ที่ใช้กับ [Authorize(Policy)]';
COMMENT ON COLUMN "Permissions"."Code" IS 'Permission code, e.g. Products.Read | รหัสสิทธิ์ เช่น Products.Read';
COMMENT ON COLUMN "Permissions"."Description" IS 'What the permission allows | คำอธิบายว่าสิทธิ์นี้อนุญาตให้ทำอะไร';

COMMENT ON TABLE "RolePermissions" IS 'Which role grants which permission (edit to change access without redeploying) | บทบาทใดได้สิทธิ์ใดบ้าง (แก้ข้อมูลเพื่อเปลี่ยนสิทธิ์ได้โดยไม่ต้อง deploy ใหม่)';
COMMENT ON COLUMN "RolePermissions"."Role" IS 'Role name, same value as Users.Role | ชื่อบทบาท ตรงกับ Users.Role';
COMMENT ON COLUMN "RolePermissions"."PermissionId" IS 'Granted permission (FK Permissions) | สิทธิ์ที่ได้รับ (อ้างอิง Permissions)';

COMMENT ON TABLE "Products" IS 'Legacy sample product catalog (predates Thing-Tag) | ตารางสินค้าตัวอย่างเดิม (มีก่อนระบบ Thing-Tag)';
COMMENT ON COLUMN "Products"."Name" IS 'Product name | ชื่อสินค้า';
COMMENT ON COLUMN "Products"."Price" IS 'Unit price | ราคาต่อหน่วย';

-- ---------------------------------------------------------------------------
-- Master data
-- ---------------------------------------------------------------------------
COMMENT ON TABLE "ItemCategories" IS 'Master data: item categories (electronics, food, dangerous goods, ...) | ข้อมูลหลัก: ประเภทสิ่งของ (อิเล็กทรอนิกส์ อาหาร สินค้าอันตราย ฯลฯ)';
COMMENT ON COLUMN "ItemCategories"."Name" IS 'Category name | ชื่อประเภท';
COMMENT ON COLUMN "ItemCategories"."Description" IS 'Description | คำอธิบาย';
COMMENT ON COLUMN "ItemCategories"."RequiredAttributes" IS 'Attribute keys every item in this category must fill in (e.g. serialNumber, expiry) | ชื่อข้อมูลเพิ่มเติมที่สิ่งของในประเภทนี้ต้องกรอก (เช่น serialNumber, expiry)';
COMMENT ON COLUMN "ItemCategories"."IsFragile" IS 'Fragile, handle with care | แตกง่าย ต้องระมัดระวังในการขนส่ง';
COMMENT ON COLUMN "ItemCategories"."RequiresTemperatureControl" IS 'Needs temperature control (cold chain) | ต้องควบคุมอุณหภูมิ (ห่วงโซ่ความเย็น)';
COMMENT ON COLUMN "ItemCategories"."IsDangerousGoods" IS 'Dangerous goods under IATA DGR / IMDG | สินค้าอันตรายตามข้อกำหนด IATA DGR / IMDG';
COMMENT ON COLUMN "ItemCategories"."UnNumber" IS 'UN number for dangerous goods, e.g. UN1263 | หมายเลข UN ของสินค้าอันตราย เช่น UN1263';
COMMENT ON COLUMN "ItemCategories"."HazardClass" IS 'Hazard class, e.g. 3 (flammable liquid), 2.1 | ประเภทความอันตราย เช่น 3 (ของเหลวไวไฟ), 2.1';

COMMENT ON TABLE "Locations" IS 'Master data: places items move between (warehouses, hubs, ports, airports, customer addresses) | ข้อมูลหลัก: สถานที่ที่สิ่งของเคลื่อนผ่าน (คลังสินค้า ศูนย์กระจาย ท่าเรือ สนามบิน ที่อยู่ลูกค้า)';
COMMENT ON COLUMN "Locations"."Name" IS 'Location name | ชื่อสถานที่';
COMMENT ON COLUMN "Locations"."Type" IS 'LocationType: Warehouse, Hub, Branch, DropPoint, CustomerAddress, Port, Airport, RailStation, ContainerYard, CustomsOffice, BorderCrossing, Other | ประเภทสถานที่: คลังสินค้า ศูนย์กระจาย สาขา จุดรับส่ง ที่อยู่ลูกค้า ท่าเรือ สนามบิน สถานีรถไฟ ลานตู้ ด่านศุลกากร ด่านชายแดน อื่น ๆ';
COMMENT ON COLUMN "Locations"."AddressLine" IS 'Street address | ที่อยู่ (บ้านเลขที่ ถนน)';
COMMENT ON COLUMN "Locations"."SubDistrict" IS 'Sub-district | ตำบล/แขวง';
COMMENT ON COLUMN "Locations"."District" IS 'District | อำเภอ/เขต';
COMMENT ON COLUMN "Locations"."Province" IS 'Province / state | จังหวัด';
COMMENT ON COLUMN "Locations"."PostalCode" IS 'Postal code | รหัสไปรษณีย์';
COMMENT ON COLUMN "Locations"."Country" IS 'ISO 3166-1 alpha-2 country code, e.g. TH | รหัสประเทศ 2 ตัวอักษร ISO 3166-1 เช่น TH';
COMMENT ON COLUMN "Locations"."Latitude" IS 'Latitude (WGS84) | ละติจูด (WGS84)';
COMMENT ON COLUMN "Locations"."Longitude" IS 'Longitude (WGS84) | ลองจิจูด (WGS84)';
COMMENT ON COLUMN "Locations"."ContactName" IS 'Contact person | ชื่อผู้ติดต่อ';
COMMENT ON COLUMN "Locations"."ContactPhone" IS 'Contact phone | เบอร์โทรผู้ติดต่อ';
COMMENT ON COLUMN "Locations"."UnLocode" IS 'UN/LOCODE, e.g. THLCH (Laem Chabang) | รหัสสถานที่สากล UN/LOCODE เช่น THLCH (แหลมฉบัง)';
COMMENT ON COLUMN "Locations"."IataCode" IS 'IATA airport code, e.g. BKK | รหัสสนามบิน IATA เช่น BKK';
COMMENT ON COLUMN "Locations"."TimeZone" IS 'IANA time zone for showing local times, e.g. Asia/Bangkok | เขตเวลา IANA สำหรับแสดงเวลาท้องถิ่น เช่น Asia/Bangkok';

COMMENT ON TABLE "Parties" IS 'Master data: senders, receivers, customers and item owners | ข้อมูลหลัก: ผู้ส่ง ผู้รับ ลูกค้า และเจ้าของสิ่งของ';
COMMENT ON COLUMN "Parties"."Name" IS 'Person or company name | ชื่อบุคคลหรือบริษัท';
COMMENT ON COLUMN "Parties"."Type" IS 'PartyType: Individual, Company | ประเภท: บุคคล, บริษัท';
COMMENT ON COLUMN "Parties"."TaxId" IS 'Tax identification number | เลขประจำตัวผู้เสียภาษี';
COMMENT ON COLUMN "Parties"."ContactName" IS 'Contact person | ชื่อผู้ติดต่อ';
COMMENT ON COLUMN "Parties"."Phone" IS 'Phone number | เบอร์โทรศัพท์';
COMMENT ON COLUMN "Parties"."Email" IS 'Email address | อีเมล';
COMMENT ON COLUMN "Parties"."AddressLine" IS 'Street address | ที่อยู่ (บ้านเลขที่ ถนน)';
COMMENT ON COLUMN "Parties"."SubDistrict" IS 'Sub-district | ตำบล/แขวง';
COMMENT ON COLUMN "Parties"."District" IS 'District | อำเภอ/เขต';
COMMENT ON COLUMN "Parties"."Province" IS 'Province / state | จังหวัด';
COMMENT ON COLUMN "Parties"."PostalCode" IS 'Postal code | รหัสไปรษณีย์';
COMMENT ON COLUMN "Parties"."Country" IS 'ISO 3166-1 alpha-2 country code, e.g. TH | รหัสประเทศ 2 ตัวอักษร ISO 3166-1 เช่น TH';

COMMENT ON TABLE "EventTypes" IS 'Master data: kinds of tracking events and the item status each one produces | ข้อมูลหลัก: ประเภทเหตุการณ์การติดตาม และสถานะสิ่งของที่เกิดขึ้นหลังเหตุการณ์';
COMMENT ON COLUMN "EventTypes"."Code" IS 'Event code, e.g. PICKED_UP; some codes are used by the API itself (see CLAUDE.md) | รหัสเหตุการณ์ เช่น PICKED_UP บางรหัสถูกใช้โดยระบบโดยตรง (ดู CLAUDE.md)';
COMMENT ON COLUMN "EventTypes"."NameTh" IS 'Thai name | ชื่อภาษาไทย';
COMMENT ON COLUMN "EventTypes"."NameEn" IS 'English name | ชื่อภาษาอังกฤษ';
COMMENT ON COLUMN "EventTypes"."Description" IS 'Description | คำอธิบาย';
COMMENT ON COLUMN "EventTypes"."ResultingStatus" IS 'ItemStatus the item moves to when recorded; NULL = informational, status unchanged | สถานะของสิ่งของหลังบันทึกเหตุการณ์ ถ้าว่างคือเหตุการณ์แจ้งข้อมูล สถานะไม่เปลี่ยน';
COMMENT ON COLUMN "EventTypes"."IsTerminal" IS 'No further events expected afterwards (e.g. Delivered) | เหตุการณ์สุดท้าย ไม่คาดว่าจะมีเหตุการณ์ต่อ (เช่น ส่งสำเร็จ)';
COMMENT ON COLUMN "EventTypes"."RequiresReason" IS 'Recording this event needs a reason code | ต้องระบุรหัสสาเหตุเมื่อบันทึกเหตุการณ์นี้';
COMMENT ON COLUMN "EventTypes"."SortOrder" IS 'Display order (logistics lifecycle order) | ลำดับการแสดงผล (ตามขั้นตอนการขนส่ง)';

COMMENT ON TABLE "ReasonCodes" IS 'Master data: reasons for exception events (failed delivery, damage, customs hold, ...) | ข้อมูลหลัก: สาเหตุของเหตุการณ์ผิดปกติ (ส่งไม่สำเร็จ เสียหาย ติดศุลกากร ฯลฯ)';
COMMENT ON COLUMN "ReasonCodes"."NameTh" IS 'Thai name | ชื่อภาษาไทย';
COMMENT ON COLUMN "ReasonCodes"."NameEn" IS 'English name | ชื่อภาษาอังกฤษ';
COMMENT ON COLUMN "ReasonCodes"."Description" IS 'Description | คำอธิบาย';
COMMENT ON COLUMN "ReasonCodes"."EventTypeCodes" IS 'Event type codes this reason applies to; empty = any | รหัสประเภทเหตุการณ์ที่ใช้สาเหตุนี้ได้ ถ้าว่างคือใช้ได้ทุกเหตุการณ์';
COMMENT ON COLUMN "ReasonCodes"."RequiresNote" IS 'The event must include a note explaining (e.g. OTHER) | ต้องเขียนหมายเหตุอธิบายเพิ่ม (เช่น อื่น ๆ)';
COMMENT ON COLUMN "ReasonCodes"."SortOrder" IS 'Display order | ลำดับการแสดงผล';

COMMENT ON TABLE "Carriers" IS 'Master data: transport providers (trucking, railway, airline, shipping line, courier) | ข้อมูลหลัก: ผู้ให้บริการขนส่ง (รถบรรทุก รถไฟ สายการบิน สายเรือ ขนส่งด่วน)';
COMMENT ON COLUMN "Carriers"."Name" IS 'Carrier name | ชื่อผู้ให้บริการ';
COMMENT ON COLUMN "Carriers"."Modes" IS 'TransportMode names the carrier operates: Road, Rail, Air, Sea, Courier | รูปแบบการขนส่งที่ให้บริการ: ถนน รถไฟ อากาศ ทะเล ขนส่งด่วน';
COMMENT ON COLUMN "Carriers"."ScacCode" IS 'Standard Carrier Alpha Code, e.g. MAEU | รหัสผู้ขนส่งมาตรฐาน SCAC เช่น MAEU';
COMMENT ON COLUMN "Carriers"."IataCode" IS 'IATA airline designator, e.g. TG | รหัสสายการบิน IATA เช่น TG';
COMMENT ON COLUMN "Carriers"."ContactName" IS 'Contact person | ชื่อผู้ติดต่อ';
COMMENT ON COLUMN "Carriers"."Phone" IS 'Phone number | เบอร์โทรศัพท์';
COMMENT ON COLUMN "Carriers"."Email" IS 'Email address | อีเมล';
COMMENT ON COLUMN "Carriers"."Website" IS 'Website URL | เว็บไซต์';
COMMENT ON COLUMN "Carriers"."TrackingUrlTemplate" IS 'Carrier tracking page URL; {number} is replaced by the document number | ลิงก์หน้าติดตามของผู้ให้บริการ {number} จะถูกแทนด้วยเลขเอกสาร';

COMMENT ON TABLE "Vehicles" IS 'Master data: trucks, trains, aircraft and vessels | ข้อมูลหลัก: ยานพาหนะ (รถบรรทุก รถไฟ เครื่องบิน เรือ)';
COMMENT ON COLUMN "Vehicles"."Name" IS 'Vehicle name, e.g. vessel name | ชื่อยานพาหนะ เช่น ชื่อเรือ';
COMMENT ON COLUMN "Vehicles"."Mode" IS 'TransportMode: Road, Rail, Air, Sea, Courier | รูปแบบการขนส่ง: ถนน รถไฟ อากาศ ทะเล ขนส่งด่วน';
COMMENT ON COLUMN "Vehicles"."CarrierId" IS 'Operating carrier (FK Carriers) | ผู้ให้บริการที่เป็นเจ้าของ/ดำเนินการ (อ้างอิง Carriers)';
COMMENT ON COLUMN "Vehicles"."RegistrationNumber" IS 'Licence plate, aircraft registration or train set number | ทะเบียนรถ ทะเบียนเครื่องบิน หรือหมายเลขขบวนรถไฟ';
COMMENT ON COLUMN "Vehicles"."ImoNumber" IS 'IMO ship number, 7 digits (vessels only) | หมายเลขเรือ IMO 7 หลัก (เฉพาะเรือ)';
COMMENT ON COLUMN "Vehicles"."CapacityKg" IS 'Maximum load in kg | น้ำหนักบรรทุกสูงสุด (กิโลกรัม)';

COMMENT ON TABLE "Containers" IS 'Handling units that hold items or other containers (pallet, ISO container, air ULD); nestable | หน่วยบรรจุที่ใส่สิ่งของหรือหน่วยบรรจุอื่น (พาเลท ตู้คอนเทนเนอร์ ULD) ซ้อนกันได้';
COMMENT ON COLUMN "Containers"."Code" IS 'Container code; ISO 6346 number for shipping containers, e.g. CSQU3054383 | รหัสหน่วยบรรจุ ตู้คอนเทนเนอร์ใช้เลข ISO 6346 เช่น CSQU3054383';
COMMENT ON COLUMN "Containers"."Type" IS 'ContainerType: Pallet, Box, Container20GP, Container40GP, Container40HC, Reefer20, Reefer40, Uld, Other | ประเภท: พาเลท กล่อง ตู้ 20/40 ฟุต ตู้สูง ตู้เย็น ULD อื่น ๆ';
COMMENT ON COLUMN "Containers"."SealNumber" IS 'Seal number | หมายเลขซีล';
COMMENT ON COLUMN "Containers"."ParentContainerId" IS 'Container this one is inside (FK Containers), e.g. pallet inside a container | หน่วยบรรจุที่ตัวนี้อยู่ข้างใน (อ้างอิง Containers) เช่น พาเลทในตู้';
COMMENT ON COLUMN "Containers"."CurrentLocationId" IS 'Where the container is now (FK Locations) | ตำแหน่งปัจจุบันของหน่วยบรรจุ (อ้างอิง Locations)';
COMMENT ON COLUMN "Containers"."MaxPayloadKg" IS 'Maximum payload in kg | น้ำหนักบรรจุสูงสุด (กิโลกรัม)';

-- ---------------------------------------------------------------------------
-- Tracked items and history
-- ---------------------------------------------------------------------------
COMMENT ON TABLE "TrackedItems" IS 'Tagged physical items; also holds each item''s current state (status, location) | สิ่งของที่ติดแท็ก 1 แถวต่อของจริง 1 ชิ้น และเก็บสถานะปัจจุบัน (สถานะ ตำแหน่ง)';
COMMENT ON COLUMN "TrackedItems"."TagCode" IS 'Tag code on the item (QR / barcode / RFID), unique, upper-case | รหัสแท็กบนสิ่งของ (QR/บาร์โค้ด/RFID) ไม่ซ้ำ ตัวพิมพ์ใหญ่';
COMMENT ON COLUMN "TrackedItems"."Name" IS 'Item name | ชื่อสิ่งของ';
COMMENT ON COLUMN "TrackedItems"."Description" IS 'Description | รายละเอียด';
COMMENT ON COLUMN "TrackedItems"."Status" IS 'Current ItemStatus (Registered, InTransit, Delivered, Returned, Lost, Damaged, OnHold); changed only by tracking events | สถานะปัจจุบัน เปลี่ยนผ่านเหตุการณ์การติดตามเท่านั้น';
COMMENT ON COLUMN "TrackedItems"."WeightKg" IS 'Weight in kg | น้ำหนัก (กิโลกรัม)';
COMMENT ON COLUMN "TrackedItems"."LengthCm" IS 'Length in cm | ความยาว (เซนติเมตร)';
COMMENT ON COLUMN "TrackedItems"."WidthCm" IS 'Width in cm | ความกว้าง (เซนติเมตร)';
COMMENT ON COLUMN "TrackedItems"."HeightCm" IS 'Height in cm | ความสูง (เซนติเมตร)';
COMMENT ON COLUMN "TrackedItems"."DeclaredValue" IS 'Declared value (see Currency) | มูลค่าที่สำแดง (ดูสกุลเงินใน Currency)';
COMMENT ON COLUMN "TrackedItems"."Currency" IS 'ISO 4217 currency of DeclaredValue, e.g. THB | สกุลเงินของมูลค่าที่สำแดง ISO 4217 เช่น THB';
COMMENT ON COLUMN "TrackedItems"."Attributes" IS 'Extra key/value fields per item type (JSON), e.g. serialNumber | ข้อมูลเพิ่มเติมตามชนิดสิ่งของ (JSON) เช่น serialNumber';
COMMENT ON COLUMN "TrackedItems"."IsArchived" IS 'Archived instead of deleted so history is kept | เก็บเข้าคลังแทนการลบ เพื่อรักษาประวัติ';
COMMENT ON COLUMN "TrackedItems"."CategoryId" IS 'Item category (FK ItemCategories) | ประเภทสิ่งของ (อ้างอิง ItemCategories)';
COMMENT ON COLUMN "TrackedItems"."CurrentLocationId" IS 'Current location (FK Locations); changed only by tracking events | ตำแหน่งปัจจุบัน (อ้างอิง Locations) เปลี่ยนผ่านเหตุการณ์เท่านั้น';
COMMENT ON COLUMN "TrackedItems"."OwnerPartyId" IS 'Owner of the item (FK Parties) | เจ้าของสิ่งของ (อ้างอิง Parties)';
COMMENT ON COLUMN "TrackedItems"."CurrentContainerId" IS 'Pallet / container the item is packed in (FK Containers) | พาเลท/ตู้ที่สิ่งของบรรจุอยู่ (อ้างอิง Containers)';
COMMENT ON COLUMN "TrackedItems"."HsCode" IS 'Customs Harmonized System code, e.g. 8471.30 | พิกัดศุลกากร HS Code เช่น 8471.30';
COMMENT ON COLUMN "TrackedItems"."OriginCountry" IS 'Country of origin, ISO 3166-1 alpha-2 | ประเทศแหล่งกำเนิดสินค้า (ISO 3166-1)';
COMMENT ON COLUMN "TrackedItems"."LastEventAt" IS 'OccurredAt of the newest tracking event; older back-dated events do not change current state | เวลาของเหตุการณ์ล่าสุด เหตุการณ์ย้อนหลังที่เก่ากว่าจะไม่เปลี่ยนสถานะปัจจุบัน';

COMMENT ON TABLE "TrackingEvents" IS 'Item movement history; never updated or deleted (wrong events are voided). Partitioned by month on OccurredAt | ประวัติการเคลื่อนไหวของสิ่งของ ไม่แก้ไขหรือลบ (รายการผิดใช้การยกเลิก) แบ่งตารางรายเดือนตาม OccurredAt';
COMMENT ON COLUMN "TrackingEvents"."Id" IS 'Event id, globally unique (primary key is Id + OccurredAt because of partitioning) | รหัสเหตุการณ์ ไม่ซ้ำทั้งระบบ (primary key คือ Id + OccurredAt เพราะแบ่งตาราง)';
COMMENT ON COLUMN "TrackingEvents"."TrackedItemId" IS 'Item this event belongs to (FK TrackedItems) | สิ่งของที่เกิดเหตุการณ์ (อ้างอิง TrackedItems)';
COMMENT ON COLUMN "TrackingEvents"."EventTypeId" IS 'What happened (FK EventTypes) | ประเภทเหตุการณ์ (อ้างอิง EventTypes)';
COMMENT ON COLUMN "TrackingEvents"."LocationId" IS 'Where it happened (FK Locations) | สถานที่ที่เกิดเหตุการณ์ (อ้างอิง Locations)';
COMMENT ON COLUMN "TrackingEvents"."OccurredAt" IS 'When it actually happened (UTC, may be back-dated); partition key | เวลาที่เกิดขึ้นจริง (UTC อาจย้อนหลังได้) ใช้แบ่งตาราง';
COMMENT ON COLUMN "TrackingEvents"."RecordedAt" IS 'When it was entered into the system (UTC) | เวลาที่บันทึกเข้าระบบ (UTC)';
COMMENT ON COLUMN "TrackingEvents"."RecordedBy" IS 'Username who recorded it | ชื่อผู้ใช้ที่บันทึก';
COMMENT ON COLUMN "TrackingEvents"."Source" IS 'EventSource: Manual, Scan, Container, Shipment | ที่มาของเหตุการณ์: บันทึกเอง สแกน จากตู้ จาก Shipment';
COMMENT ON COLUMN "TrackingEvents"."Note" IS 'Free-text note | หมายเหตุ';
COMMENT ON COLUMN "TrackingEvents"."Latitude" IS 'GPS latitude at the event | ละติจูด GPS ขณะเกิดเหตุการณ์';
COMMENT ON COLUMN "TrackingEvents"."Longitude" IS 'GPS longitude at the event | ลองจิจูด GPS ขณะเกิดเหตุการณ์';
COMMENT ON COLUMN "TrackingEvents"."ShipmentId" IS 'Related shipment (FK Shipments) | Shipment ที่เกี่ยวข้อง (อ้างอิง Shipments)';
COMMENT ON COLUMN "TrackingEvents"."ShipmentLegId" IS 'Related shipment leg (FK ShipmentLegs) | ช่วงการขนส่งที่เกี่ยวข้อง (อ้างอิง ShipmentLegs)';
COMMENT ON COLUMN "TrackingEvents"."ContainerId" IS 'Related container (FK Containers) | หน่วยบรรจุที่เกี่ยวข้อง (อ้างอิง Containers)';
COMMENT ON COLUMN "TrackingEvents"."ReasonCodeId" IS 'Reason for an exception event (FK ReasonCodes) | สาเหตุของเหตุการณ์ผิดปกติ (อ้างอิง ReasonCodes)';
COMMENT ON COLUMN "TrackingEvents"."IsSystemManaged" IS 'Recorded by a shipment/container operation; undo it there, cannot be voided directly | บันทึกโดยการทำงานของ Shipment/ตู้ ต้องแก้ที่ต้นทาง ยกเลิกตรง ๆ ไม่ได้';
COMMENT ON COLUMN "TrackingEvents"."IsVoided" IS 'Voided (cancelled) but kept for audit | ถูกยกเลิกแล้ว แต่เก็บไว้เพื่อการตรวจสอบ';
COMMENT ON COLUMN "TrackingEvents"."VoidedAt" IS 'When it was voided (UTC) | เวลาที่ยกเลิก (UTC)';
COMMENT ON COLUMN "TrackingEvents"."VoidedBy" IS 'Username who voided it | ชื่อผู้ใช้ที่ยกเลิก';
COMMENT ON COLUMN "TrackingEvents"."VoidReason" IS 'Why it was voided | เหตุผลที่ยกเลิก';
COMMENT ON COLUMN "TrackingEvents"."ReplacesEventId" IS 'On a correction: the voided event this one replaces (no DB FK because of partitioning) | กรณีแก้ไข: เหตุการณ์เดิมที่ถูกแทนที่ (ไม่มี FK ในฐานข้อมูลเพราะแบ่งตาราง)';

-- ---------------------------------------------------------------------------
-- Shipments
-- ---------------------------------------------------------------------------
COMMENT ON TABLE "Shipments" IS 'Consignments from a sender/origin to a receiver/destination over one or more legs | การขนส่งจากผู้ส่ง/ต้นทาง ถึงผู้รับ/ปลายทาง ผ่าน 1 ช่วงหรือหลายช่วง';
COMMENT ON COLUMN "Shipments"."TrackingNumber" IS 'Tracking number, unique (generated TS + date + random if not given) | เลขติดตามพัสดุ ไม่ซ้ำ (ระบบสร้าง TS + วันที่ + สุ่ม ถ้าไม่ระบุ)';
COMMENT ON COLUMN "Shipments"."Reference" IS 'Customer reference (PO / order number) | เลขอ้างอิงของลูกค้า (เลขใบสั่งซื้อ/คำสั่งซื้อ)';
COMMENT ON COLUMN "Shipments"."SenderPartyId" IS 'Sender (FK Parties) | ผู้ส่ง (อ้างอิง Parties)';
COMMENT ON COLUMN "Shipments"."ReceiverPartyId" IS 'Receiver (FK Parties) | ผู้รับ (อ้างอิง Parties)';
COMMENT ON COLUMN "Shipments"."OriginLocationId" IS 'Origin (FK Locations) | ต้นทาง (อ้างอิง Locations)';
COMMENT ON COLUMN "Shipments"."DestinationLocationId" IS 'Destination (FK Locations) | ปลายทาง (อ้างอิง Locations)';
COMMENT ON COLUMN "Shipments"."Status" IS 'ShipmentStatus: Planned, InTransit, Delivered, Cancelled, ReturnedToSender | สถานะ: วางแผน กำลังขนส่ง ส่งสำเร็จ ยกเลิก ตีกลับผู้ส่ง';
COMMENT ON COLUMN "Shipments"."Incoterm" IS 'Incoterms 2020 rule, e.g. FOB, CIF, DAP (international) | เงื่อนไขการส่งมอบ Incoterms 2020 เช่น FOB, CIF, DAP (ระหว่างประเทศ)';
COMMENT ON COLUMN "Shipments"."CustomsStatus" IS 'CustomsStatus: NotRequired, Pending, InProgress, Hold, Cleared | สถานะศุลกากร: ไม่ต้องผ่าน รอดำเนินการ กำลังดำเนินการ ถูกกัก ผ่านแล้ว';
COMMENT ON COLUMN "Shipments"."PlannedPickupAt" IS 'Planned pickup time (UTC) | เวลารับของตามแผน (UTC)';
COMMENT ON COLUMN "Shipments"."DeliveredAt" IS 'Actual delivery time (UTC) | เวลาส่งมอบจริง (UTC)';
COMMENT ON COLUMN "Shipments"."Notes" IS 'Notes | หมายเหตุ';

COMMENT ON TABLE "ShipmentItems" IS 'Items in each shipment (an item can be in only one open shipment at a time) | รายการสิ่งของในแต่ละ Shipment (สิ่งของ 1 ชิ้นอยู่ได้ใน Shipment ที่ยังเปิดอยู่ได้ครั้งละ 1 รายการ)';
COMMENT ON COLUMN "ShipmentItems"."ShipmentId" IS 'Shipment (FK Shipments) | Shipment (อ้างอิง Shipments)';
COMMENT ON COLUMN "ShipmentItems"."TrackedItemId" IS 'Item (FK TrackedItems) | สิ่งของ (อ้างอิง TrackedItems)';
COMMENT ON COLUMN "ShipmentItems"."AddedAt" IS 'When the item was added (UTC) | เวลาที่เพิ่มสิ่งของเข้า Shipment (UTC)';

COMMENT ON TABLE "ShipmentLegs" IS 'Segments of a shipment, each by one mode/vehicle (road -> sea -> road ...) | ช่วงการขนส่งของ Shipment แต่ละช่วงใช้รูปแบบ/พาหนะเดียว (ถนน -> ทะเล -> ถนน ...)';
COMMENT ON COLUMN "ShipmentLegs"."ShipmentId" IS 'Shipment (FK Shipments) | Shipment (อ้างอิง Shipments)';
COMMENT ON COLUMN "ShipmentLegs"."Sequence" IS 'Order within the shipment, starting at 1 | ลำดับช่วงใน Shipment เริ่มที่ 1';
COMMENT ON COLUMN "ShipmentLegs"."Mode" IS 'TransportMode: Road, Rail, Air, Sea, Courier | รูปแบบการขนส่ง: ถนน รถไฟ อากาศ ทะเล ขนส่งด่วน';
COMMENT ON COLUMN "ShipmentLegs"."CarrierId" IS 'Carrier (FK Carriers) | ผู้ให้บริการขนส่ง (อ้างอิง Carriers)';
COMMENT ON COLUMN "ShipmentLegs"."VehicleId" IS 'Vehicle from master data (FK Vehicles) | ยานพาหนะจากข้อมูลหลัก (อ้างอิง Vehicles)';
COMMENT ON COLUMN "ShipmentLegs"."VehicleName" IS 'Vehicle as free text for outside carriers (vessel name, aircraft type) | ชื่อยานพาหนะแบบข้อความ สำหรับผู้ขนส่งภายนอก (ชื่อเรือ รุ่นเครื่องบิน)';
COMMENT ON COLUMN "ShipmentLegs"."VoyageNumber" IS 'Voyage (sea), flight (air), train (rail) or trip (road) number | เลขเที่ยวเรือ เที่ยวบิน ขบวนรถไฟ หรือเที่ยวรถ';
COMMENT ON COLUMN "ShipmentLegs"."OriginLocationId" IS 'Leg origin (FK Locations) | ต้นทางของช่วงนี้ (อ้างอิง Locations)';
COMMENT ON COLUMN "ShipmentLegs"."DestinationLocationId" IS 'Leg destination (FK Locations) | ปลายทางของช่วงนี้ (อ้างอิง Locations)';
COMMENT ON COLUMN "ShipmentLegs"."PlannedDeparture" IS 'ETD - planned departure (UTC) | ETD เวลาออกตามแผน (UTC)';
COMMENT ON COLUMN "ShipmentLegs"."PlannedArrival" IS 'ETA - planned arrival (UTC) | ETA เวลาถึงตามแผน (UTC)';
COMMENT ON COLUMN "ShipmentLegs"."ActualDeparture" IS 'ATD - actual departure (UTC) | ATD เวลาออกจริง (UTC)';
COMMENT ON COLUMN "ShipmentLegs"."ActualArrival" IS 'ATA - actual arrival (UTC) | ATA เวลาถึงจริง (UTC)';
COMMENT ON COLUMN "ShipmentLegs"."DocumentType" IS 'TransportDocumentType: BillOfLading, AirWaybill, RailWaybill, RoadConsignmentNote, CourierWaybill | ประเภทเอกสารขนส่ง: B/L (เรือ) AWB (อากาศ) ใบกำกับรถไฟ ใบกำกับรถ ใบนำส่งด่วน';
COMMENT ON COLUMN "ShipmentLegs"."DocumentNumber" IS 'Transport document number (B/L, AWB, ...) | เลขที่เอกสารขนส่ง (B/L, AWB ฯลฯ)';

-- ---------------------------------------------------------------------------
-- Maintenance functions
-- ---------------------------------------------------------------------------
COMMENT ON FUNCTION create_tracking_event_partition(DATE) IS 'Creates the TrackingEvents partition for one month (UTC); false if it exists | สร้าง partition ของ TrackingEvents สำหรับ 1 เดือน (UTC) คืนค่า false ถ้ามีอยู่แล้ว';
COMMENT ON FUNCTION create_tracking_event_partitions(INT) IS 'Ensures partitions exist for this month + N months ahead; returns how many were created | สร้าง partition ของเดือนนี้และล่วงหน้า N เดือน คืนจำนวนที่สร้างใหม่';
