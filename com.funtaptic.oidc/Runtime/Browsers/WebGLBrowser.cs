// using System;
// using System.Runtime.InteropServices;
// using System.Threading;
// using System.Threading.Tasks;
// using UnityEngine;
//
// namespace Funtaptic.OIDC.WebGL
// {
//     public sealed class WebGLBrowser : IBrowser
//     {
//         public static string ResolveRedirectUri(string applicationUrl, string callbackPath)
//         {
//             if (string.IsNullOrWhiteSpace(callbackPath))
//                 throw new ArgumentException("WebGL callback path must not be empty.");
//             var page = new Uri(applicationUrl, UriKind.Absolute);
//             var callback = new Uri(page, callbackPath);
//             if ((callback.Scheme != "https" && callback.Scheme != "http") ||
//                 callback.GetLeftPart(UriPartial.Authority) != page.GetLeftPart(UriPartial.Authority) ||
//                 !string.IsNullOrEmpty(callback.Query) || !string.IsNullOrEmpty(callback.Fragment) ||
//                 !string.IsNullOrEmpty(callback.UserInfo))
//                 throw new ArgumentException("WebGL callback must be an HTTP(S) URL on the game's origin without a query or fragment.");
//             return callback.AbsoluteUri;
//         }
//
//         public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
//         {
// #if UNITY_WEBGL && !UNITY_EDITOR
//             cancellationToken.ThrowIfCancellationRequested();
//             if (FuntapticOIDCWebBegin(options.StartUrl, options.EndUrl) == 0)
//                 return new BrowserResult { ResultType = BrowserResultType.UnknownError, Error = "A WebGL authentication session is already running." };
//             var started = Time.realtimeSinceStartupAsDouble;
//             var runInBackground = Application.runInBackground;
//             Application.runInBackground = true;
//             try
//             {
//                 while (true)
//                 {
//                     cancellationToken.ThrowIfCancellationRequested();
//                     var status = FuntapticOIDCWebPoll();
//                     if (status != 0)
//                         return new BrowserResult
//                         {
//                             ResultType = status == 1 ? BrowserResultType.Success : BrowserResultType.UserCancel,
//                             // Preserve percent encoding; OidcClient parses and validates state.
//                             Response = status == 1 ? FuntapticOIDCWebResponse() : null,
//                             Error = status == 1 ? null : "Authentication window was closed or cancelled."
//                         };
//                     if (Time.realtimeSinceStartupAsDouble - started >= options.Timeout.TotalSeconds)
//                         return new BrowserResult { ResultType = BrowserResultType.Timeout, Error = "Authentication timed out." };
//                     await Awaitable.NextFrameAsync(cancellationToken);
//                 }
//             }
//             finally
//             {
//                 FuntapticOIDCWebClose();
//                 Application.runInBackground = runInBackground;
//             }
// #else
//             await Task.CompletedTask;
//             throw new PlatformNotSupportedException("The WebGL browser requires a WebGL player build.");
// #endif
//         }
//
//         internal static void FlushCache()
//         {
// #if UNITY_WEBGL && !UNITY_EDITOR
//             FuntapticOIDCWebFlushCache();
// #endif
//         }
//
// #if UNITY_WEBGL && !UNITY_EDITOR
//         [DllImport("__Internal")] private static extern int FuntapticOIDCWebBegin(string startUrl, string endUrl);
//         [DllImport("__Internal")] private static extern int FuntapticOIDCWebPoll();
//         [DllImport("__Internal")] private static extern string FuntapticOIDCWebResponse();
//         [DllImport("__Internal")] private static extern void FuntapticOIDCWebClose();
//         [DllImport("__Internal")] private static extern void FuntapticOIDCWebFlushCache();
// #endif
//     }
// }
