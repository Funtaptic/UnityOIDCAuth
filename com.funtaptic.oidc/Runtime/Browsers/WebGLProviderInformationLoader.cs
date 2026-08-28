using System;
using System.Text.Json;
using System.Threading;
using Duende.IdentityModel.Jwk;
using Duende.IdentityModel.OidcClient;
using UnityEngine;
using UnityEngine.Networking;

namespace Funtaptic.OIDC.WebGL
{
    public sealed class WebGLProviderInformationLoader
    {
        private readonly string _authority;

        public WebGLProviderInformationLoader(string authority)
        {
            _authority = authority.TrimEnd('/');
        }

        public async Awaitable<ProviderInformation> LoadAsync(CancellationToken cancellationToken = default)
        {
            var discoveryJson = await GetAsync($"{_authority}/.well-known/openid-configuration", cancellationToken);
            using var discoveryDocument = JsonDocument.Parse(discoveryJson);
            var discovery = discoveryDocument.RootElement;
            var jwksJson = await GetAsync(discovery.GetProperty("jwks_uri").GetString(), cancellationToken);

            return new ProviderInformation
            {
                IssuerName = discovery.GetProperty("issuer").GetString(),
                AuthorizeEndpoint = discovery.GetProperty("authorization_endpoint").GetString(),
                TokenEndpoint = discovery.GetProperty("token_endpoint").GetString(),
                EndSessionEndpoint = discovery.GetProperty("end_session_endpoint").GetString(),
                UserInfoEndpoint = discovery.GetProperty("userinfo_endpoint").GetString(),
                KeySet = new JsonWebKeySet(jwksJson)
            };
        }

        private static async Awaitable<string> GetAsync(string url, CancellationToken cancellationToken)
        {
            using var request = UnityWebRequest.Get(url);
            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Awaitable.NextFrameAsync(cancellationToken);

            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException($"OIDC request failed for {url}: {request.error}");

            return request.downloadHandler.text;
        }
    }
}
