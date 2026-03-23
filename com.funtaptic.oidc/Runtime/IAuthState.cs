using System;

namespace Funtaptic.OIDC
{
    public interface IAuthState : IDisposable
    {
        bool IsDoingWork { get; }

        void Update();
    }
}