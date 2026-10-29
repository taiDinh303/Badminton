import { test } from 'node:test';
import assert from 'node:assert/strict';
import { dateInputValue, formatDay, formatVnd, vietnamDate } from '../src/utils/format.ts';

test('booking display dates and ISO dates keep the same date input value', () => {
  assert.equal(dateInputValue('31/10/2026'), '2026-10-31');
  assert.equal(dateInputValue('2026-10-31'), '2026-10-31');
  assert.equal(dateInputValue(' 1/2/2026 '), '2026-02-01');
  assert.equal(dateInputValue('29/02/2024'), '2024-02-29');
});

test('invalid booking dates cannot silently roll into another day', () => {
  for (const value of ['', '31/02/2026', '29/02/2026', '2026-02-30', '2026-13-01', '0000-01-01', '10/31/2026', '2026-10-31T23:00:00Z']) {
    assert.equal(dateInputValue(value), '', value);
  }
});

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
