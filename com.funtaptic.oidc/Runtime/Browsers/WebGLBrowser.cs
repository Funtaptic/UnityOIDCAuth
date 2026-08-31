using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Duende.IdentityModel.OidcClient.Browser;
using UnityEngine;

namespace Funtaptic.OIDC.WebGL
{
    public sealed class WebGLBrowser : IBrowser
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int FuntapticOIDCPreparePopup();

        [DllImport("__Internal")]
        private static extern void FuntapticOIDCClosePreparedPopup();

        [DllImport("__Internal")]
        private static extern void FuntapticOIDCNavigatePopup(string url);

        [DllImport("__Internal")]
        private static extern IntPtr FuntapticOIDCGetCallbackUrl();

        [DllImport("__Internal")]
        private static extern void FuntapticOIDCForwardCallbackToOpener();
#endif

        private const long PopupClosedPointerValue = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ForwardCallbackToOpener()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FuntapticOIDCForwardCallbackToOpener();
#endif
        }

        public static bool PreparePopup()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var prepared = FuntapticOIDCPreparePopup() != 0;
            Debug.Log(prepared
                ? "[OIDC WebGL] Authentication popup prepared."
                : "[OIDC WebGL] Authentication popup could not be opened; the browser may have blocked it.");
            return prepared;
#else
            return false;
#endif
        }

        public static void ClosePreparedPopup()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FuntapticOIDCClosePreparedPopup();
            Debug.Log("[OIDC WebGL] Authentication popup closed by Unity.");
#endif
        }

        public static string GetCurrentPageUrl()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var pageUrl = Application.absoluteURL;
            var queryIndex = pageUrl.IndexOf('?');
            if (queryIndex >= 0)
                pageUrl = pageUrl.Substring(0, queryIndex);

            var fragmentIndex = pageUrl.IndexOf('#');
            if (fragmentIndex >= 0)
                pageUrl = pageUrl.Substring(0, fragmentIndex);

            var lastSlashIndex = pageUrl.LastIndexOf('/');
            return lastSlashIndex >= 0 ? pageUrl.Substring(0, lastSlashIndex + 1) : $"{pageUrl}/";
#else
            return string.Empty;
#endif
        }

        public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[OIDC WebGL] Navigating authentication popup to {DescribeUrl(options.StartUrl)}.");
            FuntapticOIDCNavigatePopup(options.StartUrl);
            var closedPopupFrames = 0;
            var timeoutAt = Time.realtimeSinceStartupAsDouble + options.Timeout.TotalSeconds;

            while (!cancellationToken.IsCancellationRequested)
            {
                if (Time.realtimeSinceStartupAsDouble >= timeoutAt)
                {
                    Debug.LogWarning("[OIDC WebGL] Authentication browser wait timed out.");
                    return new BrowserResult { ResultType = BrowserResultType.Timeout };
                }

                var callbackPointer = FuntapticOIDCGetCallbackUrl();
                if (callbackPointer != IntPtr.Zero)
                {
                    if (callbackPointer.ToInt64() == PopupClosedPointerValue)
                    {
                        // The JavaScript callback function uses this reserved pointer
                        // value to report that the popup was closed without a callback.
                        if (++closedPopupFrames >= 10)
                        {
                            Debug.LogWarning("[OIDC WebGL] Authentication popup closed before a callback was received.");
                            return new BrowserResult
                            {
                                ResultType = BrowserResultType.UserCancel,
                                Error = "The authentication popup was closed before the callback was received."
                            };
                        }
                    }
                    else
                    {
                        var callbackUrl = Marshal.PtrToStringAnsi(callbackPointer);
                        Debug.Log($"[OIDC WebGL] Authentication callback received from {DescribeUrl(callbackUrl)}.");
                        return new BrowserResult
                        {
                            ResultType = BrowserResultType.Success,
                            Response = callbackUrl
                        };
                    }
                }
                else
                {
                    closedPopupFrames = 0;
                }

                await Awaitable.NextFrameAsync(cancellationToken);
            }

            Debug.LogWarning("[OIDC WebGL] Authentication browser wait was cancelled.");
            return new BrowserResult { ResultType = BrowserResultType.UserCancel };
#else
            await Task.CompletedTask;
            return new BrowserResult { ResultType = BrowserResultType.UnknownError, Error = "WebGL browser is only available in a WebGL player." };
#endif
        }

        private static string DescribeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return "<empty>";

            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                ? $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}"
                : "<invalid-url>";
        }
    }
}
