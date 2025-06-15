//
//  onionfruitd.h
//  onionfruitd
//
//  Created by Albie on 01/06/2025.
//

#import "XPCProtocol.h"
#import <Foundation/Foundation.h>

// This object implements the protocol which we have defined. It provides the actual behavior for the service. It is 'exported' by the service to make it available to the process hosting the service over an NSXPCConnection.
@interface OnionFruitXPC : NSObject<OnionFruitXPCProtocol>
@end
