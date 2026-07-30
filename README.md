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

1. Add the following entries to the `dependencies` object in your Unity project's
   `Packages/manifest.json`:

   ```json
   "com.github-glitchenzo.nugetforunity": "https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity",
   "com.google.external-dependency-manager": "https://github.com/googlesamples/unity-jar-resolver.git?path=upm"
   ```

2. Let Unity import both packages.

3. In Unity, open the NuGet package manager and install
   `Duende.IdentityModel.OidcClient`.

4. Add the Unity OIDC Authentication package to the project.

Installing the dependencies first prevents missing assembly references during
package import and ensures the required Android libraries can be resolved.
