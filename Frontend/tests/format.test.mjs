import { test } from 'node:test';
import assert from 'node:assert/strict';
import { formatDay, formatVnd, vietnamDate } from '../src/utils/format.ts';

test('Vietnam date crosses midnight independently of the browser timezone', () => {
  assert.equal(vietnamDate(new Date('2026-10-24T16:59:59Z')), '2026-10-24');
  assert.equal(vietnamDate(new Date('2026-10-24T17:00:00Z')), '2026-10-25');
  assert.equal(vietnamDate(new Date('2026-10-25T00:00:00Z')), '2026-10-25');
});

test('date-only bookings keep their calendar day', () => assert.equal(formatDay('2026-10-20'), '20/10/2026'));
test('UTC timestamps display the Vietnam calendar day', () => assert.equal(formatDay('2026-10-19T20:30:00Z'), '20/10/2026'));
test('Vietnamese date and integer currency stay compatible', () => {
  assert.equal(formatDay('20/10/2026'), '20/10/2026');
  assert.equal(formatVnd(120000), '120.000đ');
});
