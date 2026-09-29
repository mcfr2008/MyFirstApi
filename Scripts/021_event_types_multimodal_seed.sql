-- Feature: Multimodal / customs / container event types (seed data)
-- Codes used by the API when it records events automatically
-- (ShipmentService leg departure/arrival, customs, delivery; ContainerService load/unload):
-- keep these codes; names and sort order can be edited freely.

INSERT INTO "EventTypes" ("Code", "NameTh", "NameEn", "ResultingStatus", "IsTerminal", "SortOrder") VALUES
    ('VEHICLE_DEPARTED',        'รถออกเดินทาง',              'Vehicle departed',          'InTransit', FALSE, 42),
    ('VEHICLE_ARRIVED',         'รถถึงจุดหมาย',              'Vehicle arrived',           'InTransit', FALSE, 44),
    ('LOADED_ON_TRAIN',         'ขึ้นรถไฟแล้ว',              'Loaded on train',           'InTransit', FALSE, 46),
    ('TRAIN_DEPARTED',          'รถไฟออกจากสถานี',           'Train departed',            'InTransit', FALSE, 47),
    ('TRAIN_ARRIVED',           'รถไฟถึงสถานี',              'Train arrived',             'InTransit', FALSE, 48),
    ('LOADED_ON_FLIGHT',        'ขึ้นเครื่องบินแล้ว',          'Loaded on flight',          'InTransit', FALSE, 50),
    ('FLIGHT_DEPARTED',         'เครื่องบินออกเดินทาง',        'Flight departed',           'InTransit', FALSE, 51),
    ('FLIGHT_ARRIVED',          'เครื่องบินถึงปลายทาง',        'Flight arrived',            'InTransit', FALSE, 52),
    ('LOADED_ON_VESSEL',        'ขึ้นเรือแล้ว',               'Loaded on vessel',          'InTransit', FALSE, 54),
    ('VESSEL_DEPARTED',         'เรือออกจากท่า',             'Vessel departed',           'InTransit', FALSE, 55),
    ('VESSEL_ARRIVED',          'เรือถึงท่า',                'Vessel arrived',            'InTransit', FALSE, 56),
    ('DISCHARGED_FROM_VESSEL',  'ขนลงจากเรือแล้ว',            'Discharged from vessel',    'InTransit', FALSE, 57),
    ('CUSTOMS_HOLD',            'ถูกกักที่ศุลกากร',           'Held by customs',           'OnHold',    FALSE, 58),
    ('CUSTOMS_CLEARED',         'ผ่านพิธีการศุลกากร',         'Customs cleared',           'InTransit', FALSE, 59),
    ('LOADED_INTO_CONTAINER',   'บรรจุเข้าตู้/พาเลท',          'Loaded into container',     NULL,        FALSE, 15),
    ('UNLOADED_FROM_CONTAINER', 'นำออกจากตู้/พาเลท',          'Unloaded from container',   NULL,        FALSE, 16)
ON CONFLICT ("Code") DO NOTHING;
