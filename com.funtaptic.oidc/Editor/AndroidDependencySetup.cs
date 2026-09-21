#if UNITY_ANDROID
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Funtaptic.OIDC.Editor
{
    internal static class AndroidDependencySetup
    {
        [InitializeOnLoadMethod]
        private static void EnsurePluginDirectory()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Plugins", "Android"));
        }
    }
}
#endif
