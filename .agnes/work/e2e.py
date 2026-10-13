#!/usr/bin/env python3
"""E2E test: 4 accounts + booking + voucher + on-site payment."""
import json, urllib.request, urllib.error, uuid, sys

BASE = "http://localhost:5131/api/v1"
PW   = "Hovaten123@"
fail = 0

def req(method, path, token=None, body=None):
    """Returns (status_code, parsed_json_or_text)."""
    url = BASE + path
    data = json.dumps(body).encode() if body else None
    headers = {"Content-Type": "application/json"} if body else {}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    r = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(r, timeout=15) as resp:
            return resp.status, json.loads(resp.read().decode() or "{}")
    except urllib.error.HTTPError as e:
        try:
            return e.code, json.loads(e.read().decode() or "{}")
        except:
            return e.code, {}

def check(name, expected, actual, extra=""):
    global fail
    ok = actual == expected
    print(f"{'PASS' if ok else 'FAIL'}  {name}" + (f"  ({extra})" if extra and not ok else ""))
    if not ok: fail += 1

# 1-4: login all 4
for phone in ("0900000003","0900000004","0900000005","0900000006"):
    s, j = req("POST", "/auth/login", body={"phoneNumber": phone, "password": PW})
    check(f"login {phone}", 200, s, f"got {s}: {j.get('code','')[:30]}")

# 5: wrong pw
s, _ = req("POST", "/auth/login", body={"phoneNumber":"0900000003","password":"wrong"})
check("login wrong-pw -> 400", 400, s)

# 6: self-register admin must 409
s, j = req("POST", "/auth/register", body={"fullName":"X","phoneNumber":"0900000099","password":PW,"role":"Admin"})
check("self-register admin -> 409", 409, s, f"got {s}: {j.get('code','')[:40]}")

# get tokens
_, aJ = req("POST", "/auth/login", body={"phoneNumber":"0900000006","password":PW})
adminTok = aJ["data"]["accessToken"]
_, cJ = req("POST", "/auth/login", body={"phoneNumber":"0900000003","password":PW})
custTok  = cJ["data"]["accessToken"]
print(f"adminTok={bool(adminTok)} custTok={bool(custTok)}")

# public data
for name, path, tok in [
    ("courts", "/courts?branchId=1", adminTok),
    ("time-slots", "/time-slots", adminTok),
    ("vouchers", "/vouchers", adminTok),
    ("voucher preview", "/vouchers/WELCOME10/preview?amount=100000", custTok),
]:
    s, j = req("GET", path, tok)
    check(name, 200, s, f"got {s}: {j.get('code','')[:40]}")

# booking with voucher — court 2 peak (220k), WELCOME10 (min_spend 200000 ->
# met; 10% off capped 50000). Random date within the +30d window + random peak
# slot so repeated runs never collide on an already-booked slot.
import datetime, random
day2 = (datetime.date.today() + datetime.timedelta(days=random.randint(1, 28))).isoformat()
slot_id = random.randint(13, 17)  # peak slots 18:00-22:00
idem = "e2e-" + str(uuid.uuid4().hex)[:8]
s, j = req("POST", "/bookings", custTok,
           {"courtId":2,"slotId":slot_id,"date":day2,"voucherCode":"WELCOME10","idempotencyKey":idem})
check("create booking (voucher WELCOME10)", 200, s, f"got {s}: {j.get('code','')[:50]}")
newBkId = None
if s == 200:
    bk = j["data"]
    newBkId = bk["id"]
    print(f"  booking: {bk['bookingCode']}  court={bk['courtName']} slot={bk['slotLabel']} "
          f"total={bk['totalAmount']} disc={bk['discountAmount']} payable={bk['payableAmount']} pts={bk['pointsEarned']}")

# duplicate the SAME slot just booked -> 409 anti-overbooking (ux_court_slot_active)
s2, j2 = req("POST", "/bookings", custTok, {"courtId":2,"slotId":slot_id,"date":day2})
check("duplicate slot -> 409", 409, s2, f"got {s2}: {j2.get('code','')[:40]}")

# my bookings
s, j = req("GET", "/bookings/mine", custTok)
check("booking mine", 200, s)

# pay the NEW booking on-site (cash)
if newBkId:
    s2, j2 = req("POST", f"/payments/{newBkId}/on-site", adminTok, {"method":"Cash"})
    check("pay on-site (cash)", 200, s2, f"got {s2}: {j2.get('code','')[:40]}")

print("---")
print(f"RESULT: {fail} failed" if fail else "ALL E2E PASSED ✓")
sys.exit(fail)
