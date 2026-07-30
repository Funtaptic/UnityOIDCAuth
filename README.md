## What `com.funtaptic.oidc` does

`com.funtaptic.oidc` connects a Unity application to an existing OpenID Connect
(OIDC) identity provider. It handles the client-side authentication flow; it does
not provide or host the identity service itself.

The package provides:

- An `AuthHelper` component for configuring the identity-provider URL, client ID,
  requested scopes, and token-cache filename.
- Sign-in and sign-out flows using the system browser appropriate for each
  platform: a loopback browser callback on Windows and macOS, Chrome Custom Tabs
  on Android, and `ASWebAuthenticationSession` on iOS.
- `SignedIn` and `SignedOut` states, exposed through `AuthHelper.State` and the
  `StateChanged` event, so game code can react to authentication changes.
- Local persistence of access, identity, and refresh tokens in
  `Application.persistentDataPath`, allowing a session to be restored when the
  application starts.
- Automatic access-token refresh. If refreshing fails, the cached session is
  removed and the user returns to the signed-out state.
- User-info requests and identity-provider logout for authenticated users.
- Android and iOS build processing that registers the custom callback URL scheme
  required to return from the browser to the application.

## Dependencies

Install the following dependencies before adding the OIDC package to your project.

### NuGetForUnity

[NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) installs and manages the
.NET libraries used by the package. It is required because Unity's Package Manager
does not install NuGet packages.

### External Dependency Manager for Unity

[External Dependency Manager for Unity](https://github.com/googlesamples/unity-jar-resolver)
resolves the Android library declared in `AuthDependencies.xml`. In particular, it
adds AndroidX Browser to Android builds so the sign-in page can open in a Chrome
Custom Tab.

### Duende.IdentityModel.OidcClient

[`Duende.IdentityModel.OidcClient`](https://www.nuget.org/packages/Duende.IdentityModel.OidcClient)
provides the core OIDC protocol implementation used for sign-in, sign-out, token
handling, and browser callbacks. The Unity package supplies the platform-specific
browser integrations around this client.

## Installation

1. Open **Window > Package Management > Package Manager** in Unity.

2. Select **+ > Install package from git URL**, enter the NuGetForUnity URL, and
   select **Install**:

   ```text
   https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity
   ```

3. Repeat the same Package Manager flow for External Dependency Manager:

   ```text
   https://github.com/googlesamples/unity-jar-resolver.git?path=upm
   ```

4. Let Unity finish importing both packages.

5. In Unity, open the NuGet package manager and install
   `Duende.IdentityModel.OidcClient`.

6. Return to the Unity Package Manager, select
   **+ > Install package from git URL**, and enter:

   ```text
   https://github.com/Funtaptic/UnityOIDCAuth.git?path=/com.funtaptic.oidc
   ```
