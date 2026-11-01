package com.menagaming.mafia;

import android.app.Activity;
import android.os.CancellationSignal;
import android.util.Log;

import androidx.core.content.ContextCompat;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.NoCredentialException;

import com.google.android.libraries.identity.googleid.GetGoogleIdOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import com.unity3d.player.UnityPlayer;

/**
 * Google sign-in for the game: shows Google's account chooser (Credential Manager) and sends the ID token back to Unity.
 * The token is made for the game's WEB client id (serverClientId), because the rewards server is the one that checks it.
 * Answers go to the Unity object named by "receiver": OnGoogleToken(token) or OnGoogleError(cancelled | no_account | failed).
 */
public final class GoogleSignInBridge {
    private static final String TAG = "GoogleSignInBridge";

    private GoogleSignInBridge() {}

    public static void signIn(final String webClientId, final String receiver) {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) { UnityPlayer.UnitySendMessage(receiver, "OnGoogleError", "failed"); return; }
        activity.runOnUiThread(new Runnable() {
            @Override public void run() {
                try {
                    CredentialManager manager = CredentialManager.create(activity);
                    GetGoogleIdOption option = new GetGoogleIdOption.Builder()
                            .setFilterByAuthorizedAccounts(false)
                            .setServerClientId(webClientId)
                            .setAutoSelectEnabled(false)
                            .build();
                    GetCredentialRequest request = new GetCredentialRequest.Builder().addCredentialOption(option).build();
                    manager.getCredentialAsync(activity, request, new CancellationSignal(), ContextCompat.getMainExecutor(activity),
                            new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                                @Override public void onResult(GetCredentialResponse result) {
                                    Credential credential = result.getCredential();
                                    if (credential instanceof CustomCredential
                                            && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
                                        GoogleIdTokenCredential google = GoogleIdTokenCredential.createFrom(((CustomCredential) credential).getData());
                                        UnityPlayer.UnitySendMessage(receiver, "OnGoogleToken", google.getIdToken());
                                    } else {
                                        UnityPlayer.UnitySendMessage(receiver, "OnGoogleError", "failed");
                                    }
                                }

                                @Override public void onError(GetCredentialException e) {
                                    Log.w(TAG, "Google sign-in failed: " + e.getType() + " " + e.getMessage());
                                    String reason = e instanceof GetCredentialCancellationException ? "cancelled" : (e instanceof NoCredentialException ? "no_account" : "failed");
                                    UnityPlayer.UnitySendMessage(receiver, "OnGoogleError", reason);
                                }
                            });
                } catch (Exception e) {
                    Log.w(TAG, "Google sign-in could not start: " + e.getMessage());
                    UnityPlayer.UnitySendMessage(receiver, "OnGoogleError", "failed");
                }
            }
        });
    }
}
