using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Funtaptic.OIDC;
using UnityEngine;

public class AuthenticateBehaviour : MonoBehaviour
{
    [SerializeField]
    private AuthHelper _authHelper;

    private static List<Claim> Parse(string identityToken)
    {
        string[] strArray = identityToken.Split('.');
        if (strArray.Length != 3)
        {
            return null;
        }

        var node = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(strArray[1].AsSpan()));

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
                if (GUILayout.Button("Sign in", GUILayout.Height(250), GUILayout.Width(200)))
                {
                    _ = notAuthenticatedStateBehaviour.AuthenticateAsync();
                }

                break;
            }
            case SignedIn authenticatedStateBehaviour:
            {
                var claims = Parse(authenticatedStateBehaviour.State.IdentityToken);
                foreach (var claim in claims)
                {
                    GUILayout.Label($"{claim.Type}: {claim.Value}");
                }

                if (GUILayout.Button("Sign out", GUILayout.Height(250), GUILayout.Width(200)))
                {
                    _ = authenticatedStateBehaviour.LogOutAsync(destroyCancellationToken);
                }

                break;
            }
        }
    }
}