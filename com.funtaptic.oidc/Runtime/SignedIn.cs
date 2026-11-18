using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Funtaptic.OIDC
{
    public class SignedIn : IAuthState
    {
        private AuthHelper _authHelper;

        private Task _cTask;
        private const int RefreshLeadTimeSeconds = 60;
        private const int RefreshRetryDelaySeconds = 30;
        private DateTimeOffset _nextRefreshAttempt;
        private bool _lastRefreshSucceeded;

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
            if (IsDoingWork || DateTimeOffset.UtcNow < _nextRefreshAttempt)
                return;

            if (State.AccessTokenExpiration <= DateTimeOffset.UtcNow.AddSeconds(RefreshLeadTimeSeconds))
            {
                _cTask = TryRefreshAsync();
            }
        }

        public async Awaitable<bool> RefreshAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_authHelper.State, this))
                return false;

            if (!IsDoingWork)
            {
                if (!_lastRefreshSucceeded && DateTimeOffset.UtcNow < _nextRefreshAttempt)
                    return false;
                _cTask = TryRefreshAsync();
            }

            while (IsDoingWork)
                await Awaitable.NextFrameAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            return ReferenceEquals(_authHelper.State, this) && _lastRefreshSucceeded;
        }

        public async Task<UserInfoResult> GetUserInfoAsync(CancellationToken cancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                _disposeCancellationTokenSource.Token);
            cts.Token.ThrowIfCancellationRequested();

            var client = await _authHelper.GetClientAsync();
            cts.Token.ThrowIfCancellationRequested();

            if (client == null)
                throw new InvalidOperationException("Failed to create the authentication client.");

            var userInfo = await client.GetUserInfoAsync(State.AccessToken, cts.Token);

            if (userInfo.IsRight)
                throw new InvalidOperationException(userInfo.Right.Message);

            return userInfo.Left;
        }

        private async Task DoLogOutAsync()
        {
            var client = await _authHelper.GetClientAsync();

            if (client == null)
                return;

            try
            {
                //State.IdentityToken
                await client.LogoutAsync(State.IdentityToken, default);
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
            _lastRefreshSucceeded = false;
            try
            {
                _lastRefreshSucceeded = await RefreshTokenAsync();
            }
            catch (OperationCanceledException) when (_disposeCancellationTokenSource.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                _nextRefreshAttempt = DateTimeOffset.UtcNow.AddSeconds(RefreshRetryDelaySeconds);
            }
        }

        private async Awaitable<bool> RefreshTokenAsync()
        {
            Debug.Log("Refreshing token...");
            Either<OAuthTokens, Error> result;
            if (!string.IsNullOrEmpty(State.RefreshTokenProvider))
            {
                var provider = _authHelper.TokenRefreshProvider;
                if (provider == null || provider.Name != State.RefreshTokenProvider)
                    throw new InvalidOperationException("The saved login requires its configured refresh provider.");
                result = await provider.RefreshAsync(State.RefreshToken, _disposeCancellationTokenSource.Token);
            }
            else
            {
                var client = await _authHelper.GetClientAsync();
                _disposeCancellationTokenSource.Token.ThrowIfCancellationRequested();
                if (client == null)
                {
                    Debug.LogError("Failed to create client.");
                    return false;
                }
                result = await client.RefreshAsync(State.RefreshToken, _disposeCancellationTokenSource.Token);
            }
            _disposeCancellationTokenSource.Token.ThrowIfCancellationRequested();

            if (result.IsRight)
            {
                if (string.Equals(result.Right.Code, "invalid_grant", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log("Saved authentication session has expired. Sign in again to continue.");
                    _authHelper.DeleteCache();
                    _authHelper.SetState(new SignedOut(_authHelper));
                }
                else
                {
                    Debug.LogError($"Failed to refresh token: {result.Right.Message}");
                }

                return false;
            }

            Debug.Log("Token refreshed successfully.");

            var tokens = result.Left;
            
            State = new AuthState()
            {
                AccessToken = tokens.AccessToken,
                // Some providers omit a replacement refresh token when token
                // rotation is disabled. Keep the existing one in that case.
                RefreshToken = string.IsNullOrWhiteSpace(tokens.RefreshToken)
                    ? State.RefreshToken
                    : tokens.RefreshToken,
                IdentityToken = tokens.IdToken,
                RefreshTokenProvider = State.RefreshTokenProvider,
                AccessTokenExpiration = tokens.ExpiresAt.Value
            };

            _authHelper.SaveToCache(State);
            return true;
        }

        public void Dispose()
        {
            // Cancel pending requests when leaving this state. Keep the source available
            // so asynchronous continuations can still observe cancellation.
            _disposeCancellationTokenSource.Cancel();
        }
    }
}
