using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Web;
using UnityEngine;
using UnityEngine.Networking;

namespace Funtaptic.OIDC
{
    public class ClientSettings
    {
        public string ClientId { get; set; }

        public string RedirectUri { get; set; }

        public string PostLogoutRedirectUri { get; set; }

        public IBrowser Browser { get; set; }

        public string Scope { get; set; }
    }

    public class Error
    {
        public string Message { get; set; }

        public Error(string message)
        {
            Message = message;
        }
    }

    /// <summary>Call on Unity's main thread. Every outgoing HTTP request uses UnityWebRequest.</summary>
    public sealed class OAuthClient
    {
        private readonly ClientSettings _clientSettings;
        private OAuthDiscovery _discovery;

        public OAuthClient(OAuthDiscovery discovery, ClientSettings clientSettings)
        {
            _clientSettings = clientSettings;
            _discovery = discovery;
        }

        public async Awaitable<Either<OAuthTokens, Error>> LoginAsync(TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var verifier = CreateRandomValue();
            var state = CreateRandomValue();
            var challenge = CreateChallenge(verifier);

            var builder = new UriBuilder(_discovery.AuthorizationEndpoint);
            var query = HttpUtility.ParseQueryString(builder.Query);
            query["response_type"] = "code";
            query["client_id"] = _clientSettings.ClientId;
            query["redirect_uri"] = _clientSettings.RedirectUri;
            query["scope"] = _clientSettings.Scope;
            query["state"] = state;
            query["code_challenge"] = challenge;
            query["code_challenge_method"] = "S256";
            builder.Query = query.ToString();
            var url = builder.Uri.AbsoluteUri;

            var browserOptions = new BrowserOptions(url, _clientSettings.RedirectUri)
            {
                Timeout = timeout
            };
            var browserResult =
                await _clientSettings.Browser.AuthorizeAsync(browserOptions,
                    cancellationToken);

            if (browserResult.IsRight)
                return browserResult.Right;

            var code = ReadCode(browserResult.Left, new Uri(_clientSettings.RedirectUri), state,
                _discovery.Issuer);

            if (code.IsRight)
                return code.Right;

            using var request = UnityWebRequest.Post(_discovery.TokenEndpoint, new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = _clientSettings.ClientId,
                ["code"] = code.Left,
                ["code_verifier"] = verifier,
                ["redirect_uri"] = _clientSettings.RedirectUri
            });

            var res = await request.SendAsync<OAuthTokens>(cancellationToken);

            if (res.IsRight)
                return res.Right;

            var tokens = res.Left;

            if (tokens.IsError(out var tokenError))
                return tokenError;

            return tokens;
        }

        public async Awaitable LogoutAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_discovery.EndSessionEndpoint))
                throw new InvalidOperationException("The provider does not advertise a logout endpoint.");

            if (!Uri.TryCreate(_discovery.EndSessionEndpoint, UriKind.Absolute, out var endpoint) ||
                endpoint.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(endpoint.UserInfo) ||
                !string.IsNullOrEmpty(endpoint.Fragment))
                throw new InvalidOperationException("The provider logout endpoint must be an HTTPS URL.");

            var state = CreateRandomValue();
            var builder = new UriBuilder(endpoint);
            var query = HttpUtility.ParseQueryString(builder.Query);
            query["client_id"] = _clientSettings.ClientId;
            query["post_logout_redirect_uri"] = _clientSettings.PostLogoutRedirectUri;
            query["state"] = state;
            builder.Query = query.ToString();

            var result = await _clientSettings.Browser.AuthorizeAsync(
                new BrowserOptions(builder.Uri.AbsoluteUri, _clientSettings.RedirectUri), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.IsRight)
                throw new InvalidOperationException(result.Right.Message);

            if (!UriHelpers.IsExpectedCallback(result.Left, new Uri(_clientSettings.PostLogoutRedirectUri)))
                throw new InvalidOperationException("The logout callback did not match the configured redirect.");

            var fields = HttpUtility.ParseQueryString(result.Left.Query);
            var states = fields.GetValues("state");
            if (states == null || states.Length != 1 ||
                !string.Equals(states[0], state, StringComparison.Ordinal))
                throw new InvalidOperationException("The logout callback state did not match this attempt.");
            if (fields.GetValues("error") != null)
                throw new InvalidOperationException("The provider declined logout.");
        }

        public static string CreateRandomValue()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return Base64Url(bytes);
        }

        public static string CreateChallenge(string verifier)
        {
            using (var sha = SHA256.Create())
                return Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
        }

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static Either<string, Error> ReadCode(Uri callback, Uri redirect, string state, string issuer)
        {
            if (UriHelpers.IsExpectedCallback(callback, redirect) == false)
                return new Error("OAuth callback or state did not match this login attempt.");

            var fields = HttpUtility.ParseQueryString(callback.Query);
            if (fields["iss"] != null && fields["iss"] != issuer)
                return new Error("The callback issuer did not match the configured provider.");
            if (fields["error"] != null)
                return new Error("The provider declined authorization. Try signing in again.");
            var code = fields["code"];
            if (string.IsNullOrWhiteSpace(code))
                return new Error("The callback contained no authorization code.");
            if (string.Equals(fields["state"], state, StringComparison.Ordinal) == false)
                return new Error("The callback state did not match the configured provider.");
            return code;
        }

        public async Awaitable<Either<OAuthTokens, Error>> RefreshAsync(string refreshToken,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
                throw new InvalidOperationException("No refresh token is available.");

            using var request = UnityWebRequest.Post(_discovery.TokenEndpoint, new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = _clientSettings.ClientId,
                ["refresh_token"] = refreshToken
            });

            var res = await request.SendAsync<OAuthTokens>(cancellationToken);
            if (res.IsRight)
                return res.Right;

            var tokens = res.Left;

            if (tokens.IsError(out var tokenError))
                return tokenError;

            // Providers may rotate the refresh token or omit it when the old one remains valid.
            if (string.IsNullOrEmpty(tokens.RefreshToken))
                tokens.RefreshToken = refreshToken;

            return tokens;
        }
    }
}