import { test } from 'node:test';
import assert from 'node:assert/strict';
import { apiBase, proxyTarget } from '../config/environment.mjs';

test('API default and loopback base preserve /api/v1', () => {
  assert.equal(apiBase(), '/api/v1');
  assert.equal(apiBase('http://localhost:5131/api/v1/'), 'http://localhost:5131/api/v1');
  assert.equal(apiBase('https://booking.example.invalid/api/v1'), 'https://booking.example.invalid/api/v1');
});
for (const input of ['https://booking.example.invalid', 'http://booking.example.invalid/api/v1', 'https://a:b@booking.example.invalid/api/v1', 'https://booking.example.invalid/api/v1?token=x', '//booking.example.invalid/api/v1', 'javascript:alert(1)']) {
  test(`reject unsafe API base: ${input}`, () => assert.throws(() => apiBase(input), /CONFIGURATION_INVALID/));
}
test('proxy accepts origin only and never client credentials', () => {
  assert.equal(proxyTarget(), 'http://localhost:5131');
  assert.throws(() => proxyTarget('http://localhost:5131/api/v1'));
  assert.throws(() => proxyTarget('https://user:secret@booking.example.invalid'));
});
