using System;

namespace Funtaptic.OIDC
{
    public class AuthState
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }

        public string IdentityToken { get; set; }
        
        public DateTimeOffset AccessTokenExpiration { get; set; }
    }
}