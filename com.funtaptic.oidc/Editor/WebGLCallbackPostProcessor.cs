using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Funtaptic.OIDC.Editor
{
    public sealed class WebGLCallbackPostProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
                return;
            // Hand off synchronously before closing, even if the game's frames are suspended.
            File.WriteAllText(Path.Combine(report.summary.outputPath, "oidc-callback.html"),
                "<!doctype html><html lang=\"en\"><meta charset=\"utf-8\">" +
                "<meta name=\"referrer\" content=\"no-referrer\">" +
                "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
                "<title>Authentication complete</title><body>" +
                "<p>Return to the game to finish authentication. This window will close automatically.</p>" +
                "<script>try { if (window.opener && window.opener.FuntapticOIDCWebComplete && " +
                "window.opener.FuntapticOIDCWebComplete(window)) window.close(); } catch (_) {}</script>" +
                "</body></html>");
        }
    }
}
