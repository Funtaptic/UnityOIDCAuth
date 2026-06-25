using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Duende.IdentityModel.OidcClient.Browser;
using UnityEngine;

namespace Funtaptic.OIDC.IOS
{
    public class IOSAuthenticationSessionBrowser : IBrowser
    {
        public const string Scheme = "funtaptic.oidc";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AuthenticationSessionCallback(string url, string error);

        private static readonly object SyncRoot = new object();

        private static readonly AuthenticationSessionCallback NativeCallback = HandleAuthenticationSessionCompleted;

        private static TaskCompletionSource<BrowserResult> _completionSource;

        public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken)
        {
            if (Application.platform != RuntimePlatform.IPhonePlayer)
                throw new InvalidOperationException("This method can only be called on iOS.");

            var completionSource = new TaskCompletionSource<BrowserResult>();

            lock (SyncRoot)
            {
                if (_completionSource != null && !_completionSource.Task.IsCompleted)
                {
                    return new BrowserResult
                    {
                        ResultType = BrowserResultType.UnknownError,
                        Error = "An iOS authentication session is already running."
                    };
                }

                _completionSource = completionSource;
            }

            try
            {
                using var autoExpire = new CancellationTokenSource(options.Timeout);
                using var canceled = cancellationToken.Register(() =>
                {
                    CancelNativeAuthenticationSession();
                    completionSource.TrySetCanceled();
                });
                using var timedOut = autoExpire.Token.Register(() =>
                {
                    CancelNativeAuthenticationSession();
                    TrySetBrowserResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.Timeout,
                        Error = "Timed out"
                    });
                });

                var startUrl = NormalizeStartUrl(options?.StartUrl);
                if (string.IsNullOrEmpty(startUrl))
                {
                    ClearCompletionSource(completionSource);
                    return new BrowserResult
                    {
                        ResultType = BrowserResultType.UnknownError,
                        Error = "Missing iOS authentication start URL."
                    };
                }

                Debug.Log($"Starting iOS OIDC authentication session. Start URL length: {startUrl.Length}");
                StartNativeAuthenticationSession(startUrl, Scheme, NativeCallback);
                return await completionSource.Task;
            }
            catch (Exception e)
            {
                ClearCompletionSource(completionSource);

                return new BrowserResult
                {
                    ResultType = BrowserResultType.UnknownError,
                    Error = e.Message
                };
            }
        }

        private static string NormalizeStartUrl(string startUrl)
        {
            if (string.IsNullOrWhiteSpace(startUrl))
                return startUrl;

            startUrl = startUrl.Trim();

            if (Uri.TryCreate(startUrl, UriKind.Absolute, out var uri))
                return uri.AbsoluteUri;

            return Uri.EscapeUriString(startUrl);
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FuntapticOIDCStartAuthenticationSession(
            IntPtr startUrl,
            IntPtr callbackScheme,
            AuthenticationSessionCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FuntapticOIDCCancelAuthenticationSession();

        private static void StartNativeAuthenticationSession(
            string startUrl,
            string callbackScheme,
            AuthenticationSessionCallback callback)
        {
            var startUrlPointer = IntPtr.Zero;
            var callbackSchemePointer = IntPtr.Zero;

            try
            {
                startUrlPointer = StringToUtf8Pointer(startUrl);
                callbackSchemePointer = StringToUtf8Pointer(callbackScheme);

                FuntapticOIDCStartAuthenticationSession(startUrlPointer, callbackSchemePointer, callback);
            }
            finally
            {
                Marshal.FreeHGlobal(startUrlPointer);
                Marshal.FreeHGlobal(callbackSchemePointer);
            }
        }

        private static void CancelNativeAuthenticationSession()
        {
            FuntapticOIDCCancelAuthenticationSession();
        }

        private static IntPtr StringToUtf8Pointer(string value)
        {
            if (value == null)
                return IntPtr.Zero;

            var bytes = Encoding.UTF8.GetBytes(value);
            var pointer = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            Marshal.WriteByte(pointer, bytes.Length, 0);
            return pointer;
        }
#else
        private static void StartNativeAuthenticationSession(
            string startUrl,
            string callbackScheme,
            AuthenticationSessionCallback callback)
        {
            throw new PlatformNotSupportedException("ASWebAuthenticationSession is only available on iOS player builds.");
        }

        private static void CancelNativeAuthenticationSession()
        {
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        [AOT.MonoPInvokeCallback(typeof(AuthenticationSessionCallback))]
#endif
        private static void HandleAuthenticationSessionCompleted(string url, string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                TrySetBrowserResult(new BrowserResult
                {
                    ResultType = error == "canceled"
                        ? BrowserResultType.UserCancel
                        : BrowserResultType.UnknownError,
                    Error = error
                });
                return;
            }

            try
            {
                var uri = new Uri(url);
                var response = string.IsNullOrEmpty(uri.Query) ? uri.Fragment : uri.Query;
                var responseParams = HttpUtility.UrlDecode(response);

                TrySetBrowserResult(new BrowserResult
                {
                    ResultType = BrowserResultType.Success,
                    Response = responseParams
                });
            }
            catch (Exception e)
            {
                TrySetBrowserResult(new BrowserResult
                {
                    ResultType = BrowserResultType.UnknownError,
                    Error = e.Message
                });
            }
        }

        private static void TrySetBrowserResult(BrowserResult result)
        {
            TaskCompletionSource<BrowserResult> completionSource;

            lock (SyncRoot)
            {
                completionSource = _completionSource;
                _completionSource = null;
            }

            completionSource?.TrySetResult(result);
        }

        private static void ClearCompletionSource(TaskCompletionSource<BrowserResult> completionSource)
        {
            lock (SyncRoot)
            {
                if (_completionSource == completionSource)
                    _completionSource = null;
            }
        }
    }
}
