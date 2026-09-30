"""DEV ONLY - measures API response times against the load-test data set
(Scripts/dev/load_test_seed.sql). Run with the API up:

    python3 Scripts/dev/benchmark.py [baseUrl]

Each request is warmed up once, then timed 5 times; the median is reported.
Part of the quarterly load test in Scripts/maintenance/README.md.
"""
import json, statistics, sys, time, urllib.parse, urllib.request
from datetime import datetime, timedelta, timezone

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5106").rstrip("/") + "/api"
RUNS = 5


def call(method, path, body=None, token=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    start = time.perf_counter()
    with urllib.request.urlopen(req) as resp:
        payload = resp.read()
    return (time.perf_counter() - start) * 1000, json.loads(payload) if payload else None


def q(params):
    return urllib.parse.urlencode(params)


_, login = call("POST", "/Auth/login", {"username": "admin", "password": "ChangeMe123!"})
token = login["token"]
_, locations = call("GET", "/Locations?" + q({"search": "LOAD-LOC-7", "pageSize": 1}), token=token)
location_id = locations["items"][0]["id"]
week_to = datetime.now(timezone.utc) - timedelta(days=200)
week_from = week_to - timedelta(days=7)

cases = [
    ("Item by tag code", "GET", "/TrackedItems/by-tag/LOAD-0100000", None),
    ("Item timeline (1 item)", "GET", "/TrackingEvents?" + q({"tagCode": "LOAD-0100000", "oldestFirst": "true"}), None),
    ("Latest events, first page (all 3M)", "GET", "/TrackingEvents?" + q({"pageSize": 50}), None),
    ("Events at one location", "GET", "/TrackingEvents?" + q({"locationId": location_id, "pageSize": 50}), None),
    ("Events in a 1-week range", "GET", "/TrackingEvents?" + q({
        "from": week_from.isoformat(), "to": week_to.isoformat(), "pageSize": 50}), None),
    ("Item search (substring)", "GET", "/TrackedItems?" + q({"search": "0123456"}), None),
    ("Shipment search by B/L no.", "GET", "/Shipments?" + q({"search": "LOADBL00042424"}), None),
    ("Scan 100 tags (write)", "POST", "/TrackingEvents/scan", {
        "tagCodes": [f"LOAD-{n:07d}" for n in range(150001, 150101)],
        "eventTypeCode": "ARRIVED_AT_HUB", "locationId": location_id}),
]

print(f"{'case':<38}{'median ms':>10}{'min':>8}{'max':>8}")
for name, method, path, body in cases:
    call(method, path, body, token)
    times = [call(method, path, body, token)[0] for _ in range(RUNS)]
    print(f"{name:<38}{statistics.median(times):>10.1f}{min(times):>8.1f}{max(times):>8.1f}")
