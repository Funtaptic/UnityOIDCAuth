const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { runInNewContext } = require('node:vm');
const source = readFileSync('com.funtaptic.oidc/Runtime/Plugins/WebGL/FuntapticOIDC.jslib', 'utf8');
function bridge(blocked = false) {
  const opened = [], elements = [];
  const popup = { closed: false, location: { href: 'https://provider.example/login' }, close() { this.closed = true; } };
  const context = {
    URL, LibraryManager: { library: {} }, mergeInto: Object.assign,
    UTF8ToString: value => value, stringToNewUTF8: value => value,
    window: { open(...args) { opened.push(args); return blocked ? null : popup; } },
    document: { createElement() { const element = { style: {}, setAttribute() {}, appendChild() {}, focus() {}, remove() {} }; elements.push(element); return element; }, body: { appendChild() {} } }
  };
  runInNewContext(source, context);
  const api = context.LibraryManager.library;
  context.FuntapticOIDCWeb = api.$FuntapticOIDCWeb;
  return { api, opened, elements, popup };
}
const start = 'https://provider.example/authorize?state=test';
const end = 'https://game.example/oidc-callback.html';
const { api, opened, elements, popup } = bridge();
assert.equal(api.FuntapticOIDCWebBegin(start, end), 1);
assert.equal(opened.length, 1, 'OAuth must open immediately without an extra Continue click');
assert.equal(opened[0][0], start);
assert.equal(elements.length, 0, 'No preliminary dialog should be created');
assert.equal(api.FuntapticOIDCWebBegin(start, end), 0);
assert.equal(opened.length, 1);
Object.defineProperty(popup.location, 'href', { configurable: true, get() { throw new Error('cross-origin'); } });
assert.equal(api.FuntapticOIDCWebPoll(), 0);
Object.defineProperty(popup.location, 'href', { configurable: true, writable: true, value: end + '?code=a%2Bb&state=test' });
assert.equal(api.FuntapticOIDCWebPoll(), 1);
assert.equal(api.FuntapticOIDCWebResponse(), end + '?code=a%2Bb&state=test');
api.FuntapticOIDCWebClose();
assert.equal(popup.closed, true);
api.FuntapticOIDCWebClose();
const cancelled = bridge();
cancelled.api.FuntapticOIDCWebBegin(start, end);
cancelled.popup.closed = true;
assert.equal(cancelled.api.FuntapticOIDCWebPoll(), 2);
cancelled.api.FuntapticOIDCWebClose();
const blocked = bridge(true);
assert.equal(blocked.api.FuntapticOIDCWebBegin(start, end), -1);
assert.equal(blocked.api.FuntapticOIDCWebPoll(), 2);
assert.equal(blocked.api.FuntapticOIDCWebBegin(start, end), -1, 'Blocked launches must not leave a stale session');
console.log('PASS: direct launch, no overlay, callback, cancellation, cleanup, and blocked popup handling');
