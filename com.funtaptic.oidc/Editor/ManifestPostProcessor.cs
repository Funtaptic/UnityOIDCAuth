using System.IO;
using System.Xml;
using Funtaptic.OIDC;
using Funtaptic.OIDC.Editor;
using UnityEditor.Android;
using UnityEngine;

public class AndroidManifestPostProcessor : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 0;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        var manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
        var document = new XmlDocument();
        document.Load(manifestPath);
        AndroidAuthenticationManifest.Apply(document, OidcSettings.Instance.AndroidScheme);
        document.Save(manifestPath);
        Debug.Log("Successfully configured AuthRedirectActivity in AndroidManifest.");
    }
}
