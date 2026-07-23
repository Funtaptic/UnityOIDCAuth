using Funtaptic.OIDC;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Funtaptic.OIDC.Editor
{
    public sealed class OidcBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var settings = Resources.Load<OidcSettings>(OidcSettings.ResourcePath);
            if (settings == null)
            {
                throw new BuildFailedException(
                    "OIDC settings asset is missing. Create it from " +
                    "'Assets > Create > Funtaptic > OIDC Settings' and save it as " +
                    "'FuntapticOidcSettings.asset' inside a Resources folder.");
            }

            if (report.summary.platform == BuildTarget.Android &&
                string.IsNullOrWhiteSpace(settings.AndroidScheme))
            {
                throw new BuildFailedException(
                    "OIDC settings AndroidScheme must not be empty.");
            }

            if (report.summary.platform == BuildTarget.iOS &&
                string.IsNullOrWhiteSpace(settings.IOSScheme))
            {
                throw new BuildFailedException(
                    "OIDC settings IOSScheme must not be empty.");
            }
        }
    }
}
