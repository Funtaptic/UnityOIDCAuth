using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Scripting;

namespace Funtaptic.OIDC.Android
{
    public sealed class AndroidChromeTabsBrowser : IBrowser
    {
        public const string ActivityClassName = "com.funtaptic.AuthRedirectActivity";
        private readonly string _scheme;
        private static bool _running;

        public AndroidChromeTabsBrowser(string scheme)
        {
            if (string.IsNullOrWhiteSpace(scheme))
                throw new ArgumentException("Android callback scheme must not be empty.", nameof(scheme));
            _scheme = scheme;
        }

        [Preserve]
        public sealed class RedirectCallbackProxy : AndroidJavaProxy
        {
            private readonly TaskCompletionSource<string> _response;
            public RedirectCallbackProxy(TaskCompletionSource<string> response)
                : base("com.funtaptic.RedirectCallback") => _response = response;

            [Preserve]
            public void callback(string uri) => _response.TrySetResult(uri);
        }

        public async Awaitable<Either<Uri, Error>> InvokeAsync(BrowserOptions options,
            CancellationToken cancellationToken = default)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
#if UNITY_ANDROID
            if (_running) return new Error("An Android authentication session is already running.");
            if (cancellationToken.IsCancellationRequested) return new Error("Authentication was cancelled.");
            if (options.Timeout <= TimeSpan.Zero) return new Error("Authentication timed out.");
            _running = true;
            var response = new TaskCompletionSource<string>();
            var proxy = new RedirectCallbackProxy(response);
            var lostFocus = false;
            var returnedAt = double.PositiveInfinity;
            void OnFocusChanged(bool focused)
            {
                if (!focused) lostFocus = true;
                else if (lostFocus) returnedAt = Time.realtimeSinceStartupAsDouble;
            }

            Application.focusChanged += OnFocusChanged;
            try
            {
                using var activity = new AndroidJavaClass(ActivityClassName);
                activity.SetStatic("callback", proxy);
                using var builder = new AndroidJavaObject("androidx.browser.customtabs.CustomTabsIntent$Builder");
                using var intent = builder.Call<AndroidJavaObject>("build");
                using var uriClass = new AndroidJavaClass("android.net.Uri");
                using var uri = uriClass.CallStatic<AndroidJavaObject>("parse", options.StartUrl);
                var started = Time.realtimeSinceStartupAsDouble;
                intent.Call("launchUrl", AndroidApplication.currentActivity, uri);
                while (!response.Task.IsCompleted)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return new Error("Authentication was cancelled.");
                    if (Time.realtimeSinceStartupAsDouble - started >= options.Timeout.TotalSeconds)
                        return new Error("Authentication timed out.");
                    // Allow the redirect activity's callback to arrive before treating focus as dismissal.
                    if (Time.realtimeSinceStartupAsDouble - returnedAt >= 1.0)
                        return new Error("Authentication window was closed or cancelled.");
                    await Awaitable.NextFrameAsync();
                }

                if (!Uri.TryCreate(response.Task.Result, UriKind.Absolute, out var callback) ||
                    !string.Equals(callback.Scheme, _scheme, StringComparison.OrdinalIgnoreCase))
                    return new Error("Unexpected Android callback URI.");
                return callback;
            }
            catch (Exception exception)
            {
                return new Error(exception.Message);
            }
            finally
            {
                Application.focusChanged -= OnFocusChanged;
                try
                {
                    using var activity = new AndroidJavaClass(ActivityClassName);
                    activity.SetStatic<AndroidJavaObject>("callback", null);
                }
                finally
                {
                    GC.KeepAlive(proxy);
                    _running = false;
                }
            }
#else
            throw new PlatformNotSupportedException("Chrome Custom Tabs requires an Android player build.");
#endif
        }
    }
}