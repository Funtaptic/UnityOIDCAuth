Requires Unity 6.0 (6000.0) or newer.

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

## Create the OIDC settings asset

The package loads its mobile callback schemes from a `FuntapticOidcSettings`
asset. Create it before building the application:

1. In the Project window, create an `Assets/Resources` folder if the project does
   not already have one.
2. Right-click inside that folder and select
   **Create > Funtaptic > OIDC Settings**.
3. Keep the asset filename exactly `FuntapticOidcSettings.asset`. The package
   loads it by this name from a `Resources` folder.
4. Select the asset and configure **Android Scheme** and **iOS Scheme** in the
   Inspector.

Use a scheme unique to the application, such as a reverse-domain identifier:

```text
com.example.mygame
```

For that value, register these callback URLs for the OIDC client at the identity
provider:

```text
com.example.mygame://login_callback
com.example.mygame://logout_callback
```

The Android and iOS schemes can be different if the identity provider uses
separate clients for each platform. During a build, the package adds the Android
scheme to the generated manifest and the iOS scheme to `Info.plist`. A build will
fail with a clear validation error if the asset is missing or the scheme for the
selected mobile platform is empty.

## Android incremental builds (0.2.3)

The Android build processor reuses the existing authentication activity instead
of adding another copy on each build. It merges duplicate activity declarations
left by earlier versions, preserving their routes, metadata and nonconflicting
attributes. Conflicting activity attributes still produce an explicit error.
Login/logout routes already narrowed by PixiPlay remain unchanged on later builds.

Update the `com.funtaptic.oidc` Git package in Unity Package Manager to obtain this
fix. Updating PixiPlay alone does not update a locked OIDC Git dependency. No
scene or settings changes, or routine deletion of `Library/Bee`, are required.

Package regression tests can be enabled by adding `com.funtaptic.oidc` to the
project manifest's `testables` list and running `AndroidAuthenticationManifestTests`
in the Unity EditMode Test Runner.

## Usage example

Add `AuthHelper` to a GameObject, then configure its identity-provider URL, client
ID, and scopes in the Inspector. The following component can be connected to
Unity UI buttons for sign-in, profile loading, and sign-out:

```csharp
using Funtaptic.OIDC;
using UnityEngine;

public sealed class LoginController : MonoBehaviour
{
    [SerializeField] private AuthHelper auth;

    private void OnEnable()
    {
        auth.StateChanged += HandleStateChanged;
        HandleStateChanged(auth.State);
    }

    private void OnDisable()
    {
        auth.StateChanged -= HandleStateChanged;
    }

    public async void SignIn()
    {
        if (auth.State is SignedOut signedOut)
        {
            bool succeeded = await signedOut.AuthenticateAsync();
            Debug.Log(succeeded ? "Signed in." : "Sign-in failed or was cancelled.");
        }
    }

    public async void LoadProfile()
    {
        if (auth.State is not SignedIn signedIn)
            return;

        var userInfo = await signedIn.GetUserInfoAsync();

        foreach (var claim in userInfo.Claims)
            Debug.Log($"{claim.Type}: {claim.Value}");
    }

    public async void SignOut()
    {
        if (auth.State is SignedIn signedIn)
            await signedIn.LogOut();
    }

    private static void HandleStateChanged(IAuthState state)
    {
        switch (state)
        {
            case SignedIn:
                Debug.Log("Authentication state: signed in");
                break;
            case SignedOut:
                Debug.Log("Authentication state: signed out");
                break;
        }
    }
}
```

The identity provider must allow the client ID, scopes, and callback URLs used by
the application. On Android and iOS, the callback scheme comes from the
`FuntapticOidcSettings` asset.

## Dependencies

Install the NuGet dependencies before OIDC, then add EDM for Android as shown below.

### NuGetForUnity

[NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) installs and manages the
.NET libraries used by the package. It is required because Unity's Package Manager
does not install NuGet packages.

### External Dependency Manager for Unity

[External Dependency Manager for Unity](https://github.com/googlesamples/unity-jar-resolver)
1.2.189 or newer is required for Android. It reads `AuthDependencies.xml` and adds
AndroidX Browser 1.8.0 for Chrome Custom Tabs login. OIDC only ensures the
`Assets/Plugins/Android` folder exists; EDM manages the Gradle templates and dependencies.

Select Android as the build platform. Accept EDM's **Enable** prompts for
Android auto-resolution and Gradle templates, then run
**Assets > External Dependency Manager > Android Resolver > Force Resolve**
before the first Android build. Wait for **Resolution Succeeded**.

If an existing project disabled template integration, enable **Custom Main Gradle
Template**, **Custom Gradle Properties Template** and **Custom Gradle Settings
Template** in Android Player Settings > Publishing Settings. In Android Resolver >
Settings, enable **Patch mainTemplate.gradle**, **Use Jetifier**,
**Patch gradleTemplate.properties** and **Copy and patch settingsTemplate.gradle
from 2022.2** before resolving again. Keep your existing templates and customizations.

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

3. Open **NuGet > Manage NuGet Packages** and install
   `Duende.IdentityModel.OidcClient` version `7.1.0`.

4. Return to Package Manager, select **+ > Install package from git URL**, and enter:

   ```text
   https://github.com/Funtaptic/UnityOIDCAuth.git?path=/com.funtaptic.oidc
   ```

5. Let Unity finish importing and compiling OIDC.

6. **Android only:** install External Dependency Manager through Package Manager:

   ```text
   https://github.com/googlesamples/unity-jar-resolver.git?path=upm
   ```

   Follow the Android resolution steps in **External Dependency Manager for Unity** above.
