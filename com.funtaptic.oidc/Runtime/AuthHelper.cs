using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
//using Funtaptic.OIDC.Android;
// using Funtaptic.OIDC.IOS;
// using Funtaptic.OIDC.WebGL;
using Funtaptic.OIDC.Standalone.Funtaptic.OIDC.Auth;
using Funtaptic.OIDC.WebGL;
using UnityEngine;

namespace Funtaptic.OIDC
{
    public class AuthHelper : MonoBehaviour
    {
        [SerializeField] private string _authUrl;

        [SerializeField] public string _clientId;

        [SerializeField] private string _cacheFileName = "token_cache.json";

        [SerializeField] private string _scopes = "openid profile roles offline_access";

        private string CacheKey => $"Funtaptic.OIDC.{_cacheFileName}";

        public IAuthState State { get; private set; }

        public event Action<IAuthState> StateChanged;

        public string AuthUrl
        {
            get => _authUrl;
            set => _authUrl = value;
        }

        public string ClientId
        {
            get => _clientId;
            set => _clientId = value;
        }

        // Retained for API and serialized-scene compatibility; this now names a PlayerPrefs entry.
        public string CacheFileName
        {
            get => _cacheFileName;
            set => _cacheFileName = value;
        }

        public string Scopes
        {
            get => _scopes;
            set => _scopes = value;
        }

        public void DeleteCache()
        {
            PlayerPrefs.DeleteKey(CacheKey);
            PlayerPrefs.Save();
        }

        public bool TryLoadFromCache(out AuthState cache)
        {
            if (PlayerPrefs.HasKey(CacheKey))
            {
                var cacheContent = PlayerPrefs.GetString(CacheKey);
                cache = JsonSerializer.Deserialize<AuthState>(cacheContent);
                return true;
            }

            cache = null;
            return false;
        }

        public void SaveToCache(AuthState cache)
        {
            PlayerPrefs.SetString(CacheKey, JsonSerializer.Serialize(cache));
            PlayerPrefs.Save();
        }

        private OAuthClient _client;

        private void Awake()
        {
            if (TryLoadFromCache(out var cache))
            {
                SetState(new SignedIn(this, cache));
            }
            else
            {
                SetState(new SignedOut(this));
            }
        }

        private void OnDestroy()
        {
            SetState(null);
        }

        private void Update()
        {
            State?.Update();
        }

        private static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public async Task<OAuthClient> GetClientAsync()
        {
            if (_client != null)
                return _client;

            var discoveryDocument = await DiscoveryService.DiscoverAsync(_authUrl, destroyCancellationToken);

            if (discoveryDocument.IsRight)
                return null;
            
            var clientSettings = new ClientSettings()
            {
                ClientId = _clientId,
                Scope = _scopes,
            };
            SetupPlatform(clientSettings);

            _client = new OAuthClient(discoveryDocument.Left, clientSettings);
            return _client;
        }

        private static void SetupPlatform(ClientSettings clientOptions)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WebGLPlayer:
                {
                    var redirectUri = WebGLBrowser.ResolveRedirectUri(
                        Application.absoluteURL, OidcSettings.Instance.WebGLCallbackPath);
                    clientOptions.RedirectUri = redirectUri;
                    clientOptions.PostLogoutRedirectUri = redirectUri;
                    clientOptions.Browser = new WebGLBrowser();
                    break;
                }
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                {
                    var baseUri = $"http://localhost:{GetRandomUnusedPort()}/";
                    var editorRedirectUri = $"{baseUri}login-callback";
                    var editorLogOutRedirectUri = $"{baseUri}logOut-callback";
                    clientOptions.RedirectUri = editorRedirectUri;
                    clientOptions.PostLogoutRedirectUri = editorLogOutRedirectUri;

                    clientOptions.Browser = new StandaloneBrowser();
                    break;
                }
                case RuntimePlatform.Android:
                {
                    // var scheme = OidcSettings.Instance.AndroidScheme;
                    // clientOptions.RedirectUri = $"{scheme}://login_callback";
                    // clientOptions.PostLogoutRedirectUri = $"{scheme}://logout_callback";
                    // clientOptions.Browser = new AndroidChromeTabsBrowser(scheme);
                    // break;
                    throw new NotImplementedException();
                }
                case RuntimePlatform.IPhonePlayer:
                {
                    // var scheme = OidcSettings.Instance.IOSScheme;
                    // clientOptions.RedirectUri = $"{scheme}://login_callback";
                    // clientOptions.PostLogoutRedirectUri = $"{scheme}://logout_callback";
                    // clientOptions.Browser = new IOSAuthenticationSessionBrowser(scheme);
                    // break;
                    throw new NotImplementedException();
                }
                default:
                    throw new NotSupportedException($"Unsupported platform: {Application.platform}");
            }
        }

        public void SetState(IAuthState b)
        {
            if (State != null)
                State.Dispose();

            State = b;
            State?.Update();
            StateChanged?.Invoke(State);
        }
    }
}