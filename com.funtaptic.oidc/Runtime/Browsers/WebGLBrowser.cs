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
            return FuntapticOIDCPreparePopup() != 0;
#else
            return false;
#endif
        }

        public static void ClosePreparedPopup()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FuntapticOIDCClosePreparedPopup();
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
            FuntapticOIDCNavigatePopup(options.StartUrl);

            while (!cancellationToken.IsCancellationRequested)
            {
                var callbackPointer = FuntapticOIDCGetCallbackUrl();
                if (callbackPointer != IntPtr.Zero)
                {
                    var callbackUrl = Marshal.PtrToStringAnsi(callbackPointer);
                    return new BrowserResult
                    {
                        ResultType = BrowserResultType.Success,
                        Response = callbackUrl
                    };
                }

                await Awaitable.NextFrameAsync(cancellationToken);
            }

            return new BrowserResult { ResultType = BrowserResultType.UserCancel };
#else
            await Task.CompletedTask;
            return new BrowserResult { ResultType = BrowserResultType.UnknownError, Error = "WebGL browser is only available in a WebGL player." };
#endif
        }
    }
}
