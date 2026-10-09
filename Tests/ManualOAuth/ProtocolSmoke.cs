using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using UnitySample.ManualOAuth;

var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("Failed: " + name);
    checks++;
}
void Reject(Action action, string name)
{
    try { action(); }
    catch (InvalidOperationException) { checks++; return; }
    throw new Exception("Accepted invalid input: " + name);
}

// RFC 7636 Appendix B known-answer vector, not a round trip against our own code.
Check(PkceProtocol.CreateChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk") ==
      "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", "RFC 7636 S256 vector");
var verifier = PkceProtocol.CreateRandomValue();
Check(Regex.IsMatch(verifier, "^[A-Za-z0-9_-]{43}$"), "256-bit base64url verifier");
Check(verifier != PkceProtocol.CreateRandomValue(), "fresh randomness");
var redirect = PkceProtocol.RequireLoopbackRedirect("http://localhost:7890/login-callback");
const string issuer = "https://example.com/realm";
const string state = "state-123";
var callback = new Uri(redirect + "?state=" + state + "&code=a%2Bb%26c%3Dd");
Check(PkceProtocol.ReadCode(callback, redirect, state, issuer) == "a+b&c=d", "decode values once after splitting query");
Check(!PkceProtocol.IsExpectedCallback(callback, redirect, "wrong"), "state mismatch");
Check(!PkceProtocol.IsExpectedCallback(new Uri("http://localhost:7891/login-callback?state=" + state), redirect, state), "wrong port");
Check(!PkceProtocol.IsExpectedCallback(new Uri("http://localhost:7890/other?state=" + state), redirect, state), "wrong path");
Check(!PkceProtocol.IsExpectedCallback(new Uri("http://127.0.0.1:7890/login-callback?state=" + state), redirect, state), "wrong host");
Check(!PkceProtocol.IsExpectedCallback(new Uri(callback + "#fragment"), redirect, state), "unexpected fragment");
Reject(() => PkceProtocol.ReadCode(new Uri(redirect + "?state=" + state + "&state=" + state + "&code=x"), redirect, state, issuer), "duplicate state");
Reject(() => PkceProtocol.ReadCode(new Uri(redirect + "?state=" + state + "&%73tate=" + state + "&code=x"), redirect, state, issuer), "encoded duplicate state");
Reject(() => PkceProtocol.ReadCode(new Uri(redirect + "?state=" + state + "&code=x&code=y"), redirect, state, issuer), "duplicate code");
Check(PkceProtocol.ReadCode(new Uri(redirect + "?state=" + state + "&code=a%2Cb%2Bc+d"), redirect, state, issuer) == "a,b+c d", "literal comma, encoded plus and form space");
Reject(() => PkceProtocol.ReadCode(new Uri(redirect + "?state=" + state + "&error=&code=x"), redirect, state, issuer), "empty OAuth error still rejects code");
Reject(() => PkceProtocol.ReadCode(new Uri(redirect + "?state=" + state + "&code=x&iss=https%3A%2F%2Fevil.example"), redirect, state, issuer), "issuer mismatch");
Reject(() => PkceProtocol.ReadCode(new Uri(redirect + "?state=" + state + "&error=access_denied"), redirect, state, issuer), "provider denial");
Reject(() => PkceProtocol.ReadCode(new Uri(redirect + "?state=" + state), redirect, state, issuer), "missing code");
Reject(() => PkceProtocol.RequireHttps("http://example.com"), "insecure provider");
Reject(() => PkceProtocol.RequireLoopbackRedirect("http://example.com:7890/login-callback"), "non-loopback callback");
Reject(() => PkceProtocol.RequireLoopbackRedirect("http://localhost:7890/login-callback?x=y"), "callback query");
var url = PkceProtocol.AuthorizationUrl(issuer + "/authorize?audience=game%2Bapi&state=old&state=older", "client + & one", redirect.ToString(), "profile roles", state, "challenge");
var authorizationFields = HttpUtility.ParseQueryString(new Uri(url).Query);
Check(authorizationFields["code_challenge_method"] == "S256" && authorizationFields["response_type"] == "code" &&
      authorizationFields["code_challenge"] == "challenge", "authorization parameters");
Check(authorizationFields["client_id"] == "client + & one" && authorizationFields["scope"] == "profile roles" &&
      authorizationFields["redirect_uri"] == redirect.ToString(), "query values encoded without corruption");
Check(authorizationFields["audience"] == "game+api", "preserve existing endpoint parameters");
Check(authorizationFields["state"] == state, "replace existing OAuth parameters without duplicates");
var tokens = JsonSerializer.Deserialize<OAuthTokens>("{\"access_token\":\"test-only\",\"token_type\":\"Bearer\",\"expires_in\":3600,\"refresh_token\":\"refresh-only\"}");
tokens.Validate();
Check(tokens.AccessToken == "test-only" && tokens.RefreshToken == "refresh-only" && tokens.ExpiresAt > DateTimeOffset.UtcNow, "JSON mapping and expiry");
Reject(() => new OAuthTokens { TokenType = "Bearer" }.Validate(), "missing access token");
Reject(() => new OAuthTokens { AccessToken = "x", TokenType = "MAC" }.Validate(), "unsupported token type");
Reject(() => new OAuthTokens { AccessToken = "x", TokenType = "Bearer", ExpiresIn = -1 }.Validate(), "negative lifetime");
Reject(() => new OAuthTokens { AccessToken = "x", TokenType = "Bearer", ExpiresIn = long.MaxValue }.Validate(), "lifetime overflow");
var callbackResult = Either<string, Uri>.FromRight(callback);
Check(callbackResult.IsRight && !callbackResult.IsLeft && callbackResult.Right == callback, "Either callback branch");
Reject(() => { _ = callbackResult.Left; }, "Either callback cannot expose an error");
var errorResult = Either<string, Uri>.FromLeft("Browser unavailable.");
Check(errorResult.IsLeft && !errorResult.IsRight && errorResult.Left == "Browser unavailable.", "Either error branch");
Reject(() => { _ = errorResult.Right; }, "Either error cannot expose a callback");
Check(Either<string, string>.FromRight("right").IsRight && Either<string, string>.FromLeft("left").IsLeft,
    "Either discriminates even when both types match");
Console.WriteLine($"Passed {checks} manual OAuth checks.");
