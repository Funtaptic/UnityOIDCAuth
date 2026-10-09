// using System;
// using System.Threading;
// using UnityEngine;
//
// namespace UnitySample.ManualOAuth
// {
//     /// <summary>Small interactive OAuth test scene. Tokens are only retained in memory.</summary>
//     public sealed class ManualOAuthDemo : MonoBehaviour
//     {
//         [SerializeField] private string _issuer = "https://dev.auth.pixiplay.eu/realms/pixiplay-users";
//         [SerializeField] private string _clientId = "unity-mother-app";
//         [SerializeField] private string _scopes = "profile roles offline_access";
//         [SerializeField] private string _redirectUri = "http://localhost:7890/login-callback";
//         [SerializeField, Min(30)] private int _loginTimeoutSeconds = 180;
//
//         public OAuthTokens Tokens { get; private set; }
//         public string Status { get; private set; } = "Ready to sign in.";
//         public bool IsBusy => _operation != null;
//
//         private CancellationTokenSource _operation;
//         
//         private OAuthClient _client;
//         
//         private bool _previousRunInBackground;
//         private Vector2 _scroll;
//
//         private async void Start()
//         {
//             try
//             {
//                 Status = "Pull discovery json.";
//                 var discovery = await DiscoveryService.DiscoverAsync(_issuer, destroyCancellationToken);
//
//                 Status = "Authenticating";
//
//                 _client = new OAuthClient(discovery, new ClientSettings()
//                 {
//                     ClientId = _clientId,
//                     Browser = new EditorBrowser(),
//                     RedirectUri = _redirectUri,
//                     Scope = _scopes
//                 });
//             }
//             catch (Exception e)
//             {
//                 Console.WriteLine(e);
//                 throw;
//             }
//         }
//
//         public Awaitable SignInAsync() => RunAsync(SignInAsync);
//
//
//         private async Awaitable SignInAsync(CancellationToken cancellationToken)
//         {
//             // Tokens = null;
//             // _client = null;
//             // var issuer = _issuer;
//             // var redirectUri = _redirectUri;
//             // var redirect = new Uri(redirectUri, UriKind.Absolute);
//             // var browser = Browser;
//
//             var result = await _client.LoginAsync(cancellationToken);
//             if (result.IsLeft)
//             {
//                 Tokens = result.Left;
//                 Status = "Access token acquired.";
//             }
//             else
//             {
//                 Tokens = null;
//                 Status = "Failed to acquire access token.";
//             }
//         }
//
//         public Awaitable RefreshAsync() => RunAsync(async cancellationToken =>
//         {
//             if (Tokens == null) 
//                 throw new InvalidOperationException("Sign in first.");
//             
//             Status = "Refreshing access token...";
//             var tokens = await _client.RefreshAsync(Tokens.RefreshToken, cancellationToken);
//             cancellationToken.ThrowIfCancellationRequested();
//             Tokens = tokens;
//             Status = "Access token refreshed.";
//         });
//
//         public Awaitable LogOutAsync() => RunAsync(async cancellationToken =>
//         {
//             if (Tokens == null) return;
//
//             // End the local session even if browser logout fails or is canceled.
//             Tokens = null;
//             Status = "Logging out in the browser...";
//             try
//             {
//                 await _client.LogoutAsync(cancellationToken);
//                 Status = "Logged out. You can sign in again.";
//             }
//             catch (OperationCanceledException)
//             {
//                 Status = "Local tokens cleared. Browser logout was canceled or timed out; the browser may still be signed in.";
//             }
//             catch (InvalidOperationException exception)
//             {
//                 Status = "Local tokens cleared. Browser logout was not confirmed: " + exception.Message;
//             }
//             catch (Exception)
//             {
//                 Status = "Local tokens cleared. Browser logout failed; the browser may still be signed in.";
//             }
//         });
//
//         private async Awaitable RunAsync(Func<CancellationToken, Awaitable> action)
//         {
//             if (IsBusy || !isActiveAndEnabled) return;
//             using var operation = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(30, _loginTimeoutSeconds)));
//             _operation = operation;
//             try
//             {
//                 await action(operation.Token);
//             }
//             catch (OperationCanceledException)
//             {
//                 Status = "Operation canceled or timed out.";
//             }
//             catch (InvalidOperationException exception)
//             {
//                 Status = exception.Message;
//             }
//             catch (Exception)
//             {
//                 Status = "OAuth operation failed. Check the provider and redirect configuration.";
//             }
//             finally
//             {
//                 _operation = null;
//             }
//         }
//
//         public void Cancel() => _operation?.Cancel();
//
//         public void ClearSession()
//         {
//             if (IsBusy) return;
//             Tokens = null;
//             Status = "Local tokens cleared. The browser session remains signed in.";
//         }
//
//         private void OnGUI()
//         {
//             GUILayout.BeginArea(new Rect(24, 24, Mathf.Max(240, Mathf.Min(680, Screen.width - 48)), Screen.height - 48),
//                 GUI.skin.box);
//             _scroll = GUILayout.BeginScrollView(_scroll);
//             GUILayout.Label("Manual OAuth - Authorization Code + PKCE");
//             GUILayout.Label("UnityWebRequest / System.Text.Json");
//             GUI.enabled = !IsBusy && Tokens == null;
//             GUILayout.Label("Issuer");
//             _issuer = GUILayout.TextField(_issuer);
//             GUILayout.Label("Client ID");
//             _clientId = GUILayout.TextField(_clientId);
//             GUILayout.Label("Scopes");
//             _scopes = GUILayout.TextField(_scopes);
//             GUILayout.Label("Registered redirect URI");
//             _redirectUri = GUILayout.TextField(_redirectUri);
//             GUI.enabled = true;
//             GUILayout.Space(12);
//             GUILayout.Label(Status);
//             if (IsBusy)
//             {
//                 if (GUILayout.Button("Cancel", GUILayout.Height(36))) Cancel();
//             }
//             else if (Tokens == null)
//             {
//                 if (GUILayout.Button("Sign in", GUILayout.Height(36))) _ = SignInAsync();
//             }
//             else
//             {
//                 GUILayout.Label(Tokens.ExpiresAt.HasValue
//                     ? $"Expires (UTC): {Tokens.ExpiresAt.Value:u}"
//                     : "Expiry not supplied by provider.");
//                 GUILayout.Label(
//                     $"Expired: {Tokens.ExpiresAt.HasValue && Tokens.ExpiresAt.Value <= DateTimeOffset.UtcNow}");
//                 GUILayout.Label("Tokens are kept in memory and are not displayed or logged.");
//                 GUI.enabled = !string.IsNullOrEmpty(Tokens.RefreshToken);
//                 if (GUILayout.Button("Refresh token", GUILayout.Height(36))) _ = RefreshAsync();
//                 GUI.enabled = true;
//                 if (GUILayout.Button("Log out", GUILayout.Height(36))) _ = LogOutAsync();
//                 if (GUILayout.Button("Clear local session", GUILayout.Height(36))) ClearSession();
//             }
//
//             GUILayout.EndScrollView();
//             GUILayout.EndArea();
//         }
//     }
// }
