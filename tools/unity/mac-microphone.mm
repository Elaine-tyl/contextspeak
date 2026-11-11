#import <AVFoundation/AVFoundation.h>

// Unity's RequestUserAuthorization does not implement macOS microphone access.
extern "C" __attribute__((visibility("default"))) int CS_MicPermissionStatus()
{
    return (int)[AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeAudio];
}

extern "C" __attribute__((visibility("default"))) void CS_RequestMicPermission()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if ([AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeAudio] == AVAuthorizationStatusNotDetermined)
            [AVCaptureDevice requestAccessForMediaType:AVMediaTypeAudio completionHandler:^(BOOL granted) {}];
    });
}
