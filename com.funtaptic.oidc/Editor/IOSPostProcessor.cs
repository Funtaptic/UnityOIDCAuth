#if UNITY_IOS
using System.IO;
using Funtaptic.OIDC;
using Funtaptic.OIDC.IOS;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

public static class IOSPostProcessor
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
            return;

        var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        var root = plist.root;
        var urlTypes = root.values.TryGetValue("CFBundleURLTypes", out var existingUrlTypes)
            ? existingUrlTypes.AsArray()
            : root.CreateArray("CFBundleURLTypes");
        var scheme = OidcSettings.Instance.IOSScheme;

        if (!ContainsScheme(urlTypes, scheme))
        {
            var urlType = urlTypes.AddDict();
            urlType.SetString("CFBundleURLName", scheme);

            var schemes = urlType.CreateArray("CFBundleURLSchemes");
            schemes.AddString(scheme);
        }

        plist.WriteToFile(plistPath);
        Debug.Log($"Successfully added {scheme} URL scheme to Info.plist.");
    }

    private static bool ContainsScheme(PlistElementArray urlTypes, string scheme)
    {
        foreach (var urlType in urlTypes.values)
        {
            var urlTypeDict = urlType as PlistElementDict;
            if (urlTypeDict == null ||
                !urlTypeDict.values.TryGetValue("CFBundleURLSchemes", out var schemesElement))
            {
                continue;
            }

            foreach (var schemeElement in schemesElement.AsArray().values)
            {
                if (schemeElement.AsString() == scheme)
                    return true;
            }
        }

        return false;
    }
}
#endif
