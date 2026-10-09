using System;

namespace Funtaptic.OIDC
{
    /// <summary>Inputs for one browser authorization attempt.</summary>
    public sealed class BrowserOptions
    {
        public string StartUrl { get; }
        public string EndUrl { get; }

        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5.0);

        public BrowserOptions(string startUrl, string endUrl)
        {
            StartUrl = startUrl ?? throw new ArgumentNullException(nameof(startUrl));
            EndUrl = endUrl ?? throw new ArgumentNullException(nameof(endUrl));
        }
    }
}
