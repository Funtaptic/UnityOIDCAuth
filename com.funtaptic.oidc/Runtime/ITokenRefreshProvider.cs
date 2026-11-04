using System.Threading;
using UnityEngine;

namespace Funtaptic.OIDC
{
    public interface ITokenRefreshProvider
    {
        string Name { get; }
        Awaitable<Either<OAuthTokens, Error>> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    }
}
