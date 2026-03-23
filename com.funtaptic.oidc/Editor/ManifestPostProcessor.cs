using System.IO;
using System.Xml;
using Funtaptic.OIDC.Android;
using UnityEditor.Android;
using UnityEngine;

public class AndroidManifestPostProcessor : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 0;
    
    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
        
        XmlDocument doc = new XmlDocument();
        doc.Load(manifestPath);
        
        XmlNode applicationNode = doc.SelectSingleNode("/manifest/application");
        
        XmlElement activityElement = doc.CreateElement("activity");
        activityElement.SetAttribute("name", "http://schemas.android.com/apk/res/android",
            AndroidChromeTabsBrowser.ActivityClassName);
        activityElement.SetAttribute("exported", "http://schemas.android.com/apk/res/android", "true");
        activityElement.SetAttribute("label", "http://schemas.android.com/apk/res/android", "@string/app_name");

        // Create Intent Filter
        XmlElement intentFilter = doc.CreateElement("intent-filter");

        // Action
        XmlElement action = doc.CreateElement("action");
        action.SetAttribute("name", "http://schemas.android.com/apk/res/android", "android.intent.action.VIEW");
        intentFilter.AppendChild(action);

        // Categories
        string[] categories = { "android.intent.category.DEFAULT", "android.intent.category.BROWSABLE" };
        foreach (var cat in categories)
        {
            XmlElement category = doc.CreateElement("category");
            category.SetAttribute("name", "http://schemas.android.com/apk/res/android", cat);
            intentFilter.AppendChild(category);
        }

        // Data
        XmlElement data = doc.CreateElement("data");
        data.SetAttribute("scheme", "http://schemas.android.com/apk/res/android", AndroidChromeTabsBrowser.Scheme);
        intentFilter.AppendChild(data);

        activityElement.AppendChild(intentFilter);
        applicationNode.AppendChild(activityElement);

        doc.Save(manifestPath);
        Debug.Log("Successfully added AuthRedirectActivity to AndroidManifest.");
    }
}