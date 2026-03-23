package com.funtaptic;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.util.Log;
import android.view.Window;

import com.unity3d.player.UnityPlayer;

public class AuthRedirectActivity extends Activity {

    private static String TAG = "AuthRedirectActivity"; 

    private static RedirectCallback callback;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        requestWindowFeature(Window.FEATURE_NO_TITLE);
        Log.d(TAG, "onCreate");
        Uri dataUri = getIntent().getData();
        
        if (dataUri != null) 
        {
            if(callback!=null)
            {
                callback.callback(dataUri.toString());
            }
            else
            {
                Log.e(TAG, String.format("Data uri:%s", dataUri.toString()));
            }
        }

        Intent newIntent = new Intent(this, getMainActivityClass());
        this.startActivity(newIntent);
        finish();
    }

    private Class<?> getMainActivityClass() {
        String packageName = this.getPackageName();
        Intent launchIntent = this.getPackageManager().getLaunchIntentForPackage(packageName);
        try {
            return Class.forName(launchIntent.getComponent().getClassName());
        } catch (Exception e) {
            Log.e(TAG, "Unable to find Main Activity Class");
            return null;
        }
    }

}