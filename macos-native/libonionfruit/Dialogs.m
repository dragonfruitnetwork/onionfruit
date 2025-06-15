//
//  Dialogs.m
//  onionfruit
//
//  Created by Albie Spriddell on 10/06/2025.
//

#import "Dialogs.h"

#import <AppKit/NSAlert.h>
#import <AppKit/NSWindow.h>
#import <Foundation/Foundation.h>

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
