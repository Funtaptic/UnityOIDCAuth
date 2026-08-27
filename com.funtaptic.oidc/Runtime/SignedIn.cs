using System;
using System.Threading;
using System.Threading.Tasks;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Results;
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

        public void LogOut(bool callLogOut = true)
        {
            _authHelper.DeleteCache();
            _authHelper.SetState(new SignedOut(_authHelper));

            if (callLogOut)
                _ = DoLogOutAsync();
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

            var result =
                await client.RefreshTokenAsync(State.RefreshToken, null, null,
                    _disposeCancellationTokenSource.Token);

            if (result.IsError)
            {
                Debug.LogError($"Failed to refresh token: {result.Error}");
                return false;
            }

            Debug.Log("Token refreshed successfully.");

            State = new AuthState()
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                IdentityToken = result.IdentityToken,
                AccessTokenExpiration = result.AccessTokenExpiration
            };

            _authHelper.SaveToCache(State);

            return true;
        }

        public void Dispose()
        {
            _disposeCancellationTokenSource?.Dispose();
        }
    }
}