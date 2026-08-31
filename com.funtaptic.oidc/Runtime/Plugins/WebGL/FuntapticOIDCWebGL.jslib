mergeInto(LibraryManager.library, {
    FuntapticOIDCPreparePopup: function () {
        window.funtapticOidcCallbackUrl = null;

        if (!window.funtapticOidcMessageHandler) {
            window.funtapticOidcMessageHandler = function (event) {
                if (event.origin !== window.location.origin || !event.data || event.data.type !== 'funtaptic-oidc-callback') {
                    return;
                }

                window.funtapticOidcCallbackUrl = event.data.url;
                console.log('[OIDC WebGL] Callback message received from the authentication popup.');
            };
            window.addEventListener('message', window.funtapticOidcMessageHandler);
        }

        window.funtapticOidcPopup = window.open('about:blank', 'FuntapticOIDC', 'popup=yes,width=520,height=760,scrollbars=yes,resizable=yes');
        if (!window.funtapticOidcPopup) {
            console.warn('[OIDC WebGL] Browser blocked the authentication popup.');
            return 0;
        }

        console.log('[OIDC WebGL] Authentication popup prepared.');
        window.funtapticOidcPopup.document.title = 'Signing in...';
        window.funtapticOidcPopup.document.body.innerHTML = '<p style="font: 20px sans-serif; padding: 24px">Preparing sign in...</p>';
        return 1;
    },

    FuntapticOIDCClosePreparedPopup: function () {
        if (window.funtapticOidcPopup && !window.funtapticOidcPopup.closed) {
            window.funtapticOidcPopup.close();
        }
        window.funtapticOidcPopup = null;
    },

    FuntapticOIDCNavigatePopup: function (urlPointer) {
        if (!window.funtapticOidcPopup || window.funtapticOidcPopup.closed) {
            console.error('[OIDC WebGL] Cannot navigate authentication popup because it is closed.');
            return;
        }
        window.funtapticOidcPopup.location.href = UTF8ToString(urlPointer);
        window.funtapticOidcPopup.focus();
    },

    FuntapticOIDCGetCallbackUrl: function () {
        if (!window.funtapticOidcCallbackUrl) {
            return 0;
        }

        var callbackUrl = window.funtapticOidcCallbackUrl;
        window.funtapticOidcCallbackUrl = null;
        return stringToNewUTF8(callbackUrl);
    },

    FuntapticOIDCIsPopupClosed: function () {
        return !window.funtapticOidcPopup || window.funtapticOidcPopup.closed ? 1 : 0;
    },

    FuntapticOIDCForwardCallbackToOpener: function () {
        var parameters = new URLSearchParams(window.location.search);
        if (!parameters.has('oidc_callback') || !window.opener) {
            return;
        }

        window.opener.postMessage({ type: 'funtaptic-oidc-callback', url: window.location.href }, window.location.origin);
        console.log('[OIDC WebGL] Authentication callback sent to the opener.');
        window.setTimeout(function () { window.close(); }, 100);
    }
});
