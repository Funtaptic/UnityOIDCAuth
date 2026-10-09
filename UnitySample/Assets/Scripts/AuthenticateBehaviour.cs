using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Funtaptic.OIDC;
using UnityEngine;

public class AuthenticateBehaviour : MonoBehaviour
{
    [SerializeField] private AuthHelper _authHelper;

    private static List<Claim> Parse(string identityToken)
    {
        string[] strArray = identityToken.Split('.');
        if (strArray.Length != 3)
        {
            return null;
        }

        var payload = strArray[1].Replace('-', '+').Replace('_', '/');
        // JWT payloads use base64url without trailing padding.
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var node = Encoding.UTF8.GetString(Convert.FromBase64String(payload));

        var dictionary = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(node);
        var claimList = new List<Claim>();
        foreach (var keyValuePair in dictionary)
        {
            if (keyValuePair.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement enumerate in keyValuePair.Value.EnumerateArray())
                    claimList.Add(new Claim(keyValuePair.Key, enumerate.ToString()));
            }
            else
                claimList.Add(new Claim(keyValuePair.Key, keyValuePair.Value.ToString()));
        }

        return claimList;
    }

    private void OnGUI()
    {
        var authState = _authHelper.State;

        if (authState == null)
        {
            GUILayout.Label("No state.");
            return;
        }

        if (authState.IsDoingWork)
        {
            GUILayout.Label("Working...");
            return;
        }

        switch (authState)
        {
            case SignedOut notAuthenticatedStateBehaviour:
            {
                if (GUILayout.Button("Sign in", GUILayout.Height(200), GUILayout.Width(200)))
                {
                    _ = notAuthenticatedStateBehaviour.AuthenticateAsync();
                }

                if (GUILayout.Button("Register", GUILayout.Height(200), GUILayout.Width(200)))
                {
                    _ = notAuthenticatedStateBehaviour.RegisterAsync();
                }

                break;
            }
            case SignedIn authenticatedStateBehaviour:
            {
                var claims = Parse(authenticatedStateBehaviour.State.IdentityToken);
                GUILayout.Label($"Name: {claims.FirstOrDefault(a => a.Type == "name")?.Value}");

                if (GUILayout.Button("Sign out", GUILayout.Height(200), GUILayout.Width(200)))
                {
                    authenticatedStateBehaviour.LogOut();
                }

                if (GUILayout.Button("Refresh", GUILayout.Height(200), GUILayout.Width(200)))
                {
                    _ = GetUserInfoAsync(authenticatedStateBehaviour);
                }

                break;
            }
        }
    }

    private async Awaitable GetUserInfoAsync(SignedIn signedIn)
    {
        Debug.Log("GetUserInfoAsync");
        var result = await signedIn.GetUserInfoAsync();
        Debug.Log($"GetUserInfoAsync result {result.Claims.Count}");
        foreach (var claim in result.Claims)
        {
            Debug.Log($"{claim.Type}: {claim.Value}");
        }
    }
}