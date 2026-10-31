package com.menagaming.mafia;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.util.Log;

import com.facebook.AccessToken;
import com.facebook.CallbackManager;
import com.facebook.FacebookCallback;
import com.facebook.FacebookException;
import com.facebook.login.LoginManager;
import com.facebook.login.LoginResult;
import com.unity3d.player.UnityPlayer;

import java.util.Arrays;

/**
 * Facebook sign-in for the game: a transparent activity that runs Facebook's login (the Facebook app, or a browser tab without it)
 * and sends the user access token back to Unity. Its own activity, because Facebook answers through onActivityResult, which the Unity
 * activity does not pass on. Only "public_profile" is asked: the server only needs the app-scoped user id.
 * Answers go to the Unity object named by "receiver": OnFacebookToken(token) or OnFacebookError(cancelled | failed).
 * The app id and client token are in AndroidManifest.xml (meta-data), as Facebook's SDK reads them from there.
 */
public final class FacebookLoginActivity extends Activity {
    private static final String TAG = "FacebookLogin";
    private static String receiver = "FacebookSignInReceiver";
    private CallbackManager callbacks;
    private boolean answered;

    public static void signIn(final String receiverName) {
        receiver = receiverName;
        final Activity unity = UnityPlayer.currentActivity;
        if (unity == null) { UnityPlayer.UnitySendMessage(receiverName, "OnFacebookError", "failed"); return; }
        unity.runOnUiThread(new Runnable() {
            @Override public void run() {
                try { unity.startActivity(new Intent(unity, FacebookLoginActivity.class)); }
                catch (Exception e) { Log.w(TAG, "could not start: " + e.getMessage()); UnityPlayer.UnitySendMessage(receiver, "OnFacebookError", "failed"); }
            }
        });
    }

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        try {
            callbacks = CallbackManager.Factory.create();
            LoginManager.getInstance().registerCallback(callbacks, new FacebookCallback<LoginResult>() {
                @Override public void onSuccess(LoginResult result) {
                    AccessToken token = result.getAccessToken();
                    answer(token != null ? "OnFacebookToken" : "OnFacebookError", token != null ? token.getToken() : "failed");
                }
                @Override public void onCancel() { answer("OnFacebookError", "cancelled"); }
                @Override public void onError(FacebookException e) { Log.w(TAG, "Facebook login failed: " + e.getMessage()); answer("OnFacebookError", "failed"); }
            });
            LoginManager.getInstance().logIn(this, Arrays.asList("public_profile"));
        } catch (Exception e) {
            Log.w(TAG, "Facebook login could not start: " + e.getMessage());
            answer("OnFacebookError", "failed");
        }
    }

    @Override protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (callbacks != null) callbacks.onActivityResult(requestCode, resultCode, data);
    }

    @Override protected void onDestroy() {
        if (callbacks != null) LoginManager.getInstance().unregisterCallback(callbacks);
        if (!answered) UnityPlayer.UnitySendMessage(receiver, "OnFacebookError", "cancelled");      // closed without an answer
        super.onDestroy();
    }

    private void answer(String method, String value) {
        if (answered) return;
        answered = true;
        UnityPlayer.UnitySendMessage(receiver, method, value);
        finish();
    }
}
