const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { runInNewContext } = require('node:vm');
const source = readFileSync('com.funtaptic.oidc/Runtime/Plugins/WebGL/FuntapticOIDC.jslib', 'utf8');
const callbackSource = readFileSync('com.funtaptic.oidc/Editor/WebGLCallbackPostProcessor.cs', 'utf8');
const callbackHtml = [...callbackSource.matchAll(/"(?:[^"\\]|\\.)*"/g)].map(match => JSON.parse(match[0])).join('');
const callbackScript = /<script>([\s\S]*?)<\/script>/.exec(callbackHtml)?.[1] || '';
function bridge(blocked = false, alwaysBlocked = false) {
  const opened = [], elements = [];
  let userGesture = false;
  const popup = { closed: false, location: { href: 'https://provider.example/login' }, close() { this.closed = true; } };
  const context = {
    URL, LibraryManager: { library: {} }, mergeInto: Object.assign,
    UTF8ToString: value => value, stringToNewUTF8: value => value,
    window: { open(...args) { opened.push(args); return alwaysBlocked || (blocked && !userGesture) ? null : popup; } },
    document: { createElement() { const element = { style: {}, setAttribute() {}, appendChild() {}, focus() {}, remove() { this.removed = true; } }; elements.push(element); return element; }, body: { appendChild() {} } }
  };
  runInNewContext(source, context);
  const api = context.LibraryManager.library;
  context.FuntapticOIDCWeb = api.$FuntapticOIDCWeb;
  return { api, opened, elements, popup, window: context.window, click(element) { userGesture = true; try { element.onclick(); } finally { userGesture = false; } } };
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
assert.equal(blocked.api.FuntapticOIDCWebBegin(start, end), 1, 'Blocked launch must offer a user-gesture retry');
assert.equal(blocked.api.FuntapticOIDCWebPoll(), 0);
assert.equal(blocked.api.FuntapticOIDCWebBegin(start, end), 0);
const continueButton = blocked.elements.find(element => element.textContent === 'Continue sign-in');
assert.ok(continueButton, 'A native button must let Safari obtain fresh user activation');
blocked.click(continueButton);
assert.equal(blocked.opened.length, 2);
assert.equal(blocked.opened[1][0], start);
assert.equal(blocked.elements[0].removed, true, 'Successful retry must remove the overlay');
blocked.popup.location.href = end + '?code=retry&state=test';
assert.equal(blocked.api.FuntapticOIDCWebPoll(), 1);
blocked.api.FuntapticOIDCWebClose();
assert.equal(blocked.popup.closed, true);
const dismissed = bridge(true);
dismissed.api.FuntapticOIDCWebBegin(start, end);
dismissed.click(dismissed.elements.find(element => element.textContent === 'Cancel'));
assert.equal(dismissed.api.FuntapticOIDCWebPoll(), 2);
assert.equal(dismissed.elements[0].removed, true);
dismissed.api.FuntapticOIDCWebClose();
assert.equal(dismissed.api.FuntapticOIDCWebBegin(start, end), 1, 'Cancellation must release the session');
dismissed.api.FuntapticOIDCWebClose();
const timedOut = bridge(true);
timedOut.api.FuntapticOIDCWebBegin(start, end);
timedOut.api.FuntapticOIDCWebClose(); // Unity uses this same cleanup on timeout or cancellation.
assert.equal(timedOut.elements[0].removed, true);
timedOut.click(timedOut.elements.find(element => element.textContent === 'Continue sign-in'));
assert.equal(timedOut.opened.length, 1, 'A stale button must not open a popup after cleanup');
const policyBlocked = bridge(true, true);
policyBlocked.api.FuntapticOIDCWebBegin(start, end);
policyBlocked.click(policyBlocked.elements.find(element => element.textContent === 'Continue sign-in'));
assert.equal(policyBlocked.api.FuntapticOIDCWebPoll(), 0);
assert.ok(policyBlocked.elements.some(element => /Allow popups/.test(element.textContent)));
policyBlocked.api.FuntapticOIDCWebClose();
assert.equal(policyBlocked.elements[0].removed, true);
const closedAfterLogin = bridge();
closedAfterLogin.api.FuntapticOIDCWebBegin(start, end);
closedAfterLogin.popup.location.href = end + '?code=a%2Bb&state=test';
closedAfterLogin.popup.opener = closedAfterLogin.window;
runInNewContext(callbackScript, { window: closedAfterLogin.popup });
assert.equal(closedAfterLogin.popup.closed, true, 'The callback must close itself after the game accepts its URL');
closedAfterLogin.popup.closed = true;
assert.equal(closedAfterLogin.api.FuntapticOIDCWebPoll(), 1,
  'Closing the completed callback before the next Unity frame must not report cancellation');
assert.equal(closedAfterLogin.api.FuntapticOIDCWebResponse(), end + '?code=a%2Bb&state=test');
closedAfterLogin.api.FuntapticOIDCWebClose();
assert.equal(closedAfterLogin.window.FuntapticOIDCWebComplete, undefined, 'Cleanup must remove the callback handler');
const untrusted = bridge();
untrusted.api.FuntapticOIDCWebBegin(start, end);
assert.equal(untrusted.window.FuntapticOIDCWebComplete({ location: { href: end + '?code=forged' } }), false);
untrusted.popup.opener = untrusted.window;
for (const url of ['https://other.example/oidc-callback.html?code=bad', 'https://game.example/wrong-path?code=bad']) {
  untrusted.popup.location.href = url;
  runInNewContext(callbackScript, { window: untrusted.popup });
  assert.equal(untrusted.popup.closed, false, 'A rejected callback must not auto-close');
  assert.equal(untrusted.api.FuntapticOIDCWebPoll(), 0);
}
const staleComplete = untrusted.window.FuntapticOIDCWebComplete;
untrusted.api.FuntapticOIDCWebClose();
assert.equal(staleComplete(untrusted.popup), false, 'An old callback must not revive a completed session');
runInNewContext(callbackScript, { window: { opener: null, close() { assert.fail('No opener must not close'); } } });
console.log('PASS: direct launch, callback, cancellation, cleanup, user-gesture popup recovery, and close after login');
