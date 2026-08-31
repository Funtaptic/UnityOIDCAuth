using System;
using System.Threading;
using System.Threading.Tasks;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Results;
using Funtaptic.OIDC.WebGL;
using UnityEngine;

namespace Funtaptic.OIDC
{
    public class SignedIn : IAuthState
    {
        private AuthHelper _authHelper;

        private Task _cTask;

        public AuthState State { get; private set; }

        public bool IsDoingWork => _cTask is { IsCompleted: false };

        private readonly CancellationTokenSource _disposeCancellationTokenSource = new CancellationTokenSource();

        public SignedIn(AuthHelper coreBehaviour, AuthState state)
        {
            _authHelper = coreBehaviour;
            State = state;
            _disposeCancellationTokenSource = new CancellationTokenSource();
        }

        public void Update()
        {
            if (IsDoingWork)
                return;

            if (State.AccessTokenExpiration < DateTimeOffset.Now)
            {
                _cTask = TryRefreshAsync();
            }
        }

        public async Task<UserInfoResult> GetUserInfoAsync(CancellationToken cancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                _disposeCancellationTokenSource.Token);

            var client = await _authHelper.GetClientAsync();

            if (client == null)
                throw new InvalidOperationException("Client is null");

            var userInfo = await client.GetUserInfoAsync(
                State.AccessToken,
                cts.Token);

            if (userInfo.IsError)
                throw new InvalidOperationException(userInfo.Error);

            return userInfo;
        }

        private async Task DoLogOutAsync()
        {
            var client = await _authHelper.GetClientAsync();

            if (client == null)
                return;

            try
            {
                await client.LogoutAsync(new LogoutRequest
                {
                    IdTokenHint = State.IdentityToken
                }, default);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public async Task LogOut(bool callLogOut = true)
        {
            _authHelper.DeleteCache();
            _authHelper.SetState(new SignedOut(_authHelper));

            if (callLogOut)
                await DoLogOutAsync();
        }

        private async Task TryRefreshAsync()
        {
            try
            {
                var refreshed = await RefreshTokenAsync();

                if (refreshed)
                    return;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            _authHelper.DeleteCache();
            _authHelper.SetState(new SignedOut(_authHelper));
        }

        private async Awaitable<bool> RefreshTokenAsync()
        {
            Debug.Log("Refreshing token...");
            var client = await _authHelper.GetClientAsync();

            if (client == null)
            {
                Debug.LogError("Failed to create client.");
                return false;
            }

            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                var tokenClient = new WebGLTokenClient(
                    client.Options.ProviderInformation.TokenEndpoint,
                    client.Options.ClientId);
                var webGLResult = await tokenClient.RefreshAsync(
                    State.RefreshToken,
                    _disposeCancellationTokenSource.Token);

                ApplyRefreshedState(
                    webGLResult.AccessToken,
                    webGLResult.RefreshToken,
                    webGLResult.IdentityToken,
                    webGLResult.AccessTokenExpiration);
                Debug.Log("Token refreshed successfully through UnityWebRequest.");
                return true;
            }

            var result =
                await client.RefreshTokenAsync(State.RefreshToken, null, null,
                    _disposeCancellationTokenSource.Token);

            if (result.IsError)
            {
                if (string.Equals(result.Error, "invalid_grant", StringComparison.OrdinalIgnoreCase))
                {
                    // A revoked or expired refresh token is a normal sign-out
                    // condition when restoring a cached session. TryRefreshAsync
                    // clears the cache and transitions to SignedOut below.
                    Debug.Log("Saved authentication session has expired. Sign in again to continue.");
                }
                else
                {
                    Debug.LogError($"Failed to refresh token: {result.Error}");
                }

                return false;
            }

            Debug.Log("Token refreshed successfully.");

            ApplyRefreshedState(
                result.AccessToken,
                result.RefreshToken,
                result.IdentityToken,
                result.AccessTokenExpiration);

            return true;
        }

        private void ApplyRefreshedState(
            string accessToken,
            string refreshToken,
            string identityToken,
            DateTimeOffset accessTokenExpiration)
        {
            State = new AuthState()
            {
                AccessToken = accessToken,
                RefreshToken = string.IsNullOrWhiteSpace(refreshToken)
                    ? State.RefreshToken
                    : refreshToken,
                IdentityToken = string.IsNullOrWhiteSpace(identityToken)
                    ? State.IdentityToken
                    : identityToken,
                AccessTokenExpiration = accessTokenExpiration
            };

            _authHelper.SaveToCache(State);
        }

        public void Dispose()
        {
            _disposeCancellationTokenSource?.Dispose();
        }
    }
}
