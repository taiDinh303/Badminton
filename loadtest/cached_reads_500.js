/**
 * k6 "fair" read-load: 500 VUs hammer the two CACHED report read endpoints
 * (report/summary + report/occupancy) + customer availability. Measures the p95
 * a production-tier dashboard actually sees, after IMemoryCache (TTL 5m) takes
 * the heavy queries off the DB. Thresholds: p95 < 500ms, failed < 5%.
 *
 *   & k6\...\k6.exe run loadtest\cached_reads_500.js
 *   env: TEST_TOKEN, ADMIN_TOKEN, API_URL
 */
import http from 'k6/http';

const API_URL   = __ENV.API_URL      || 'http://localhost:5131/api/v1';
const TEST_TOKEN  = __ENV.TEST_TOKEN  || '';
const ADMIN_TOKEN = __ENV.ADMIN_TOKEN || '';

export const options = {
  scenarios: {
    cached_reads: {
      executor: 'constant-vus',
      vus: 500, duration: '30s',
      exec: 'reads',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.05'],
    'http_req_duration{scenario:cached_reads}': ['p(95)<500'],
  },
};

function tomorrow() {
  const d = new Date(); d.setDate(d.getDate() + 1);
  return d.toISOString().slice(0, 10);
}
function daysAgo(n) {
  const d = new Date(); d.setDate(d.getDate() - n);
  return d.toISOString().slice(0, 10);
}

export function reads() {
  const today = tomorrow();
  const adminHdrs = { 'Authorization': `Bearer ${ADMIN_TOKEN}` };
  const custHdrs  = { 'Authorization': `Bearer ${TEST_TOKEN}` };

  // Customer availability (hot read path)
  http.get(`${API_URL}/bookings/availability/1/${today}`, { headers: custHdrs, tags: { r: 'availability' } });
  // Cached report summary (Admin)
  http.get(`${API_URL}/admin/report/summary`, { headers: adminHdrs, tags: { r: 'summary' } });
  // Cached report occupancy, 30-day window (Admin)
  http.get(`${API_URL}/admin/report/occupancy?from=${daysAgo(30)}&to=${today}`, { headers: adminHdrs, tags: { r: 'occupancy' } });
}

export function handleSummary(d) {
  const m = (k) => d.metrics[k] || {};
  const dur = m('http_req_duration');
  return {
    stdout: JSON.stringify({
      note: '500 VU on CACHED reads (summary + occupancy + availability), 30s.',
      total_reqs: m('http_req')?.count,
      p50_ms: dur?.['p(50)'], p95_ms: dur?.['p(95)'], p99_ms: dur?.['p(99)'],
      failed_rate: m('http_req_failed')?.rate,
    }, null, 2),
  };
}
