using System;
using System.Threading;
using System.Threading.Tasks;
using Duende.IdentityModel.OidcClient;
using UnityEngine;

namespace Funtaptic.OIDC
{
    public class SignedOut : IAuthState
    {
        public bool IsDoingWork => _cTask is { IsCompleted: false };

        public void Update()
        {
        }

        private AuthHelper _authHelper;

        private Task _cTask;

        private CancellationTokenSource _disposeCancellationTokenSource = new CancellationTokenSource();

        public SignedOut(AuthHelper coreBehaviour)
        {
            _authHelper = coreBehaviour;
        }

        public Task<bool> AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            return StartAuthenticationAsync(false, cancellationToken);
        }

        /// <summary>
        /// Opens the provider's registration page and signs in after registration.
        /// Requires a provider that supports prompt=create and allows user registration.
        /// </summary>
        public Task<bool> RegisterAsync(CancellationToken cancellationToken = default)
        {
            return StartAuthenticationAsync(true, cancellationToken);
        }

        private async Task<bool> StartAuthenticationAsync(bool register, CancellationToken cancellationToken)
        {
            if (IsDoingWork)
                return false;

            var authTask = AuthenticateAsyncInternal(register, cancellationToken);
            _cTask = authTask;
            return await authTask;
        }

        private async Task<bool> AuthenticateAsyncInternal(bool register, CancellationToken cancellationToken)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                    _disposeCancellationTokenSource.Token);

                var client = await _authHelper.GetClientAsync();

                if (client == null)
                    return false;

                var request = new LoginRequest
                {
                    BrowserTimeout = 300
                };

                if (register)
                    request.FrontChannelExtraParameters.Add("prompt", "create");

                var result = await client.LoginAsync(request, cts.Token);

                if (result.IsError)
                {
                    Debug.LogError($"Failed to login: {result.Error}");
                    return false;
                }

                var state = new AuthState()
                {
                    AccessTokenExpiration = result.AccessTokenExpiration,
                    AccessToken = result.AccessToken,
                    IdentityToken = result.IdentityToken,
                    RefreshToken = result.RefreshToken
                };

                _authHelper.SaveToCache(state);

                _authHelper.SetState(new SignedIn(_authHelper,
                    state));

                return true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            return false;
        }

        public void Dispose()
        {
            _disposeCancellationTokenSource.Cancel();
        }
    }
}
