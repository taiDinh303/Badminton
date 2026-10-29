import { test } from 'node:test';
import assert from 'node:assert/strict';
import { clubInstant, formatClubTime } from '../src/modules/club/utils/clubTime.ts';

test('club SQL Server timestamps and explicit offsets keep the same UTC instant in every client timezone', () => {
  const previous = process.env.TZ;
  try {
    for (const timezone of ['UTC', 'Asia/Ho_Chi_Minh', 'America/New_York']) {
      process.env.TZ = timezone;
      for (const value of ['2026-11-02T03:00:00', '2026-11-02T03:00:00Z', '2026-11-02T10:00:00+07:00']) {
        assert.equal(clubInstant(value).toISOString(), '2026-11-02T03:00:00.000Z');
        assert.match(formatClubTime(value), /10:00/);
      }
    }
  } finally { if (previous === undefined) delete process.env.TZ; else process.env.TZ = previous; }
});
test('malformed club timestamps show an empty placeholder instead of crashing the page', () => assert.equal(formatClubTime('bad-date'), '—'));
