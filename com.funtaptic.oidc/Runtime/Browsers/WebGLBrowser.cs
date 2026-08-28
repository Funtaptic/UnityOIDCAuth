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
        private static extern bool FuntapticOIDCPreparePopup();

        [DllImport("__Internal")]
        private static extern void FuntapticOIDCClosePreparedPopup();

        [DllImport("__Internal")]
        private static extern void FuntapticOIDCNavigatePopup(string url);

        [DllImport("__Internal")]
        private static extern string FuntapticOIDCGetCallbackUrl();

        [DllImport("__Internal")]
        private static extern string FuntapticOIDCGetCurrentPageUrl();

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
            return FuntapticOIDCPreparePopup();
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
            return FuntapticOIDCGetCurrentPageUrl();
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
                var callbackUrl = FuntapticOIDCGetCallbackUrl();
                if (!string.IsNullOrEmpty(callbackUrl))
                {
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
