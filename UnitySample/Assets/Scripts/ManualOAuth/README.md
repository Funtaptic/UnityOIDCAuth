# Manual OAuth PKCE sample

Open `Assets/Scenes/ManualOAuthPKCE.unity` and enter Play mode in the Unity Editor, or include this scene in a desktop build. The scene contains a camera and the `ManualOAuthDemo` component with a small debug interface.

## Provider setup

The defaults use the existing development provider and client (`unity-mother-app`). On that provider, configure a **public client** with authorization code / standard flow enabled and PKCE S256 supported. No client secret is sent or stored.

Register this exact redirect URI:

```text
http://localhost:7890/login-callback
```

The issuer, client ID, scopes and redirect URI can be changed in the Inspector or the sample interface before signing in. If port 7890 is occupied, choose another port and register the corresponding redirect URI. Provider/discovery/token URLs must use HTTPS. Only the local browser callback uses HTTP.

Press **Sign in**, complete authorization in the system browser, then return to Unity. **Refresh token** is enabled if the provider issued a refresh token; requesting `offline_access` is subject to provider permissions and consent. **Cancel** stops an outstanding request/listener. The entire operation times out after 180 seconds by default. `BrowserOptions.Timeout` independently limits the browser wait to five minutes by default; whichever deadline occurs first wins; each outgoing HTTP request has a 30-second timeout. Disabling the component or leaving Play mode cancels outstanding work and clears tokens.

## Code layout

- `PkceProtocol.cs`: random verifier/state, SHA-256 challenge, authorization URLs built with `HttpUtility.ParseQueryString` and `UriBuilder`, callback/state/issuer checks.
- `ManualOAuthClient.cs`: discovery GET, authorization-code POST and refresh POST, using `UnityWebRequest.Get` and its dictionary-based `Post` overload.
- `OAuthResponses.cs`: `System.Text.Json` response models with explicit OAuth property names. `link.xml` preserves these models for IL2CPP reflection serialization; the project already includes System.Text.Json through NuGetForUnity.
- `BrowserOptions.cs`: string `StartUrl`, `EndUrl` and `State` properties plus a configurable `TimeSpan Timeout` defaulting to five minutes. `EditorBrowser` parses and validates `EndUrl` as a loopback URI before opening the listener.
- `Either.cs`: immutable two-case result with implicit conversions from either value type and guarded value access. Use explicit `FromLeft` / `FromRight` factories when the type arguments are identical or conversions would be ambiguous.
- `IBrowser.cs`: `AuthorizeAsync(BrowserOptions, CancellationToken)` returns `Awaitable<Either<string, Uri>>`: `Left` is an error message (including cancellation/timeout), and `Right` is the original encoded callback URI. A received callback still requires OAuth validation. Null options throw an argument exception.
- `EditorBrowser.cs`: default `IBrowser` implementation adapted from `com.funtaptic.oidc/Runtime/Browsers/StandaloneBrowser.cs`. Starts an `HttpListener`, opens the system browser, receives the callback and writes the browser response. Async listener operations replace the original worker thread; a linked cancellation token combines the browser timeout with caller cancellation. Callback validation and original URI encoding are preserved.
- `ManualOAuthDemo.cs`: sample flow, configuration, cancellation and debug controls.

`ManualOAuthDemo.Browser` defaults to `EditorBrowser`. Assign another `IBrowser` before signing in to replace the browser integration:

```csharp
demo.Browser = new MyBrowser();
await demo.SignInAsync();
```

The browser owns platform-specific redirect requirements and cleanup. The demo always validates the returned callback, state and optional issuer before exchanging the code. Browser replacement is rejected while an OAuth operation is running; null implementations are rejected.

The async APIs return Unity `Awaitable` / `Awaitable<T>` and must be called on the Unity main thread. Await each returned instance only once. HTTP waits use cancellation-aware `Awaitable.NextFrameAsync`; cancellation switches to the main thread before aborting the Unity request. Incoming localhost requests use .NET's asynchronous listener API inside an `Awaitable` method. There is no blocking listener thread, coroutine, `HttpClient`, or OAuth SDK dependency in this sample.

Token requests use `UnityWebRequest.Post(endpoint, fields)` with unencoded dictionary values; Unity handles form encoding, upload/download handlers and the `application/x-www-form-urlencoded` content type. JSON responses use `JsonSerializer.Deserialize`. Redirect following is disabled for outgoing requests. The callback must match the redirect host, port and path and the fresh login state. Callbacks are parsed with `HttpUtility.ParseQueryString`; duplicate parameters are rejected using `GetValues`, and an `iss` response parameter is checked when present.

Tokens are exposed through `ManualOAuthDemo.Tokens` for authenticated API calls and kept only in memory. They are never displayed, logged or persisted. **Clear local session** only clears local tokens; it does not revoke them or end the provider's browser session. This is an OAuth authorization sample, not an OIDC identity-token validator: ID tokens/identity claims are not consumed or trusted. The default scopes intentionally omit `openid`.

The original scene, OAuth package and build scene selection are unchanged. Android, iOS and WebGL callback integrations are outside this desktop sample.

## Validation

From the repository root (adjust the installed Editor path):

```powershell
dotnet build Tests/ManualOAuth/RuntimeCompile.csproj -p:UnityEditorPath=G:/UnityHub/6000.3.25f1/Editor
dotnet run --project Tests/ManualOAuth/ProtocolSmoke.csproj
```

The build checks the real Unity and installed System.Text.Json assemblies. The executable checks the RFC 7636 vector, verifier format/randomness, authorization query encoding, callback state/host/port/path/issuer rejection, duplicate query parameters, OAuth denial, and JSON token validation. It does not simulate UnityWebRequest or establish a live provider session.

For the integration check, run this scene and verify successful login and refresh, user-denied login, cancel while waiting in the browser, timeout, and leaving Play mode during login. Repeating login after cancellation should reuse the port successfully. A complete browser login and an IL2CPP desktop build still need to be tested in Unity with the registered provider client.

References: [PKCE (RFC 7636)](https://www.rfc-editor.org/rfc/rfc7636), [Unity Awaitable](https://docs.unity3d.com/6000.3/Documentation/Manual/async-await-support.html).


## Log out

After signing in, press **Log out** to clear local tokens and open the provider's
discovered `end_session_endpoint` in the browser. Confirm logout there and return
to Unity. Register the sample's redirect URI (by default
`http://localhost:7890/login-callback`) as an allowed **post-logout redirect URI**
as well as a login redirect URI. The provider must support logout with `client_id`
and a registered redirect without an ID-token hint; the default OAuth-only scopes
do not request an ID token.

The callback URL and a fresh logout state are validated before reporting success.
Cancel, timeout, unsupported logout, or browser failure leave local tokens cleared
and report that provider logout was not confirmed. Sign-in remains available after
logout or **Clear local session**. Neither action revokes already-issued tokens.

Manually verify login → logout → login, cancel during logout, and a provider with
no logout endpoint. See [RP-Initiated Logout](https://openid.net/specs/openid-connect-rpinitiated-1_0.html)
for the provider protocol.
