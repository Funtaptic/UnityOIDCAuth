using UnityEngine;

namespace Funtaptic.OIDC
{
    [CreateAssetMenu(
        fileName = "FuntapticOidcSettings",
        menuName = "Funtaptic/OIDC Settings")]
    public sealed class OidcSettings : ScriptableObject
    {
        public const string DefaultAndroidScheme = "funtaptic.oidc";
        public const string DefaultIOSScheme = "funtaptic.oidc";
        public const string ResourcePath = "FuntapticOidcSettings";

        private static OidcSettings _defaultInstance;

        public string AndroidScheme = DefaultAndroidScheme;
        public string IOSScheme = DefaultIOSScheme;

        public static OidcSettings Instance
        {
            get
            {
                var settings = Resources.Load<OidcSettings>(ResourcePath);
                if (settings != null)
                    return settings;

                if (_defaultInstance == null)
                {
                    _defaultInstance = CreateInstance<OidcSettings>();
                    _defaultInstance.hideFlags = HideFlags.HideAndDontSave;
                }

                return _defaultInstance;
            }
        }
    }
}
