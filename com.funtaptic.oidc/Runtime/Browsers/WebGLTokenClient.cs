using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace Funtaptic.OIDC.WebGL
{
    internal sealed class WebGLTokenClient
    {
        private readonly string _tokenEndpoint;
        private readonly string _clientId;

        public WebGLTokenClient(string tokenEndpoint, string clientId)
        {
            _tokenEndpoint = tokenEndpoint;
            _clientId = clientId;
        }

        public Awaitable<WebGLTokenResult> ExchangeAuthorizationCodeAsync(
            string code,
            string redirectUri,
            string codeVerifier,
            CancellationToken cancellationToken)
        {
            return RequestAsync(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
                new KeyValuePair<string, string>("code_verifier", codeVerifier)
            }, cancellationToken);
        }

        public Awaitable<WebGLTokenResult> RefreshAsync(
            string refreshToken,
            CancellationToken cancellationToken)
        {
            return RequestAsync(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("refresh_token", refreshToken)
            }, cancellationToken);
        }

        private async Awaitable<WebGLTokenResult> RequestAsync(
            IReadOnlyList<KeyValuePair<string, string>> parameters,
            CancellationToken cancellationToken)
        {
            var requestBody = BuildFormBody(parameters);
            using var request = new UnityWebRequest(_tokenEndpoint, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestBody)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 30
            };

            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            request.SetRequestHeader("Accept", "application/json");

            Debug.Log($"[OIDC WebGL] Direct token request started: {DescribeUri(_tokenEndpoint)}.");
            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Awaitable.NextFrameAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException(
                    $"OIDC token request failed with HTTP {request.responseCode}: {request.error}");

            Debug.Log($"[OIDC WebGL] Direct token request completed with HTTP {request.responseCode}.");
            using var document = JsonDocument.Parse(request.downloadHandler.text);
            var root = document.RootElement;
            var accessToken = root.GetProperty("access_token").GetString();
            var expiresIn = root.GetProperty("expires_in").GetInt32();
            var refreshToken = TryGetString(root, "refresh_token", out var receivedRefreshToken)
                ? receivedRefreshToken
                : string.Empty;
            var identityToken = TryGetString(root, "id_token", out var receivedIdentityToken)
                ? receivedIdentityToken
                : string.Empty;

            Debug.Log($"[OIDC WebGL] Token response parsed. Refresh token: {!string.IsNullOrWhiteSpace(refreshToken)}; ID token: {!string.IsNullOrWhiteSpace(identityToken)}; Expires in: {expiresIn}s.");

            return new WebGLTokenResult(
                accessToken,
                refreshToken,
                identityToken,
                DateTimeOffset.UtcNow.AddSeconds(expiresIn));
        }

        private static string BuildFormBody(IReadOnlyList<KeyValuePair<string, string>> parameters)
        {
            var builder = new StringBuilder();

            for (var index = 0; index < parameters.Count; index++)
            {
                if (index > 0)
                    builder.Append('&');

                builder.Append(Uri.EscapeDataString(parameters[index].Key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(parameters[index].Value));
            }

            return builder.ToString();
        }

        private static bool TryGetString(JsonElement root, string propertyName, out string value)
        {
            if (root.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                value = property.GetString();
                return true;
            }

            value = string.Empty;
            return false;
        }

        private static string DescribeUri(string url)
        {
            var uri = new Uri(url);
            return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
        }
    }

    internal sealed class WebGLTokenResult
    {
        public string AccessToken { get; }
        public string RefreshToken { get; }
        public string IdentityToken { get; }
        public DateTimeOffset AccessTokenExpiration { get; }

        public WebGLTokenResult(
            string accessToken,
            string refreshToken,
            string identityToken,
            DateTimeOffset accessTokenExpiration)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            IdentityToken = identityToken;
            AccessTokenExpiration = accessTokenExpiration;
        }
    }
}
