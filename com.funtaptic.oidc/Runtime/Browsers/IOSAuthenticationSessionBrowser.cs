using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Funtaptic.OIDC.IOS
{
    public sealed class IOSAuthenticationSessionBrowser : IBrowser
    {
        private readonly string _scheme;
        private static TaskCompletionSource<Either<Uri, Error>> _completionSource;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AuthenticationSessionCallback(string url, string error);
        private static readonly AuthenticationSessionCallback NativeCallback = HandleAuthenticationSessionCompleted;

        public IOSAuthenticationSessionBrowser(string scheme)
        {
            if (string.IsNullOrWhiteSpace(scheme))
                throw new ArgumentException("iOS callback scheme must not be empty.", nameof(scheme));
            _scheme = scheme;
        }

        public async Awaitable<Either<Uri, Error>> InvokeAsync(BrowserOptions options,
            CancellationToken cancellationToken = default)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (Application.platform != RuntimePlatform.IPhonePlayer)
                throw new PlatformNotSupportedException("ASWebAuthenticationSession requires an iOS player build.");
            var completion = new TaskCompletionSource<Either<Uri, Error>>();
            if (Interlocked.CompareExchange(ref _completionSource, completion, null) != null)
                return new Error("An iOS authentication session is already running.");

            try
            {
                if (cancellationToken.IsCancellationRequested) return new Error("Authentication was cancelled.");
                if (options.Timeout <= TimeSpan.Zero) return new Error("Authentication timed out.");
                var started = Time.realtimeSinceStartupAsDouble;
                StartNativeAuthenticationSession(options.StartUrl, _scheme, NativeCallback);
                while (!completion.Task.IsCompleted)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return new Error("Authentication was cancelled.");
                    if (Time.realtimeSinceStartupAsDouble - started >= options.Timeout.TotalSeconds)
                        return new Error("Authentication timed out.");
                    await Awaitable.NextFrameAsync();
                }

                var result = completion.Task.Result;
                if (result.IsLeft && !string.Equals(result.Left.Scheme, _scheme, StringComparison.OrdinalIgnoreCase))
                    return new Error("Unexpected iOS callback URI.");
                return result;
            }
            catch (Exception exception)
            {
                return new Error(exception.Message);
            }
            finally
            {
                CancelNativeAuthenticationSession();
                Interlocked.CompareExchange(ref _completionSource, null, completion);
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [AOT.MonoPInvokeCallback(typeof(AuthenticationSessionCallback))]
#endif
        private static void HandleAuthenticationSessionCompleted(string url, string error)
        {
            var completion = Volatile.Read(ref _completionSource);
            if (!string.IsNullOrEmpty(error))
                completion?.TrySetResult(new Error(error == "canceled" ? "Authentication was cancelled." : error));
            else if (Uri.TryCreate(url, UriKind.Absolute, out var callback))
                completion?.TrySetResult(callback);
            else
                completion?.TrySetResult(new Error("Invalid iOS callback URI."));
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


    }
}
