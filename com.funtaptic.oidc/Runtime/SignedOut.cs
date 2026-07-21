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

        public async Task<bool> AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            if (IsDoingWork)
                return false;

            var authTask = AuthenticateAsyncInternal(cancellationToken);
            _cTask = authTask;
            return await authTask;
        }

        private async Task<bool> AuthenticateAsyncInternal(CancellationToken cancellationToken = default)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                    _disposeCancellationTokenSource.Token);

                var discoveryDocument = await _authHelper.GetDiscoveryDocumentAsync();
                if (discoveryDocument.IsError)
                {
                    Debug.LogError(discoveryDocument.Error);
                    return false;
                }

                var client = _authHelper.GetClient(discoveryDocument);

                if (client == null)
                    return false;

                var result = await client.LoginAsync(new LoginRequest()
                {
                    BrowserTimeout = 300,
                }, cts.Token);

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