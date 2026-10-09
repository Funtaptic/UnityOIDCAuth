mergeInto(LibraryManager.library, {
  $FuntapticOIDCWeb: { session: null },

  FuntapticOIDCWebBegin__deps: ['$FuntapticOIDCWeb'],
  FuntapticOIDCWebBegin: function (startPointer, endPointer) {
    if (FuntapticOIDCWeb.session) return 0;
    var start = UTF8ToString(startPointer);
    var end = new URL(UTF8ToString(endPointer));
    var popup = window.open(start, '_blank', 'popup,width=520,height=720');
    if (!popup) return -1;
    FuntapticOIDCWeb.session = { end: end, popup: popup, status: 0, response: '' };
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
  }
});
