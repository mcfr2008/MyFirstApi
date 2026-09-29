-- Feature: Master data - tracking event types (seed data)
-- Standard logistics events. Edit or add rows here / via /api/EventTypes.

INSERT INTO "EventTypes" ("Code", "NameTh", "NameEn", "ResultingStatus", "IsTerminal", "SortOrder") VALUES
    ('REGISTERED',       'ลงทะเบียนสิ่งของ',       'Item registered',      'Registered', FALSE, 10),
    ('PICKED_UP',        'รับสิ่งของแล้ว',          'Picked up',            'InTransit',  FALSE, 20),
    ('ARRIVED_AT_HUB',   'ถึงศูนย์กระจายสินค้า',     'Arrived at hub',       'InTransit',  FALSE, 30),
    ('DEPARTED_HUB',     'ออกจากศูนย์กระจายสินค้า',  'Departed hub',         'InTransit',  FALSE, 40),
    ('OUT_FOR_DELIVERY', 'กำลังนำส่ง',             'Out for delivery',     'InTransit',  FALSE, 50),
    ('DELIVERED',        'ส่งถึงปลายทางแล้ว',       'Delivered',            'Delivered',  TRUE,  60),
    ('DELIVERY_FAILED',  'นำส่งไม่สำเร็จ',          'Delivery attempt failed', NULL,      FALSE, 70),
    ('RETURNED',         'ตีกลับต้นทาง',            'Returned to sender',   'Returned',   TRUE,  80),
    ('DAMAGED',          'สิ่งของเสียหาย',          'Damaged',              'Damaged',    FALSE, 90),
    ('LOST',             'สิ่งของสูญหาย',           'Lost',                 'Lost',       FALSE, 100)
ON CONFLICT ("Code") DO NOTHING;
