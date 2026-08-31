using System;
using System.Threading;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Browser;
using UnityEngine;

namespace Funtaptic.OIDC.WebGL
{
    internal sealed class WebGLLoginFlow
    {
        public async Awaitable<WebGLTokenResult> AuthenticateAsync(
            OidcClient client,
            CancellationToken cancellationToken)
        {
            Debug.Log("[OIDC WebGL] Preparing authorization request with state and PKCE.");
            var authorizeState = await client.PrepareLoginAsync(null, cancellationToken);
            Debug.Log("[OIDC WebGL] Authorization request prepared. Opening the authentication browser.");

            var browser = new WebGLBrowser();
            var browserResult = await browser.InvokeAsync(new BrowserOptions(
                authorizeState.StartUrl,
                authorizeState.RedirectUri)
            {
                DisplayMode = DisplayMode.Visible,
                Timeout = TimeSpan.FromSeconds(300)
            }, cancellationToken);

            if (browserResult.ResultType != BrowserResultType.Success)
                throw new InvalidOperationException(
                    $"OIDC browser returned {browserResult.ResultType}: {browserResult.Error}");

            var callbackUri = new Uri(browserResult.Response);

            if (TryGetQueryParameter(callbackUri, "error", out var protocolError))
                throw new InvalidOperationException($"OIDC authorization failed: {protocolError}");

            if (!TryGetQueryParameter(callbackUri, "state", out var returnedState) ||
                !string.Equals(returnedState, authorizeState.State, StringComparison.Ordinal))
                throw new InvalidOperationException("OIDC authorization callback contains an invalid state.");

            if (TryGetQueryParameter(callbackUri, "iss", out var returnedIssuer) &&
                !string.Equals(
                    returnedIssuer.TrimEnd('/'),
                    client.Options.ProviderInformation.IssuerName.TrimEnd('/'),
                    StringComparison.Ordinal))
                throw new InvalidOperationException("OIDC authorization callback contains an invalid issuer.");

            if (!TryGetQueryParameter(callbackUri, "code", out var authorizationCode))
                throw new InvalidOperationException("OIDC authorization callback does not contain a code.");

            Debug.Log("[OIDC WebGL] Authorization callback validated. Redeeming the code directly through UnityWebRequest.");
            var tokenClient = new WebGLTokenClient(
                client.Options.ProviderInformation.TokenEndpoint,
                client.Options.ClientId);

            return await tokenClient.ExchangeAuthorizationCodeAsync(
                authorizationCode,
                authorizeState.RedirectUri,
                authorizeState.CodeVerifier,
                cancellationToken);
        }

        private static bool TryGetQueryParameter(Uri uri, string name, out string value)
        {
            var query = uri.Query;
            if (query.StartsWith("?", StringComparison.Ordinal))
                query = query.Substring(1);

            foreach (var item in query.Split('&'))
            {
                var separatorIndex = item.IndexOf('=');
                var encodedName = separatorIndex >= 0 ? item.Substring(0, separatorIndex) : item;
                var decodedName = DecodeQueryValue(encodedName);
                if (!string.Equals(decodedName, name, StringComparison.Ordinal))
                    continue;

                var encodedValue = separatorIndex >= 0 ? item.Substring(separatorIndex + 1) : string.Empty;
                value = DecodeQueryValue(encodedValue);
                return true;
            }

            value = string.Empty;
            return false;
        }

        private static string DecodeQueryValue(string value)
        {
            return Uri.UnescapeDataString(value.Replace("+", " "));
        }
    }
}
