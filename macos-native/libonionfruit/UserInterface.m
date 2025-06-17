//
//  UserInterface.m
//  libonionfruit-macos
//
//  Created by Albie on 10/06/2025.
//

#import "UserInterface.h"

#import <AppKit/NSAlert.h>
#import <AppKit/NSWindow.h>
#import <Foundation/Foundation.h>

#include <CoreServices/CoreServices.h>

// see https://github.com/chromium/chromium/blob/d3732d842d2d9d34bbe13a12ab590cd1fc0c707a/base/mac/mac_util.mm#L198-L220
uint8_t currentProcessLaunchedAsLoginItem(void) {
    ProcessSerialNumber psn = {0, kCurrentProcess};
    ProcessInfoRec info = {};
    info.processInfoLength = sizeof(info);

  // GetProcessInformation has been deprecated since macOS 10.9, but there is no
  // replacement that provides the information we need. See
  // https://crbug.com/650854.
  #pragma clang diagnostic push
  #pragma clang diagnostic ignored "-Wdeprecated-declarations"
    if (GetProcessInformation(&psn, &info) == noErr) {
  #pragma clang diagnostic pop
      ProcessInfoRec parent_info = {};
      parent_info.processInfoLength = sizeof(parent_info);
  #pragma clang diagnostic push
  #pragma clang diagnostic ignored "-Wdeprecated-declarations"
      if (GetProcessInformation(&info.processLauncher, &parent_info) == noErr) {
  #pragma clang diagnostic pop
        return parent_info.processSignature == 'lgnw';
      }
    }

    return false;
}

void showMessageBox(const char* pTitle, const char* pMessage, const char* pButtonText) {
    NSString* title = [NSString stringWithUTF8String: pTitle];
    NSString* message = pMessage ? [NSString stringWithUTF8String: pMessage] : nil;
    NSString* buttonText = pButtonText ? [NSString stringWithUTF8String: pButtonText] : @"OK";

    NSAlert* alert = [[NSAlert alloc] init];

    [alert setMessageText: title];
    [alert addButtonWithTitle: buttonText];
    [alert setAlertStyle: NSAlertStyleCritical];

    if (message) {
        [alert setInformativeText: message];
    }
    
    // bring it to the front, as it's most likely this will be used when there is no window to show
    alert.window.level = NSFloatingWindowLevel;
    
    [alert runModal];
}
