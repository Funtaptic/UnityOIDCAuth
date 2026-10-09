mergeInto(LibraryManager.library, {
  $FuntapticOIDCWeb: { session: null, syncing: false, syncAgain: false },

  FuntapticOIDCWebBegin__deps: ['$FuntapticOIDCWeb'],
  FuntapticOIDCWebBegin: function (startPointer, endPointer) {
    if (FuntapticOIDCWeb.session) return 0;
    var start = UTF8ToString(startPointer);
    var end = new URL(UTF8ToString(endPointer));
    var panel = document.createElement('div');
    panel.setAttribute('role', 'dialog');
    panel.setAttribute('aria-label', 'Authentication');
    panel.style.cssText = 'position:fixed;inset:0;z-index:2147483647;display:flex;align-items:center;justify-content:center;gap:16px;background:rgba(0,0,0,.75);color:white;font:16px sans-serif;flex-wrap:wrap';
    var message = document.createElement('span');
    message.textContent = 'Continue to your account in a new window.';
    var launch = document.createElement('button');
    launch.textContent = 'Continue';
    var cancel = document.createElement('button');
    cancel.textContent = 'Cancel';
    var session = { end: end, popup: null, panel: panel, status: 0, response: '' };
    FuntapticOIDCWeb.session = session;
    // Unity's async discovery and input loop can lose transient user activation.
    // A DOM click guarantees window.open executes directly in a browser gesture.
    launch.onclick = function () {
      if (session.popup && !session.popup.closed) { session.popup.focus(); return; }
      session.popup = window.open(start, '_blank', 'popup,width=520,height=720');
      message.textContent = session.popup
        ? 'Complete authentication in the new window.'
        : 'Allow popups for this site, then select Continue again.';
    };
    cancel.onclick = function () { session.status = 2; };
    panel.appendChild(message);
    panel.appendChild(launch);
    panel.appendChild(cancel);
    document.body.appendChild(panel);
    launch.focus();
    return 1;
  },

  FuntapticOIDCWebPoll__deps: ['$FuntapticOIDCWeb'],
  FuntapticOIDCWebPoll: function () {
    var session = FuntapticOIDCWeb.session;
    if (!session) return 2;
    if (session.status) return session.status;
    if (!session.popup) return 0;
    if (session.popup.closed) return 2;
    try {
      var url = new URL(session.popup.location.href);
      if (url.origin === session.end.origin && url.pathname === session.end.pathname) {
        session.response = url.href;
        session.status = 1;
      }
    } catch (_) {
      // Cross-origin access is expected while the provider is displayed.
    }
    return session.status;
  },

  FuntapticOIDCWebResponse__deps: ['$FuntapticOIDCWeb', '$stringToNewUTF8'],
  FuntapticOIDCWebResponse: function () {
    // Unity frees the returned UTF-8 string after marshalling it.
    return stringToNewUTF8(FuntapticOIDCWeb.session.response);
  },

  FuntapticOIDCWebClose__deps: ['$FuntapticOIDCWeb'],
  FuntapticOIDCWebClose: function () {
    var session = FuntapticOIDCWeb.session;
    if (!session) return;
    FuntapticOIDCWeb.session = null;
    if (session.popup && !session.popup.closed) session.popup.close();
    session.panel.remove();
  },

  FuntapticOIDCWebFlushCache__deps: ['$FuntapticOIDCWeb', '$FS'],
  FuntapticOIDCWebFlushCache: function () {
    if (FuntapticOIDCWeb.syncing) { FuntapticOIDCWeb.syncAgain = true; return; }
    FuntapticOIDCWeb.syncing = true;
    var flush = function () {
      FuntapticOIDCWeb.syncAgain = false;
      FS.syncfs(false, function (error) {
        if (error) console.error('OIDC token cache could not be persisted.');
        if (FuntapticOIDCWeb.syncAgain) flush();
        else FuntapticOIDCWeb.syncing = false;
      });
    };
    flush();
  }
});
