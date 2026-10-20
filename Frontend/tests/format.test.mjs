import { test } from 'node:test';
import assert from 'node:assert/strict';
import { formatDay, formatVnd } from '../src/utils/format.ts';

test('date-only bookings keep their calendar day', () => assert.equal(formatDay('2026-10-20'), '20/10/2026'));
test('UTC timestamps display the Vietnam calendar day', () => assert.equal(formatDay('2026-10-19T20:30:00Z'), '20/10/2026'));
test('Vietnamese date and integer currency stay compatible', () => {
  assert.equal(formatDay('20/10/2026'), '20/10/2026');
  assert.equal(formatVnd(120000), '120.000đ');
});
