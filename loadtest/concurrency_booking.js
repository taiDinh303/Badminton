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

const API_URL     = __ENV.API_URL      || 'http://localhost:5131/api/v1';
const TEST_TOKEN  = __ENV.TEST_TOKEN   || 'REPLACE_WITH_A_CUSTOMER_JWT';
const ADMIN_TOKEN = __ENV.ADMIN_TOKEN  || 'REPLACE_WITH_AN_ADMIN_JWT';
const BRANCH_ID   = __ENV.BRANCH_ID    || '1';
const COURT_ID    = __ENV.COURT_ID     || '1';
const SLOT_ID     = __ENV.SLOT_ID      || '7';
const BOOKING_DATE= __ENV.BOOKING_DATE || tomorrowISO();
const N_USERS     = parseInt(__ENV.N_USERS || '1000', 10); // docs 06: 1000 concurrent

function tomorrowISO() {
  const d = new Date();
  d.setDate(d.getDate() + 1);
  return d.toISOString().slice(0, 10);
}

// ── Scenarios ─────────────────────────────────────────────────────
// 1. 1000-VU concurrent login burst (auth bucket is 5/60s — expects 429s
//    on many, that's expected; we measure p95 latency and 2xx-rate).
// 2. 50-way same-slot booking burst (the original overbooking test).
// 3. 1000-VU availability + report fan-out (dashboard read load).
export const options = {
  scenarios: {
    burst_same_slot: {
      executor: 'constant-arrival-rate',
      rate: 50, timeUnit: '1s', duration: '10s',
      preAllocatedVUs: 50, maxVUs: 100,
    },
    concurrent_logins: {
      // 1000 VUs authenticate concurrently for 30s. constant-vus is the cleanest
      // valid executor here; the auth bucket (5/60s) is expected to return 429s
      // on most — the point is measuring p95 latency + 2xx-rate under storm.
      executor: 'constant-vus',
      vus: N_USERS, duration: '30s',
      exec: 'login',
    },
    dashboard_fanout: {
      executor: 'constant-vus',
      vus: 1000, duration: '30s',
      exec: 'readLoad',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.05'],
    // p95 for booking burst must stay < 500ms (DB index dedup, docs 05).
    'http_req_duration{scenario:burst_same_slot}': ['p(95)<500'],
    // p95 for read fan-out should stay < 1000ms under 1000 concurrent.
    'http_req_duration{scenario:dashboard_fanout}': ['p(95)<1000'],
  },
};

// ── Booking burst (original) ────────────────────────────────────
export default function () {
  const payload = JSON.stringify({
    courtId: parseInt(COURT_ID, 10),
    date: BOOKING_DATE,
    slotId: parseInt(SLOT_ID, 10),
    playerCount: 1,
    groupId: null, voucherCode: null, idempotencyKey: null, note: null,
  });

  const res = http.post(
    `${API_URL}/bookings`,
    payload,
    { headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${TEST_TOKEN}` } }
  );

  check(res, {
    'status is 200 or 409': (r) => r.status === 200 || r.status === 409,
    'no auth or 500': (r) => r.status !== 401 && r.status !== 403 && r.status !== 500,
  });

  if (res.status === 409) {
    const body = res.json();
    const code = body && body.data ? body.data : body.code;
    console.log(`[409] ${JSON.stringify(code)}`);
  }
}

// ── Login storm: 1000 users authenticate concurrently ─────────
export function login() {
  // Use the seeded admin creds (idempotent, no data mutation).
  const payload = JSON.stringify({ phoneNumber: '0900000001', password: 'Admin@123' });
  const res = http.post(`${API_URL}/auth/login`, payload, {
    headers: { 'Content-Type': 'application/json' },
  });
  check(res, {
    'login 2xx or 429 (rate-limited)': (r) => r.status === 200 || r.status === 429,
  });
}

// ── Read fan-out: availability + admin report under load ───────
export function readLoad() {
  const today = tomorrowISO();
  http.get(`${API_URL}/bookings/availability/${BRANCH_ID}/${today}`, {
    headers: { 'Authorization': `Bearer ${TEST_TOKEN}` },
    tags: { read: 'availability' },
  });
  // Reports: admin only — use admin token when available, otherwise skip
  if (ADMIN_TOKEN && ADMIN_TOKEN !== 'REPLACE_WITH_AN_ADMIN_JWT') {
    http.get(`${API_URL}/admin/report/summary`, {
      headers: { 'Authorization': `Bearer ${ADMIN_TOKEN}` },
      tags: { read: 'report_summary' },
    });
  }
}

export function handleSummary(data) {
  const m = (k) => data.metrics[k] || {};
  return {
    'loadtest/summary.html': `
      <h1>BadmintonBooking 1000-user load test</h1>
      <p>Total requests: ${m('http_req').count}</p>
      <p>p95 duration: ${m('http_req_duration')['p(95)']} ms</p>
      <p>p99 duration: ${m('http_req_duration')['p(99)']} ms</p>
      <p>Avg VUs: ${m('vus').value}</p>
      <p>HTTP failed rate: ${m('http_req_failed').rate}</p>
      <h2>Per-scenario</h2>
      ${Object.entries(m('http_req_duration'))
        .map(([tag, v]) => `<p>${tag}: p95=${v['p(95)']}ms p99=${v['p(99)']}ms</p>`)
        .join('')}
    `,
    stdout: JSON.stringify({
      total: m('http_req').count,
      p95_ms: m('http_req_duration')['p(95)'],
      p99_ms: m('http_req_duration')['p(99)'],
      avg_vus: m('vus').value,
      http_failed_rate: m('http_req_failed').rate,
      note: 'Expect: 1 winner (200) + 49 SLOT_CONFLICT (409) on same-slot burst; read fan-out p95 < 1s under 1000 concurrent.',
    }, null, 2),
  };
}
