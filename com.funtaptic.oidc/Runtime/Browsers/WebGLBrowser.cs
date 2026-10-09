using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Funtaptic.OIDC.WebGL
{
    public sealed class WebGLBrowser : IBrowser
    {
        public static string ResolveRedirectUri(string applicationUrl, string callbackPath)
        {
            if (string.IsNullOrWhiteSpace(callbackPath))
                throw new ArgumentException("WebGL callback path must not be empty.");
            var page = new Uri(applicationUrl, UriKind.Absolute);
            var callback = new Uri(page, callbackPath);
            if ((callback.Scheme != "https" && callback.Scheme != "http") ||
                callback.GetLeftPart(UriPartial.Authority) != page.GetLeftPart(UriPartial.Authority) ||
                !string.IsNullOrEmpty(callback.Query) || !string.IsNullOrEmpty(callback.Fragment) ||
                !string.IsNullOrEmpty(callback.UserInfo))
                throw new ArgumentException("WebGL callback must be an HTTP(S) URL on the game's origin without a query or fragment.");
            return callback.AbsoluteUri;
        }

        public async Awaitable<Either<Uri, Error>> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
        {
#if UNITY_WEBGL
            cancellationToken.ThrowIfCancellationRequested();
            var beginResult = FuntapticOIDCWebBegin(options.StartUrl, options.EndUrl);
            if (beginResult < 0)
                return new Error("The browser blocked the authentication window. Allow popups for this site and try again.");
            if (beginResult == 0)
                return new Error("A WebGL authentication session is already running.");
            var started = Time.realtimeSinceStartupAsDouble;
            var runInBackground = Application.runInBackground;
            Application.runInBackground = true;
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var status = FuntapticOIDCWebPoll();
                    if (status != 0)
                    {
                        switch (status)
                        {
                            case 1:
                                return new Uri(FuntapticOIDCWebResponse());
                            default:
                                return new Error("Authentication window was closed or cancelled.");
                        }

                    }

                    if (Time.realtimeSinceStartupAsDouble - started >= options.Timeout.TotalSeconds)
                    {
                        return new Error("Authentication timed out.");
                    }
                    await Awaitable.NextFrameAsync(cancellationToken);
                }
            }
            finally
            {
                FuntapticOIDCWebClose();
                Application.runInBackground = runInBackground;
            }
#else
            throw new PlatformNotSupportedException("The WebGL browser requires a WebGL player build.");
#endif
        }

#if UNITY_WEBGL
        [DllImport("__Internal")] private static extern int FuntapticOIDCWebBegin(string startUrl, string endUrl);
        [DllImport("__Internal")] private static extern int FuntapticOIDCWebPoll();
        [DllImport("__Internal")] private static extern string FuntapticOIDCWebResponse();
        [DllImport("__Internal")] private static extern void FuntapticOIDCWebClose();
#endif
    }
}
