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
            {
                Debug.LogWarning("[OIDC] Login request ignored because another login is already in progress.");
                return false;
            }

            Debug.Log("[OIDC] Login flow started.");

            var authTask = AuthenticateAsyncInternal(cancellationToken);
            _cTask = authTask;
            var succeeded = await authTask;
            Debug.Log($"[OIDC] Login flow finished. Success: {succeeded}.");
            return succeeded;
        }

        private async Task<bool> AuthenticateAsyncInternal(CancellationToken cancellationToken = default)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                    _disposeCancellationTokenSource.Token);

                var client = await _authHelper.GetClientAsync();

                if (client == null)
                {
                    Debug.LogError("[OIDC] Login stopped because the OIDC client could not be created.");
                    return false;
                }

                Debug.Log("[OIDC] OIDC client created. Opening the authentication browser.");

                var loginTask = client.LoginAsync(new LoginRequest()
                {
                    BrowserTimeout = 300,
                }, cts.Token);
                Debug.Log($"[OIDC] LoginAsync task created. Completed: {loginTask.IsCompleted}; Faulted: {loginTask.IsFaulted}.");
                var result = await loginTask;

                Debug.Log($"[OIDC] OIDC LoginAsync completed. IsError: {result.IsError}; Error: {result.Error ?? "none"}.");

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

                Debug.Log("[OIDC] Tokens were received and the signed-in state was applied.");
                return true;
            }
            catch (OperationCanceledException exception)
            {
                Debug.LogWarning($"[OIDC] Login flow was cancelled or timed out: {exception.Message}");
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
