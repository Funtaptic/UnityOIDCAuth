using System;
using System.Text.Json.Serialization;

namespace Funtaptic.OIDC
{
    public sealed class OAuthDiscovery
    {
        [JsonPropertyName("issuer")] public string Issuer { get; set; }

        [JsonPropertyName("authorization_endpoint")]
        public string AuthorizationEndpoint { get; set; }

        [JsonPropertyName("token_endpoint")] public string TokenEndpoint { get; set; }

        [JsonPropertyName("end_session_endpoint")]
        public string EndSessionEndpoint { get; set; }
    }

    public sealed class OAuthTokens
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; }
        [JsonPropertyName("token_type")] public string TokenType { get; set; }
        [JsonPropertyName("expires_in")] public long? ExpiresIn { get; set; }
        
        [JsonPropertyName("id_token")] public string IdToken { get; set; }

        [JsonPropertyName("scope")] public string Scope { get; set; }

        [JsonIgnore] public DateTimeOffset? ExpiresAt { get; private set; }

        public bool IsError(out Error error)
        {
            if (string.IsNullOrWhiteSpace(AccessToken) ||
                !string.Equals(TokenType, "Bearer", StringComparison.OrdinalIgnoreCase) || ExpiresIn < 0)
            {
                error = new Error("The provider returned an invalid bearer token response.");
                return true;
            }

            if (ExpiresIn.HasValue)
            {
                try
                {
                    ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(ExpiresIn.Value);
                }
                catch (ArgumentOutOfRangeException)
                {
                    error = new Error("The provider returned an invalid token lifetime.");
                    return true;
                }
            }

            error = null;
            return false;
        }
    }
}