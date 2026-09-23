using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Funtaptic.OIDC.Android;

namespace Funtaptic.OIDC.Editor
{
    public static class AndroidAuthenticationManifest
    {
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

        public static void Apply(XmlDocument document, string scheme)
        {
            var application = (XmlElement)document.SelectSingleNode("/manifest/application");
            var activities = application.SelectNodes("activity").Cast<XmlElement>()
                .Where(activity => activity.GetAttribute("name", AndroidNamespace) == AndroidChromeTabsBrowser.ActivityClassName)
                .ToArray();
            var authentication = activities.Length == 0 ? document.CreateElement("activity") : activities[0];
            foreach (var duplicate in activities.Skip(1))
                MergeActivity(authentication, duplicate);

            if (activities.Length == 0)
            {
                authentication.SetAttribute("name", AndroidNamespace, AndroidChromeTabsBrowser.ActivityClassName);
                application.AppendChild(authentication);
            }
            authentication.SetAttribute("exported", AndroidNamespace, "true");
            if (!authentication.HasAttribute("label", AndroidNamespace))
                authentication.SetAttribute("label", AndroidNamespace, "@string/app_name");

            if (HasCallback(authentication, scheme, "login_callback") && HasCallback(authentication, scheme, "logout_callback"))
                return;

            var filter = document.CreateElement("intent-filter");
            AddElement(filter, "action", "name", "android.intent.action.VIEW");
            AddElement(filter, "category", "name", "android.intent.category.DEFAULT");
            AddElement(filter, "category", "name", "android.intent.category.BROWSABLE");
            AddElement(filter, "data", "scheme", scheme);
            authentication.AppendChild(filter);
        }

        private static void MergeActivity(XmlElement authentication, XmlElement duplicate)
        {
            foreach (XmlAttribute attribute in duplicate.Attributes)
            {
                if (attribute.NamespaceURI == "http://www.w3.org/2000/xmlns/")
                    continue;
                if (authentication.HasAttribute(attribute.LocalName, attribute.NamespaceURI))
                {
                    if (authentication.GetAttribute(attribute.LocalName, attribute.NamespaceURI) != attribute.Value)
                        throw new InvalidOperationException($"Conflicting '{attribute.Name}' values on duplicate OIDC redirect activities.");
                    continue;
                }
                authentication.SetAttribute(attribute.LocalName, attribute.NamespaceURI, attribute.Value);
            }

            foreach (var child in duplicate.ChildNodes.OfType<XmlElement>())
            {
                if (!authentication.ChildNodes.OfType<XmlElement>().Any(existing =>
                    XNode.DeepEquals(XElement.Parse(existing.OuterXml), XElement.Parse(child.OuterXml))))
                    authentication.AppendChild(child.CloneNode(true));
            }
            duplicate.ParentNode.RemoveChild(duplicate);
        }

        private static bool HasCallback(XmlElement authentication, string scheme, string host)
        {
            return authentication.SelectNodes("intent-filter").Cast<XmlElement>().Any(filter =>
                HasNamedElement(filter, "action", "android.intent.action.VIEW") &&
                HasNamedElement(filter, "category", "android.intent.category.DEFAULT") &&
                HasNamedElement(filter, "category", "android.intent.category.BROWSABLE") &&
                filter.SelectNodes("data").Count == 1 &&
                filter.SelectNodes("data").Cast<XmlElement>().Any(data =>
                    data.GetAttribute("scheme", AndroidNamespace) == scheme &&
                    (!data.HasAttribute("host", AndroidNamespace) || data.GetAttribute("host", AndroidNamespace) == host) &&
                    data.Attributes.Cast<XmlAttribute>().Where(attribute => attribute.NamespaceURI == AndroidNamespace)
                        .All(attribute => attribute.LocalName == "scheme" || attribute.LocalName == "host")));
        }

        private static bool HasNamedElement(XmlElement parent, string elementName, string name) =>
            parent.SelectNodes(elementName).Cast<XmlElement>().Any(element => element.GetAttribute("name", AndroidNamespace) == name);

        private static void AddElement(XmlElement parent, string elementName, string attribute, string value)
        {
            var element = parent.OwnerDocument.CreateElement(elementName);
            element.SetAttribute(attribute, AndroidNamespace, value);
            parent.AppendChild(element);
        }
    }
}
