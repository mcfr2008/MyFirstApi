-- Feature: Reason codes (seed data). Edit or add rows here / via /api/ReasonCodes.

INSERT INTO "ReasonCodes" ("Code", "NameTh", "NameEn", "EventTypeCodes", "RequiresNote", "SortOrder") VALUES
    -- Failed delivery / returns
    ('RECIPIENT_ABSENT',   'ผู้รับไม่อยู่',                 'Recipient not available',      '{DELIVERY_FAILED,RETURNED}', FALSE, 10),
    ('WRONG_ADDRESS',      'ที่อยู่ไม่ถูกต้อง',              'Incorrect address',            '{DELIVERY_FAILED,RETURNED}', FALSE, 20),
    ('REFUSED',            'ผู้รับปฏิเสธการรับ',             'Refused by recipient',         '{DELIVERY_FAILED,RETURNED}', FALSE, 30),
    ('BUSINESS_CLOSED',    'สถานที่ปิดทำการ',               'Business closed',              '{DELIVERY_FAILED}',          FALSE, 40),
    ('UNREACHABLE',        'ติดต่อผู้รับไม่ได้',              'Recipient unreachable',        '{DELIVERY_FAILED,RETURNED}', FALSE, 50),
    -- Damage / loss
    ('PACKAGING_DAMAGE',   'บรรจุภัณฑ์เสียหาย',             'Packaging damaged',            '{DAMAGED}',                  FALSE, 60),
    ('WATER_DAMAGE',       'เสียหายจากน้ำ',                 'Water damage',                 '{DAMAGED}',                  FALSE, 70),
    ('CRUSHED',            'ถูกทับ/บุบ',                    'Crushed',                      '{DAMAGED}',                  FALSE, 80),
    ('TEMPERATURE_BREACH', 'อุณหภูมิเกินเกณฑ์',              'Temperature out of range',     '{DAMAGED}',                  FALSE, 90),
    ('MISSING_AT_SCAN',    'สแกนไม่พบที่จุดตรวจ',            'Not found at checkpoint scan', '{LOST}',                     FALSE, 100),
    ('THEFT',              'ถูกโจรกรรม',                   'Theft',                        '{LOST}',                     FALSE, 110),
    -- Customs
    ('MISSING_DOCUMENTS',  'เอกสารไม่ครบ',                  'Missing documents',            '{CUSTOMS_HOLD}',             FALSE, 120),
    ('INSPECTION',         'สุ่มตรวจสินค้า',                'Physical inspection',          '{CUSTOMS_HOLD}',             FALSE, 130),
    ('DUTIES_UNPAID',      'ยังไม่ชำระภาษีอากร',             'Duties and taxes unpaid',      '{CUSTOMS_HOLD}',             FALSE, 140),
    ('RESTRICTED_GOODS',   'สินค้าควบคุม/ต้องใบอนุญาต',      'Restricted goods / permit',    '{CUSTOMS_HOLD}',             FALSE, 150),
    -- Any event
    ('WEATHER',            'สภาพอากาศ',                    'Weather',                      '{}',                         FALSE, 200),
    ('VEHICLE_BREAKDOWN',  'ยานพาหนะขัดข้อง',               'Vehicle breakdown',            '{}',                         FALSE, 210),
    ('PORT_CONGESTION',    'ท่าเรือ/สนามบินแออัด',            'Port / airport congestion',    '{}',                         FALSE, 220),
    ('OTHER',              'อื่น ๆ (ระบุในหมายเหตุ)',         'Other (explain in note)',      '{}',                         TRUE,  999)
ON CONFLICT ("Code") DO NOTHING;
