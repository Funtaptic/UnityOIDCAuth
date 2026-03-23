using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Duende.IdentityModel.OidcClient.Browser;

using UnityEngine;
using UnityEngine.Android;

namespace Funtaptic.OIDC.Android
{
    public class AndroidChromeTabsBrowser : IBrowser
    {
        public const string Scheme = "funtaptic.oidc";

        public const string ActivityClassName = "com.funtaptic.AuthRedirectActivity";

        private class DisposableAction : IDisposable
        {
            private Action _action;

            public DisposableAction(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                _action?.Invoke();
                _action = null;
            }
        }

        public class RedirectCallbackProxy : AndroidJavaProxy
        {
            private static event Action<string> Callback;

            private static RedirectCallbackProxy _instance;

            public static IDisposable Register(Action<string> callback)
            {
                Callback += callback;
                return new DisposableAction(() => Callback -= callback);
            }

            [RuntimeInitializeOnLoadMethod]
            private static void AutoInit()
            {
                if (Application.platform != RuntimePlatform.Android)
                    return;

                using var javaClass = new AndroidJavaClass(ActivityClassName);

                _instance = new RedirectCallbackProxy();
                javaClass.SetStatic("callback", _instance);
            }

            public RedirectCallbackProxy() : base("com.funtaptic.RedirectCallback")
            {
            }

            public void callback(string uri)
            {
                Callback?.Invoke(uri);
            }
        }

        public static class AndroidChromeCustomTab
        {
            public static void LaunchUrl(string url)
            {
                if (Application.platform != RuntimePlatform.Android)
                    throw new InvalidOperationException("This method can only be called on Android");

#if UNITY_ANDROID
                using var intentBuilder = new AndroidJavaObject("androidx.browser.customtabs.CustomTabsIntent$Builder");
                using var intent = intentBuilder.Call<AndroidJavaObject>("build");
                using var uriClass = new AndroidJavaClass("android.net.Uri");
                using var uri = uriClass.CallStatic<AndroidJavaObject>("parse", url);
                intent.Call("launchUrl", AndroidApplication.currentActivity, uri);
#endif
            }
        }

        private IDisposable SubscribeAppFocused(Action<bool> onFocusChanged)
        {
            Application.focusChanged += onFocusChanged;
            return new DisposableAction(() => { Application.focusChanged -= onFocusChanged; });
        }

        public async Task<BrowserResult> InvokeAsync(BrowserOptions options,
            CancellationToken cancellationToken)
        {
            try
            {
                var completionSource = new TaskCompletionSource<BrowserResult>();

                using var autoExpire = new CancellationTokenSource(options.Timeout);

                using var canceled = cancellationToken.Register(() => { completionSource.SetCanceled(); });

                using var registration = autoExpire.Token.Register(() =>
                {
                    completionSource.SetResult(new BrowserResult()
                    {
                        ResultType = BrowserResultType.Timeout,
                        Error = "Timed out"
                    });
                });

                using var callbackSub = RedirectCallbackProxy.Register(url =>
                {
                    try
                    {
                        var uri = new Uri(url);
                        var queryParams = HttpUtility.UrlDecode(uri.Query);

                        completionSource.SetResult(new BrowserResult()
                        {
                            ResultType = BrowserResultType.Success,
                            Response = queryParams
                        });
                    }
                    catch (Exception e)
                    {
                        completionSource.SetException(e);
                    }
                });

                using var focusSub = SubscribeAppFocused(isFocused =>
                {
                    if (isFocused == false)
                        return;

                    completionSource.TrySetResult(new BrowserResult()
                    {
                        ResultType = BrowserResultType.UserCancel,
                        Error = "User cancelled"
                    });
                });

                AndroidChromeCustomTab.LaunchUrl(options.StartUrl);

                return await completionSource.Task;
            }
            catch (Exception e)
            {
                return new BrowserResult()
                {
                    ResultType = BrowserResultType.UnknownError,
                    Error = e.Message
                };
            }
        }
    }
}