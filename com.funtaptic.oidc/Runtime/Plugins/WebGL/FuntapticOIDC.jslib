mergeInto(LibraryManager.library, {
  $FuntapticOIDCWeb: {
    session: null,
    capture: function (popup) {
      var session = FuntapticOIDCWeb.session;
      if (!session || !popup || popup !== session.popup) return false;
      if (session.status) return session.status === 1;
      try {
        var url = new URL(popup.location.href);
        if (url.origin === session.end.origin && url.pathname === session.end.pathname) {
          session.response = url.href;
          session.status = 1;
          return true;
        }
      } catch (_) {
        // Cross-origin access is expected while the provider is displayed.
      }
      return false;
    }
  },

  FuntapticOIDCWebBegin__deps: ['$FuntapticOIDCWeb'],
  FuntapticOIDCWebBegin: function (startPointer, endPointer) {
    if (FuntapticOIDCWeb.session) return 0;
    var start = UTF8ToString(startPointer);
    var end = new URL(UTF8ToString(endPointer));
    var popup = window.open(start, '_blank', 'popup,width=520,height=720');
    var session = { end: end, popup: popup, status: 0, response: '', overlay: null };
    FuntapticOIDCWeb.session = session;
    // The callback can finish even while Unity's animation frames are suspended.
    session.complete = function (callbackWindow) {
      if (FuntapticOIDCWeb.session !== session) return false;
      return FuntapticOIDCWeb.capture(callbackWindow);
    };
    window.FuntapticOIDCWebComplete = session.complete;
    if (!popup) {
      // Unity callbacks or async setup can outlive Safari's user activation.
      // Retry synchronously from a native button, only when direct launch failed.
      var previousFocus = document.activeElement;
      var overlay = document.createElement('div');
      overlay.setAttribute('role', 'dialog');
      overlay.setAttribute('aria-label', 'Authentication');
      overlay.style.cssText = 'position:fixed;inset:0;z-index:2147483647;display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,.65);font:16px sans-serif;color:#111;';
      var panel = document.createElement('div');
      panel.style.cssText = 'background:white;padding:24px;border-radius:12px;max-width:360px;margin:16px;';
      var message = document.createElement('p');
      message.textContent = 'Continue to open the authentication window.';
      message.setAttribute('aria-live', 'polite');
      var proceed = document.createElement('button');
      proceed.type = 'button';
      proceed.textContent = 'Continue sign-in';
      proceed.style.cssText = 'padding:12px;margin:4px;cursor:pointer;';
      var cancel = document.createElement('button');
      cancel.type = 'button';
      cancel.textContent = 'Cancel';
      cancel.style.cssText = proceed.style.cssText;
      session.dismiss = function () {
        overlay.remove();
        session.overlay = null;
        if (previousFocus && previousFocus.isConnected) previousFocus.focus();
      };
      proceed.onclick = function () {
        if (FuntapticOIDCWeb.session !== session || session.status || session.popup) return;
        session.popup = window.open(start, '_blank', 'popup,width=520,height=720');
        if (session.popup) session.dismiss();
        else message.textContent = 'The window is still blocked. Allow popups for this site, then try again.';
      };
      cancel.onclick = function () {
        if (FuntapticOIDCWeb.session !== session) return;
        session.status = 2;
        session.dismiss();
      };
      overlay.onkeydown = function (event) {
        if (event.key === 'Escape') { event.preventDefault(); cancel.onclick(); }
      };
      panel.appendChild(message);
      panel.appendChild(proceed);
      panel.appendChild(cancel);
      overlay.appendChild(panel);
      session.overlay = overlay;
      document.body.appendChild(overlay);
      proceed.focus();
    }
    return 1;
  },

  FuntapticOIDCWebPoll__deps: ['$FuntapticOIDCWeb'],
  FuntapticOIDCWebPoll: function () {
    var session = FuntapticOIDCWeb.session;
    if (!session) return 2;
    if (session.status) return session.status;
    if (!session.popup) return 0;
    if (session.popup.closed) return 2;
    FuntapticOIDCWeb.capture(session.popup);
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
    if (window.FuntapticOIDCWebComplete === session.complete) delete window.FuntapticOIDCWebComplete;
    if (session.overlay) session.dismiss();
    if (session.popup && !session.popup.closed) session.popup.close();
  }
});
