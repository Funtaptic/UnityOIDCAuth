#import <AuthenticationServices/AuthenticationServices.h>
#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

typedef void (*FuntapticOIDCAuthenticationCallback)(const char *url, const char *error);

@interface FuntapticOIDCAuthenticationSessionDelegate : NSObject <ASWebAuthenticationPresentationContextProviding>
@end

@implementation FuntapticOIDCAuthenticationSessionDelegate

- (ASPresentationAnchor)presentationAnchorForWebAuthenticationSession:(ASWebAuthenticationSession *)session
{
    UIWindow *keyWindow = nil;

    if (@available(iOS 13.0, *))
    {
        for (UIScene *scene in UIApplication.sharedApplication.connectedScenes)
        {
            if (scene.activationState != UISceneActivationStateForegroundActive ||
                ![scene isKindOfClass:UIWindowScene.class])
            {
                continue;
            }

            UIWindowScene *windowScene = (UIWindowScene *)scene;
            for (UIWindow *window in windowScene.windows)
            {
                if (window.isKeyWindow)
                {
                    keyWindow = window;
                    break;
                }
            }

            if (keyWindow != nil)
                break;
        }
    }
    else
    {
        keyWindow = UIApplication.sharedApplication.keyWindow;
    }

    return keyWindow != nil ? keyWindow : UIApplication.sharedApplication.windows.firstObject;
}

@end

static ASWebAuthenticationSession *funtapticOIDCSession = nil;
static FuntapticOIDCAuthenticationSessionDelegate *funtapticOIDCSessionDelegate = nil;
static FuntapticOIDCAuthenticationCallback funtapticOIDCCallback = nil;

static void FuntapticOIDCComplete(const char *url, const char *error)
{
    FuntapticOIDCAuthenticationCallback callback = funtapticOIDCCallback;
    funtapticOIDCCallback = nil;
    funtapticOIDCSession = nil;
    funtapticOIDCSessionDelegate = nil;

    if (callback != nil)
        callback(url, error);
}

extern "C" void FuntapticOIDCStartAuthenticationSession(
    const char *startUrl,
    const char *callbackScheme,
    FuntapticOIDCAuthenticationCallback callback)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (@available(iOS 12.0, *))
        {
            funtapticOIDCCallback = callback;

            if (funtapticOIDCSession != nil)
                [funtapticOIDCSession cancel];

            NSString *startUrlString = startUrl != nil ? [NSString stringWithUTF8String:startUrl] : nil;
            NSString *callbackSchemeString = callbackScheme != nil ? [NSString stringWithUTF8String:callbackScheme] : nil;
            NSURL *url = startUrlString != nil ? [NSURL URLWithString:startUrlString] : nil;

            if (url == nil)
            {
                FuntapticOIDCComplete("", "Invalid start URL.");
                return;
            }

            funtapticOIDCSessionDelegate = [FuntapticOIDCAuthenticationSessionDelegate new];
            funtapticOIDCSession = [[ASWebAuthenticationSession alloc]
                initWithURL:url
                callbackURLScheme:callbackSchemeString
                completionHandler:^(NSURL *callbackURL, NSError *error) {
                    if (error != nil)
                    {
                        if (error.code == ASWebAuthenticationSessionErrorCodeCanceledLogin)
                            FuntapticOIDCComplete("", "canceled");
                        else
                            FuntapticOIDCComplete("", error.localizedDescription.UTF8String);

                        return;
                    }

                    if (callbackURL == nil)
                    {
                        FuntapticOIDCComplete("", "Authentication session completed without a callback URL.");
                        return;
                    }

                    FuntapticOIDCComplete(callbackURL.absoluteString.UTF8String, "");
                }];

            if (@available(iOS 13.0, *))
            {
                funtapticOIDCSession.presentationContextProvider = funtapticOIDCSessionDelegate;
            }

            if (![funtapticOIDCSession start])
                FuntapticOIDCComplete("", "Failed to start ASWebAuthenticationSession.");
        }
        else
        {
            if (callback != nil)
                callback("", "ASWebAuthenticationSession requires iOS 12 or later.");
        }
    });
}

extern "C" void FuntapticOIDCCancelAuthenticationSession()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (funtapticOIDCSession != nil)
            [funtapticOIDCSession cancel];
    });
}
