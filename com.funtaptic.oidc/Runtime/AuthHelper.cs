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
using UnityEngine;

namespace Funtaptic.OIDC
{
    public class AuthHelper : MonoBehaviour
    {
        [SerializeField]
        private string _authUrl;

        [SerializeField]
        public string _clientId;

        [SerializeField]
        private string _cacheFileName = "token_cache.json";

        private DiscoveryPolicy _discoveryPolicy;

        private string CacheFilePath => $"{Application.persistentDataPath}/{_cacheFileName}.json";

        public IAuthState State { get; private set; }

        public event Action<IAuthState> StateChanged;
        
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

        public Task<DiscoveryDocumentResponse> GetDiscoveryDocumentAsync()
        {
            return _discoveryCache.GetAsync();
        }

        public OidcClient GetClientAsync(DiscoveryDocumentResponse discoveryDocument)
        {
            var options = new OidcClientOptions
            {
                Authority = _authUrl,
                ClientId = _clientId,
                Scope = "openid profile roles",
                ProviderInformation = new ProviderInformation
                {
                    IssuerName = discoveryDocument.Issuer,
                    AuthorizeEndpoint = discoveryDocument.AuthorizeEndpoint,
                    TokenEndpoint = discoveryDocument.TokenEndpoint,
                    EndSessionEndpoint = discoveryDocument.EndSessionEndpoint,
                    UserInfoEndpoint = discoveryDocument.UserInfoEndpoint,
                    KeySet = discoveryDocument.KeySet
                },
                LoadProfile = false,
                Policy = new Policy
                {
                    Discovery = _discoveryPolicy
                }
            };

            SetupPlatform(options);

            options.LoggerFactory.AddProvider(UnityAuthLoggerProvider.Instance);
            return new OidcClient(options);
        }

        private static void SetupPlatform(OidcClientOptions clientOptions)
        {
            switch (Application.platform)
            {
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
                    clientOptions.RedirectUri = $"{AndroidChromeTabsBrowser.Scheme}://login_callback";
                    clientOptions.PostLogoutRedirectUri = $"{AndroidChromeTabsBrowser.Scheme}://logout_callback";
                    clientOptions.Browser = new AndroidChromeTabsBrowser();
                    break;
                }
                case RuntimePlatform.IPhonePlayer:
                {
                    clientOptions.RedirectUri = $"{IOSAuthenticationSessionBrowser.Scheme}://login_callback";
                    clientOptions.PostLogoutRedirectUri = $"{IOSAuthenticationSessionBrowser.Scheme}://logout_callback";
                    clientOptions.Browser = new IOSAuthenticationSessionBrowser();
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
