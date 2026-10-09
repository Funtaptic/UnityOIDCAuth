using System;
using System.Threading;
using UnityEngine;

namespace Funtaptic.OIDC
{
    /// <summary>Opens authorization in a browser and receives the provider's callback.</summary>
    public interface IBrowser
    {
        /// <summary>
        /// Call on Unity's main thread and await the result once. Right contains the original encoded
        /// callback URI, including OAuth error parameters; Left contains a browser error message.
        /// Honor options.Timeout and caller cancellation, returning an error and releasing browser/listener resources.
        /// The caller validates the callback before token exchange. Null options throw an argument exception.
        /// </summary>
        Awaitable<Either<Uri, Error>> InvokeAsync(BrowserOptions options,
            CancellationToken cancellationToken);
    }
}
