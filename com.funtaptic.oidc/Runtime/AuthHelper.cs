using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient;
using Funtaptic.OIDC.Android;
using Funtaptic.OIDC.IOS;
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
        
        private DiscoveryPolicy _discoveryPolicy;

        private string CacheFilePath => $"{Application.persistentDataPath}/{_cacheFileName}.json";

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
            File.Delete(CacheFilePath);
        }

        public bool TryLoadFromCache(out AuthState cache)
        {
            if (File.Exists(CacheFilePath))
            {
                var cacheContent = File.ReadAllText(CacheFilePath);
                cache = JsonSerializer.Deserialize<AuthState>(cacheContent);
                return true;
            }

            cache = null;
            return false;
        }

        public void SaveToCache(AuthState cache)
        {
            File.WriteAllText(CacheFilePath, JsonSerializer.Serialize(cache));
        }

        private DiscoveryCache _discoveryCache;
        
        private OidcClient _client;

        private void Awake()
        {
            _discoveryPolicy = new DiscoveryPolicy()
            {
                RequireHttps = false
            };

            _discoveryCache = new DiscoveryCache(_authUrl, _discoveryPolicy);

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

        public async Task<OidcClient> GetClientAsync()
        {
            if(_client != null)
                return _client;
            
            ProviderInformation providerInformation;
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                providerInformation = await new WebGLProviderInformationLoader(_authUrl).LoadAsync();
            }
            else
            {
                var discoveryDocument = await _discoveryCache.GetAsync();
                if (discoveryDocument.IsError)
                    throw new InvalidOperationException(discoveryDocument.Error);

                providerInformation = new ProviderInformation
                {
                    IssuerName = discoveryDocument.Issuer,
                    AuthorizeEndpoint = discoveryDocument.AuthorizeEndpoint,
                    TokenEndpoint = discoveryDocument.TokenEndpoint,
                    EndSessionEndpoint = discoveryDocument.EndSessionEndpoint,
                    UserInfoEndpoint = discoveryDocument.UserInfoEndpoint,
                    KeySet = discoveryDocument.KeySet
                };
            }

            var options = new OidcClientOptions
            {
                Authority = _authUrl,
                ClientId = _clientId,
                Scope = _scopes,
                ProviderInformation = providerInformation,
                LoadProfile = false,
                Policy = new Policy
                {
                    Discovery = _discoveryPolicy
                }
            };

            SetupPlatform(options);

            options.LoggerFactory.AddProvider(UnityAuthLoggerProvider.Instance);
            _client = new OidcClient(options);
            return _client;
        }

        private static void SetupPlatform(OidcClientOptions clientOptions)
        {
            switch (Application.platform)
            {
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
                    var scheme = OidcSettings.Instance.AndroidScheme;
                    clientOptions.RedirectUri = $"{scheme}://login_callback";
                    clientOptions.PostLogoutRedirectUri = $"{scheme}://logout_callback";
                    clientOptions.Browser = new AndroidChromeTabsBrowser(scheme);
                    break;
                }
                case RuntimePlatform.IPhonePlayer:
                {
                    var scheme = OidcSettings.Instance.IOSScheme;
                    clientOptions.RedirectUri = $"{scheme}://login_callback";
                    clientOptions.PostLogoutRedirectUri = $"{scheme}://logout_callback";
                    clientOptions.Browser = new IOSAuthenticationSessionBrowser(scheme);
                    break;
                }
                case RuntimePlatform.WebGLPlayer:
                {
                    var baseUrl = WebGLBrowser.GetCurrentPageUrl();
                    clientOptions.RedirectUri = $"{baseUrl}oidc-callback.html?oidc_callback=login";
                    clientOptions.PostLogoutRedirectUri = $"{baseUrl}oidc-callback.html?oidc_callback=logout";
                    clientOptions.Browser = new WebGLBrowser();
                    break;
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
