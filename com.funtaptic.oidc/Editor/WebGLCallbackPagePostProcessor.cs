using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Funtaptic.OIDC.Editor
{
    public sealed class WebGLCallbackPagePostProcessor : IPostprocessBuildWithReport
    {
        private const string CallbackPage = @"<!doctype html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <title>Signing in...</title>
</head>
<body>
    <p>Completing sign-in...</p>
    <script>
        (function () {
            var parameters = new URLSearchParams(window.location.search);
            if (!parameters.has('oidc_callback') || !window.opener) {
                return;
            }

            window.opener.postMessage(
                { type: 'funtaptic-oidc-callback', url: window.location.href },
                window.location.origin
            );
            window.close();
        }());
    </script>
</body>
</html>
";

        public int callbackOrder => 1000;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
                return;

            var callbackPagePath = Path.Combine(report.summary.outputPath, "oidc-callback.html");
            File.WriteAllText(callbackPagePath, CallbackPage);
        }
    }
}
