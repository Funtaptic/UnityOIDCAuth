using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

                Dictionary<string, string> frontChannelExtraParameters = null;
                if (register)
                {
                    frontChannelExtraParameters = new Dictionary<string, string>
                    {
                        { "prompt", "create" }
                    };
                }

                var result = await client.LoginAsync(TimeSpan.FromSeconds(300), frontChannelExtraParameters, cts.Token);

                if (result.IsRight)
                {
                    Debug.LogError($"Failed to login: {result.Right}");
                    return false;
                }

                var tokens = result.Left;

                var state = new AuthState()
                {
                    AccessTokenExpiration = tokens.ExpiresAt.Value,
                    AccessToken = tokens.AccessToken,
                    IdentityToken = tokens.IdToken,
                    RefreshToken = tokens.RefreshToken
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