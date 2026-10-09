using System;
using System.Text.Json;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace Funtaptic.OIDC
{
    public static class AsyncExtensions
    {
        public static async Awaitable<Either<T, Error>> SendAsync<T>(this UnityWebRequest request,
            CancellationToken cancellationToken)
            where T : class
        {
            cancellationToken.ThrowIfCancellationRequested();

            request.SetRequestHeader("Accept", "application/json");

            var operation = request.SendWebRequest();
            try
            {
                while (operation.isDone == false)
                    await Awaitable.NextFrameAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Awaitable cancellation may complete on the timeout timer thread.
                await Awaitable.MainThreadAsync();
                request.Abort();
                throw;
            }

            if (request.result != UnityWebRequest.Result.Success || request.responseCode < 200 ||
                request.responseCode >= 300)
                // Never include the response body: it may contain tokens or other sensitive data.
                return new Error(
                    $"OAuth HTTP request failed (status {request.responseCode}). Check provider settings and connectivity.");
            try
            {
                var result = JsonSerializer.Deserialize<T>(request.downloadHandler.text);
                if (result == null)
                    return new Error("The provider returned an empty JSON response.");
                return result;
            }
            catch (Exception e)
            {
                return new Error(e.Message);
            }
        }
    }

    public class UriHelpers
    {
        public static bool IsExpectedCallback(Uri callback, Uri redirect)
        {
            return callback is { IsAbsoluteUri: true }
                   && redirect is { IsAbsoluteUri: true }
                   && callback.Fragment.Length == 0
                   && callback.UserInfo.Length == 0
                   && Uri.Compare(
                       callback,
                       redirect,
                       UriComponents.SchemeAndServer | UriComponents.Path,
                       UriFormat.UriEscaped,
                       StringComparison.Ordinal) == 0;
        }
    }

    public class DiscoveryService
    {
        public static async Awaitable<Either<OAuthDiscovery, Error>> DiscoverAsync(string issuer,
            CancellationToken cancellationToken)
        {
            using var request = UnityWebRequest.Get(issuer.TrimEnd('/') + "/.well-known/openid-configuration");

            return await request.SendAsync<OAuthDiscovery>(cancellationToken);
        }
    }
}