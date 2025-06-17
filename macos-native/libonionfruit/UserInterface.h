//
//  UserInterface.h
//  libonionfruit-macos
//
//  Created by Albie on 10/06/2025.
//

#import <Foundation/Foundation.h>

#ifndef Dialogs_h
#define Dialogs_h

FOUNDATION_EXPORT uint8_t currentProcessLaunchedAsLoginItem(void);

FOUNDATION_EXPORT void showMessageBox(const char* pTitle, const char* pMessage, const char* pButtonText);

#endif /* Dialogs_h */
