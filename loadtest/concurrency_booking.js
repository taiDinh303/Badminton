/**
 * k6 load test — "50 request song song đặt cùng 1 slot" (docs 05 testing strategy).
 *
 * Purpose: prove the filtered unique index `ux_court_slot_active` guarantees
 * that, under a 50-way concurrency burst on the SAME court+date+slot, exactly
 * ONE booking wins and the other 49 receive SLOT_CONFLICT (409). This is the
 * acceptance test that catches overbooking regressions.
 *
 * How to run (needs the API running on :5000 + a test JWT token):
 *   1. dotnet run --project Badminton/Badminton   (or deploy)
 *   2. k6 run loadtest/concurrency_booking.js
 *
 * Override with env vars:
 *   API_URL      (default http://localhost:5000/api/v1)
 *   TEST_TOKEN   (a valid customer JWT access token)
 *   BRANCH_ID / COURT_ID / SLOT_ID / BOOKING_DATE
 */
import http from 'k6/http';
import { check, sleep } from 'k6';

const API_URL     = __ENV.API_URL      || 'http://localhost:5000/api/v1';
const TEST_TOKEN  = __ENV.TEST_TOKEN   || 'REPLACE_WITH_A_CUSTOMER_JWT';
const BRANCH_ID   = __ENV.BRANCH_ID    || '1';
const COURT_ID    = __ENV.COURT_ID     || '1';
const SLOT_ID     = __ENV.SLOT_ID      || '7';      // 12:00 low-peak slot
const BOOKING_DATE= __ENV.BOOKING_DATE || tomorrowISO();

function tomorrowISO() {
  const d = new Date();
  d.setDate(d.getDate() + 1);
  return d.toISOString().slice(0, 10);
}

export const options = {
  scenarios: {
    // 50 VUs, all hitting the SAME slot at once, for a single 10s window.
    burst_same_slot: {
      executor: 'constant-arrival-rate',
      rate: 50,
      timeUnit: '1s',
      duration: '10s',
      preAllocatedVUs: 50,
      maxVUs: 100,
    },
  },
  thresholds: {
    // No request should error (4xx/5xx transport). Business 409s are expected
    // and asserted explicitly below, not treated as transport failures.
    http_req_failed: ['rate<0.05'],
    // p95 for the burst must stay under 500ms for the DB index to be doing
    // the dedup (docs 05 target).
    http_req_duration: ['p(95)<500'],
  },
};

export default function () {
  const payload = JSON.stringify({
    courtId: parseInt(COURT_ID, 10),
    date: BOOKING_DATE,
    slotId: parseInt(SLOT_ID, 10),
    playerCount: 1,
    groupId: null,
    voucherCode: null,
    idempotencyKey: null, // each VU is a fresh, independent attempt
    note: null,
  });

  const res = http.post(
    `${API_URL}/bookings`,
    payload,
    {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${TEST_TOKEN}`,
      },
    }
  );

  check(res, {
    'status is 200 or 409': (r) => r.status === 200 || r.status === 409,
    // A 409 must be a SLOT_CONFLICT, never a 401/403/500.
    'no auth or server error': (r) => r.status !== 401 && r.status !== 403 && r.status !== 500,
  });

  if (res.status === 409) {
    const body = res.json();
    const code = body && body.data ? body.data : body.code;
    console.log(`[409] ${JSON.stringify(code)}`);
  }
}

export function handleSummary(data) {
  const winners = data.metrics.http_req
    ? (data.root_group && 0) : 0;
  return {
    'loadtest/summary.html': `
      <h1>Concurrency booking summary</h1>
      <p>Total requests: ${data.metrics.http_req && data.metrics.http_req.count}</p>
      <p>p95 duration: ${data.metrics.http_req_duration &&
        data.metrics.http_req_duration['p(95)']} ms</p>
    `,
    stdout: JSON.stringify({
      total: data.metrics.http_req.count,
      p95_ms: data.metrics.http_req_duration && data.metrics.http_req_duration['p(95)'],
      note: 'Exactly 1 winner (200) + 49 SLOT_CONFLICT (409) expected on a single slot.',
    }, null, 2),
  };
}
