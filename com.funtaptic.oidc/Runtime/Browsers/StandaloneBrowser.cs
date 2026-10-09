using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Funtaptic.OIDC.Standalone
{
    namespace Funtaptic.OIDC.Auth
    {
        public class StandaloneBrowser : IBrowser
        {
             public static Uri RequireLoopbackRedirect(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttp ||
                !(uri.Host == "localhost" || uri.Host == "127.0.0.1") ||
                uri.Port <= 0 || !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidOperationException(
                    "Use an HTTP localhost or 127.0.0.1 redirect with no query or fragment.");
            return uri;
        }

        public async Awaitable<Either<Uri, Error>> InvokeAsync(BrowserOptions options,
            CancellationToken cancellationToken)
        {
            try
            {
                var redirect = RequireLoopbackRedirect(options.EndUrl);

                // Start listening before opening the browser, as in StandaloneBrowser.
                using var listener = new HttpListener();
                listener.Prefixes.Add(redirect.GetLeftPart(UriPartial.Authority) + "/");
                try
                {
                    listener.Start();
                }
                catch (HttpListenerException)
                {
                    return new Error("Could not listen on the redirect port. Choose an available port and register the matching redirect URI with the provider.");
                }

                // The caller's token covers both cancellation and the overall login timeout.
                // Closing the listener also releases any outstanding GetContextAsync operation.
                using var registration = cancellationToken.Register(() => listener.Close());
                cancellationToken.ThrowIfCancellationRequested();
                Application.OpenURL(options.StartUrl);
                var callback = await ListenForCallbackAsync(listener, redirect, cancellationToken);

                return callback;
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                return new Error("Operation canceled or timed out.");
            }
        }

        private static async Awaitable<Uri> ListenForCallbackAsync(HttpListener listener,
            Uri redirect, CancellationToken cancellationToken)
        {
            while (true)
            {
                var context = await listener.GetContextAsync();
                cancellationToken.ThrowIfCancellationRequested();
                var callback = context.Request.Url;
                context.Response.StatusCode = 200;
                await WriteResponseAsync(context.Response,
                    "Callback received. You can now close this window and return to Unity to check the result.",
                    cancellationToken);
                return callback;
            }
        }

        private static async Awaitable WriteResponseAsync(HttpListenerResponse response,
            string message, CancellationToken cancellationToken)
        {
            response.ContentType = "text/html; charset=utf-8";
            response.Headers["Cache-Control"] = "no-store";
            response.Headers["Referrer-Policy"] = "no-referrer";
            var buffer = Encoding.UTF8.GetBytes(
                "<!doctype html><title>OAuth callback</title><p>" + WebUtility.HtmlEncode(message) + "</p>");
            try
            {
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
            }
            catch (HttpListenerException)
            {
                /* Closing the browser tab must not lose a valid code. */
            }
            finally
            {
                response.Close();
            }
        }
        }
    }
}